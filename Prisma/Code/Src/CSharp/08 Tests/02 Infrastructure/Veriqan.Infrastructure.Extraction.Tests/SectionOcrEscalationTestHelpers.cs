using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using IndQuestResults;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Shared test helper: a no-op fake <see cref="IHeaderProductOcrEngine"/> for constructing an
/// <see cref="EscalatingStatementFieldExtractor"/> in tests that are NOT exercising
/// <see cref="SectionAnchorOcrEscalationStage"/> behavior directly (RC1.S6). Configuring an
/// explicit empty-success return is required — an unconfigured NSubstitute fake returns a
/// completed <c>Task</c> wrapping <see langword="default"/>(<see cref="Result{T}"/>) (i.e.
/// <see langword="null"/>, since <see cref="Result{T}"/> is a reference type), which crashes with
/// a <see cref="System.NullReferenceException"/> the moment
/// <see cref="SectionAnchorOcrEscalationStage.EscalateAsync"/> calls
/// <c>ocrResult.IsFailure</c> on it — and several real demo fixtures (<c>good.pdf</c> and its
/// siblings) DO trigger the escalation ladder (their section headings are raster-rendered, the
/// same real-corpus family as RC1's calibration corpus), so any test that runs
/// <c>ExtractFullAsync</c>/<c>ExtractHeaderAsync</c> over them must supply a safely-configured
/// engine even when the test's own assertions have nothing to do with section escalation.
/// </summary>
internal static class SectionOcrEscalationTestHelpers
{
    /// <summary>
    /// A fake <see cref="IHeaderProductOcrEngine"/> that always returns an empty successful OCR
    /// read — safe for <see cref="SectionAnchorOcrEscalationStage"/> to consume (empty text
    /// matches no anchor, so the section list comes back unchanged) without ever touching real
    /// Tesseract.
    /// </summary>
    public static IHeaderProductOcrEngine NoOpSectionOcrEngine()
    {
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess(string.Empty)));
        return engine;
    }
}
