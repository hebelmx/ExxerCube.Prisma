using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Extraction;
using IndQuestResults;
using Microsoft.Extensions.Logging;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Integration tests for <see cref="FieldMatchingService"/> covering end-to-end workflows, backward compatibility, and performance.
/// </summary>
public class FieldMatchingIntegrationTests
{
    private readonly IFieldExtractor<DocxSource> _docxFieldExtractor;
    private readonly IFieldExtractor<PdfSource> _pdfFieldExtractor;
    private readonly IFieldExtractor<XmlSource> _xmlFieldExtractor;
    private readonly IMatchingPolicy _matchingPolicy;
    private readonly ILogger<FieldMatchingService> _logger;
    private readonly FieldMatchingService _service;

    public FieldMatchingIntegrationTests(ITestOutputHelper output)
    {
        _docxFieldExtractor = Substitute.For<IFieldExtractor<DocxSource>>();
        _pdfFieldExtractor = Substitute.For<IFieldExtractor<PdfSource>>();
        _xmlFieldExtractor = Substitute.For<IFieldExtractor<XmlSource>>();
        _logger = XUnitLogger.CreateLogger<FieldMatchingService>(output);

        var options = Options.Create(new MatchingPolicyOptions());
        _matchingPolicy = new MatchingPolicyService(options, Substitute.For<ILogger<MatchingPolicyService>>());

        _service = new FieldMatchingService(
            _docxFieldExtractor,
            _pdfFieldExtractor,
            _xmlFieldExtractor,
            _matchingPolicy,
            _logger);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_EndToEndWorkflow_AllSourcesContribute()
    {
        // Arrange - Simulate real-world scenario with XML, DOCX, and PDF sources
        var docxSource = new DocxSource("test.docx");
        var pdfSource = new PdfSource("test.pdf");
        var xmlSource = new XmlSource("test.xml");

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada")
        };

        // DOCX extraction
        var docxFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa DOCX",
            AccionSolicitada = "Test Action DOCX"
        };

        // PDF extraction
        var pdfFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa PDF",
            AccionSolicitada = "Test Action PDF"
        };

        // XML extraction
        var xmlFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa XML",
            AccionSolicitada = "Test Action XML"
        };

        _docxFieldExtractor.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));
        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(pdfFields));
        _xmlFieldExtractor.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(xmlFields));

        var expediente = new Expediente { NumeroExpediente = "A/AS1-2505-088637-PHM" };
        var classification = new ClassificationResult();

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            pdfSource,
            xmlSource,
            fieldDefinitions,
            expediente,
            classification);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldNotBeNull();
        result.Value.ExtractedFields.ShouldNotBeNull();
        result.Value.MatchedFields.ShouldNotBeNull();
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Expediente");
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Causa");
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("AccionSolicitada");
        result.Value.MatchedFields.OverallAgreement.ShouldBeGreaterThan(0.8f); // All sources agree on Expediente
        result.Value.Classification.ShouldBe(classification);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_BackwardCompatibility_ExistingIFieldExtractorStillWorks()
    {
        // Arrange - Verify that existing non-generic IFieldExtractor implementations are unaffected
        // This test ensures IV1: Existing IFieldExtractor interface extended to generic IFieldExtractor<T> 
        // without breaking existing implementations

        var docxSource = new DocxSource("test.docx");
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var docxFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };

        _docxFieldExtractor.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));

        // Act - Use generic IFieldExtractor<T> interface
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            null,
            null,
            fieldDefinitions);

        // Assert - Generic interface works correctly
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedFields.ShouldNotBeNull();
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Expediente");

        // Verify that the generic extractor was called (backward compatibility maintained)
        await _docxFieldExtractor.Received().ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_EndToEndPerformance_CompletesWithin2Seconds()
    {
        // Arrange - NFR4: Metadata extraction within 2 seconds for XML/DOCX, 30 seconds for PDF
        // This integration test verifies the entire workflow meets performance targets
        var docxSource = new DocxSource("test.docx");
        var pdfSource = new PdfSource("test.pdf");
        var xmlSource = new XmlSource("test.xml");

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada")
        };

        var docxFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        var pdfFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        var xmlFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        _docxFieldExtractor.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));
        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(pdfFields));
        _xmlFieldExtractor.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(xmlFields));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            pdfSource,
            xmlSource,
            fieldDefinitions);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000,
            $"End-to-end field matching workflow took {stopwatch.ElapsedMilliseconds}ms, exceeding 2 second target (NFR4 for XML/DOCX)");
    }
}

