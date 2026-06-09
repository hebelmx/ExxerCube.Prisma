using ClosedXML.Excel;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Mutation-hardening tests for <see cref="ExcelLayoutGenerator"/> — the SIRO Excel-registration layout (FR18).
/// Each test pins exact header/cell values (by round-tripping the produced workbook through ClosedXML),
/// the validation messages, the party-row gate, and the Exception / OperationCanceledException catch paths.
/// </summary>
public class ExcelLayoutGeneratorTests
{
    private static ILogger<ExcelLayoutGenerator> Logger() => XUnitLogger.CreateLogger<ExcelLayoutGenerator>();

    private static ExcelLayoutGenerator Generator() => new(Logger());

    private static UnifiedMetadataRecord MinimalValid() => new()
    {
        Expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-088637-PHM",
            NumeroOficio = "214-1-18714972/2025",
        },
    };

    private static UnifiedMetadataRecord FullyPopulated() => new()
    {
        Expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-088637-PHM",
            NumeroOficio = "214-1-18714972/2025",
            SolicitudSiara = "SIARA-99887",
            Folio = 12345,
            OficioYear = 2025,
            AreaClave = 77,
            AreaDescripcion = "ASEGURAMIENTO",
            FechaPublicacion = new DateTime(2025, 5, 15),
            DiasPlazo = 10,
            AutoridadNombre = "CNBV",
            SolicitudPartes = new List<SolicitudParte>
            {
                new()
                {
                    ParteId = 501,
                    Caracter = "Patrón Determinado",
                    PersonaTipo = "Fisica",
                    Paterno = "Pérez",
                    Materno = "García",
                    Nombre = "Carlos",
                    Rfc = "PEGC800101AAA",
                },
            },
        },
    };

    private static XLWorkbook Load(MemoryStream stream)
    {
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    // ---------------------------------------------------------------------
    // Cancellation / null / writability guards
    // ---------------------------------------------------------------------

    /// <summary>Verifies the pre-start cancellation guard short-circuits to a Cancelled result.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_CancelledToken_ReturnsCancelled()
    {
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Generator().GenerateExcelLayoutAsync(MinimalValid(), stream, cts.Token);

        result.IsCancelled().ShouldBeTrue();
        stream.Length.ShouldBe(0);
    }

    /// <summary>Verifies null metadata yields the exact validation failure message.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_NullMetadata_ReturnsFailure()
    {
        using var stream = new MemoryStream();

        var result = await Generator().GenerateExcelLayoutAsync(null!, stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Metadata cannot be null");
    }

    /// <summary>Verifies null output stream yields the exact validation failure message.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_NullStream_ReturnsFailure()
    {
        var result = await Generator().GenerateExcelLayoutAsync(MinimalValid(), null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Output stream cannot be null");
    }

    /// <summary>Verifies a read-only stream is rejected with the exact message.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_NonWritableStream_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[16], writable: false);

        var result = await Generator().GenerateExcelLayoutAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Output stream is not writable");
    }

    /// <summary>Verifies a null Expediente (validated inside the try) is rejected with the exact message.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_NullExpediente_ReturnsFailure()
    {
        using var stream = new MemoryStream();
        var metadata = new UnifiedMetadataRecord { Expediente = null };

        var result = await Generator().GenerateExcelLayoutAsync(metadata, stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Expediente is required for Excel layout generation");
    }

    /// <summary>Verifies a valid record produces a non-empty workbook successfully.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_ValidRecord_ReturnsSuccess()
    {
        using var stream = new MemoryStream();

        var result = await Generator().GenerateExcelLayoutAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        stream.Length.ShouldBeGreaterThan(0);
    }

    // ---------------------------------------------------------------------
    // Worksheet name, header row, header styling
    // ---------------------------------------------------------------------

    /// <summary>Verifies the worksheet is named "SIRO Registration".</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_NamesWorksheetSiroRegistration()
    {
        using var stream = new MemoryStream();

        await Generator().GenerateExcelLayoutAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        using var wb = Load(stream);
        wb.Worksheet(1).Name.ShouldBe("SIRO Registration");
    }

    /// <summary>Verifies every header cell carries its exact column label.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_WritesAllHeaderLabels()
    {
        using var stream = new MemoryStream();

        await Generator().GenerateExcelLayoutAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        var expected = new[]
        {
            "NumeroExpediente", "NumeroOficio", "SolicitudSiara", "Folio", "OficioYear", "AreaClave",
            "AreaDescripcion", "FechaPublicacion", "DiasPlazo", "AutoridadNombre", "RFC", "NombreCompleto",
        };
        using var wb = Load(stream);
        var ws = wb.Worksheet(1);
        for (var i = 0; i < expected.Length; i++)
        {
            ws.Cell(1, i + 1).GetString().ShouldBe(expected[i]);
        }
    }

    /// <summary>Verifies the header row is bold and shaded light gray.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_StylesHeaderRowBoldAndGray()
    {
        using var stream = new MemoryStream();

        await Generator().GenerateExcelLayoutAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        using var wb = Load(stream);
        var headerCell = wb.Worksheet(1).Cell(1, 1);
        headerCell.Style.Font.Bold.ShouldBeTrue();
        headerCell.Style.Fill.BackgroundColor.Color.ToArgb().ShouldBe(XLColor.LightGray.Color.ToArgb());
    }

    /// <summary>Verifies columns are auto-fitted to content (the widest column grows past the default width).</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_AutoFitsColumnsToContents()
    {
        using var stream = new MemoryStream();

        await Generator().GenerateExcelLayoutAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        using var wb = Load(stream);
        // Column 1 holds the "NumeroExpediente" header (16 chars); AdjustToContents widens it well past
        // the ~8.43 default. Without the auto-fit call the column keeps the default width.
        wb.Worksheet(1).Column(1).Width.ShouldBeGreaterThan(12.0);
    }

    // ---------------------------------------------------------------------
    // Data row: expediente core values
    // ---------------------------------------------------------------------

    /// <summary>Verifies the data row carries every core expediente value at its exact column.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_WritesExpedienteCoreValues()
    {
        using var stream = new MemoryStream();

        await Generator().GenerateExcelLayoutAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        using var wb = Load(stream);
        var ws = wb.Worksheet(1);
        ws.Cell(2, 1).GetString().ShouldBe("A/AS1-2505-088637-PHM");
        ws.Cell(2, 2).GetString().ShouldBe("214-1-18714972/2025");
        ws.Cell(2, 3).GetString().ShouldBe("SIARA-99887");
        ws.Cell(2, 4).GetValue<int>().ShouldBe(12345);
        ws.Cell(2, 5).GetValue<int>().ShouldBe(2025);
        ws.Cell(2, 6).GetValue<int>().ShouldBe(77);
        ws.Cell(2, 7).GetString().ShouldBe("ASEGURAMIENTO");
        ws.Cell(2, 8).GetString().ShouldBe("2025-05-15");
        ws.Cell(2, 9).GetValue<int>().ShouldBe(10);
        ws.Cell(2, 10).GetString().ShouldBe("CNBV");
    }

    /// <summary>Verifies FechaPublicacion is written as the exact yyyy-MM-dd string.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_FormatsFechaPublicacionAsIsoDate()
    {
        using var stream = new MemoryStream();

        await Generator().GenerateExcelLayoutAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        using var wb = Load(stream);
        wb.Worksheet(1).Cell(2, 8).GetString().ShouldBe("2025-05-15");
    }

    // ---------------------------------------------------------------------
    // Party row: RFC + composed name, gate, null handling
    // ---------------------------------------------------------------------

    /// <summary>Verifies the first party's RFC and composed full name are written when a party exists.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_WithParty_WritesRfcAndComposedName()
    {
        using var stream = new MemoryStream();

        await Generator().GenerateExcelLayoutAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        using var wb = Load(stream);
        var ws = wb.Worksheet(1);
        ws.Cell(2, 11).GetString().ShouldBe("PEGC800101AAA");
        ws.Cell(2, 12).GetString().ShouldBe("Carlos Pérez García");
    }

    /// <summary>Verifies a party with a null RFC and null surnames yields an empty RFC cell and a trimmed name.</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_PartyWithNullRfcAndSurnames_WritesEmptyRfcAndTrimmedName()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.SolicitudPartes.Add(new SolicitudParte
        {
            Nombre = "Carlos",
            Rfc = null,
            Paterno = null,
            Materno = null,
        });

        await Generator().GenerateExcelLayoutAsync(metadata, stream, TestContext.Current.CancellationToken);

        using var wb = Load(stream);
        var ws = wb.Worksheet(1);
        ws.Cell(2, 11).GetString().ShouldBe(string.Empty);
        // "Carlos" + " " + "" + " " + "" => "Carlos  " then Trim() => "Carlos".
        ws.Cell(2, 12).GetString().ShouldBe("Carlos");
    }

    /// <summary>Verifies an empty party list leaves the RFC/name cells blank yet still succeeds (no index-out-of-range).</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_EmptyPartyList_LeavesPartyCellsBlankAndSucceeds()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.SolicitudPartes = new List<SolicitudParte>();

        var result = await Generator().GenerateExcelLayoutAsync(metadata, stream, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        using var wb = Load(stream);
        var ws = wb.Worksheet(1);
        ws.Cell(2, 11).GetString().ShouldBe(string.Empty);
        ws.Cell(2, 12).GetString().ShouldBe(string.Empty);
    }

    // ---------------------------------------------------------------------
    // Exception / OCE catch paths
    // ---------------------------------------------------------------------

    /// <summary>Verifies an exception during workbook save is converted to a failure result (not thrown).</summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_SaveThrows_ReturnsFailure()
    {
        using var stream = new ThrowOnWriteStream();

        var result = await Generator().GenerateExcelLayoutAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Error generating Excel layout");
    }

    /// <summary>
    /// Verifies an OperationCanceledException raised mid-save (with a now-cancelled token) is caught by the
    /// OCE filter and converted to a Cancelled result.
    /// </summary>
    [Fact]
    public async Task GenerateExcelLayoutAsync_SaveThrowsOperationCanceled_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        using var stream = new CancelOnWriteStream(cts);

        var result = await Generator().GenerateExcelLayoutAsync(MinimalValid(), stream, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // Test doubles
    // ---------------------------------------------------------------------

    /// <summary>A writable, seekable stream that throws on write to exercise the generic catch path.</summary>
    private sealed class ThrowOnWriteStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
            => throw new IOException("simulated workbook save failure");
    }

    /// <summary>
    /// A writable, seekable stream that cancels its own token source and throws OCE on write, so the
    /// generator's OCE filter (which requires an already-cancelled token) is reached from inside the try.
    /// </summary>
    private sealed class CancelOnWriteStream : MemoryStream
    {
        private readonly CancellationTokenSource _cts;

        public CancelOnWriteStream(CancellationTokenSource cts) => _cts = cts;

        public override void Write(byte[] buffer, int offset, int count)
        {
            _cts.Cancel();
            throw new OperationCanceledException(_cts.Token);
        }
    }
}
