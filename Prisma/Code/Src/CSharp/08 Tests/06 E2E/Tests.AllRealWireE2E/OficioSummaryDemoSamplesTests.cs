using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExxerCube.Prisma.Tests.AllRealWireE2E;

/// <summary>
/// Pins the demo behavior of the <c>/oficio-summary</c> page (Etapa 4 / Paso 7). The five sample
/// texts mirrored here are the exact quick-fill samples in <c>OficioSummary.razor</c>; each must
/// trigger its primary apartado through the REAL <see cref="SemanticAnalyzerService"/> so the live
/// demo is reliable. The classifier uses fuzzy phrase matching, so secondary cross-triggers (e.g.
/// "bloqueo" fuzzy-matching "desbloqueo") are tolerated — we only assert the PRIMARY category.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "DemoChecklist")]
public sealed class OficioSummaryDemoSamplesTests
{
    private static SemanticAnalyzerService BuildAnalyzer()
    {
        var textComparer = new LevenshteinTextComparer(NullLogger<LevenshteinTextComparer>.Instance);
        return new SemanticAnalyzerService(
            textComparer,
            NullLogger<SemanticAnalyzerService>.Instance,
            ollamaClient: null,
            ollamaOptions: null);
    }

    [Fact]
    public async Task BloqueoSample_TriggersBloqueo_WithSubAnswers()
    {
        var analyzer = BuildAnalyzer();
        var result = await analyzer.AnalyzeDirectivesAsync(SampleBloqueo, expediente: null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var a = result.Value!;
        a.RequiereBloqueo.ShouldNotBeNull("Bloqueo sample must detect a freeze requirement");
        a.RequiereBloqueo!.EsRequerido.ShouldBeTrue();
        a.RequiereBloqueo.CuentasEspecificas.ShouldNotBeEmpty("accounts must be extracted");
        a.RequiereBloqueo.Monto.ShouldNotBeNull("amount must be extracted");
        a.RequiereBloqueo.EsParcial.ShouldBeTrue("'carácter parcial' must set EsParcial");
        a.RequiereBloqueo.ProductosEspecificos.ShouldNotBeEmpty("products must be extracted");
    }

    [Fact]
    public async Task DesbloqueoSample_TriggersDesbloqueo()
    {
        var analyzer = BuildAnalyzer();
        var result = await analyzer.AnalyzeDirectivesAsync(SampleDesbloqueo, expediente: null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RequiereDesbloqueo.ShouldNotBeNull("Desbloqueo sample must detect an unfreeze requirement");
        result.Value!.RequiereDesbloqueo!.EsRequerido.ShouldBeTrue();
    }

    [Fact]
    public async Task DocumentacionSample_TriggersDocumentacion()
    {
        var analyzer = BuildAnalyzer();
        var result = await analyzer.AnalyzeDirectivesAsync(SampleDocumentacion, expediente: null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RequiereDocumentacion.ShouldNotBeNull("Documentación sample must detect a document requirement");
        result.Value!.RequiereDocumentacion!.EsRequerido.ShouldBeTrue();
    }

    [Fact]
    public async Task TransferenciaSample_TriggersTransferencia()
    {
        var analyzer = BuildAnalyzer();
        var result = await analyzer.AnalyzeDirectivesAsync(SampleTransferencia, expediente: null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RequiereTransferencia.ShouldNotBeNull("Transferencia sample must detect a transfer requirement");
        result.Value!.RequiereTransferencia!.EsRequerido.ShouldBeTrue();
    }

    [Fact]
    public async Task InformacionSample_TriggersInformacion()
    {
        var analyzer = BuildAnalyzer();
        var result = await analyzer.AnalyzeDirectivesAsync(SampleInformacion, expediente: null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RequiereInformacionGeneral.ShouldNotBeNull("Información sample must detect an information requirement");
        result.Value!.RequiereInformacionGeneral!.EsRequerido.ShouldBeTrue();
    }

    // ── Mirror of the OficioSummary.razor quick-fill samples (keep in sync). ──

    private const string SampleBloqueo =
        "Por medio del presente oficio, esta autoridad instruye al banco el ASEGURAMIENTO DE CUENTAS y " +
        "BLOQUEO DE CUENTAS a nombre de AEROLINEAS PAYASO ORGULLO NACIONAL, RFC APON333334. " +
        "Se ordena el aseguramiento precautorio de la cuenta 12345678 y la cuenta 87654321, " +
        "así como de los productos TARJETA DE CRÉDITO asociados. El monto a asegurar asciende a " +
        "$500,000.00 pesos (quinientos mil pesos 00/100 M.N.). El bloqueo es de carácter parcial. " +
        "Atentamente, SUBDELEGACION 8 SAN ANGEL.";

    private const string SampleDesbloqueo =
        "Por medio del presente oficio se ordena el DESBLOQUEO DE CUENTAS y el levantamiento del " +
        "aseguramiento que fue decretado previamente. Se solicita la liberación de fondos de la cuenta " +
        "12345678, que corresponde al expediente A/AS1-2505-088637-PHM que originó el bloqueo inicial. " +
        "Atentamente, SUBDELEGACION 8 SAN ANGEL.";

    private const string SampleDocumentacion =
        "Por medio del presente oficio se requiere la presentación de documentación correspondiente al " +
        "cliente identificado. Se solicita la entrega de documentación requerida, incluyendo estados de " +
        "cuenta del periodo, así como la certificación de saldos y movimientos de la cuenta 12345678. " +
        "Atentamente, SUBDELEGACION 8 SAN ANGEL.";

    private const string SampleTransferencia =
        "Por medio del presente oficio se ordena la transferencia de fondos de la cuenta del cliente hacia " +
        "la cuenta de abono 002180012345678901. El monto a transferir es de $250,000.00 pesos. Se instruye " +
        "realizar la transferencia de recursos a la brevedad. Atentamente, SUBDELEGACION 8 SAN ANGEL.";

    private const string SampleInformacion =
        "Por medio del presente oficio se requiere información relativa a las operaciones del cliente. " +
        "Se solicita información sobre los movimientos y saldos, así como cualquier dato de identificación " +
        "disponible. Atentamente, SUBDELEGACION 8 SAN ANGEL.";
}
