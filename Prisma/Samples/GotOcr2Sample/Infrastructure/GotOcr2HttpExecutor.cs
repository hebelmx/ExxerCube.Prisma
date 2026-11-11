using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GotOcr2Sample.Domain.Interfaces;
using GotOcr2Sample.Domain.Models;
using GotOcr2Sample.Domain.ValueObjects;
using IndQuestResults;
using Microsoft.Extensions.Logging;

namespace GotOcr2Sample.Infrastructure;

/// <summary>
/// GOT-OCR2 implementation using FastAPI HTTP service
/// </summary>
public class GotOcr2HttpExecutor : IOcrExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GotOcr2HttpExecutor> _logger;
    private readonly string _baseUrl;

    public GotOcr2HttpExecutor(
        HttpClient httpClient,
        ILogger<GotOcr2HttpExecutor> logger,
        string baseUrl = "http://localhost:8000")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseUrl = baseUrl;
    }

    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        try
        {
            _logger.LogInformation(
                "Executing GOT-OCR2 OCR via HTTP on image: {SourcePath}, Page: {PageNumber}/{TotalPages}",
                imageData.SourcePath,
                imageData.PageNumber,
                imageData.TotalPages
            );

            // Encode image to base64
            string base64Image = Convert.ToBase64String(imageData.Data);

            // Create request
            var request = new
            {
                image_base64 = base64Image,
                language = config.Language,
                confidence_threshold = config.ConfidenceThreshold
            };

            // Call FastAPI endpoint
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/ocr", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return Result<OCRResult>.Failure($"HTTP {response.StatusCode}: {errorContent}");
            }

            // Parse response
            var ocrResponse = await response.Content.ReadFromJsonAsync<OcrApiResponse>();

            if (ocrResponse == null)
            {
                return Result<OCRResult>.Failure("Failed to parse OCR response");
            }

            // Map to domain model
            var result = new OCRResult(
                text: ocrResponse.text,
                confidenceAvg: (float)ocrResponse.confidence_avg,
                confidenceMedian: (float)ocrResponse.confidence_median,
                confidences: ocrResponse.confidences.ConvertAll(c => (float)c),
                languageUsed: ocrResponse.language_used
            );

            _logger.LogInformation(
                "GOT-OCR2 OCR completed for {SourcePath}. Text length: {TextLength}, Confidence: {Confidence:F2}",
                imageData.SourcePath,
                result.Text.Length,
                result.ConfidenceAvg
            );

            return Result<OCRResult>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for {SourcePath}", imageData.SourcePath);
            return Result<OCRResult>.Failure($"HTTP request failed: {ex.Message}. Is the FastAPI server running?");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GOT-OCR2 OCR execution failed for {SourcePath}", imageData.SourcePath);
            return Result<OCRResult>.Failure($"OCR execution failed: {ex.Message}");
        }
    }

    // Response model matching FastAPI Pydantic model
    private class OcrApiResponse
    {
        public string text { get; set; } = string.Empty;
        public double confidence_avg { get; set; }
        public double confidence_median { get; set; }
        public List<double> confidences { get; set; } = new();
        public string language_used { get; set; } = string.Empty;
    }
}
