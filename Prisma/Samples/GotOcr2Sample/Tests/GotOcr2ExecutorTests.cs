using System;
using System.Threading.Tasks;
using CSnakes.Runtime;
using GotOcr2Sample.Domain.Interfaces;
using GotOcr2Sample.Domain.Models;
using GotOcr2Sample.Domain.ValueObjects;
using GotOcr2Sample.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GotOcr2Sample.Tests;

/// <summary>
/// Unit tests for GotOcr2Executor
/// Note: Full integration testing with Python is done in IntegrationTests.cs
/// These tests validate basic input validation and error handling only.
/// </summary>
public class GotOcr2ExecutorTests
{
    private readonly IPythonEnvironment _mockPythonEnv;
    private readonly ILogger<GotOcr2Executor> _mockLogger;
    private readonly IOcrExecutor _sut;

    public GotOcr2ExecutorTests()
    {
        _mockPythonEnv = Substitute.For<IPythonEnvironment>();
        _mockLogger = Substitute.For<ILogger<GotOcr2Executor>>();
        _sut = new GotOcr2Executor(_mockPythonEnv, _mockLogger);
    }

    [Fact]
    public void Constructor_WithNullPythonEnvironment_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new GotOcr2Executor(null!, _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new GotOcr2Executor(_mockPythonEnv, null!));
    }

    [Fact]
    public async Task ExecuteOcrAsync_WithEmptyImageData_ReturnsFailure()
    {
        // Arrange
        var imageData = new ImageData(Array.Empty<byte>(), "test.png");
        var config = new OCRConfig();

        // Act
        var result = await _sut.ExecuteOcrAsync(imageData, config);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("empty");
    }

    [Fact]
    public async Task ExecuteOcrAsync_WithNullImageData_ReturnsFailure()
    {
        // Arrange
        var imageData = new ImageData(null!, "test.png");
        var config = new OCRConfig();

        // Act
        var result = await _sut.ExecuteOcrAsync(imageData, config);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("empty");
    }

    // Note: Additional tests that require Python module mocking are not practical
    // due to CSnakes using dynamic types and extension methods.
    // See IntegrationTests.cs for full end-to-end testing with real Python environment.
}
