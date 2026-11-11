using Castle.Core.Logging;
using CSnakes.Runtime;
using GotOcr2Sample.Domain.Interfaces;
using GotOcr2Sample.Domain.Models;
using GotOcr2Sample.Domain.ValueObjects;
using GotOcr2Sample.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Shouldly;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace GotOcr2Sample.Tests;

/// <summary>
/// Integration tests for GotOcr2Executor with real Python environment
/// These tests require Python and GOT-OCR2 model to be available
/// </summary>
[Collection("Integration")]
public class IntegrationTests : IDisposable
{
    private readonly IHost _host;
    private readonly IOcrExecutor _executor;
    private readonly string _fixturesPath;
    private readonly ILogger<IntegrationTests> _logger;
    private readonly ITestOutputHelper _output;

    public IntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XunitLogger.CreateLogger<IntegrationTests>.CreateLogger<IntegrationTests>();
        // Setup Python environment path
        var pythonLibPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "PythonOcrLib"
        );
        pythonLibPath = Path.GetFullPath(pythonLibPath);

        var venvPath = Path.Combine(pythonLibPath, ".venv");

        // Build host with Python environment
        var builder = Host.CreateApplicationBuilder();

        builder.Services
            .WithPython()
            .WithHome(pythonLibPath)
            .WithVirtualEnvironment(venvPath, true)
            .FromRedistributable("3.13")
            .WithPipInstaller("requirements.txt");

        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        _host = builder.Build();

        // Create executor
        var pythonEnv = _host.Services.GetRequiredService<IPythonEnvironment>();
        var logger = _host.Services.GetRequiredService<ILogger<GotOcr2Executor>>();
        _executor = new GotOcr2Executor(pythonEnv, logger);

        // Setup fixtures path
        _fixturesPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "Fixtures", "PRP1"
        );
        _fixturesPath = Path.GetFullPath(_fixturesPath);
    }

    [Fact]
    public async Task ExecuteOcrAsync_WithRealPdfFixture_ExtractsText()
    {
        // Arrange
        var imageFile = Path.Combine(_fixturesPath, "222AAA-44444444442025_page-0001.jpg");

        if (!File.Exists(imageFile))
        {
            throw new FileNotFoundException($"Fixture not found: {imageFile}");
        }

        var imageBytes = await File.ReadAllBytesAsync(imageFile);
        var imageData = new ImageData(imageBytes, imageFile, 1, 1);
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor.ExecuteOcrAsync(imageData, config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Text.ShouldNotBeEmpty();
        result.Value.ConfidenceAvg.ShouldBeGreaterThan(0);

        Console.WriteLine($"Extracted text length: {result.Value.Text.Length}");
        Console.WriteLine($"Confidence avg: {result.Value.ConfidenceAvg:F2}");
        Console.WriteLine($"Text preview: {result.Value.Text.Substring(0, Math.Min(200, result.Value.Text.Length))}...");
    }

    [Fact]
    public async Task ExecuteOcrAsync_WithMultipleFixtures_ProcessesAll()
    {
        // Arrange
        var imageFiles = Directory.GetFiles(_fixturesPath, "*.jpg");

        if (imageFiles.Length == 0)
        {
            throw new FileNotFoundException("No JPG fixtures found");
        }

        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);
        var successCount = 0;

        // Act
        foreach (var imageFile in imageFiles)
        {
            var imageBytes = await File.ReadAllBytesAsync(imageFile);
            var imageData = new ImageData(imageBytes, imageFile);

            var result = await _executor.ExecuteOcrAsync(imageData, config);

            if (result.IsSuccess && result.Value is not null)

            {
                successCount++;
                Console.WriteLine($"✓ {Path.GetFileName(imageFile)}: {result.Value.Text.Length} chars, {result.Value.ConfidenceAvg:F2}% confidence");
            }
            else
            {
                Console.WriteLine($"✗ {Path.GetFileName(imageFile)}: {result.Error}");
            }
        }

        // Assert
        successCount.ShouldBeGreaterThan(0);
        Console.WriteLine($"\nProcessed {successCount}/{imageFiles.Length} files successfully");
    }

    [Fact]
    public async Task ExecuteOcrAsync_WithPdfPage_ExtractsSpanishText()
    {
        // Arrange
        var imageFile = Path.Combine(_fixturesPath, "222AAA-44444444442025_page-0001.jpg");

        if (!File.Exists(imageFile))
        {
            throw new FileNotFoundException($"Fixture not found: {imageFile}");
        }

        var imageBytes = await File.ReadAllBytesAsync(imageFile);
        var imageData = new ImageData(imageBytes, imageFile, 1, 4);
        var config = new OCRConfig("spa", 1, 6, "eng", 0.7f);

        // Act
        var result = await _executor.ExecuteOcrAsync(imageData, config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        result.Value.LanguageUsed.ShouldBe("spa");

        // Check for Spanish content indicators
        var text = result.Value.Text.ToLower();
        var hasSpanishContent =
            text.Contains("de") ||
            text.Contains("el") ||
            text.Contains("la") ||
            text.Contains("en") ||
            text.Contains("los");

        hasSpanishContent.ShouldBeTrue("Expected Spanish content in extracted text");

        Console.WriteLine($"Extracted Spanish text: {result.Value.Text.Length} characters");
    }

    [Fact]
    public async Task ExecuteOcrAsync_WithInvalidImage_ReturnsFailureOrEmptyResult()
    {
        // Arrange
        var invalidBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var imageData = new ImageData(invalidBytes, "invalid.bin");
        var config = new OCRConfig();

        // Act
        var result = await _executor.ExecuteOcrAsync(imageData, config);

        // Assert
        // Either fails or returns empty text
        result.IsSuccess.ShouldBeTrue();

        result.Value.ShouldNotBeNull();
        result.Value.Text.ShouldBeEmpty();
        result.Value.ConfidenceAvg.ShouldBe(0);
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}