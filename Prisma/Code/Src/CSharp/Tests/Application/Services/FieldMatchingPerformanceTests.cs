using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Classification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Performance tests for <see cref="FieldMatchingService"/> to verify NFR compliance.
/// </summary>
public class FieldMatchingPerformanceTests
{
    private readonly IFieldExtractor<DocxSource> _docxFieldExtractor;
    private readonly IFieldExtractor<PdfSource> _pdfFieldExtractor;
    private readonly IMatchingPolicy _matchingPolicy;
    private readonly ILogger<FieldMatchingService> _logger;
    private readonly FieldMatchingService _service;

    public FieldMatchingPerformanceTests()
    {
        _docxFieldExtractor = Substitute.For<IFieldExtractor<DocxSource>>();
        _pdfFieldExtractor = Substitute.For<IFieldExtractor<PdfSource>>();
        _logger = Substitute.For<ILogger<FieldMatchingService>>();
        
        var options = Options.Create(new MatchingPolicyOptions());
        _matchingPolicy = new MatchingPolicyService(options, Substitute.For<ILogger<MatchingPolicyService>>());
        
        _service = new FieldMatchingService(
            _docxFieldExtractor,
            _pdfFieldExtractor,
            null, // XML extractor not needed for these tests
            _matchingPolicy,
            _logger);
    }

    /// <summary>
    /// Tests that field matching completes within 1 second (NFR from implementation tasks).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithMultipleSources_CompletesWithin1Second()
    {
        // Arrange
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada")
        };

        var docxSource = new DocxSource("test.docx");
        var pdfSource = new PdfSource("test.pdf");

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

        _docxFieldExtractor.ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));

        _pdfFieldExtractor.ExtractFieldsAsync(
            Arg.Any<PdfSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(pdfFields));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            pdfSource,
            null,
            fieldDefinitions);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000,
            $"Field matching took {stopwatch.ElapsedMilliseconds}ms, exceeding 1 second target");
    }

    /// <summary>
    /// Tests that field matching with many fields completes within performance target.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithManyFields_CompletesWithin1Second()
    {
        // Arrange
        var fieldDefinitions = new FieldDefinition[20];
        for (int i = 0; i < 20; i++)
        {
            fieldDefinitions[i] = new FieldDefinition($"Field{i}");
        }

        var docxSource = new DocxSource("test.docx");
        var docxFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };

        _docxFieldExtractor.ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            null,
            null,
            fieldDefinitions);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000,
            $"Field matching with many fields took {stopwatch.ElapsedMilliseconds}ms, exceeding 1 second target");
    }
}

