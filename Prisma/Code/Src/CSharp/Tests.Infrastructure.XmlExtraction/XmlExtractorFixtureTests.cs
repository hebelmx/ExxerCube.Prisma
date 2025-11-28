using System.Text;
using ExxerCube.Prisma.Domain.Enums;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Infrastructure.Extraction.Teseract;

namespace ExxerCube.Prisma.Tests.System.XmlExtraction;

/// <summary>
/// System-level checks for XML extraction against real PRP1 fixtures.
/// Ensures subdivision, measure hints, accounts, RFC variants, and CURP are parsed.
/// </summary>
public class XmlExtractorFixtureTests(ITestOutputHelper output)
{
    private readonly XmlFieldExtractor _extractor = new();
    private readonly string _fixtureRoot = Path.Combine("Fixtures", "PRP1");
    private readonly ILogger<XmlExtractorFixtureTests> _logger = XUnitLogger.CreateLogger<XmlExtractorFixtureTests>(output);

    [Fact]
    public async Task Extract_Should_Parse_Aseguramiento_Fixture()
    {
        var path = Path.Combine(_fixtureRoot, "222AAA-44444444442025.xml");
        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.Expediente.ShouldBe("A/AS1-1111-222222-AAA");
        fields.AdditionalFields["Subdivision"].ShouldBe("Aseguramiento");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Aseguramiento");
        fields.AdditionalFields["DiasPlazo"].ShouldBe("7");

        LogFields("Aseguramiento fixture", fields);
    }

    [Fact]
    public async Task Extract_Should_Parse_Hacendario_Documentacion()
    {
        var path = Path.Combine(_fixtureRoot, "333BBB-44444444442025.xml");
        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.AdditionalFields["Subdivision"].ShouldBe("Hacendario");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Documentacion");
        fields.AdditionalFields["RfcList"]?.ShouldContain("DOPJ111111222");

        LogFields("Hacendario fixture", fields);
    }

    [Fact]
    public async Task Extract_Should_Parse_Judicial_With_Curp_And_Rfc()
    {
        var path = Path.Combine(_fixtureRoot, "333ccc-6666666662025.xml");
        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.AdditionalFields["Subdivision"].ShouldBe("Judicial");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Aseguramiento");
        fields.AdditionalFields["RfcList"]?.ShouldContain("ZUCM444444555");
        if (fields.AdditionalFields.TryGetValue("Curp", out var curp))
        {
            curp!.ShouldBe("ZUCM444444ABCDEF");
        }

        LogFields("Judicial fixture", fields);
    }

    [Fact]
    public async Task Extract_Should_Parse_OperacionesIlicitas_With_Accounts_And_Rfc_Variants()
    {
        var path = Path.Combine(_fixtureRoot, "555CCC-66666662025.xml");
        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.AdditionalFields["Subdivision"].ShouldBe("OperacionesIlicitas");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Desbloqueo");
        fields.AdditionalFields["RfcList"]?.ShouldContain("LUMH111111111");
        fields.AdditionalFields["RfcList"]?.ShouldContain("LUMH222222222");
        fields.AdditionalFields["CuentasRaw"]?.ShouldContain("00466773850");
        fields.AdditionalFields["CuentasRaw"]?.ShouldContain("00195019117");

        LogFields("Operaciones Ilícitas fixture", fields);
    }

    [Fact]
    public async Task Extract_With_Missing_Expediente_Allows_Nulls()
    {
        var path = Path.Combine(_fixtureRoot, "missing_expediente.xml");
        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.Expediente.ShouldBeNull();
        fields.AdditionalFields["Subdivision"].ShouldBe("Hacendario");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Informacion");
        fields.AdditionalFields["DiasPlazo"].ShouldBe("5");

        LogFields("Missing expediente fixture", fields);
    }

    [Fact]
    public async Task Extract_With_Missing_Subdivision_Falls_Back_To_Unknown()
    {
        var path = Path.Combine(_fixtureRoot, "missing_subdivision.xml");
        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.Expediente.ShouldBe("X/XX1-0000-000000-XXX");
        fields.AdditionalFields["Subdivision"].ShouldBe("Unknown");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Aseguramiento");
        fields.AdditionalFields["DiasPlazo"].ShouldBe("3");

        LogFields("Missing subdivision fixture", fields);
    }

    private void LogFields(string label, ExtractedFields fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{label}] Expediente: {fields.Expediente}");
        foreach (var kvp in fields.AdditionalFields.OrderBy(k => k.Key))
        {
            sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
        }
        _logger.LogInformation("{Details}", sb.ToString());
    }
}
