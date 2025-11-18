using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Web.UI.Hubs;

namespace ExxerCube.Prisma.Web.UI.Controllers;

/// <summary>
/// API controller for document processing operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentProcessingController : ControllerBase
{
    private readonly IOcrProcessingService _ocrService;
    private readonly ILogger<DocumentProcessingController> _logger;
    private readonly ProcessingHub _processingHub;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentProcessingController"/> class.
    /// </summary>
    /// <param name="ocrService">The OCR processing service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="processingHub">The processing hub for real-time updates.</param>
    public DocumentProcessingController(
        IOcrProcessingService ocrService,
        ILogger<DocumentProcessingController> logger,
        ProcessingHub processingHub)
    {
        _ocrService = ocrService;
        _logger = logger;
        _processingHub = processingHub;
    }

    /// <summary>
    /// Uploads and processes a document.
    /// </summary>
    /// <param name="file">The document file to process.</param>
    /// <returns>The processing response with job ID and status.</returns>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            // Validate file size (20MB limit for legal documents)
            const long maxFileSize = 20 * 1024 * 1024; // 20MB
            if (file.Length > maxFileSize)
            {
                return BadRequest($"File size exceeds limit. Maximum size: 20MB");
            }

            // Validate file type
            var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".tiff", ".bmp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest($"Unsupported file type. Allowed types: {string.Join(", ", allowedExtensions)}");
            }

            // Validate content type
            var allowedContentTypes = new[] { 
                "application/pdf", 
                "image/png", 
                "image/jpeg", 
                "image/jpg", 
                "image/tiff", 
                "image/bmp" 
            };
            
            if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest($"Invalid content type: {file.ContentType}");
            }

            var jobId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting document processing: JobId={JobId}, FileName={FileName}, Size={Size}bytes", 
                jobId, file.FileName, file.Length);

            // Convert file to ImageData
            var imageData = await ConvertFileToImageData(file, jobId);
            
            // Create processing configuration
            var config = new ProcessingConfig
            {
                OCRConfig = new OCRConfig
                {
                    Language = "spa",
                    ConfidenceThreshold = 80.0f
                }
            };

            // Start processing in background
            _ = Task.Run(async () => await ProcessDocumentAsync(jobId, imageData, config));

            return Ok(new ProcessingResponse
            {
                JobId = jobId,
                Status = "Processing",
                Message = "Document uploaded successfully and processing started"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document upload failed");
            return StatusCode(500, "Internal processing error");
        }
    }

    /// <summary>
    /// Gets the processing status for a job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The current processing status.</returns>
    [HttpGet("status/{jobId}")]
    public IActionResult GetProcessingStatus(string jobId)
    {
        try
        {
            // This would typically query a job store
            // For now, we'll return a basic status
            return Ok(new ProcessingStatusResponse
            {
                JobId = jobId,
                Status = "Processing",
                Progress = 50,
                Message = "Document is being processed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing status for JobId={JobId}", jobId);
            return StatusCode(500, "Failed to retrieve processing status");
        }
    }

    /// <summary>
    /// Gets the processing results for a completed job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The processing results.</returns>
    [HttpGet("results/{jobId}")]
    public IActionResult GetProcessingResults(string jobId)
    {
        try
        {
            // This would typically query a result store
            // For now, we'll return a placeholder
            return Ok(new ProcessingResultResponse
            {
                JobId = jobId,
                Status = "Completed",
                Message = "Results retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing results for JobId={JobId}", jobId);
            return StatusCode(500, "Failed to retrieve processing results");
        }
    }

    /// <summary>
    /// Converts an uploaded file to ImageData.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The ImageData representation of the file.</returns>
    private async Task<ImageData> ConvertFileToImageData(IFormFile file, string jobId)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        
        return new ImageData
        {
            SourcePath = file.FileName,
            Data = memoryStream.ToArray(),
            PageNumber = 1,
            TotalPages = 1
        };
    }

    /// <summary>
    /// Processes a document asynchronously with real-time updates.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    private async Task ProcessDocumentAsync(string jobId, ImageData imageData, ProcessingConfig config)
    {
        try
        {
            // Send initial status
            await _processingHub.UpdateProcessingStatus(jobId, "Starting", 0, "Initializing processing");

            // Simulate processing steps with real-time updates
            await _processingHub.UpdateProcessingStatus(jobId, "Preprocessing", 20, "Preprocessing image");
            await Task.Delay(1000); // Simulate preprocessing time

            await _processingHub.UpdateProcessingStatus(jobId, "OCR Processing", 50, "Performing OCR analysis");
            await Task.Delay(2000); // Simulate OCR processing time

            await _processingHub.UpdateProcessingStatus(jobId, "Field Extraction", 80, "Extracting document fields");
            await Task.Delay(1000); // Simulate field extraction time

            // Process with actual OCR service
            var result = await _ocrService.ProcessDocumentAsync(imageData, config);

            if (result.IsSuccess)
            {
                await _processingHub.UpdateProcessingStatus(jobId, "Completed", 100, "Processing completed successfully");
                await _processingHub.ProcessingComplete(jobId, result.Value!);
                
                _logger.LogInformation("Document processing completed successfully: JobId={JobId}", jobId);
            }
            else
            {
                await _processingHub.UpdateProcessingStatus(jobId, "Failed", 0, "Processing failed");
                await _processingHub.ProcessingError(jobId, result.Error!);
                
                _logger.LogError("Document processing failed: JobId={JobId}, Error={Error}", jobId, result.Error);
            }
        }
        catch (Exception ex)
        {
            await _processingHub.UpdateProcessingStatus(jobId, "Failed", 0, "Unexpected error occurred");
            await _processingHub.ProcessingError(jobId, ex.Message);
            
            _logger.LogError(ex, "Unexpected error during document processing: JobId={JobId}", jobId);
        }
    }
}
