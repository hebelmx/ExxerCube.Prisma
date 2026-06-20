// <copyright file="SiroXmlStructureValidatorTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.SiroXml;

namespace ExxerCube.Prisma.QaHarness.Tests.Validators;

/// <summary>
/// Fast unit tests for <see cref="SiroXmlStructureValidator"/>.
/// All tests run without Docker or network access.
/// </summary>
public sealed class SiroXmlStructureValidatorTests
{
    private static readonly SiroXmlStructureValidator Sut = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Writes XML content to a temp file and returns the path.</summary>
    private static async Task<string> WriteTempXmlAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"siro-test-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        return path;
    }

    private static string ValidSiroXml() => """
        <?xml version="1.0" encoding="utf-8"?>
        <SiroResponse xmlns="http://siro.regulatory.namespace">
          <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
          <NumeroOficio>214-1-18714972/2025</NumeroOficio>
        </SiroResponse>
        """;

    // ── Conformant cases ─────────────────────────────────────────────────────

    /// <summary>A minimal valid SIRO XML file should be conformant with no Critical/Major findings.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_ValidSiroXml_IsConformant()
    {
        var path = await WriteTempXmlAsync(ValidSiroXml());
        try
        {
            var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.ValidatorId.ShouldBe("SIRO-STRUCT");
            result.IsConformant.ShouldBeTrue();
            result.Findings.ShouldNotContain(f =>
                f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Non-conformant: missing file ─────────────────────────────────────────

    /// <summary>A non-existent file path should produce a Critical finding and IsConformant=false.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingFile_IsNotConformant_WithCriticalFinding()
    {
        var path = Path.Combine(Path.GetTempPath(), "does-not-exist-siro.xml");

        var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f =>
            f.Severity == FindingSeverity.Critical && f.RuleId == "SIRO-STRUCT-01");
    }

    // ── Non-conformant: null/empty path ──────────────────────────────────────

    /// <summary>An empty path should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_EmptyPath_IsNotConformant_WithCriticalFinding()
    {
        var result = await Sut.ValidateAsync(string.Empty, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Non-conformant: wrong namespace ──────────────────────────────────────

    /// <summary>A SIRO XML with a wrong namespace should produce a Major finding on the namespace rule.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_WrongNamespace_IsNotConformant_WithMajorFinding()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <SiroResponse xmlns="http://wrong.namespace">
              <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
              <NumeroOficio>214-1-18714972/2025</NumeroOficio>
            </SiroResponse>
            """;
        var path = await WriteTempXmlAsync(xml);
        try
        {
            var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            var nsFinding = result.Findings.FirstOrDefault(f => f.RuleId == "SIRO-STRUCT-04");
            nsFinding.ShouldNotBeNull();
            nsFinding!.Severity.ShouldBe(FindingSeverity.Major);
            nsFinding.Observed.ShouldBe("http://wrong.namespace");
            nsFinding.Expected.ShouldBe(SiroXmlStructureValidator.SiroNamespace);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Non-conformant: missing required element ──────────────────────────────

    /// <summary>A SIRO XML missing NumeroExpediente should produce a Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingNumeroExpediente_IsNotConformant_WithMajorFinding()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <SiroResponse xmlns="http://siro.regulatory.namespace">
              <NumeroOficio>214-1-18714972/2025</NumeroOficio>
            </SiroResponse>
            """;
        var path = await WriteTempXmlAsync(xml);
        try
        {
            var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            result.Findings.ShouldContain(f =>
                f.RuleId.Contains("NumeroExpediente") && f.Severity == FindingSeverity.Major);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Non-conformant: wrong root element ───────────────────────────────────

    /// <summary>A SIRO XML with the wrong root element name should produce a Major finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_WrongRootElement_IsNotConformant_WithMajorFinding()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Response xmlns="http://siro.regulatory.namespace">
              <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
              <NumeroOficio>214-1-18714972/2025</NumeroOficio>
            </Response>
            """;
        var path = await WriteTempXmlAsync(xml);
        try
        {
            var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            var rootFinding = result.Findings.FirstOrDefault(f => f.RuleId == "SIRO-STRUCT-03");
            rootFinding.ShouldNotBeNull();
            rootFinding!.Observed.ShouldBe("Response");
            rootFinding.Expected.ShouldBe("SiroResponse");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    /// <summary>A pre-cancelled token should yield IsConformant=false with a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_CancelledToken_IsNotConformant()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Sut.ValidateAsync("/any/path.xml", cts.Token);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Findings carry RuleId / Severity / Observed / Expected ───────────────

    /// <summary>Every finding must carry non-null, non-empty RuleId and Description.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_NonConformant_FindingsCarryRequiredFields()
    {
        var path = Path.Combine(Path.GetTempPath(), "missing-siro.xml");
        // Ensure it doesn't exist
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var result = await Sut.ValidateAsync(path, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        foreach (var finding in result.Findings)
        {
            finding.RuleId.ShouldNotBeNullOrWhiteSpace();
            finding.Description.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
