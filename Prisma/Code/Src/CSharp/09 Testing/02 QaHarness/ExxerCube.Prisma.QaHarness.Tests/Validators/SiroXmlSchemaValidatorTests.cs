// <copyright file="SiroXmlSchemaValidatorTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Xml;
using System.Xml.Schema;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.SiroXml;

namespace ExxerCube.Prisma.QaHarness.Tests.Validators;

/// <summary>
/// Fast unit tests for <see cref="SiroXmlSchemaValidator"/>.
/// All tests run without Docker or network access.
/// </summary>
public sealed class SiroXmlSchemaValidatorTests
{
    private const string SiroNs = SiroXmlStructureValidator.SiroNamespace;

    private static string ValidSiroXml() => """
        <?xml version="1.0" encoding="utf-8"?>
        <SiroResponse xmlns="http://siro.regulatory.namespace">
          <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
          <NumeroOficio>214-1-18714972/2025</NumeroOficio>
        </SiroResponse>
        """;

    private static async Task<string> WriteTempXmlAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"siro-schema-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        return path;
    }

    // ── No schema set (well-formedness only) ─────────────────────────────────

    /// <summary>A well-formed SIRO XML with no schema set should produce IsConformant=true + an Info finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_WellFormedNoSchema_IsConformant_WithInfoFinding()
    {
        var sut = new SiroXmlSchemaValidator();
        var path = await WriteTempXmlAsync(ValidSiroXml());
        try
        {
            var result = await sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeTrue();
            result.ValidatorId.ShouldBe("SIRO-SCHEMA");
            result.Findings.ShouldContain(f =>
                f.Severity == FindingSeverity.Info && f.RuleId == "SIRO-SCHEMA-INFO");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── XSD schema conformance ────────────────────────────────────────────────

    /// <summary>A SIRO XML that conforms to a restrictive XSD should be conformant.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_ConformsToSchema_IsConformant()
    {
        // Minimal XSD: allow any content inside SiroResponse
        const string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       targetNamespace="http://siro.regulatory.namespace"
                       elementFormDefault="qualified">
              <xs:element name="SiroResponse">
                <xs:complexType>
                  <xs:sequence>
                    <xs:any minOccurs="0" maxOccurs="unbounded" processContents="skip"/>
                  </xs:sequence>
                </xs:complexType>
              </xs:element>
            </xs:schema>
            """;
        var schemaSet = new XmlSchemaSet();
        schemaSet.Add(SiroNs, XmlReader.Create(new StringReader(xsd)));

        var sut = new SiroXmlSchemaValidator(schemaSet);
        var path = await WriteTempXmlAsync(ValidSiroXml());
        try
        {
            var result = await sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeTrue();
            result.Findings.ShouldNotContain(f => f.Severity == FindingSeverity.Major);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A SIRO XML that violates the XSD schema should produce Major finding(s) with IsConformant=false.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_ViolatesSchema_IsNotConformant_WithMajorFindings()
    {
        // XSD with an empty content model: any child element is a violation.
        const string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       targetNamespace="http://siro.regulatory.namespace">
              <xs:element name="SiroResponse">
                <xs:complexType/>
              </xs:element>
            </xs:schema>
            """;
        var schemaSet = new XmlSchemaSet();
        schemaSet.Add(SiroNs, XmlReader.Create(new StringReader(xsd)));

        var sut = new SiroXmlSchemaValidator(schemaSet);
        var path = await WriteTempXmlAsync(ValidSiroXml());
        try
        {
            var result = await sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            result.Findings.ShouldContain(f =>
                f.Severity == FindingSeverity.Major && f.RuleId == "SIRO-SCHEMA-04");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Non-conformant: malformed XML ─────────────────────────────────────────

    /// <summary>Malformed XML should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MalformedXml_IsNotConformant_WithCriticalFinding()
    {
        const string broken = "<not valid xml <<>";
        var path = await WriteTempXmlAsync(broken);
        try
        {
            var sut = new SiroXmlSchemaValidator();
            var result = await sut.ValidateAsync(path, TestContext.Current.CancellationToken);

            result.IsConformant.ShouldBeFalse();
            result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Non-conformant: missing file ─────────────────────────────────────────

    /// <summary>A missing file should produce a Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_MissingFile_IsNotConformant_WithCriticalFinding()
    {
        var sut = new SiroXmlSchemaValidator();
        var path = "/definitely/does/not/exist.xml";

        var result = await sut.ValidateAsync(path, TestContext.Current.CancellationToken);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    /// <summary>A pre-cancelled token yields IsConformant=false with Critical finding.</summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task Validate_CancelledToken_IsNotConformant()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sut = new SiroXmlSchemaValidator();
        var result = await sut.ValidateAsync("/any.xml", cts.Token);

        result.IsConformant.ShouldBeFalse();
        result.Findings.ShouldContain(f => f.Severity == FindingSeverity.Critical);
    }
}
