using System.IO;
using ClosedXML.Excel;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export.Adaptive;

/// <summary>
/// Unit tests for <see cref="DatosCargaOficioLayoutGenerator"/>.
/// Follows ITDD per ADR-005: tests were written before the implementation.
/// </summary>
public sealed class DatosCargaOficioLayoutGeneratorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // -------------------------------------------------------------------------
    // 24-column header constant (display order drives position)
    // -------------------------------------------------------------------------

    private static readonly string[] ExpectedHeaders =
    [
        "Procedencia",
        "Numero de expediente",
        "Oficio",
        "Fecha de registro",
        "Fecha de recepción",
        "Días",
        "Fecha estimada de conclusión",
        "Estatus",
        "Tipo de asunto",
        "Grupo",
        "Área remitente",
        "Subdivisión",
        "Entidad Financiera",
        "Descripción",
        "Nombre del remitente",
        "Origen",
        "Tipo de documento",
        "Medio de seguimiento",
        "Nombre Abogado Interno",
        "Nombre abogado responsable",
        "Despacho",
        "Estado",
        "Ciudad",
        "Zona",
    ];

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private static UnifiedMetadataRecord BuildRecord(
        string numeroExpediente = "A/AS1-2505-088637-PHM",
        string numeroOficio = "214-1-18714972/2025",
        int diasPlazo = 15,
        bool tieneAseguramiento = true,
        string areaDescripcion = "ASEGURAMIENTO",
        LegalSubdivisionKind? subdivision = null,
        string autoridad = "CNBV-TEST",
        List<SolicitudParte>? partes = null,
        List<ComplianceAction>? actions = null,
        DateTime? fechaRecepcion = null,
        DateTime? fechaEstimadaConclusion = null)
    {
        var exp = new Expediente
        {
            NumeroExpediente = numeroExpediente,
            NumeroOficio = numeroOficio,
            DiasPlazo = diasPlazo,
            TieneAseguramiento = tieneAseguramiento,
            AreaDescripcion = areaDescripcion,
            Subdivision = subdivision ?? LegalSubdivisionKind.A_AS,
            AutoridadNombre = autoridad,
            FechaRecepcion = fechaRecepcion ?? new DateTime(2026, 6, 14),
            FechaRegistro = fechaRecepcion ?? new DateTime(2026, 6, 14),
            FechaEstimadaConclusion = fechaEstimadaConclusion ?? new DateTime(2026, 7, 5),
            SolicitudPartes = partes ?? new List<SolicitudParte>
            {
                new() { Nombre = "EUGENIO", Paterno = "GARCIA", Materno = "ZAVALA" }
            },
        };

        return new UnifiedMetadataRecord
        {
            Expediente = exp,
            ComplianceActions = actions ?? new List<ComplianceAction>(),
        };
    }

    private static DatosCargaOficioLayoutGenerator CreateSut(ITemplateRepository? repo = null)
    {
        var mapper = new TemplateFieldMapper(NullLogger<TemplateFieldMapper>.Instance);
        return new DatosCargaOficioLayoutGenerator(
            mapper,
            NullLogger<DatosCargaOficioLayoutGenerator>.Instance,
            repo);
    }

    // -------------------------------------------------------------------------
    // TC-1: 24 headers in exact display order
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_ProducesWorksheet_With24HeadersInOrder()
    {
        var sut = CreateSut();
        using var stream = new MemoryStream();

        var result = await sut.GenerateAsync(BuildRecord(), stream, Ct);

        result.IsSuccess.ShouldBeTrue(result.Error ?? "");

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        for (int i = 0; i < ExpectedHeaders.Length; i++)
        {
            ws.Cell(1, i + 1).GetString().ShouldBe(
                ExpectedHeaders[i],
                $"Column {i + 1} header mismatch");
        }

        ws.LastColumnUsed()!.ColumnNumber().ShouldBe(24, "Worksheet must have exactly 24 columns");
    }

    // -------------------------------------------------------------------------
    // TC-2: Fixed values are present
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_FixedValues_ArePresent()
    {
        var sut = CreateSut();
        using var stream = new MemoryStream();

        await sut.GenerateAsync(BuildRecord(), stream, Ct);

        var row2 = ReadDataRow(stream);

        row2[0].ShouldBe("C.N.B.V. JUZGADOS",   "Col 1  Procedencia");
        row2[7].ShouldBe("registrado",             "Col 8  Estatus");
        row2[9].ShouldBe("CNBV - Filiales",       "Col 10 Grupo");
        row2[10].ShouldBe("COMISION NACIONAL BANCARIA Y DE VALORES", "Col 11 Área remitente");
        row2[12].ShouldBe("Banco",                "Col 13 Entidad Financiera");
        row2[15].ShouldBe("Oficio",               "Col 16 Origen");
        row2[16].ShouldBe("Oficio",               "Col 17 Tipo de documento");
        row2[17].ShouldBe("Carta",                "Col 18 Medio de seguimiento");
        row2[18].ShouldBe("Airam Zepol Zepol",   "Col 19 Nombre Abogado Interno");
        row2[19].ShouldBe("Nauj Zerep Zerep",     "Col 20 Nombre abogado responsable");
        row2[20].ShouldBe("El abogado justo",     "Col 21 Despacho");
        row2[21].ShouldBe("CIUDAD DE MEXICO",     "Col 22 Estado");
        row2[22].ShouldBe("MEXICO",               "Col 23 Ciudad");
        row2[23].ShouldBe("METROPOLITANO",         "Col 24 Zona");
    }

    // -------------------------------------------------------------------------
    // TC-3: Extracted/calculated values are mapped from the record
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_ExtractedValues_MappedFromRecord()
    {
        var partes = new List<SolicitudParte>
        {
            new() { Nombre = "EUGENIO", Paterno = "GARCIA", Materno = "ZAVALA" }
        };

        var record = BuildRecord(
            numeroExpediente: "A/AS1-2505-999-PHM",
            numeroOficio: "214-1-00000001/2026",
            diasPlazo: 20,
            subdivision: LegalSubdivisionKind.J_AS,
            autoridad: "CNBV-JUZGADOS",
            partes: partes,
            fechaRecepcion: new DateTime(2026, 6, 14),
            fechaEstimadaConclusion: new DateTime(2026, 7, 10));

        var sut = CreateSut();
        using var stream = new MemoryStream();
        await sut.GenerateAsync(record, stream, Ct);

        var row2 = ReadDataRow(stream);

        row2[1].ShouldBe("A/AS1-2505-999-PHM",         "Col 2 NumeroExpediente");
        row2[2].ShouldBe("214-1-00000001/2026",          "Col 3 Oficio");
        row2[5].ShouldBe("20",                            "Col 6 Días");
        row2[11].ShouldBe("J/AS",                         "Col 12 Subdivisión (slash code)");
        row2[13].ShouldBe("GARCIA ZAVALA EUGENIO",        "Col 14 Descripción (Paterno Materno Nombre)");
        row2[14].ShouldBe("CNBV-JUZGADOS",               "Col 15 Nombre del remitente");
    }

    // -------------------------------------------------------------------------
    // TC-4: Date fields are ISO yyyy-MM-dd
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_DateFields_AreIsoFormat()
    {
        var record = BuildRecord(
            fechaRecepcion: new DateTime(2026, 6, 14),
            fechaEstimadaConclusion: new DateTime(2026, 7, 5));

        var sut = CreateSut();
        using var stream = new MemoryStream();
        await sut.GenerateAsync(record, stream, Ct);

        var row2 = ReadDataRow(stream);

        row2[3].ShouldBe("2026-06-14", "Col 4 Fecha de registro ISO");
        row2[4].ShouldBe("2026-06-14", "Col 5 Fecha de recepción ISO");
        row2[6].ShouldBe("2026-07-05", "Col 7 Fecha estimada de conclusión ISO");
    }

    // -------------------------------------------------------------------------
    // TC-5: Tipo de asunto — all 5 theory cases
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(true, "ASEGURAMIENTO", null, "EMBARGO")]
    [InlineData(false, "DESEMBARGO", null, "DESEMBARGO")]
    [InlineData(false, "DOCUMENTACION", null, "DOCUMENTACIÓN")]
    [InlineData(false, "NINGUNA", null, "INFORMACIÓN")]
    [InlineData(false, "NINGUNA", "Transfer", "TRANSFERENCIAS")]
    public async Task GenerateAsync_TipoAsunto_MapsCorrectly(
        bool tieneAseguramiento,
        string areaDescripcion,
        string? actionKindName,
        string expected)
    {
        List<ComplianceAction>? actions = null;
        if (actionKindName is not null)
        {
            var kind = ComplianceActionKind.FromName(actionKindName);
            actions = new List<ComplianceAction> { new() { ActionType = kind } };
        }

        var record = BuildRecord(
            tieneAseguramiento: tieneAseguramiento,
            areaDescripcion: areaDescripcion,
            actions: actions);

        var sut = CreateSut();
        using var stream = new MemoryStream();
        await sut.GenerateAsync(record, stream, Ct);

        var row2 = ReadDataRow(stream);
        row2[8].ShouldBe(expected, $"Tipo de asunto for area={areaDescripcion}, tiene={tieneAseguramiento}, action={actionKindName}");
    }

    // -------------------------------------------------------------------------
    // TC-6: Header row is bold and gray
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_HeaderRow_IsBoldAndGray()
    {
        var sut = CreateSut();
        using var stream = new MemoryStream();
        await sut.GenerateAsync(BuildRecord(), stream, Ct);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var headerCell = ws.Cell(1, 1);

        headerCell.Style.Font.Bold.ShouldBeTrue("Header row must be bold");
        headerCell.Style.Fill.BackgroundColor.ShouldBe(
            XLColor.LightGray, "Header row background must be light gray");
    }

    // -------------------------------------------------------------------------
    // TC-7: Worksheet name is "Datos Carga de Oficio"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_WorksheetName_IsDatosCargaDeOficio()
    {
        var sut = CreateSut();
        using var stream = new MemoryStream();
        await sut.GenerateAsync(BuildRecord(), stream, Ct);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        wb.Worksheets.First().Name.ShouldBe("Datos Carga de Oficio");
    }

    // -------------------------------------------------------------------------
    // TC-8: Null metadata → Result.WithFailure
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_NullMetadata_ReturnsFailure()
    {
        var sut = CreateSut();
        using var stream = new MemoryStream();

        var result = await sut.GenerateAsync(null!, stream, Ct);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
    }

    // -------------------------------------------------------------------------
    // TC-9: Null stream → Result.WithFailure
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_NullStream_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.GenerateAsync(BuildRecord(), null!, Ct);

        result.IsSuccess.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // TC-10: Non-writable stream → Result.WithFailure
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_NonWritableStream_ReturnsFailure()
    {
        var sut = CreateSut();
        var tmpFile = Path.GetTempFileName();
        try
        {
            await using var readOnly = new FileStream(tmpFile, FileMode.Open, FileAccess.Read);
            var result = await sut.GenerateAsync(BuildRecord(), readOnly, Ct);
            result.IsSuccess.ShouldBeFalse();
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // -------------------------------------------------------------------------
    // TC-11: Cancellation before start → Cancelled result
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_CancelledToken_ReturnsCancelled()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var stream = new MemoryStream();
        var result = await sut.GenerateAsync(BuildRecord(), stream, cts.Token);

        result.IsSuccess.ShouldBeFalse();
        result.IsCancelled().ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // TC-12: No parties in SolicitudPartes → Descripción is empty
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_NoPartes_DescripcionIsEmpty()
    {
        var record = BuildRecord(partes: new List<SolicitudParte>());
        var sut = CreateSut();
        using var stream = new MemoryStream();
        await sut.GenerateAsync(record, stream, Ct);

        var row2 = ReadDataRow(stream);
        row2[13].ShouldBe(string.Empty, "Col 14 Descripción must be empty when no partes");
    }

    // -------------------------------------------------------------------------
    // TC-13: ITemplateRepository returns null → falls back to built-in template
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_TemplateRepoReturnsNull_FallsBackToBuiltIn()
    {
        var repo = Substitute.For<ITemplateRepository>();
        repo.GetLatestTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TemplateDefinition?>(null));

        var sut = CreateSut(repo);
        using var stream = new MemoryStream();

        var result = await sut.GenerateAsync(BuildRecord(), stream, Ct);

        result.IsSuccess.ShouldBeTrue("Must succeed via fallback built-in template");
        var row2 = ReadDataRow(stream);
        row2[0].ShouldBe("C.N.B.V. JUZGADOS", "Fixed value still present via built-in");
    }

    // -------------------------------------------------------------------------
    // TC-14: No Expediente on metadata → Result.WithFailure
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_NullExpediente_ReturnsFailure()
    {
        var sut = CreateSut();
        var record = new UnifiedMetadataRecord { Expediente = null };
        using var stream = new MemoryStream();

        var result = await sut.GenerateAsync(record, stream, Ct);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the stream as an XLWorkbook and reads the data row (row 2) as strings.
    /// Returns a zero-indexed array aligned to ExpectedHeaders.
    /// </summary>
    private static string[] ReadDataRow(MemoryStream stream)
    {
        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var result = new string[24];
        for (int i = 0; i < 24; i++)
        {
            result[i] = ws.Cell(2, i + 1).GetString();
        }
        return result;
    }
}
