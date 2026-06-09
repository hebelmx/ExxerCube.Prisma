using System.Text;
using System.Xml.Linq;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Tests for <see cref="CompositeResponseExporter"/> — verifies it delegates SIRO-XML export to the XML
/// exporter and signed-PDF export to the PDF signer (the composite holds no logic of its own).
/// </summary>
public class CompositeResponseExporterTests
{
    private static CompositeResponseExporter Composite()
    {
        var xmlExporter = new SiroXmlExporter(XUnitLogger.CreateLogger<SiroXmlExporter>());
        var pdfSigner = new DigitalPdfSigner(
            Options.Create(new CertificateOptions
            {
                Source = "File",
                FileCertificatePath = "nonexistent.pfx",
                FallbackToFile = false,
            }),
            XUnitLogger.CreateLogger<DigitalPdfSigner>());
        return new CompositeResponseExporter(xmlExporter, pdfSigner, XUnitLogger.CreateLogger<CompositeResponseExporter>());
    }

    private static UnifiedMetadataRecord Metadata() => new()
    {
        Expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-088637-PHM",
            NumeroOficio = "214-1-18714972/2025",
        },
    };

    /// <summary>Verifies ExportSiroXmlAsync delegates to the XML exporter (produces a valid SIRO document).</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_DelegatesToXmlExporter()
    {
        using var stream = new MemoryStream();

        var result = await Composite().ExportSiroXmlAsync(Metadata(), stream, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var doc = XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        doc.Root!.Name.LocalName.ShouldBe("SiroResponse");
        doc.Descendants().First(e => e.Name.LocalName == "NumeroExpediente").Value.ShouldBe("A/AS1-2505-088637-PHM");
    }

    /// <summary>Verifies ExportSignedPdfAsync delegates to the PDF signer (surfaces its certificate failure).</summary>
    [Fact]
    public async Task ExportSignedPdfAsync_DelegatesToPdfSigner()
    {
        using var stream = new MemoryStream();

        var result = await Composite().ExportSignedPdfAsync(Metadata(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("certificate", Case.Insensitive);
    }
}
