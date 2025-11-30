using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Interfaces;

/// <summary>
/// Contract tests for IAdaptiveExporter interface using mocks.
/// These tests define the behavioral contract that all implementations must satisfy.
/// ITDD Step 2: Define interface behavior using mocked implementations (GREEN with mocks).
/// </summary>
public sealed class IAdaptiveExporterContractTests
{
    private readonly IAdaptiveExporter _mockExporter;

    public IAdaptiveExporterContractTests()
    {
        _mockExporter = Substitute.For<IAdaptiveExporter>();
    }

    [Fact]
    public async Task ExportAsync_WhenValidSourceAndTemplate_ReturnsSuccessWithBytes()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };
        var expectedBytes = new byte[] { 1, 2, 3, 4 };

        _mockExporter.ExportAsync(sourceObject, "Excel", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(expectedBytes));

        // Act
        var result = await _mockExporter.ExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBe(expectedBytes);
    }

    [Fact]
    public async Task ExportAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        _mockExporter.ExportAsync(sourceObject, "InvalidType", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Failure("Template 'InvalidType' not found"));

        // Act
        var result = await _mockExporter.ExportAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Template 'InvalidType' not found");
    }

    [Fact]
    public async Task ExportAsync_WhenSourceIsNull_ReturnsFailure()
    {
        // Arrange
        _mockExporter.ExportAsync(null!, "Excel", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Failure("Source object cannot be null"));

        // Act
        var result = await _mockExporter.ExportAsync(null!, "Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Source object cannot be null");
    }

    [Fact]
    public async Task ExportAsync_WhenMappingFails_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        _mockExporter.ExportAsync(sourceObject, "Excel", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Failure("Field mapping failed: Required field 'Age' not found"));

        // Act
        var result = await _mockExporter.ExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Field mapping failed");
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenValidVersion_ReturnsSuccessWithBytes()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };
        var expectedBytes = new byte[] { 5, 6, 7, 8 };

        _mockExporter.ExportWithVersionAsync(sourceObject, "Excel", "1.0.0", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(expectedBytes));

        // Act
        var result = await _mockExporter.ExportWithVersionAsync(sourceObject, "Excel", "1.0.0", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBe(expectedBytes);
    }

    [Fact]
    public async Task ExportWithVersionAsync_WhenInvalidVersion_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        _mockExporter.ExportWithVersionAsync(sourceObject, "Excel", "99.99.99", Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Failure("Template version '99.99.99' not found"));

        // Act
        var result = await _mockExporter.ExportWithVersionAsync(sourceObject, "Excel", "99.99.99", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Template version '99.99.99' not found");
    }

    [Fact]
    public async Task GetActiveTemplateAsync_WhenTemplateExists_ReturnsTemplate()
    {
        // Arrange
        var expectedTemplate = new TemplateDefinition
        {
            TemplateType = "Excel",
            Version = "1.0.0",
            Description = "Test Template"
        };

        _mockExporter.GetActiveTemplateAsync("Excel", Arg.Any<CancellationToken>())
            .Returns(Result<TemplateDefinition>.Success(expectedTemplate));

        // Act
        var result = await _mockExporter.GetActiveTemplateAsync("Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.TemplateType.ShouldBe("Excel");
        result.Value.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task GetActiveTemplateAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange
        _mockExporter.GetActiveTemplateAsync("InvalidType", Arg.Any<CancellationToken>())
            .Returns(Result<TemplateDefinition>.Failure("No active template found for type 'InvalidType'"));

        // Act
        var result = await _mockExporter.GetActiveTemplateAsync("InvalidType", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("No active template found");
    }

    [Fact]
    public async Task ValidateExportAsync_WhenValidSource_ReturnsSuccess()
    {
        // Arrange
        var sourceObject = new { Name = "Test", Age = 30 };

        _mockExporter.ValidateExportAsync(sourceObject, "Excel", Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _mockExporter.ValidateExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateExportAsync_WhenRequiredFieldMissing_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        _mockExporter.ValidateExportAsync(sourceObject, "Excel", Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Required field 'Age' not found in source object"));

        // Act
        var result = await _mockExporter.ValidateExportAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Required field 'Age' not found");
    }

    [Fact]
    public async Task ValidateExportAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        _mockExporter.ValidateExportAsync(sourceObject, "InvalidType", Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Template 'InvalidType' not found"));

        // Act
        var result = await _mockExporter.ValidateExportAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("Template 'InvalidType' not found");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenValidSource_ReturnsMappedFields()
    {
        // Arrange
        var sourceObject = new { Name = "John Doe", Age = 30 };
        var expectedMapping = new Dictionary<string, string>
        {
            { "TargetName", "John Doe" },
            { "TargetAge", "30" }
        };

        _mockExporter.PreviewMappingAsync(sourceObject, "Excel", Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, string>>.Success(expectedMapping));

        // Act
        var result = await _mockExporter.PreviewMappingAsync(sourceObject, "Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        result.Value["TargetName"].ShouldBe("John Doe");
        result.Value["TargetAge"].ShouldBe("30");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenSourceIsNull_ReturnsFailure()
    {
        // Arrange
        _mockExporter.PreviewMappingAsync(null!, "Excel", Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, string>>.Failure("Source object cannot be null"));

        // Act
        var result = await _mockExporter.PreviewMappingAsync(null!, "Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Source object cannot be null");
    }

    [Fact]
    public async Task PreviewMappingAsync_WhenTemplateNotFound_ReturnsFailure()
    {
        // Arrange
        var sourceObject = new { Name = "Test" };

        _mockExporter.PreviewMappingAsync(sourceObject, "InvalidType", Arg.Any<CancellationToken>())
            .Returns(Result<Dictionary<string, string>>.Failure("Template 'InvalidType' not found"));

        // Act
        var result = await _mockExporter.PreviewMappingAsync(sourceObject, "InvalidType", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Template 'InvalidType' not found");
    }

    [Fact]
    public async Task IsTemplateAvailableAsync_WhenTemplateExists_ReturnsTrue()
    {
        // Arrange
        _mockExporter.IsTemplateAvailableAsync("Excel", Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        // Act
        var result = await _mockExporter.IsTemplateAvailableAsync("Excel", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task IsTemplateAvailableAsync_WhenTemplateNotFound_ReturnsFalse()
    {
        // Arrange
        _mockExporter.IsTemplateAvailableAsync("InvalidType", Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        // Act
        var result = await _mockExporter.IsTemplateAvailableAsync("InvalidType", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public void ClearTemplateCache_DoesNotThrow()
    {
        // Arrange & Act
        var exception = Record.Exception(() => _mockExporter.ClearTemplateCache());

        // Assert
        exception.ShouldBeNull();
    }
}
