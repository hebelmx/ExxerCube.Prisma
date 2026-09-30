using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// Module initializer that arms <see cref="LeptonicaInteropGuard"/> as early as possible: the runtime
/// guarantees this runs before any other code in the OCR infrastructure assembly executes. Because the
/// OCR services (e.g. <c>TesseractOcrExecutor</c>) are registered at DI-composition time — before the
/// host runs any pipeline stage, and therefore before Emgu.CV's first native call — this guarantees the
/// system Leptonica occupies the global symbol scope before <c>libcvextern.so</c> can interpose its
/// bundled copy. Covers the workers and the E2E gate identically with no environment wiring.
/// </summary>
internal static class LeptonicaInteropModuleInitializer
{
    // CA2255: a module initializer in library code is normally discouraged, but here it is the whole point —
    // we need the system Leptonica in the global symbol scope before Emgu.CV/SkiaSharp load, and this is the
    // earliest, control-free hook the runtime offers. The work is idempotent, never throws, and is a no-op
    // off Linux, so the "unexpected timing" hazard the analyzer warns about does not apply.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Init() => LeptonicaInteropGuard.EnsureSystemLeptonicaLoadedFirst();
}

/// <summary>
/// Linux-only guard that prevents a native SIGSEGV when Tesseract/Leptonica OCR coexists in the
/// same process as Emgu.CV (<c>libcvextern.so</c>) and/or SkiaSharp.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Root cause.</strong> The Emgu.CV native, <c>libcvextern.so</c>, statically bundles its own
/// full copy of Leptonica and exports those symbols (<c>pixCreate</c>, <c>pixRead</c>, <c>boxaCreate</c>,
/// <c>bmfCreate</c>, <c>AlphaMaskBorderVals</c>, …) with default (global) ELF visibility. When OpenCV is
/// loaded into global scope <em>before</em> the real <c>libleptonica.so</c> / <c>libtesseract.so</c>
/// (which happens in the pipeline: Stage 1 quality analysis uses Emgu, Stage 2 OCR uses
/// Tesseract), the dynamic linker resolves Tesseract's and Leptonica's <em>own</em> Leptonica references
/// against OpenCV's incompatible bundled copy. A <c>Pix</c> then gets allocated by one Leptonica build and
/// read/freed by another — a struct-ABI mismatch — and the process crashes (exit 139).
/// </para>
/// <para>
/// Proven via <c>LD_DEBUG=bindings</c>: with Emgu resident, <c>libtesseract.so</c> / <c>libleptonica.so</c>
/// bind <c>pixCreate</c>/<c>boxaCreate</c>/<c>bmfCreate</c> into <c>libcvextern.so</c>; <c>pixCreate</c>
/// even <em>splits</em> across two libraries. The standalone OCR suite (no Emgu) never crashes.
/// </para>
/// <para>
/// <strong>Fix.</strong> Load the system Leptonica (and Tesseract) into the global symbol scope
/// <em>first</em>, with <c>RTLD_GLOBAL</c>, before any imaging native is loaded. Whichever definition is
/// first in global scope wins, so every consumer then binds to a single, consistent Leptonica and the ABI
/// mix disappears. This mirrors an <c>LD_PRELOAD</c> of the system libs but needs no environment wiring,
/// so it protects the test host and the workers identically.
/// </para>
/// <para>
/// This must run <em>before</em> Emgu's first <c>CvInvoke</c>; it is invoked from
/// <c>Infrastructure.Imaging</c>'s and the OCR DI registration (which execute at composition time, before
/// any pipeline stage) and may be called directly from a worker's <c>Main</c>. It is idempotent and a
/// no-op on non-Linux platforms.
/// </para>
/// </remarks>
public static class LeptonicaInteropGuard
{
    private const int RTLD_NOW = 0x0002;
    private const int RTLD_GLOBAL = 0x0100;
    private const int RTLD_NODELETE = 0x1000;

    // Candidate sonames, most-specific soversion first. dlopen resolves these through the ldconfig cache,
    // so no absolute path is hardcoded and the guard adapts to the host's installed soversion.
    private static readonly string[] LeptonicaSonames = { "libleptonica.so.6", "libleptonica.so.5", "libleptonica.so" };
    private static readonly string[] TesseractSonames = { "libtesseract.so.5", "libtesseract.so.4", "libtesseract.so" };

    private static readonly object Gate = new();
    private static bool _done;

    [DllImport("libdl.so.2", CharSet = CharSet.Ansi)]
    private static extern IntPtr dlopen(string filename, int flags);

    /// <summary>
    /// Ensures the system Leptonica/Tesseract natives occupy the global symbol scope before any other
    /// imaging native (Emgu.CV / SkiaSharp) can interpose an incompatible bundled Leptonica.
    /// Idempotent; no-op on non-Linux platforms or when the system libs are absent.
    /// </summary>
    public static void EnsureSystemLeptonicaLoadedFirst()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        if (_done)
        {
            return;
        }

        lock (Gate)
        {
            if (_done)
            {
                return;
            }

            // RTLD_NODELETE keeps the lib pinned for the process lifetime so the global binding never
            // unloads underneath a later consumer.
            const int flags = RTLD_NOW | RTLD_GLOBAL | RTLD_NODELETE;

            TryLoadFirst(LeptonicaSonames, flags);
            TryLoadFirst(TesseractSonames, flags);

            _done = true;
        }
    }

    private static void TryLoadFirst(string[] sonames, int flags)
    {
        foreach (var soname in sonames)
        {
            try
            {
                if (dlopen(soname, flags) != IntPtr.Zero)
                {
                    return;
                }
            }
            catch
            {
                // dlopen unavailable (non-glibc) — fall through; the guard is best-effort.
                return;
            }
        }
    }
}
