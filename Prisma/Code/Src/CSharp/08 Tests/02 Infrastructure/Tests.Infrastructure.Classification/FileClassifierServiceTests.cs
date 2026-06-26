namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="FileClassifierService"/>.
/// </summary>
public class FileClassifierServiceTests
{
    private readonly ILogger<FileClassifierService> _logger;
    private readonly FileClassifierService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileClassifierServiceTests"/> class.
    /// </summary>
    public FileClassifierServiceTests()
    {
        _logger = Substitute.For<ILogger<FileClassifierService>>();
        _service = new FileClassifierService(_logger);
    }

    /// <summary>
    /// Tests that Aseguramiento documents are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_AseguramientoDocument_ReturnsAseguramiento()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "ASEGURAMIENTO",
                NumeroExpediente = "A/AS1-2505-088637-PHM"
            }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level1.ShouldBe(ClassificationLevel1.Aseguramiento);
        result.Value.Scores.AseguramientoScore.ShouldBeGreaterThan(70);
    }

    /// <summary>
    /// Tests that Desembargo documents are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_DesembargoDocument_ReturnsDesembargo()
    {
        // Arrange
        // Note: Using "LIBERAR" instead of "DESEMBARGO" to avoid conflict with "EMBARGO" matching Aseguramiento
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "LIBERACION",
                NumeroExpediente = "LIB-001" // Avoid /AS pattern that triggers Aseguramiento
            },
            LegalReferences = new[] { "LIBERAR", "DESEMBARGAR" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level1.ShouldBe(ClassificationLevel1.Desembargo);
        result.Value.Scores.DesembargoScore.ShouldBeGreaterThan(70);
    }

    /// <summary>
    /// Tests that Documentacion documents are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_DocumentacionDocument_ReturnsDocumentacion()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "DOCUMENTACION"
            },
            LegalReferences = new[] { "SOLICITUD DOCUMENTAL" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level1.ShouldBe(ClassificationLevel1.Documentacion);
        result.Value.Scores.DocumentacionScore.ShouldBeGreaterThan(70);
    }

    /// <summary>
    /// Tests that Level 2 classification (Especial) is detected correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_EspecialDocument_ReturnsEspecialLevel2()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "ASEGURAMIENTO",
                NumeroExpediente = "A/AS1-2505-088637-PHM" // Contains /AS for Especial
            }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level2.ShouldBe(ClassificationLevel2.Especial);
    }

    /// <summary>
    /// Tests that Judicial documents are classified with Level 2 Judicial.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_JudicialDocument_ReturnsJudicialLevel2()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "JUDICIAL"
            },
            LegalReferences = new[] { "TRIBUNAL", "JUEZ" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level2.ShouldBe(ClassificationLevel2.Judicial);
    }

    /// <summary>
    /// Tests that Hacendario documents are classified with Level 2 Hacendario.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_HacendarioDocument_ReturnsHacendarioLevel2()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "HACENDARIO"
            },
            LegalReferences = new[] { "SAT", "SHCP" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level2.ShouldBe(ClassificationLevel2.Hacendario);
    }

    /// <summary>
    /// Regression test for Story 2.1: OCR body text fed into LegalReferences must reach
    /// keyword scorer. A document whose body contains ASEGURAMIENTO 4+ times (mimicking the
    /// IMSS-2023-171230 tier-1 fixture) must classify as Aseguramiento with confidence ≥ 70.
    /// </summary>
    [Fact]
    public async Task ClassifyDocument_WithAaseguramientoBodyText_ReturnsConfidenceAbove70()
    {
        // Arrange — inline stub reproducing the OCR body-text shape (no fixture file needed).
        // The 1284-char tier-1 case contains ASEGURAMIENTO 4+ times; we replicate that signal.
        const string ocrBody =
            "INSTITUTO MEXICANO DEL SEGURO SOCIAL " +
            "ASEGURAMIENTO DE CUENTAS BANCARIAS " +
            "Con fundamento en el artículo 40-A del CFF se ordena el ASEGURAMIENTO " +
            "de los fondos depositados en las cuentas. El ASEGURAMIENTO aplica a " +
            "todas las cuentas enlistadas. Favor de acusar recibo del presente ASEGURAMIENTO.";

        var metadata = new ExtractedMetadata
        {
            // Expediente intentionally minimal — keyword signal comes from LegalReferences only,
            // proving the wiring that the orchestrators now set from OCR body text.
            Expediente = new Expediente
            {
                NumeroExpediente = "TST-001",
                AreaDescripcion = string.Empty
            },
            LegalReferences = new[] { ocrBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level1.ShouldBe(ClassificationLevel1.Aseguramiento);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }

    /// <summary>
    /// Tests that confidence score is calculated correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_StrongClassification_ReturnsHighConfidence()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "ASEGURAMIENTO",
                NumeroExpediente = "A/AS1-2505-088637-PHM"
            }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Confidence.ShouldBeGreaterThan(70);
    }

    /// <summary>
    /// Story 2.2: A document with completely empty metadata (empty OCR output, wrong language,
    /// unrecognised format) must return Unknown classification with confidence 0, not a spurious
    /// (Aseguramiento, 10) caused by dictionary-insertion-order tie-breaking on all-floor scores.
    /// </summary>
    [Fact]
    public async Task Classify_WithEmptyBodyText_ReturnsUnknownType()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = string.Empty
            },
            LegalReferences = Array.Empty<string>()
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.Unknown);
        result.Value.Confidence.ShouldBe(0);
    }

    /// <summary>
    /// Story 2.2: A document whose text contains no Spanish legal keywords must not silently
    /// classify as Aseguramiento via dictionary insertion-order. The correct sentinel is Unknown.
    /// </summary>
    [Fact]
    public async Task Classify_WithNoKeywordMatches_NeverReturnsAseguramientoByDefault()
    {
        // Arrange — noise text: no legal keyword from any category appears anywhere.
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "lorem ipsum dolor sit amet",
                NumeroExpediente = "NOISE-9999"
            },
            LegalReferences = new[] { "random unrelated text with no legal signal" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldNotBe(ClassificationLevel1.Aseguramiento);
        result.Value.Level1.ShouldBe(ClassificationLevel1.Unknown);
        result.Value.Confidence.ShouldBe(0);
    }

    /// <summary>
    /// Story 2.3: The structured boolean TieneAseguramiento (populated by fusion from the XML
    /// companion) must drive the classifier to Aseguramiento at high confidence even when no
    /// keyword appears in area, expediente number, or LegalReferences.
    /// </summary>
    [Fact]
    public async Task Classify_WithTieneAseguramientoTrue_ReturnsAseguramientoAbove80()
    {
        // Arrange — boolean flag set, all text fields empty (no keyword signal).
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                TieneAseguramiento = true,
                AreaDescripcion = string.Empty,
                NumeroExpediente = string.Empty
            },
            LegalReferences = Array.Empty<string>()
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.Aseguramiento);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(80);
    }

    /// <summary>
    /// Story 2.3: When TieneAseguramiento is false and the area/text contain no Aseguramiento
    /// keywords, the boolean must not push the result to Aseguramiento.  With no other category
    /// signal present either, the result must be Unknown (no-signal guard from Story 2.2).
    /// </summary>
    [Fact]
    public async Task Classify_WithTieneAseguramientoFalse_DoesNotReturnAseguramientoOnNameAlone()
    {
        // Arrange — boolean explicitly false, neutral non-keyword text only.
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                TieneAseguramiento = false,
                AreaDescripcion = "Informe general anual de actividades",
                NumeroExpediente = "GEN-2024-001"
            },
            LegalReferences = Array.Empty<string>()
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldNotBe(ClassificationLevel1.Aseguramiento);
        result.Value.Level1.ShouldBe(ClassificationLevel1.Unknown);
        result.Value.Confidence.ShouldBe(0);
    }

    // ── Story 2.4: PLD / OperacionesIlicitas gap-closure tests ─────────────────────────────────────
    // Each test body contains ONLY the generator-emitted prose that the old classifier missed.
    // After the Story 2.4 reconciliation the text must reach OperacionesIlicitas at confidence ≥ 70.

    /// <summary>
    /// Story 2.4 (gap 1): The pld motivacion template emits "recursos de procedencia ilícita".
    /// The old classifier missed this because (a) "OPERACIONES ILICITAS" is not a substring,
    /// (b) "LAVADO" is absent, and (c) the 70-tier "ILICITO" did not match the feminine form
    /// "ilícita" after ToUpperInvariant() yielded "ILÍCITA" (accented Í ≠ unaccented I).
    /// After Story 2.4: RemoveDiacritics normalises "ILÍCITA" → "ILICITA"; and "PROCEDENCIA ILICITA"
    /// is added to the 90-tier.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_WithPldPhraseProcedenciaIlicita_ReturnsOperacionesIlicitas()
    {
        // Arrange — minimal body: ONLY the generator pld motivacion intro phrase.
        const string pldBody = "posibles operaciones con recursos de procedencia ilícita";

        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = "TST-PLD-001"
            },
            LegalReferences = new[] { pldBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.OperacionesIlicitas);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }

    /// <summary>
    /// Story 2.4 (gap 2): The pld generate_instrucciones_cuentas emits "operaciones inusuales".
    /// The old classifier had no keyword matching this phrase.
    /// After Story 2.4: "OPERACIONES INUSUALES" added to 90-tier.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_WithPldPhraseOperacionesInusuales_ReturnsOperacionesIlicitas()
    {
        // Arrange — only the exact generator instruction phrase that was previously missed.
        const string pldBody = "Identificar operaciones inusuales en las cuentas auditadas";

        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = "TST-PLD-002"
            },
            LegalReferences = new[] { pldBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.OperacionesIlicitas);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }

    /// <summary>
    /// Story 2.4 (gap 3): The variation_engine pld openings include "operaciones sospechosas".
    /// The old classifier had no keyword matching this phrase.
    /// After Story 2.4: "OPERACIONES SOSPECHOSAS" added to 90-tier.
    /// Note: body avoids "reporte" to prevent an Informacion=90 tie (both categories at 90
    /// would resolve to Informacion by dictionary insertion order; "reportes de operaciones
    /// sospechosas" is the generator opening but here we isolate only the sospechosas signal).
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_WithPldPhraseOperacionesSospechosas_ReturnsOperacionesIlicitas()
    {
        // Arrange — the sospechosas signal without "reportes" to keep Informacion at 10.
        const string pldBody = "Con motivo de la detección de operaciones sospechosas en el sistema";

        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = "TST-PLD-003"
            },
            LegalReferences = new[] { pldBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.OperacionesIlicitas);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }

    /// <summary>
    /// Story 2.4 (gap 4): The pld legal articles include "LFPIORPI artículos 17, 18 y 23".
    /// This abbreviation (Ley Federal para la Prevención e Identificación de Operaciones con
    /// Recursos de Procedencia Ilícita) uniquely identifies the PLD legal framework.
    /// After Story 2.4: "LFPIORPI" added to 90-tier.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_WithPldLawAbbreviationLfpiorpi_ReturnsOperacionesIlicitas()
    {
        // Arrange — LFPIORPI appears verbatim in the pld legal articles generated by the corpus.
        const string pldBody = "LFPIORPI artículos 17, 18 y 23";

        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = "TST-PLD-004"
            },
            LegalReferences = new[] { pldBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.OperacionesIlicitas);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }

    /// <summary>
    /// Story 2.4 (gap 5): The UIF (Unidad de Inteligencia Financiera) is the primary pld authority
    /// in the generator; its area names and facultades text contain "inteligencia financiera".
    /// After Story 2.4: "INTELIGENCIA FINANCIERA" added to 90-tier.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_WithPldAuthorityInteligenciaFinanciera_ReturnsOperacionesIlicitas()
    {
        // Arrange — UIF authority area text that was previously unmatched.
        const string pldBody = "Derivado de los análisis de inteligencia financiera sobre las operaciones";

        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = "TST-PLD-005"
            },
            LegalReferences = new[] { pldBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.OperacionesIlicitas);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }

    /// <summary>
    /// Story 2.4 (gap 6): The 70-tier previously checked only "ILICITO" (masculine form).
    /// "ilícita" (feminine form, used in "conducta ilícita", "actividad ilícita") was missed
    /// because ToUpperInvariant() yields "ILÍCITA" (accented Í) which does not contain "ILICITO".
    /// After Story 2.4: RemoveDiacritics normalises "ILÍCITA" → "ILICITA"; the 70-tier now
    /// checks the prefix "ILICIT" which matches all gender/number inflections.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_WithAccentedIlicita_ReturnsOperacionesIlicitas()
    {
        // Arrange — feminine accented form that the old "ILICITO" check missed entirely.
        const string pldBody = "la conducta ilícita investigada en las cuentas";

        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = "TST-PLD-006"
            },
            LegalReferences = new[] { pldBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.OperacionesIlicitas);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }

    // ── Story 2.5: Confidence monotonicity ──────────────────────────────────────────────────────────
    //
    // CalculateConfidence is a separation/clarity score, not a calibrated probability.
    // For single-dominant-category inputs (one category carries signal, all others at the 10-floor)
    // it must decrease strictly as match strength decreases:
    //   strong keyword (score 90) → diff=80 ≥ 60 → confidence = Math.Min(100, 90) = 90
    //   weak keyword   (score 70) → diff=60 ≥ 60 → confidence = Math.Min(100, 70) = 70
    //   no keyword     (all=10)   → maxScore == NoMatchFloor      → confidence = 0
    // Result: 90 > 70 > 0 — strictly descending.
    //
    // NOTE — Confidence_IsZeroForNoSignalDocument: the zero-coverage case is already covered by
    // Classify_WithEmptyBodyText_ReturnsUnknownType, Classify_WithNoKeywordMatches_NeverReturnsAseguramientoByDefault,
    // and Classify_NoKeyword_AllScoresDefaultTo10 (all assert Confidence == 0). Adding a duplicate
    // test would add no new failure surface; existing coverage is sufficient per Story 2.5 DoD.

    /// <summary>
    /// Story 2.5: Confidence must be strictly monotonically decreasing as single-category
    /// match strength decreases: strong (90) &gt; weak (70) &gt; no-match (0).
    /// Inputs are designed so only the Aseguramiento category varies; all others stay at the
    /// 10-floor to avoid a competing-category tie that would collapse the difference and muddy
    /// the comparison.
    /// </summary>
    [Theory]
    [InlineData("ASEGURAMIENTO", 90)] // strong keyword → Aseguramiento=90, rest=10, diff=80 ≥ 60 → 90
    [InlineData("ASEGURAR", 70)]      // weak keyword   → Aseguramiento=70, rest=10, diff=60 ≥ 60 → 70
    [InlineData("lorem ipsum no keywords aqui", 0)] // no match → all=10, maxScore==floor → 0
    public async Task Confidence_ScalesMonotonicallyWithMatchStrength(string bodyText, int expectedConfidence)
    {
        // Arrange — only LegalReferences varies; area and expediente are neutral non-keyword values
        // so they do not trigger any category keyword and keep all scores at the 10-floor except
        // the one driven by the body text above.
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = "TST-MONO-001"
            },
            LegalReferences = new[] { bodyText }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Confidence.ShouldBe(expectedConfidence,
            $"bodyText='{bodyText}' should yield confidence {expectedConfidence}");
    }

    /// <summary>
    /// Story 2.4 (accent cross-category fix): The informacion motivacion template emits
    /// "se requiere información bancaria del contribuyente" — the accented "información" was
    /// previously missed because ToUpperInvariant() yields "INFORMACIÓN" (accented Í) which
    /// does not equal "INFORMACION". After Story 2.4: RemoveDiacritics → "INFORMACION" ✓.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_WithAccentedInformacion_ReturnsInformacion()
    {
        // Arrange — the exact generator informacion motivacion phrase with accented "información".
        const string infoBody = "se requiere información bancaria del contribuyente para verificar el cumplimiento";

        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = string.Empty,
                NumeroExpediente = "TST-INF-001"
            },
            LegalReferences = new[] { infoBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Level1.ShouldBe(ClassificationLevel1.Informacion);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }
}

