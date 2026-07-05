using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Everything one resolution stage needs to attempt resolving <em>one</em> field of one document.
/// </summary>
/// <remarks>
/// <para>
/// Carries the once-parsed PdfPig corpus as a <see cref="LazyPdfCorpus"/> handle rather than an
/// already-open document, so that in E1 (no stage beyond stage 1 is registered for any field) the
/// PDF is never re-parsed — the handle is constructed once per document by
/// <c>EscalatingStatementFieldExtractor</c> and shared across every field's context for that
/// document.
/// </para>
/// <para>
/// <see cref="PriorCandidates"/> lets a stage see what earlier rungs already tried (e.g. a
/// disagreement-gate stage needs to compare against more than just the immediately-preceding
/// candidate). The list is ordered from stage 1 onward.
/// </para>
/// </remarks>
/// <typeparam name="TValue">The type of the field being resolved (e.g. <see cref="string"/>, <see cref="decimal"/>).</typeparam>
public sealed class FieldResolutionContext<TValue>
{
    /// <summary>
    /// Initializes a <see cref="FieldResolutionContext{TValue}"/> for one field of one document.
    /// </summary>
    /// <param name="fieldKind">Which field is being resolved.</param>
    /// <param name="pdfBytes">Raw bytes of the statement PDF.</param>
    /// <param name="corpus">
    /// Lazy, document-scoped handle to the parsed PdfPig word/page corpus. Shared across every
    /// field's context for the same document so the PDF is parsed at most once even when several
    /// fields escalate.
    /// </param>
    /// <param name="priorCandidates">
    /// Candidates produced by earlier rungs for this same field, ordered from stage 1 onward.
    /// Empty for the first rung.
    /// </param>
    /// <param name="budget">The document-level cost guard (e.g. remaining LLM calls).</param>
    public FieldResolutionContext(
        FieldKind fieldKind,
        byte[] pdfBytes,
        LazyPdfCorpus corpus,
        IReadOnlyList<FieldCandidate<TValue>> priorCandidates,
        StageBudget budget)
    {
        FieldKind = fieldKind;
        PdfBytes = pdfBytes ?? throw new ArgumentNullException(nameof(pdfBytes));
        Corpus = corpus ?? throw new ArgumentNullException(nameof(corpus));
        PriorCandidates = priorCandidates ?? throw new ArgumentNullException(nameof(priorCandidates));
        Budget = budget ?? throw new ArgumentNullException(nameof(budget));
    }

    /// <summary>Which field is being resolved.</summary>
    public FieldKind FieldKind { get; }

    /// <summary>Raw bytes of the statement PDF.</summary>
    public byte[] PdfBytes { get; }

    /// <summary>
    /// Lazy, document-scoped handle to the parsed PdfPig word/page corpus. A stage that needs
    /// direct PdfPig access reads <see cref="LazyPdfCorpus.Value"/>; doing so triggers the
    /// (memoized) parse for the whole document, not just this field.
    /// </summary>
    public LazyPdfCorpus Corpus { get; }

    /// <summary>
    /// Candidates produced by earlier rungs for this field, ordered from stage 1 onward. Empty
    /// for the first rung attempted.
    /// </summary>
    public IReadOnlyList<FieldCandidate<TValue>> PriorCandidates { get; }

    /// <summary>The document-level cost guard threaded through every field's resolution.</summary>
    public StageBudget Budget { get; }
}
