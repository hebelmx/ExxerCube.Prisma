using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.System.Ocr.Pipeline;

/// <summary>
/// System-level checks for XML extraction against real PRP1 fixtures.
/// Ensures subdivision, measure hints, accounts, RFC variants, and CURP are parsed.
/// </summary>
public class XmlExtractorFixtureTests(ITestOutputHelper output)
{
    private readonly XmlFieldExtractor _extractor = new();
    private readonly string _fixtureRoot = Path.Combine("Fixtures", "PRP1");

    private readonly ILogger<XmlExtractorFixtureTests> _logger = XUnitLogger.CreateLogger<XmlExtractorFixtureTests>(output);

    [Fact, Trait("Category", "XmlExtractor")]
    public async Task Extract_Should_Parse_Aseguramiento_Fixture()
    {
        var path = Path.Combine(_fixtureRoot, "222AAA-44444444442025.xml");

        _logger.LogInformation("Testing XML extraction for file: {Path}", path);
        _logger.LogInformation("Current working directory: {Directory}", Directory.GetCurrentDirectory());
        _logger.LogInformation("Fixture root directory: {FixtureRoot}", _fixtureRoot);
        _logger.LogInformation("File exists: {Exists}", File.Exists(path));
        Assert.True(File.Exists(path), $"File '{path}' does not exist.");

        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.Expediente.ShouldBe("A/AS1-1111-222222-AAA");
        fields.AdditionalFields["Subdivision"].ShouldBe("Aseguramiento");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Aseguramiento");
        fields.AdditionalFields["DiasPlazo"].ShouldBe("7");
    }

    [Fact, Trait("Category", "XmlExtractor")]
    public async Task Extract_Should_Parse_Hacendario_Documentacion()
    {
        var path = Path.Combine(_fixtureRoot, "333BBB-44444444442025.xml");
        _logger.LogInformation("Testing XML extraction for file: {Path}", path);
        _logger.LogInformation("Current working directory: {Directory}", Directory.GetCurrentDirectory());
        _logger.LogInformation("Fixture root directory: {FixtureRoot}", _fixtureRoot);
        _logger.LogInformation("File exists: {Exists}", File.Exists(path));
        Assert.True(File.Exists(path), $"File '{path}' does not exist.");

        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.AdditionalFields["Subdivision"].ShouldBe("Hacendario");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Documentacion");
        fields.AdditionalFields["RfcList"]?.ShouldContain("DOPJ111111222");
    }

    [Fact, Trait("Category", "XmlExtractor")]
    public async Task Extract_Should_Parse_Judicial_With_Curp_And_Rfc()
    {
        var path = Path.Combine(_fixtureRoot, "333ccc-6666666662025.xml");
        _logger.LogInformation("Testing XML extraction for file: {Path}", path);
        _logger.LogInformation("Current working directory: {Directory}", Directory.GetCurrentDirectory());
        _logger.LogInformation("Fixture root directory: {FixtureRoot}", _fixtureRoot);
        _logger.LogInformation("File exists: {Exists}", File.Exists(path));
        Assert.True(File.Exists(path), $"File '{path}' does not exist.");

        Assert.True(File.Exists(path), $"File '{path}' does not exist.");

        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.AdditionalFields["Subdivision"].ShouldBe("Judicial");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Aseguramiento");
        fields.AdditionalFields["RfcList"]?.ShouldContain("ZUCM444444555");
        fields.AdditionalFields["Curp"]?.ShouldBe("ZUCM444444ABCDEF01"); // Valid 18-char CURP format
    }

    [Fact, Trait("Category", "XmlExtractor")]
    public async Task Extract_Should_Parse_OperacionesIlicitas_With_Accounts_And_Rfc_Variants()
    {
        var path = Path.Combine(_fixtureRoot, "555CCC-66666662025.xml");
        _logger.LogInformation("Testing XML extraction for file: {Path}", path);
        _logger.LogInformation("Current working directory: {Directory}", Directory.GetCurrentDirectory());
        _logger.LogInformation("Fixture root directory: {FixtureRoot}", _fixtureRoot);
        _logger.LogInformation("File exists: {Exists}", File.Exists(path));
        Assert.True(File.Exists(path), $"File '{path}' does not exist.");

        Assert.True(File.Exists(path), $"File '{path}' does not exist.");

        var result = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;
        fields.AdditionalFields["Subdivision"].ShouldBe("OperacionesIlicitas");
        fields.AdditionalFields["MeasureHint"].ShouldBe("Desbloqueo");
        fields.AdditionalFields["RfcList"]?.ShouldContain("LUMH111111111");
        fields.AdditionalFields["RfcList"]?.ShouldContain("LUMH222222222");
        fields.AdditionalFields["CuentasRaw"]?.ShouldContain("00466773850");
        fields.AdditionalFields["CuentasRaw"]?.ShouldContain("00195019117");
    }
}