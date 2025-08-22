using System.Collections.Generic;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Common;
using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the file loader service for loading images from the file system.
/// </summary>
public interface IFileLoader
{
    /// <summary>
    /// Loads an image from a file path.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <returns>A result containing the loaded image data or an error.</returns>
    Task<Result<ImageData>> LoadImageAsync(string filePath);

    /// <summary>
    /// Loads multiple images from a directory.
    /// </summary>
    /// <param name="directoryPath">The path to the directory containing images.</param>
    /// <param name="supportedExtensions">The supported file extensions.</param>
    /// <returns>A result containing the list of loaded image data or an error.</returns>
    Task<Result<List<ImageData>>> LoadImagesFromDirectoryAsync(string directoryPath, string[] supportedExtensions);

    /// <summary>
    /// Gets the list of supported file extensions.
    /// </summary>
    /// <returns>An array of supported file extensions.</returns>
    string[] GetSupportedExtensions();

    /// <summary>
    /// Validates if a file path is valid and accessible.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    Task<Result<bool>> ValidateFilePathAsync(string filePath);
}
