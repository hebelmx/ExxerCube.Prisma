using ExxerCube.Prisma.Domain.Services.Manifest;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Pure domain service that reconciles a set of discovered SIARA cases against the operator-supplied
/// expected manifest and produces a <see cref="ManifestReconciliationReport"/> (Item B #8 + F #12).
/// </summary>
/// <remarks>
/// This interface is intentionally synchronous — the reconciler is a pure value-in/value-out
/// computation with no I/O. The <c>FileExpectedManifestProvider</c> handles the async I/O, so this
/// layer stays fully testable without any async infrastructure.
/// </remarks>
public interface IManifestReconciler
{
    /// <summary>
    /// Reconciles <paramref name="actual"/> discovered oficios against the <paramref name="expected"/>
    /// manifest and produces a report with the Complete / Partial / Missing / Extra buckets plus the
    /// flat downloaded-file list (F).
    /// </summary>
    /// <param name="expected">
    /// The expected manifest loaded for this cycle. Use <c>ExpectedManifest.Empty</c> when no
    /// manifest is configured — every discovered oficio will land in <em>Extra</em>.
    /// </param>
    /// <param name="actual">The oficios discovered (and partially downloaded) during this cycle.</param>
    /// <returns>A <see cref="ManifestReconciliationReport"/> with all buckets populated.</returns>
    ManifestReconciliationReport Reconcile(
        ExpectedManifest expected,
        IReadOnlyList<DiscoveredOficio> actual);
}
