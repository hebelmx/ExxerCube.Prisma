using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Drawing;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Filters;

/// <summary>
/// OpenCV-based advanced image enhancement filter using EmguCV.
/// Implements: denoise + CLAHE + bilateral filter + unsharp mask.
/// 7-parameter pipeline optimized for complex degradation patterns.
/// </summary>
public class OpenCvAdvancedEnhancementFilter : IImageEnhancementFilter
{
    private readonly ILogger<OpenCvAdvancedEnhancementFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCvAdvancedEnhancementFilter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public OpenCvAdvancedEnhancementFilter(ILogger<OpenCvAdvancedEnhancementFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ImageFilterType FilterType => ImageFilterType.OpenCvAdvanced;

    /// <inheritdoc />
    public string FilterName => "OpenCV Advanced Enhancement";

    /// <inheritdoc />
    public Task<Result<ImageData>> EnhanceAsync(ImageData imageData, ImageFilterConfig config)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            if (!config.EnableEnhancement)
            {
                _logger.LogDebug("Enhancement disabled, returning original image");
                return Task.FromResult(Result<ImageData>.Success(imageData));
            }

            var cvParams = config.OpenCvParams;
            _logger.LogInformation(
                "Applying OpenCV advanced enhancement: denoise_h={DenoiseH}, clahe_clip={ClaheClip}, " +
                "bilateral_d={BilateralD}, sigma_color={SigmaColor}, sigma_space={SigmaSpace}, " +
                "unsharp_amount={UnsharpAmount}, unsharp_radius={UnsharpRadius}",
                cvParams.DenoiseH, cvParams.ClaheClip, cvParams.BilateralD,
                cvParams.SigmaColor, cvParams.SigmaSpace,
                cvParams.UnsharpAmount, cvParams.UnsharpRadius);

            using var inputMat = new Mat();
            CvInvoke.Imdecode(imageData.Data, ImreadModes.ColorRgb, inputMat);

            if (inputMat.IsEmpty)
            {
                return Task.FromResult(Result<ImageData>.Failure("Failed to decode image data"));
            }

            // Step 1: Denoise (Non-local means denoising)
            using var denoised = ApplyDenoise(inputMat, cvParams.DenoiseH);

            // Step 2: CLAHE (Contrast Limited Adaptive Histogram Equalization)
            using var claheApplied = ApplyClahe(denoised, cvParams.ClaheClip);

            // Step 3: Bilateral filter (edge-preserving smoothing)
            using var bilateralFiltered = ApplyBilateralFilter(
                claheApplied, cvParams.BilateralD, cvParams.SigmaColor, cvParams.SigmaSpace);

            // Step 4: Unsharp mask (sharpening)
            using var sharpened = ApplyUnsharpMask(
                bilateralFiltered, cvParams.UnsharpAmount, cvParams.UnsharpRadius);

            // Encode result back to bytes
            var outputBytes = CvInvoke.Imencode(".png", sharpened);

            var result = new ImageData(
                outputBytes,
                imageData.SourcePath,
                imageData.PageNumber,
                imageData.TotalPages);

            _logger.LogInformation("OpenCV advanced enhancement completed successfully");
            return Task.FromResult(Result<ImageData>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying OpenCV advanced enhancement filter");
            return Task.FromResult(Result<ImageData>.Failure($"OpenCV enhancement failed: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public bool CanProcess(ImageData imageData)
    {
        return imageData?.Data != null && imageData.Data.Length > 0;
    }

    /// <summary>
    /// Applies non-local means denoising to an image.
    /// </summary>
    /// <param name="input">The input image.</param>
    /// <param name="h">Filter strength (higher = more denoising).</param>
    /// <returns>The denoised image.</returns>
    private static Mat ApplyDenoise(Mat input, float h)
    {
        var output = new Mat();

        if (input.NumberOfChannels == 3)
        {
            CvInvoke.FastNlMeansDenoisingColored(input, output, h, h, 7, 21);
        }
        else
        {
            CvInvoke.FastNlMeansDenoising(input, output, h, 7, 21);
        }

        return output;
    }

    /// <summary>
    /// Applies CLAHE (Contrast Limited Adaptive Histogram Equalization) to an image.
    /// </summary>
    /// <param name="input">The input image.</param>
    /// <param name="clipLimit">The clip limit for contrast limiting.</param>
    /// <returns>The CLAHE-processed image.</returns>
    private static Mat ApplyClahe(Mat input, float clipLimit)
    {
        var output = new Mat();

        // Convert to LAB color space for CLAHE on luminance channel
        using var labMat = new Mat();
        if (input.NumberOfChannels == 3)
        {
            CvInvoke.CvtColor(input, labMat, ColorConversion.Bgr2Lab);
        }
        else
        {
            CvInvoke.CvtColor(input, labMat, ColorConversion.Gray2Bgr);
            CvInvoke.CvtColor(labMat, labMat, ColorConversion.Bgr2Lab);
        }

        // Split LAB channels
        var labChannels = labMat.Split();

        // Apply CLAHE to L channel (index 0)
        using var claheResult = new Mat();
        var tileSize = new Size(8, 8);
        CvInvoke.CLAHE(labChannels[0], clipLimit, tileSize, claheResult);
        claheResult.CopyTo(labChannels[0]);

        // Merge channels back
        using var labOutput = new Mat();
        CvInvoke.Merge(new VectorOfMat(labChannels), labOutput);

        // Convert back to BGR
        CvInvoke.CvtColor(labOutput, output, ColorConversion.Lab2Bgr);

        // Clean up
        foreach (var channel in labChannels)
        {
            channel.Dispose();
        }

        return output;
    }

    /// <summary>
    /// Applies bilateral filtering to an image (edge-preserving smoothing).
    /// </summary>
    /// <param name="input">The input image.</param>
    /// <param name="d">Diameter of each pixel neighborhood.</param>
    /// <param name="sigmaColor">Filter sigma in the color space.</param>
    /// <param name="sigmaSpace">Filter sigma in the coordinate space.</param>
    /// <returns>The filtered image.</returns>
    private static Mat ApplyBilateralFilter(Mat input, int d, float sigmaColor, float sigmaSpace)
    {
        var output = new Mat();
        CvInvoke.BilateralFilter(input, output, d, sigmaColor, sigmaSpace);
        return output;
    }

    /// <summary>
    /// Applies unsharp mask sharpening to an image.
    /// Formula: sharpened = original + (original - blurred) * amount
    /// </summary>
    /// <param name="input">The input image.</param>
    /// <param name="amount">The sharpening amount (0 = no sharpening).</param>
    /// <param name="radius">The blur radius for the mask.</param>
    /// <returns>The sharpened image.</returns>
    private static Mat ApplyUnsharpMask(Mat input, float amount, float radius)
    {
        if (amount <= 0)
        {
            return input.Clone();
        }

        var output = new Mat();

        // Create Gaussian blur for the mask
        int kernelSize = (int)(radius * 2) | 1; // Ensure odd
        if (kernelSize < 3) kernelSize = 3;

        using var blurred = new Mat();
        CvInvoke.GaussianBlur(input, blurred, new Size(kernelSize, kernelSize), radius);

        // Apply unsharp mask: output = input + (input - blurred) * amount
        // Equivalent to: output = input * (1 + amount) - blurred * amount
        using var inputFloat = new Mat();
        using var blurredFloat = new Mat();

        input.ConvertTo(inputFloat, DepthType.Cv32F);
        blurred.ConvertTo(blurredFloat, DepthType.Cv32F);

        using var diff = new Mat();
        CvInvoke.Subtract(inputFloat, blurredFloat, diff);

        using var scaledDiff = new Mat();
        CvInvoke.Multiply(diff, new ScalarArray(amount), scaledDiff);

        using var resultFloat = new Mat();
        CvInvoke.Add(inputFloat, scaledDiff, resultFloat);

        // Convert back to 8-bit and clamp
        resultFloat.ConvertTo(output, DepthType.Cv8U);

        return output;
    }
}
