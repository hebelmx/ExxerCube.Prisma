using System;
using System.Threading;
using UglyToad.PdfPig;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Lazily parses and memoizes a <see cref="PdfDocument"/> for one document extraction call, so
/// that every higher resolution stage (E2+) that needs direct PdfPig word/page access shares a
/// single parse — no matter how many fields on that document escalate.
/// </summary>
/// <remarks>
/// <para>
/// As of E1 no stage reads <see cref="Value"/> (there are no higher stages registered for any
/// <c>FieldKind</c>), so the PDF is <em>never</em> re-parsed beyond the positional extractor's own
/// pass — the whole point of the lazy handle. <see cref="IsValueCreated"/> lets tests/diagnostics
/// assert that E1 truly never triggers a second parse.
/// </para>
/// <para>
/// Not itself thread-safe against concurrent disposal while a parse is in flight, but the
/// underlying <see cref="Lazy{T}"/> uses <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>
/// so concurrent fields escalating on the same document will not double-parse.
/// </para>
/// </remarks>
public sealed class LazyPdfCorpus : IDisposable
{
    private readonly Lazy<PdfDocument> _document;
    private bool _disposed;

    /// <summary>
    /// Initializes a <see cref="LazyPdfCorpus"/> over the given PDF bytes. The PDF is not opened
    /// until <see cref="Value"/> is first accessed.
    /// </summary>
    /// <param name="pdfBytes">Raw bytes of the statement PDF.</param>
    public LazyPdfCorpus(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        _document = new Lazy<PdfDocument>(
            () => PdfDocument.Open(pdfBytes),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// <see langword="true"/> once a stage has actually triggered the PdfPig parse;
    /// <see langword="false"/> while the corpus remains unparsed.
    /// </summary>
    public bool IsValueCreated => _document.IsValueCreated;

    /// <summary>
    /// The parsed <see cref="PdfDocument"/>. Triggers the (memoized) PdfPig parse on first access
    /// from any stage; subsequent accesses — from the same or a different field's resolution —
    /// return the same instance without re-parsing.
    /// </summary>
    public PdfDocument Value
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _document.Value;
        }
    }

    /// <summary>
    /// Disposes the underlying <see cref="PdfDocument"/> if it was ever parsed. A no-op when the
    /// corpus was never accessed (the common E1 case).
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_document.IsValueCreated)
            _document.Value.Dispose();

        _disposed = true;
    }
}
