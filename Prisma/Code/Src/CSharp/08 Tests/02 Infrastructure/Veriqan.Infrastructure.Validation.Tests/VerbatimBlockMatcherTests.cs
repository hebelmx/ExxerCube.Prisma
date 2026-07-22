using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for <see cref="VerbatimBlockMatcher"/> — RC1.S4.a Fix 2a (token-sequence LCS
/// ratio scoring, real-corpus triage class c) and Fix 2b (multi-candidate accepted-variant
/// matching). Exercises the matcher directly (not through a full <c>IVecValidationRule</c>) for
/// precise control over the interleaving / absence shapes described in the triage evidence.
/// </summary>
public sealed class VerbatimBlockMatcherTests
{
    // -----------------------------------------------------------------------
    // Fix 2a: interleaved near-verbatim block clears 0.82 without lowering the threshold
    // -----------------------------------------------------------------------

    /// <summary>
    /// Simulates multi-column PdfPig word interleaving (noise tokens from a "neighbor column"
    /// injected between the block's own tokens) plus a single word substitution — the exact shape
    /// the real-corpus triage attributes to §26-b ("continuarán" vs the bank's "continuarían").
    /// The combined scorer (Jaccard/Levenshtein max, RC1.S4.a-widened by the token-sequence LCS
    /// ratio) must clear 0.82; the pre-fix scorer (Jaccard/Levenshtein alone) must NOT — proving
    /// the fix is load-bearing rather than a lowered threshold.
    /// </summary>
    [Fact]
    public void BestWindowSimilarity_InterleavedOneWordDiffBlock_ClearsThreshold_PlainScoreDoesNot()
    {
        var expected = VecTextMatcher.Normalize(CondusefVerbatimCatalog.Section26NoteB);

        // Bank's near-verbatim wording: "continuarán" → "continuarían" (real-corpus evidence).
        var interleavedWindow = BuildInterleavedWindow(
            CondusefVerbatimCatalog.Section26NoteB, from: "CONTINUARAN", to: "CONTINUARIAN");

        // Embed in a larger, unrelated document so the sliding-window search (not the literal-
        // substring fast path) must find and score the block.
        var document =
            "ENCABEZADO DE OTRA SECCION SIN RELACION ALGUNA CON ESTE TEMA " +
            interleavedWindow +
            " PIE DE PAGINA SECCION SIGUIENTE DEL ESTADO DE CUENTA";

        var combinedScore = VerbatimBlockMatcher.BestWindowSimilarity(document, expected);
        var plainWindowScore = VecTextMatcher.Similarity(interleavedWindow, expected);

        combinedScore.ShouldBeGreaterThanOrEqualTo(CondusefVerbatimCatalog.DefaultSimilarityThreshold,
            "a genuinely near-verbatim (one-word-diff), interleaved block must clear 0.82 via the " +
            "token-sequence LCS ratio (RC1.S4.a Fix 2a)");
        plainWindowScore.ShouldBeLessThan(CondusefVerbatimCatalog.DefaultSimilarityThreshold,
            "the pre-fix Jaccard/Levenshtein-only score on the same interleaved window stays below " +
            "0.82 — demonstrates the fix is load-bearing, not a lowered threshold");
    }

    /// <summary>
    /// A block that is genuinely absent (replaced by unrelated content, not merely interleaved
    /// or reworded) must still score below 0.82 — the LCS ratio must not make the check vacuous.
    /// Mirrors the real-corpus §27 glossary finding: absent content must not start passing.
    /// </summary>
    [Fact]
    public void BestWindowSimilarity_GenuinelyAbsentBlock_StaysBelowThreshold()
    {
        var expected = VecTextMatcher.Normalize(CondusefVerbatimCatalog.Section27TermA);

        // Realistic statement vocabulary sharing no meaningful phrase with the CAT definition.
        var document =
            "ESTADO DE CUENTA MENSUAL DATOS GENERALES DEL USUARIO NUMERO DE TARJETA NUMERO DE " +
            "CLIENTE CLABE INTERBANCARIA DESGLOSE DE MOVIMIENTOS DEL PERIODO FECHA DE OPERACION " +
            "FECHA DE CARGO DESCRIPCION DEL MOVIMIENTO MONTO CARGOS Y ABONOS SALDO CARGOS " +
            "REGULARES SALDO CARGOS A MESES LIMITE DE CREDITO CREDITO DISPONIBLE PARA " +
            "DISPOSICIONES DE EFECTIVO ATENCION DE QUEJAS NUMERO DE SUCURSAL";

        var score = VerbatimBlockMatcher.BestWindowSimilarity(document, expected);

        score.ShouldBeLessThan(CondusefVerbatimCatalog.DefaultSimilarityThreshold,
            "a genuinely absent block (real-corpus §27 glossary evidence) must not start passing");
    }

    // -----------------------------------------------------------------------
    // Fix 2b: multi-candidate (accepted variant) matching
    // -----------------------------------------------------------------------

    [Fact]
    public void FindFailingBlocksMultiCandidate_VariantCandidateMatches_BlockPasses()
    {
        var primary = VecTextMatcher.Normalize("Texto original del DOF para el bloque X.");
        var variant = VecTextMatcher.Normalize("Redaccion alterna aceptada para el bloque X.");
        var document = "ENCABEZADO " + variant + " PIE DE SECCION";

        var blocks = new List<(string Id, IReadOnlyList<string> NormalizedCandidates)>
        {
            ("BLOQUE-X", new[] { primary, variant }),
        };

        var failing = VerbatimBlockMatcher.FindFailingBlocksMultiCandidate(document, blocks, 0.82);

        failing.ShouldBeEmpty(
            "a block whose registered variant candidate matches the document must pass");
    }

    [Fact]
    public void FindFailingBlocksMultiCandidate_NeitherPrimaryNorVariantMatches_BlockFails()
    {
        var primary = VecTextMatcher.Normalize("Texto original del DOF para el bloque X.");
        var variant = VecTextMatcher.Normalize("Redaccion alterna aceptada para el bloque X.");
        var document = "ENCABEZADO CONTENIDO COMPLETAMENTE DISTINTO SIN RELACION PIE DE SECCION";

        var blocks = new List<(string Id, IReadOnlyList<string> NormalizedCandidates)>
        {
            ("BLOQUE-X", new[] { primary, variant }),
        };

        var failing = VerbatimBlockMatcher.FindFailingBlocksMultiCandidate(document, blocks, 0.82);

        failing.ShouldHaveSingleItem();
        failing[0].BlockId.ShouldBe("BLOQUE-X");
    }

    [Fact]
    public void FindFailingBlocksMultiCandidate_PrimaryMatches_VariantIrrelevant_BlockPasses()
    {
        var primary = VecTextMatcher.Normalize("Texto original del DOF para el bloque X.");
        var variant = VecTextMatcher.Normalize("Redaccion alterna nunca usada en este documento.");
        var document = "ENCABEZADO " + primary + " PIE DE SECCION";

        var blocks = new List<(string Id, IReadOnlyList<string> NormalizedCandidates)>
        {
            ("BLOQUE-X", new[] { primary, variant }),
        };

        var failing = VerbatimBlockMatcher.FindFailingBlocksMultiCandidate(document, blocks, 0.82);

        failing.ShouldBeEmpty(
            "the DOF primary text remains an accepted candidate alongside any registered variant");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Interleaves the (normalized) DOF note's tokens with unrelated "neighbor column" noise
    /// tokens — simulating PdfPig multi-column word interleaving — and applies one word
    /// substitution to simulate a near-verbatim wording delta.
    /// </summary>
    private static string BuildInterleavedWindow(string dofText, string from, string to)
    {
        var normalized = VecTextMatcher.Normalize(dofText).Replace(from, to, StringComparison.Ordinal);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Each noise token is UNIQUE (e.g. "COL7") rather than drawn from a small repeated
        // vocabulary — a real neighboring PdfPig column contributes varied content (dollar
        // amounts, dates, distinct words), so each insertion inflates the Jaccard UNION by one
        // new token. A small repeated noise vocabulary would under-dilute the plain score and
        // fail to reproduce the ~0.67-0.8 ceiling the real-corpus triage measured.
        var interleaved = new List<string>(tokens.Length * 2);
        for (var i = 0; i < tokens.Length; i++)
        {
            interleaved.Add(tokens[i]);
            if (i % 2 == 1)
                interleaved.Add($"COL{i}");
        }

        return string.Join(' ', interleaved);
    }
}
