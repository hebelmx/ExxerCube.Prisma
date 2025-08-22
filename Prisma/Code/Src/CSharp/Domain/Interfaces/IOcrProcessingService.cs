using System.Collections.Generic;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the main OCR processing service that orchestrates the entire pipeline.
/// </summary>
public interface IOcrProcessingService
{
    /// <summary>
    /// Processes a document image and extracts structured data.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <returns>A result containing the processing result or an error.</returns>
    Task<Result<ProcessingResult>> ProcessDocumentAsync(ImageData imageData, ProcessingConfig config);

    /// <summary>
    /// Processes multiple documents concurrently.
    /// </summary>
    /// <param name="imageDataList">The list of image data to process.</param>
    /// <param name="config">The processing configuration.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent operations.</param>
    /// <returns>A result containing the list of processing results or an error.</returns>
    Task<Result<List<ProcessingResult>>> ProcessDocumentsAsync(IEnumerable<ImageData> imageDataList, ProcessingConfig config, int maxConcurrency = 5);
}
