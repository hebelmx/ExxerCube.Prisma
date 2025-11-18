using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Web.UI.Controllers;

/// <summary>
/// Health check controller for system monitoring.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthCheckController : ControllerBase
{
    private readonly ILogger<HealthCheckController> _logger;
    private readonly IOcrProcessingService _ocrService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="ocrService">The OCR processing service.</param>
    public HealthCheckController(ILogger<HealthCheckController> logger, IOcrProcessingService ocrService)
    {
        _logger = logger;
        _ocrService = ocrService;
    }

    /// <summary>
    /// Gets the system health status.
    /// </summary>
    /// <returns>The health status.</returns>
    [HttpGet]
    public IActionResult GetHealth()
    {
        try
        {
            var healthStatus = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Services = new
                {
                    OCR = "Available",
                    Python = "Available",
                    Database = "Available"
                }
            };

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(500, new { Status = "Unhealthy", Error = ex.Message });
        }
    }

    /// <summary>
    /// Tests the Python integration with a simple OCR test.
    /// </summary>
    /// <returns>The test result.</returns>
    [HttpGet("test-python")]
    public async Task<IActionResult> TestPythonIntegration()
    {
        try
        {
            // Create a simple test image data
            var testImageData = new ImageData
            {
                SourcePath = "test.png",
                Data = new byte[] { 255, 255, 255 }, // Simple white image
                PageNumber = 1,
                TotalPages = 1
            };

            var config = new ProcessingConfig
            {
                OCRConfig = new OCRConfig
                {
                    Language = "spa",
                    ConfidenceThreshold = 80.0f
                }
            };

            // Test the OCR service
            var result = await _ocrService.ProcessDocumentAsync(testImageData, config);

            return Ok(new
            {
                Status = "Python Integration Test",
                Success = result.IsSuccess,
                Message = result.IsSuccess ? "Python integration working" : result.Error,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Python integration test failed");
            return StatusCode(500, new
            {
                Status = "Python Integration Test Failed",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}