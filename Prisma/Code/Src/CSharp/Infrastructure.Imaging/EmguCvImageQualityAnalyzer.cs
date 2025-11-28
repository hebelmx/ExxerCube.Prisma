using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Imaging;

/// <summary>
/// Image quality analyzer using Emgu.CV (OpenCV) to detect blur, noise, contrast, and sharpness.
/// Uses computer vision algorithms to assess image quality and recommend appropriate filters.
/// </summary>
public class EmguCvImageQualityAnalyzer : IImageQualityAnalyzer
{
    private readonly ILogger<EmguCvImageQualityAnalyzer> _logger;

    // Quality thresholds based on empirical testing
    private const double BlurThresholdPristine = 1000.0; // High Laplacian variance = sharp
    private const double BlurThresholdGood = 500.0;
    private const double BlurThresholdMedium = 200.0;
    private const double BlurThresholdPoor = 100.0;

    private const double NoiseThresholdLow = 10.0;
    private const double NoiseThresholdMedium = 25.0;
    private const double NoiseThresholdHigh = 50.0;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmguCvImageQualityAnalyzer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public EmguCvImageQualityAnalyzer(ILogger<EmguCvImageQualityAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<ImageQualityAssessment>> AnalyzeAsync(ImageData imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        if (imageData.Data == null || imageData.Data.Length == 0)
        {
            return Task.FromResult(Result<ImageQualityAssessment>.Failure("Image data is null or empty"));
        }

        try
        {
            using var mat = new Mat();
            using var vectorOfByte = new VectorOfByte(imageData.Data);
            CvInvoke.Imdecode((IInputArray)vectorOfByte, ImreadModes.Grayscale, mat);

            if (mat.IsEmpty)
            {
                return Task.FromResult(Result<ImageQualityAssessment>.Failure("Failed to decode image"));
            }

            // Calculate quality metrics
            var blurScore = CalculateBlurScore(mat);
            var noiseLevel = EstimateNoiseLevel(mat);
            var contrastLevel = CalculateContrastLevel(mat);
            var sharpnessLevel = CalculateSharpness(mat);

            // Determine overall quality level
            var qualityLevel = DetermineQualityLevel(blurScore, noiseLevel, contrastLevel);

            // Select recommended filter based on quality metrics
            var recommendedFilter = SelectRecommendedFilter(qualityLevel, blurScore, noiseLevel, contrastLevel);

            var assessment = new ImageQualityAssessment
            {
                QualityLevel = qualityLevel,
                Confidence = 0.85f, // High confidence with CV-based analysis
                BlurScore = (float)blurScore,
                NoiseLevel = (float)(noiseLevel / 100.0), // Normalize to 0-1 range
                ContrastLevel = contrastLevel,
                SharpnessLevel = sharpnessLevel,
                RecommendedFilter = recommendedFilter
            };

            _logger.LogInformation(
                "CV Quality Analysis: Level={QualityLevel}, Blur={BlurScore:F2}, Noise={NoiseLevel:F2}, Contrast={ContrastLevel:F2}, Filter={RecommendedFilter}",
                assessment.QualityLevel,
                blurScore,
                noiseLevel,
                contrastLevel,
                assessment.RecommendedFilter);

            return Task.FromResult(Result<ImageQualityAssessment>.Success(assessment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image quality with Emgu.CV");
            return Task.FromResult(Result<ImageQualityAssessment>.Failure($"Quality analysis error: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ImageQualityLevel>> GetQualityLevelAsync(ImageData imageData)
    {
        var analysisResult = await AnalyzeAsync(imageData);

        if (!analysisResult.IsSuccess || analysisResult.Value == null)
        {
            return Result<ImageQualityLevel>.Failure(analysisResult.Error ?? "Analysis failed");
        }

        return Result<ImageQualityLevel>.Success(analysisResult.Value.QualityLevel);
    }

    /// <summary>
    /// Calculates blur score using Laplacian variance method.
    /// Higher values indicate sharper images, lower values indicate more blur.
    /// </summary>
    private double CalculateBlurScore(Mat grayImage)
    {
        using var laplacian = new Mat();
        CvInvoke.Laplacian(grayImage, laplacian, DepthType.Cv64F);

        MCvScalar mean = new MCvScalar();
        MCvScalar stdDev = new MCvScalar();
        CvInvoke.MeanStdDev(laplacian, ref mean, ref stdDev);

        // Variance of Laplacian - standard metric for blur detection
        double variance = stdDev.V0 * stdDev.V0;

        return variance;
    }

    /// <summary>
    /// Estimates noise level using standard deviation in homogeneous regions.
    /// </summary>
    private double EstimateNoiseLevel(Mat grayImage)
    {
        // Apply Gaussian blur to get smoothed version
        using var smoothed = new Mat();
        CvInvoke.GaussianBlur(grayImage, smoothed, new System.Drawing.Size(5, 5), 0);

        // Calculate difference between original and smoothed
        using var diff = new Mat();
        CvInvoke.AbsDiff(grayImage, smoothed, diff);

        // Standard deviation of difference indicates noise
        MCvScalar mean = new MCvScalar();
        MCvScalar stdDev = new MCvScalar();
        CvInvoke.MeanStdDev(diff, ref mean, ref stdDev);

        return stdDev.V0;
    }

    /// <summary>
    /// Calculates contrast level using histogram analysis.
    /// Returns value between 0 and 1, where higher is better contrast.
    /// </summary>
    private float CalculateContrastLevel(Mat grayImage)
    {
        MCvScalar mean = new MCvScalar();
        MCvScalar stdDev = new MCvScalar();
        CvInvoke.MeanStdDev(grayImage, ref mean, ref stdDev);

        // Normalize standard deviation to 0-1 range (typical range 0-127.5)
        float contrast = (float)(stdDev.V0 / 127.5);

        // Clamp to 0-1
        return Math.Min(1.0f, Math.Max(0.0f, contrast));
    }

    /// <summary>
    /// Calculates sharpness using gradient magnitude.
    /// Returns value between 0 and 1, where higher indicates sharper edges.
    /// </summary>
    private float CalculateSharpness(Mat grayImage)
    {
        using var gradX = new Mat();
        using var gradY = new Mat();

        // Calculate Sobel gradients
        CvInvoke.Sobel(grayImage, gradX, DepthType.Cv64F, 1, 0, 3);
        CvInvoke.Sobel(grayImage, gradY, DepthType.Cv64F, 0, 1, 3);

        // Calculate gradient magnitude: sqrt(gradX^2 + gradY^2)
        using var gradXSquared = new Mat();
        using var gradYSquared = new Mat();
        using var sumOfSquares = new Mat();
        using var magnitude = new Mat();

        CvInvoke.Pow(gradX, 2, gradXSquared);
        CvInvoke.Pow(gradY, 2, gradYSquared);
        CvInvoke.Add(gradXSquared, gradYSquared, sumOfSquares);
        CvInvoke.Sqrt(sumOfSquares, magnitude);

        MCvScalar mean = new MCvScalar();
        MCvScalar stdDev = new MCvScalar();
        CvInvoke.MeanStdDev(magnitude, ref mean, ref stdDev);

        // Normalize mean gradient magnitude (typical range 0-50)
        float sharpness = (float)(mean.V0 / 50.0);

        // Clamp to 0-1
        return Math.Min(1.0f, Math.Max(0.0f, sharpness));
    }

    /// <summary>
    /// Determines overall quality level based on blur, noise, and contrast metrics.
    /// </summary>
    private ImageQualityLevel DetermineQualityLevel(double blurScore, double noiseLevel, float contrastLevel)
    {
        // Pristine: Sharp, low noise, good contrast
        if (blurScore >= BlurThresholdPristine && noiseLevel <= NoiseThresholdLow && contrastLevel >= 0.6f)
        {
            return ImageQualityLevel.Pristine;
        }

        // Q1_Poor: Very blurry or very noisy
        if (blurScore < BlurThresholdPoor || noiseLevel > NoiseThresholdHigh)
        {
            return ImageQualityLevel.Q1_Poor;
        }

        // Q2_MediumPoor: Moderately blurry or moderately noisy
        if (blurScore < BlurThresholdMedium || noiseLevel > NoiseThresholdMedium)
        {
            return ImageQualityLevel.Q2_MediumPoor;
        }

        // Q3_Low: Slight blur or noise
        if (blurScore < BlurThresholdGood || noiseLevel > NoiseThresholdLow)
        {
            return ImageQualityLevel.Q3_Low;
        }

        // Q4_VeryLow: Minor quality issues
        return ImageQualityLevel.Q4_VeryLow;
    }

    /// <summary>
    /// Selects recommended filter based on quality metrics.
    /// Uses NSGA-II optimization results to choose optimal filter for each quality level.
    /// </summary>
    private ImageFilterType SelectRecommendedFilter(ImageQualityLevel qualityLevel, double blurScore, double noiseLevel, float contrastLevel)
    {
        // Based on NSGA-II optimization results:
        // - Q1 (Poor): OpenCV Advanced for aggressive enhancement
        // - Q2 (Medium-Poor): PIL Simple for balanced enhancement
        // - Q3+ (Low/Very Low): None (good enough quality)

        return qualityLevel.Value switch
        {
            1 => noiseLevel > NoiseThresholdHigh
                ? ImageFilterType.OpenCvAdvanced  // Heavy denoising needed
                : ImageFilterType.PilSimple,       // Moderate enhancement

            2 => blurScore < 150
                ? ImageFilterType.OpenCvAdvanced  // Significant blur
                : ImageFilterType.PilSimple,       // Moderate issues

            3 => contrastLevel < 0.4f
                ? ImageFilterType.PilSimple        // Low contrast boost
                : ImageFilterType.None,            // Acceptable quality

            4 => ImageFilterType.None,  // Good quality
            5 => ImageFilterType.None,    // Excellent quality

            _ => ImageFilterType.PilSimple  // Default fallback
        };
    }
}
