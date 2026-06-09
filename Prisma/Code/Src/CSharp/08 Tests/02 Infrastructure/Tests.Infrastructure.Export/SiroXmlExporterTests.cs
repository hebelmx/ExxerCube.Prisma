using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Mutation-hardening tests for <see cref="SiroXmlExporter"/> — the SIRO-XML regulatory deliverable.
/// Targets every conditional, validation message, optional-element gate, schema branch,
/// and the OCE/Exception catch paths so Stryker mutants are killed with exact-value assertions.
/// </summary>
public class SiroXmlExporterTests
{
    private static ILogger<SiroXmlExporter> Logger() => XUnitLogger.CreateLogger<SiroXmlExporter>();

    private static SiroXmlExporter Exporter() => new(Logger());

    /// <summary>Minimal metadata that passes validation (only the two required fields set).</summary>
    private static UnifiedMetadataRecord MinimalValid() => new()
    {
        Expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-088637-PHM",
            NumeroOficio = "214-1-18714972/2025",
        },
    };

    /// <summary>Fully populated metadata exercising every optional element and both collections.</summary>
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
            AutoridadEspecificaNombre = "UIF",
            NombreSolicitante = "Juan Solicitante",
            Referencia = "REF-0",
            Referencia1 = "REF-1",
            Referencia2 = "REF-2",
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
                    Relacion = "Titular",
                    Domicilio = "Av. Reforma 100",
                    Complementarios = "CURP extra",
                },
            },
            SolicitudEspecificas = new List<SolicitudEspecifica>
            {
                new()
                {
                    SolicitudEspecificaId = 9001,
                    InstruccionesCuentasPorConocer = "Inmovilizar cuentas por conocer",
                },
            },
        },
    };

    private static XDocument ParseXml(MemoryStream stream)
        => XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));

    private static string? Value(XDocument doc, string localName)
        => doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static bool Has(XDocument doc, string localName)
        => doc.Descendants().Any(e => e.Name.LocalName == localName);

    private static int DirectChildCount(XDocument doc, string parentLocalName, string childLocalName)
    {
        var parent = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == parentLocalName);
        return parent?.Elements().Count(e => e.Name.LocalName == childLocalName) ?? 0;
    }

    private static bool IsDirectChildOfRoot(XDocument doc, string localName)
        => doc.Root!.Elements().Any(e => e.Name.LocalName == localName);

    // ---------------------------------------------------------------------
    // Cancellation / null / writability guards
    // ---------------------------------------------------------------------

    /// <summary>Verifies the pre-start cancellation guard short-circuits to a Cancelled result.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_CancelledToken_ReturnsCancelled()
    {
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Exporter().ExportSiroXmlAsync(MinimalValid(), stream, cts.Token);

        result.IsCancelled().ShouldBeTrue();
        stream.Length.ShouldBe(0);
    }

    /// <summary>Verifies null metadata yields the exact validation failure message.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_NullMetadata_ReturnsFailure()
    {
        using var stream = new MemoryStream();

        var result = await Exporter().ExportSiroXmlAsync(null!, stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Metadata cannot be null");
    }

    /// <summary>Verifies null output stream yields the exact validation failure message.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_NullStream_ReturnsFailure()
    {
        var result = await Exporter().ExportSiroXmlAsync(MinimalValid(), null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Output stream cannot be null");
    }

    /// <summary>Verifies a read-only stream is rejected with the exact message.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_NonWritableStream_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[16], writable: false);

        var result = await Exporter().ExportSiroXmlAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Output stream is not writable");
    }

    // ---------------------------------------------------------------------
    // ValidateMetadata branches
    // ---------------------------------------------------------------------

    /// <summary>Verifies a null Expediente is rejected with the exact message.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_NullExpediente_ReturnsFailure()
    {
        using var stream = new MemoryStream();
        var metadata = new UnifiedMetadataRecord { Expediente = null };

        var result = await Exporter().ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Expediente is required for SIRO export");
    }

    /// <summary>Verifies an empty Expediente number is rejected with the exact message.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_EmptyNumeroExpediente_ReturnsFailure()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.NumeroExpediente = "   ";

        var result = await Exporter().ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Expediente number is required for SIRO export");
    }

    /// <summary>Verifies an empty Oficio number is rejected with the exact message.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_EmptyNumeroOficio_ReturnsFailure()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.NumeroOficio = "   ";

        var result = await Exporter().ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Oficio number is required for SIRO export");
    }

    /// <summary>Verifies a fully valid record exports successfully.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_ValidRecord_ReturnsSuccess()
    {
        using var stream = new MemoryStream();

        var result = await Exporter().ExportSiroXmlAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        stream.Length.ShouldBeGreaterThan(0);
    }

    // ---------------------------------------------------------------------
    // Document shape: declaration, root element, namespace, required fields
    // ---------------------------------------------------------------------

    /// <summary>Verifies the XML declaration, root element name and SIRO namespace.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_WritesDeclarationRootAndNamespace()
    {
        using var stream = new MemoryStream();

        await Exporter().ExportSiroXmlAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        var raw = Encoding.UTF8.GetString(stream.ToArray());
        raw.ShouldStartWith("<?xml");

        // Indent = true => elements are written on their own indented lines (kills the indentation mutants).
        raw.Split('\n').Length.ShouldBeGreaterThan(5);
        raw.ShouldContain("\n  <");

        var doc = XDocument.Parse(raw);
        doc.Root.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("SiroResponse");
        doc.Root.Name.NamespaceName.ShouldBe("http://siro.regulatory.namespace");
    }

    /// <summary>Verifies all always-written required elements carry the exact expediente values.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_WritesRequiredElementsWithValues()
    {
        using var stream = new MemoryStream();

        await Exporter().ExportSiroXmlAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        var doc = ParseXml(stream);
        Value(doc, "NumeroExpediente").ShouldBe("A/AS1-2505-088637-PHM");
        Value(doc, "NumeroOficio").ShouldBe("214-1-18714972/2025");
        Value(doc, "SolicitudSiara").ShouldBe("SIARA-99887");
        Value(doc, "Folio").ShouldBe("12345");
        Value(doc, "OficioYear").ShouldBe("2025");
        Value(doc, "AreaClave").ShouldBe("77");
        Value(doc, "AreaDescripcion").ShouldBe("ASEGURAMIENTO");
        Value(doc, "DiasPlazo").ShouldBe("10");
        Value(doc, "AutoridadNombre").ShouldBe("CNBV");
    }

    /// <summary>Verifies FechaPublicacion is rendered with the exact ISO yyyy-MM-dd format.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_FormatsFechaPublicacionAsIsoDate()
    {
        using var stream = new MemoryStream();

        await Exporter().ExportSiroXmlAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        Value(ParseXml(stream), "FechaPublicacion").ShouldBe("2025-05-15");
    }

    // ---------------------------------------------------------------------
    // Optional top-level elements: present when set, absent when empty
    // ---------------------------------------------------------------------

    /// <summary>Verifies every optional top-level element is emitted with its value when populated.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_FullyPopulated_WritesOptionalTopLevelElements()
    {
        using var stream = new MemoryStream();

        await Exporter().ExportSiroXmlAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        var doc = ParseXml(stream);
        Value(doc, "AutoridadEspecificaNombre").ShouldBe("UIF");
        Value(doc, "NombreSolicitante").ShouldBe("Juan Solicitante");
        Value(doc, "Referencia").ShouldBe("REF-0");
        Value(doc, "Referencia1").ShouldBe("REF-1");
        Value(doc, "Referencia2").ShouldBe("REF-2");
    }

    /// <summary>Verifies optional top-level elements are omitted when their source values are blank.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_MinimalRecord_OmitsOptionalTopLevelElements()
    {
        using var stream = new MemoryStream();

        await Exporter().ExportSiroXmlAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        var doc = ParseXml(stream);
        Has(doc, "AutoridadEspecificaNombre").ShouldBeFalse();
        Has(doc, "NombreSolicitante").ShouldBeFalse();
        Has(doc, "Referencia").ShouldBeFalse();
        Has(doc, "Referencia1").ShouldBeFalse();
        Has(doc, "Referencia2").ShouldBeFalse();
        Has(doc, "SolicitudPartes").ShouldBeFalse();
        Has(doc, "SolicitudEspecificas").ShouldBeFalse();
    }

    // ---------------------------------------------------------------------
    // SolicitudPartes collection + per-parte optional fields
    // ---------------------------------------------------------------------

    /// <summary>Verifies the SolicitudPartes wrapper and every parte field is emitted when populated.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_WithParte_WritesAllParteFields()
    {
        using var stream = new MemoryStream();

        await Exporter().ExportSiroXmlAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        var doc = ParseXml(stream);
        Has(doc, "SolicitudPartes").ShouldBeTrue();
        Has(doc, "Parte").ShouldBeTrue();
        Value(doc, "ParteId").ShouldBe("501");
        Value(doc, "Caracter").ShouldBe("Patrón Determinado");
        Value(doc, "PersonaTipo").ShouldBe("Fisica");
        Value(doc, "Paterno").ShouldBe("Pérez");
        Value(doc, "Materno").ShouldBe("García");
        Value(doc, "Nombre").ShouldBe("Carlos");
        Value(doc, "Rfc").ShouldBe("PEGC800101AAA");
        Value(doc, "Relacion").ShouldBe("Titular");
        Value(doc, "Domicilio").ShouldBe("Av. Reforma 100");
        Value(doc, "Complementarios").ShouldBe("CURP extra");
    }

    /// <summary>Verifies a parte with only required fields omits its optional child elements but keeps required ones.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_ParteWithoutOptionalFields_OmitsThemKeepsRequired()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.SolicitudPartes.Add(new SolicitudParte
        {
            ParteId = 7,
            Caracter = "Contribuyente",
            PersonaTipo = "Moral",
            Nombre = "ACME SA",
            // Paterno / Materno / Rfc / Relacion / Domicilio / Complementarios left blank
        });

        await Exporter().ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        var doc = ParseXml(stream);
        Has(doc, "Parte").ShouldBeTrue();
        Value(doc, "ParteId").ShouldBe("7");
        Value(doc, "Caracter").ShouldBe("Contribuyente");
        Value(doc, "PersonaTipo").ShouldBe("Moral");
        Value(doc, "Nombre").ShouldBe("ACME SA");
        Has(doc, "Paterno").ShouldBeFalse();
        Has(doc, "Materno").ShouldBeFalse();
        Has(doc, "Rfc").ShouldBeFalse();
        Has(doc, "Relacion").ShouldBeFalse();
        Has(doc, "Domicilio").ShouldBeFalse();
        Has(doc, "Complementarios").ShouldBeFalse();
    }

    /// <summary>Verifies an empty SolicitudPartes list produces no wrapper element.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_EmptyPartesList_OmitsWrapper()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.SolicitudPartes = new List<SolicitudParte>();

        await Exporter().ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        Has(ParseXml(stream), "SolicitudPartes").ShouldBeFalse();
    }

    // ---------------------------------------------------------------------
    // SolicitudEspecificas collection
    // ---------------------------------------------------------------------

    /// <summary>Verifies the SolicitudEspecificas wrapper and especifica fields are emitted when populated.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_WithEspecifica_WritesEspecificaFields()
    {
        using var stream = new MemoryStream();

        await Exporter().ExportSiroXmlAsync(FullyPopulated(), stream, TestContext.Current.CancellationToken);

        var doc = ParseXml(stream);
        Has(doc, "SolicitudEspecificas").ShouldBeTrue();
        Has(doc, "Especifica").ShouldBeTrue();
        Value(doc, "Id").ShouldBe("9001");
        Value(doc, "Instrucciones").ShouldBe("Inmovilizar cuentas por conocer");

        // SolicitudPartes and SolicitudEspecificas must both close as direct siblings under the root.
        // (A missing SolicitudPartes end-element would nest SolicitudEspecificas inside it.)
        IsDirectChildOfRoot(doc, "SolicitudPartes").ShouldBeTrue();
        IsDirectChildOfRoot(doc, "SolicitudEspecificas").ShouldBeTrue();
    }

    /// <summary>Verifies an empty SolicitudEspecificas list produces no wrapper element.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_EmptyEspecificasList_OmitsWrapper()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.SolicitudEspecificas = new List<SolicitudEspecifica>();

        await Exporter().ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        Has(ParseXml(stream), "SolicitudEspecificas").ShouldBeFalse();
    }

    /// <summary>Verifies multiple partes are emitted as siblings (each Parte element is closed individually).</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_MultiplePartes_RenderedAsSiblings()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.SolicitudPartes.Add(new SolicitudParte
        {
            ParteId = 1, Caracter = "A", PersonaTipo = "Fisica", Nombre = "Uno",
        });
        metadata.Expediente.SolicitudPartes.Add(new SolicitudParte
        {
            ParteId = 2, Caracter = "B", PersonaTipo = "Moral", Nombre = "Dos",
        });

        await Exporter().ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        DirectChildCount(ParseXml(stream), "SolicitudPartes", "Parte").ShouldBe(2);
    }

    /// <summary>Verifies multiple especificas are emitted as siblings (each Especifica element is closed individually).</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_MultipleEspecificas_RenderedAsSiblings()
    {
        using var stream = new MemoryStream();
        var metadata = MinimalValid();
        metadata.Expediente!.SolicitudEspecificas.Add(new SolicitudEspecifica
        {
            SolicitudEspecificaId = 1, InstruccionesCuentasPorConocer = "Uno",
        });
        metadata.Expediente.SolicitudEspecificas.Add(new SolicitudEspecifica
        {
            SolicitudEspecificaId = 2, InstruccionesCuentasPorConocer = "Dos",
        });

        await Exporter().ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        DirectChildCount(ParseXml(stream), "SolicitudEspecificas", "Especifica").ShouldBe(2);
    }

    // ---------------------------------------------------------------------
    // Schema-validation branch
    // ---------------------------------------------------------------------

    /// <summary>Verifies a non-null but empty schema set still allows export to succeed (no errors raised).</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_EmptySchemaSet_StillSucceeds()
    {
        using var stream = new MemoryStream();
        var exporter = new SiroXmlExporter(Logger(), new XmlSchemaSet());

        var result = await exporter.ExportSiroXmlAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        stream.Length.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies schema validation failures are surfaced as a failure result.</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_SchemaRejectsContent_ReturnsFailure()
    {
        // SiroResponse declared with an empty content model => any child element is a validation error.
        const string xsd = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       targetNamespace="http://siro.regulatory.namespace">
              <xs:element name="SiroResponse">
                <xs:complexType/>
              </xs:element>
            </xs:schema>
            """;
        var schemaSet = new XmlSchemaSet();
        schemaSet.Add("http://siro.regulatory.namespace", XmlReader.Create(new StringReader(xsd)));

        using var stream = new MemoryStream();
        var exporter = new SiroXmlExporter(Logger(), schemaSet);

        var result = await exporter.ExportSiroXmlAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("SIRO schema validation failed");
        // The per-error detail format ("Line N, Position N: ...") must survive into the message.
        result.Error!.ShouldContain("Position");
        // The empty content model raises exactly two validation errors; they must be joined with "; ".
        result.Error!.ShouldContain("; ");
    }

    // ---------------------------------------------------------------------
    // Exception / OCE catch paths
    // ---------------------------------------------------------------------

    /// <summary>Verifies an exception during stream write is converted to a failure result (not thrown).</summary>
    [Fact]
    public async Task ExportSiroXmlAsync_WriteThrows_ReturnsFailure()
    {
        using var stream = new ThrowOnWriteStream();

        var result = await Exporter().ExportSiroXmlAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("Error generating SIRO XML");
    }

    /// <summary>
    /// Verifies an OperationCanceledException raised mid-write (with a now-cancelled token) is
    /// caught by the OCE filter and converted to a Cancelled result.
    /// </summary>
    [Fact]
    public async Task ExportSiroXmlAsync_WriteThrowsOperationCanceled_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        using var stream = new CancelOnWriteStream(cts);

        var result = await Exporter().ExportSiroXmlAsync(MinimalValid(), stream, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExportSignedPdfAsync (placeholder until Story 1.8)
    // ---------------------------------------------------------------------

    /// <summary>Verifies the signed-PDF path honours the pre-start cancellation guard.</summary>
    [Fact]
    public async Task ExportSignedPdfAsync_CancelledToken_ReturnsCancelled()
    {
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Exporter().ExportSignedPdfAsync(MinimalValid(), stream, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Verifies the not-yet-implemented signed-PDF path returns the exact placeholder failure.</summary>
    [Fact]
    public async Task ExportSignedPdfAsync_NotImplemented_ReturnsFailure()
    {
        using var stream = new MemoryStream();

        var result = await Exporter().ExportSignedPdfAsync(MinimalValid(), stream, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("PDF signing functionality will be implemented in Story 1.8");
    }

    // ---------------------------------------------------------------------
    // Test doubles
    // ---------------------------------------------------------------------

    /// <summary>A writable stream that throws on write to exercise the generic catch path.</summary>
    private sealed class ThrowOnWriteStream : MemoryStream
    {
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new IOException("simulated write failure");
    }

    /// <summary>
    /// A writable stream that cancels its own token source and throws OCE on write, so the export's
    /// OCE filter (which requires an already-cancelled token) is reached from inside the try block.
    /// </summary>
    private sealed class CancelOnWriteStream : MemoryStream
    {
        private readonly CancellationTokenSource _cts;

        public CancelOnWriteStream(CancellationTokenSource cts) => _cts = cts;

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _cts.Cancel();
            throw new OperationCanceledException(_cts.Token);
        }
    }
}
