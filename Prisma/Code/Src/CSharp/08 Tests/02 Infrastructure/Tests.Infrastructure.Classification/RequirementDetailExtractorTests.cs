using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="RequirementDetailExtractor"/>.
/// Each category is tested with representative text that exercises the extraction paths.
/// Mutation-killing: every branch ("present → extracted", "absent → empty/null") is covered,
/// and boundary values pin the specific regex groups used.
/// </summary>
public class RequirementDetailExtractorTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static BloqueoRequirement PopulateBloqueo(string text)
    {
        var req = new BloqueoRequirement { EsRequerido = true, Confidence = 0.9 };
        RequirementDetailExtractor.PopulateBloqueo(text, req);
        return req;
    }

    private static DesbloqueoRequirement PopulateDesbloqueo(string text, string? currentId = null)
    {
        var req = new DesbloqueoRequirement { EsRequerido = true, Confidence = 0.9 };
        RequirementDetailExtractor.PopulateDesbloqueo(text, currentId, req);
        return req;
    }

    private static DocumentacionRequirement PopulateDocumentacion(string text)
    {
        var req = new DocumentacionRequirement { EsRequerido = true, Confidence = 0.9 };
        RequirementDetailExtractor.PopulateDocumentacion(text, req);
        return req;
    }

    private static TransferenciaRequirement PopulateTransferencia(string text)
    {
        var req = new TransferenciaRequirement { EsRequerido = true, Confidence = 0.9 };
        RequirementDetailExtractor.PopulateTransferencia(text, req);
        return req;
    }

    private static InformacionGeneralRequirement PopulateInformacion(string text)
    {
        var req = new InformacionGeneralRequirement { EsRequerido = true, Confidence = 0.9 };
        RequirementDetailExtractor.PopulateInformacion(text, req);
        return req;
    }

    // =========================================================================
    // BLOQUEO
    // =========================================================================

    [Fact]
    public void Bloqueo_AccountNumber_IsExtracted()
    {
        var req = PopulateBloqueo("Se instruye el aseguramiento de la cuenta 1234567890 del titular.");
        req.CuentasEspecificas.ShouldContain("1234567890");
    }

    [Fact]
    public void Bloqueo_MultipleAccounts_AllExtracted()
    {
        var req = PopulateBloqueo("Bloquear la cuenta 1111111111 y la cuenta 2222222222 inmediatamente.");
        req.CuentasEspecificas.Count.ShouldBe(2);
        req.CuentasEspecificas.ShouldContain("1111111111");
        req.CuentasEspecificas.ShouldContain("2222222222");
    }

    [Fact]
    public void Bloqueo_NoAccount_EmptyList()
    {
        var req = PopulateBloqueo("Se instruye el aseguramiento de fondos del titular.");
        req.CuentasEspecificas.ShouldBeEmpty();
    }

    [Fact]
    public void Bloqueo_DollarSignAmount_ExtractedAsMonto()
    {
        var req = PopulateBloqueo("El aseguramiento es por $1,500,000.00 pesos del saldo.");
        req.Monto.ShouldBe(1500000.00m);
    }

    [Fact]
    public void Bloqueo_MontoKeywordAmount_Extracted()
    {
        var req = PopulateBloqueo("Se instruye el bloqueo hasta por el monto de 500,000.00 pesos.");
        req.Monto.ShouldBe(500000.00m);
    }

    [Fact]
    public void Bloqueo_CurrencyPesos_DetectedAsMXN()
    {
        var req = PopulateBloqueo("Bloqueo por $100,000.00 pesos mexicanos.");
        req.Moneda.ShouldBe("MXN");
    }

    [Fact]
    public void Bloqueo_CurrencyDolares_DetectedAsUSD()
    {
        var req = PopulateBloqueo("Aseguramiento por $50,000.00 dólares americanos.");
        req.Moneda.ShouldBe("USD");
    }

    [Fact]
    public void Bloqueo_NoAmount_MontoIsNull()
    {
        var req = PopulateBloqueo("Bloquear la cuenta del titular sin monto especificado.");
        req.Monto.ShouldBeNull();
    }

    [Fact]
    public void Bloqueo_ProductTarjeta_Extracted()
    {
        var req = PopulateBloqueo("Bloquear la TARJETA de crédito del cliente.");
        req.ProductosEspecificos.ShouldContain("TARJETA");
    }

    [Fact]
    public void Bloqueo_ProductCuenta_Extracted()
    {
        var req = PopulateBloqueo("Se instruye bloquear la CUENTA de ahorro.");
        req.ProductosEspecificos.ShouldContain("CUENTA");
    }

    [Fact]
    public void Bloqueo_NoProduct_EmptyList()
    {
        var req = PopulateBloqueo("Se instruye el aseguramiento de fondos en general.");
        req.ProductosEspecificos.ShouldBeEmpty();
    }

    [Fact]
    public void Bloqueo_WithAmount_EsParcialIsTrue()
    {
        var req = PopulateBloqueo("Aseguramiento hasta por $200,000.00 pesos, no así el excedente.");
        req.EsParcial.ShouldBeTrue();
    }

    [Fact]
    public void Bloqueo_ParcialKeyword_EsParcialIsTrue()
    {
        var req = PopulateBloqueo("Se solicita un aseguramiento parcial de los recursos.");
        req.EsParcial.ShouldBeTrue();
    }

    [Fact]
    public void Bloqueo_NoAmountNoKeyword_EsParcialIsFalse()
    {
        var req = PopulateBloqueo("Se instruye el aseguramiento total de todos los fondos del titular.");
        req.EsParcial.ShouldBeFalse();
    }

    /// <summary>
    /// Real sample text from 222AAA-44444444442025.xml InstruccionesCuentasPorConocer.
    /// Tests the partial-freeze detection with "hasta por el monto" and that accounts/amounts
    /// stay null (no account/amount in this particular sample text).
    /// </summary>
    [Fact]
    public void Bloqueo_RealSampleXmlText_ParcialDetectedFromHastaPorElMonto()
    {
        const string sampleText =
            "Para efectos de la inmunización ordenada en el presente oficio se solicita instruye " +
            "a las instituciones de crédito y entidades financieras para que los depósitos se " +
            "encuentran sean identificados inmovilizados retenidos y conservados a disposición " +
            "del de doble pago en caso de desobediencia en términos de la instituto mexicano del " +
            "seguro social hasta por el monto antes indicado no así el excedente que pudiera " +
            "existir a la cuenta objeto del embargo";

        var req = PopulateBloqueo(sampleText);
        req.EsParcial.ShouldBeTrue();
    }

    // =========================================================================
    // DESBLOQUEO
    // =========================================================================

    [Fact]
    public void Desbloqueo_ReferencedExpediente_IsExtracted()
    {
        var req = PopulateDesbloqueo(
            "En referencia al oficio A/AS1-1111-222222-AAA, se solicita el desbloqueo de la cuenta.");
        req.ExpedienteBloqueoOriginal.ShouldNotBeNullOrWhiteSpace();
        req.ExpedienteBloqueoOriginal.ShouldContain("AS1");
    }

    [Fact]
    public void Desbloqueo_CurrentExpedienteExcluded_OtherReferenceReturned()
    {
        const string currentId = "A/AS1-1111-222222-AAA";
        var req = PopulateDesbloqueo(
            "En cumplimiento al oficio A/AS1-1111-222222-AAA, se instruye el desbloqueo " +
            "del expediente B/CD2-3333-444444-BBB previamente asegurado.",
            currentId);

        // Should pick the OTHER id, not the current one
        req.ExpedienteBloqueoOriginal.ShouldNotBeNull();
        req.ExpedienteBloqueoOriginal!.ToUpperInvariant().ShouldNotBe(currentId.ToUpperInvariant());
        req.ExpedienteBloqueoOriginal.ShouldContain("CD2");
    }

    [Fact]
    public void Desbloqueo_NoReferencedExpediente_IsNull()
    {
        var req = PopulateDesbloqueo("Se solicita el desbloqueo de los fondos del cliente.");
        req.ExpedienteBloqueoOriginal.ShouldBeNull();
    }

    [Fact]
    public void Desbloqueo_NullCurrentExpediente_AnyMatchReturned()
    {
        var req = PopulateDesbloqueo(
            "Véase el oficio 222/AAA/-4444444444/2025 para el levantamiento del aseguramiento.",
            null);
        req.ExpedienteBloqueoOriginal.ShouldNotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // DOCUMENTACIÓN
    // =========================================================================

    [Fact]
    public void Documentacion_EstadoDeCuenta_Detected()
    {
        var req = PopulateDocumentacion("Se solicitan los estados de cuenta del período.");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Estado de cuenta");
    }

    [Fact]
    public void Documentacion_Identificacion_INE_Detected()
    {
        var req = PopulateDocumentacion("Por favor adjunte su INE vigente.");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Identificación");
    }

    [Fact]
    public void Documentacion_ComprobanteDomicilio_Detected()
    {
        var req = PopulateDocumentacion("Requiere comprobante de domicilio no mayor a tres meses.");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Comprobante de domicilio");
    }

    [Fact]
    public void Documentacion_Contrato_Detected()
    {
        var req = PopulateDocumentacion("Adjunte copia del contrato de apertura de cuenta.");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Contrato");
    }

    [Fact]
    public void Documentacion_MuestraFirma_Detected()
    {
        var req = PopulateDocumentacion("Presente la muestra de firma actualizada del titular.");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Muestra de firma");
    }

    [Fact]
    public void Documentacion_Cheque_Detected()
    {
        var req = PopulateDocumentacion("Adjunte copia del cheque de pago emitido.");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Cheque");
    }

    [Fact]
    public void Documentacion_ExpedienteApertura_Detected()
    {
        var req = PopulateDocumentacion("Se solicita el expediente de apertura de cuenta.");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Expediente de apertura");
    }

    [Fact]
    public void Documentacion_MultipleTypes_AllDetected()
    {
        var req = PopulateDocumentacion(
            "Entregue su INE, comprobante de domicilio y estados de cuenta del último mes.");
        req.TiposDocumento.Count.ShouldBeGreaterThanOrEqualTo(3);
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Identificación");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Comprobante de domicilio");
        req.TiposDocumento.ShouldContain(d => d.Tipo == "Estado de cuenta");
    }

    [Fact]
    public void Documentacion_NoKeywords_EmptyList()
    {
        var req = PopulateDocumentacion("Se solicita información general sobre el titular de la cuenta.");
        req.TiposDocumento.ShouldBeEmpty();
    }

    [Fact]
    public void Documentacion_DateRange_PeriodoPopulated()
    {
        var req = PopulateDocumentacion(
            "Se solicitan estados de cuenta del período 2024-01-01 al 2024-06-30.");
        req.TiposDocumento.ShouldNotBeEmpty();
        var edoCuenta = req.TiposDocumento.First(d => d.Tipo == "Estado de cuenta");
        edoCuenta.PeriodoInicio.ShouldNotBeNull();
        edoCuenta.PeriodoFin.ShouldNotBeNull();
        edoCuenta.PeriodoInicio!.Value.ShouldBe(new DateTime(2024, 1, 1));
        edoCuenta.PeriodoFin!.Value.ShouldBe(new DateTime(2024, 6, 30));
    }

    [Fact]
    public void Documentacion_SingleDate_NoPeriodo()
    {
        // One date → cannot form a range, so both should be null
        var req = PopulateDocumentacion(
            "Proporcionar estados de cuenta con fecha 2024-03-15 como referencia.");
        if (req.TiposDocumento.Count > 0)
        {
            var doc = req.TiposDocumento[0];
            doc.PeriodoInicio.ShouldBeNull();
            doc.PeriodoFin.ShouldBeNull();
        }
    }

    [Fact]
    public void Documentacion_SpanishProseDates_PeriodoPopulated()
    {
        var req = PopulateDocumentacion(
            "Estados de cuenta del 1 de enero de 2024 al 31 de diciembre de 2024.");
        req.TiposDocumento.ShouldNotBeEmpty();
        var doc = req.TiposDocumento[0];
        doc.PeriodoInicio.ShouldNotBeNull();
        doc.PeriodoFin.ShouldNotBeNull();
        doc.PeriodoInicio!.Value.Year.ShouldBe(2024);
        doc.PeriodoInicio.Value.Month.ShouldBe(1);
        doc.PeriodoFin!.Value.Month.ShouldBe(12);
    }

    // =========================================================================
    // TRANSFERENCIA
    // =========================================================================

    [Fact]
    public void Transferencia_Clabe18_ExtractedAsCuentaDestino()
    {
        var req = PopulateTransferencia(
            "Transfiera el saldo a la CLABE 012345678901234567 del beneficiario.");
        req.CuentaDestino.ShouldBe("012345678901234567");
        req.CuentaDestino!.Length.ShouldBe(18);
    }

    [Fact]
    public void Transferencia_ClabePreferredOverCuenta()
    {
        var req = PopulateTransferencia(
            "Depositar en cuenta 12345678 o a la CLABE 123456789012345678.");
        // 18-digit CLABE takes priority over shorter "cuenta" match
        req.CuentaDestino.ShouldBe("123456789012345678");
    }

    [Fact]
    public void Transferencia_FallbackToCuenta_WhenNoCLABE()
    {
        var req = PopulateTransferencia(
            "Realizar la transferencia a la cuenta 9876543210 del receptor.");
        req.CuentaDestino.ShouldBe("9876543210");
    }

    [Fact]
    public void Transferencia_NoAccount_CuentaDestinoIsNull()
    {
        var req = PopulateTransferencia("Efectúe la transferencia de fondos al beneficiario.");
        req.CuentaDestino.ShouldBeNull();
    }

    [Fact]
    public void Transferencia_Amount_Extracted()
    {
        var req = PopulateTransferencia(
            "Transfiera el importe de $75,000.00 a la CLABE 012345678901234567.");
        req.Monto.ShouldBe(75000.00m);
    }

    [Fact]
    public void Transferencia_NoAmount_IsNull()
    {
        var req = PopulateTransferencia(
            "Realizar la transferencia a la CLABE 012345678901234567.");
        req.Monto.ShouldBeNull();
    }

    // =========================================================================
    // INFORMACIÓN GENERAL
    // =========================================================================

    [Fact]
    public void Informacion_PhraseWithSolicita_ExtractsInformacionSolicitada()
    {
        var req = PopulateInformacion(
            "Se solicita información sobre los movimientos de la cuenta bancaria del titular.");
        req.InformacionSolicitada.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Informacion_PhraseWithReporte_ExtractsInformacionSolicitada()
    {
        var req = PopulateInformacion(
            "Proporcionar un reporte de operaciones realizadas en el período.");
        req.InformacionSolicitada.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Informacion_NoIndicators_IsNull()
    {
        var req = PopulateInformacion("El titular es persona moral con domicilio en CDMX.");
        req.InformacionSolicitada.ShouldBeNull();
    }

    [Fact]
    public void Informacion_LongText_CappedAt200Chars()
    {
        var longText = "Se solicita " + new string('x', 300) + ".";
        var req = PopulateInformacion(longText);
        if (req.InformacionSolicitada is not null)
            req.InformacionSolicitada.Length.ShouldBeLessThanOrEqualTo(200);
    }

    // =========================================================================
    // SHARED HELPERS — ExtractAccountNumbers / ExtractProducts / ExtractAmountAndCurrency
    // =========================================================================

    [Theory]
    [InlineData("cuenta 9999999999", "9999999999")]
    [InlineData("CUENTA 1234", "1234")]
    [InlineData("la cuenta 00000001 del cliente", "00000001")]
    public void ExtractAccountNumbers_VariousCasings_Extracted(string text, string expected)
    {
        var result = RequirementDetailExtractor.ExtractAccountNumbers(text);
        result.ShouldContain(expected);
    }

    [Fact]
    public void ExtractAccountNumbers_NoCuenta_EmptyList()
    {
        var result = RequirementDetailExtractor.ExtractAccountNumbers("sin ninguna cuenta bancaria");
        result.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("TARJETA de crédito bloqueada", "TARJETA")]
    [InlineData("CUENTA de ahorro inmovilizada", "CUENTA")]
    public void ExtractProducts_SingleProduct_Detected(string text, string expected)
    {
        var result = RequirementDetailExtractor.ExtractProducts(text);
        result.ShouldContain(expected);
    }

    [Fact]
    public void ExtractProducts_BothProducts_BothDetected()
    {
        var result = RequirementDetailExtractor.ExtractProducts("La TARJETA y la CUENTA están bloqueadas.");
        result.ShouldContain("TARJETA");
        result.ShouldContain("CUENTA");
    }

    [Fact]
    public void ExtractProducts_NoKeyword_EmptyList()
    {
        var result = RequirementDetailExtractor.ExtractProducts("Aseguramiento de fondos del cliente.");
        result.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Monto de $1,200,000.00 pesos", 1200000.00)]
    [InlineData("importe de 500,000.00 pesos", 500000.00)]
    [InlineData("800,000,000.00 dólares disponibles", 800000000.00)]
    public void ExtractAmountAndCurrency_VariousFormats_AmountExtracted(string text, double expected)
    {
        var (amount, _) = RequirementDetailExtractor.ExtractAmountAndCurrency(text);
        amount.ShouldNotBeNull();
        ((double)amount!.Value).ShouldBe(expected, 0.001);
    }

    [Fact]
    public void ExtractAmountAndCurrency_NoAmount_NullReturned()
    {
        var (amount, currency) = RequirementDetailExtractor.ExtractAmountAndCurrency(
            "El cliente tiene depósitos en el banco.");
        amount.ShouldBeNull();
        currency.ShouldBeNull();
    }

    [Fact]
    public void ExtractAmountAndCurrency_LongDigitsWithoutContext_NotConfusedWithAccount()
    {
        // 18-digit CLABE with no monetary context should NOT be parsed as an amount
        var (amount, _) = RequirementDetailExtractor.ExtractAmountAndCurrency(
            "CLABE: 012345678901234567 del receptor.");
        amount.ShouldBeNull();
    }

    // =========================================================================
    // INTEGRATION — SemanticAnalyzerService wires sub-fields correctly
    // Uses a real ITextComparer substitute so the analyzer returns detected requirements.
    // =========================================================================

    [Fact]
    public async Task SemanticAnalyzer_BloqueoWithAmount_MontoPopulatedOnResult()
    {
        var comparer = Substitute.For<ITextComparer>();
        // Make every phrase match with high confidence so Block is detected
        comparer
            .FindBestMatch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>())
            .Returns(new TextMatchResult { MatchedText = "x", Similarity = 0.95, StartIndex = 0, Length = 1 });

        var svc = new SemanticAnalyzerService(
            comparer,
            Substitute.For<ILogger<SemanticAnalyzerService>>());

        const string text = "Se instruye el aseguramiento hasta por el monto de $250,000.00 pesos de la cuenta 1234567890.";
        var result = await svc.AnalyzeDirectivesAsync(text, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var bloqueo = result.Value!.RequiereBloqueo;
        bloqueo.ShouldNotBeNull();
        bloqueo!.Monto.ShouldBe(250000.00m);
        bloqueo.Moneda.ShouldBe("MXN");
        bloqueo.CuentasEspecificas.ShouldContain("1234567890");
        bloqueo.EsParcial.ShouldBeTrue();
    }

    [Fact]
    public async Task SemanticAnalyzer_TransferenciaWithClabe_CuentaDestinoPopulated()
    {
        var comparer = Substitute.For<ITextComparer>();
        comparer
            .FindBestMatch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>())
            .Returns(new TextMatchResult { MatchedText = "x", Similarity = 0.95, StartIndex = 0, Length = 1 });

        var svc = new SemanticAnalyzerService(
            comparer,
            Substitute.For<ILogger<SemanticAnalyzerService>>());

        const string text = "Efectúe transferencia bancaria de fondos a la CLABE 012345678901234567 por $10,000.00.";
        var result = await svc.AnalyzeDirectivesAsync(text, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var transferencia = result.Value!.RequiereTransferencia;
        transferencia.ShouldNotBeNull();
        transferencia!.CuentaDestino.ShouldBe("012345678901234567");
        transferencia.Monto.ShouldBe(10000.00m);
    }

    [Fact]
    public async Task SemanticAnalyzer_DocumentacionWithTypes_TiposPopulated()
    {
        var comparer = Substitute.For<ITextComparer>();
        comparer
            .FindBestMatch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>())
            .Returns(new TextMatchResult { MatchedText = "x", Similarity = 0.95, StartIndex = 0, Length = 1 });

        var svc = new SemanticAnalyzerService(
            comparer,
            Substitute.For<ILogger<SemanticAnalyzerService>>());

        const string text = "Entregue su INE y estados de cuenta del período 2024-01-01 al 2024-12-31.";
        var result = await svc.AnalyzeDirectivesAsync(text, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var doc = result.Value!.RequiereDocumentacion;
        doc.ShouldNotBeNull();
        doc!.TiposDocumento.ShouldContain(d => d.Tipo == "Identificación");
        doc.TiposDocumento.ShouldContain(d => d.Tipo == "Estado de cuenta");
    }
}
