namespace TransformersSharp.Models;

/// <summary>
/// Represents the pixel mode for image data.
/// </summary>
public enum ImagePixelMode
{
    /// <summary>
    /// RGB color mode with three channels (red, green, blue).
    /// </summary>
    RGB,
    
    /// <summary>
    /// Greyscale mode with a single channel.
    /// </summary>
    Greyscale,
}

/// <summary>
/// Represents image data with pixel information.
/// </summary>
public struct ImageData
{
    /// <summary>
    /// Gets or sets the raw image bytes.
    /// </summary>
    public required byte[] ImageBytes { get; set; }
    
    /// <summary>
    /// Gets or sets the width of the image in pixels.
    /// </summary>
    public required int Width { get; set; }
    
    /// <summary>
    /// Gets or sets the height of the image in pixels.
    /// </summary>
    public required int Height { get; set; }
    
    /// <summary>
    /// Gets or sets the pixel mode of the image.
    /// </summary>
    public required ImagePixelMode PixelMode { get; set; }
}
