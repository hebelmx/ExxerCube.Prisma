using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Strategies;

/// <summary>
/// Advanced polynomial-based filter selection strategy.
/// Uses polynomial regression models trained on NSGA-II optimization results
/// to predict optimal filter parameters from image quality metrics.
///
/// Architecture:
/// 1. Feature Extraction: [blur, noise, contrast, sharpness] → normalized [0,1]^4
/// 2. Filter Type Selection: Polynomial logistic regression
/// 3. Parameter Prediction: Separate polynomial models for each parameter
/// 4. Configuration Assembly: Combine predictions into ImageFilterConfig
///
/// TODO: Train polynomial models from filtering study results when available.
/// Currently uses stub models that return reasonable defaults.
/// </summary>
public class PolynomialFilterSelectionStrategy : IFilterSelectionStrategy
{
    private readonly ILogger<PolynomialFilterSelectionStrategy> _logger;
    private readonly FeatureNormalizer _normalizer;

    // Polynomial models for PIL parameters
    private readonly PolynomialModel _pilContrastModel;
    private readonly PolynomialModel _pilMedianModel;

    // Polynomial models for OpenCV parameters
    private readonly PolynomialModel _opencvDenoiseModel;
    private readonly PolynomialModel _opencvClaheModel;
    private readonly PolynomialModel _opencvBilateralDModel;
    private readonly PolynomialModel _opencvSigmaColorModel;
    private readonly PolynomialModel _opencvSigmaSpaceModel;
    private readonly PolynomialModel _opencvUnsharpAmountModel;
    private readonly PolynomialModel _opencvUnsharpRadiusModel;

    // Polynomial models for filter type classification
    private readonly PolynomialModel _filterTypeNoneScore;
    private readonly PolynomialModel _filterTypePilScore;
    private readonly PolynomialModel _filterTypeOpenCvScore;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolynomialFilterSelectionStrategy"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public PolynomialFilterSelectionStrategy(ILogger<PolynomialFilterSelectionStrategy> logger)
    {
        _logger = logger;
        _normalizer = new FeatureNormalizer();

        // TODO: Replace stub models with trained models from filtering study
        // For now, initialize with stub models that return reasonable defaults

        // PIL parameter models
        _pilContrastModel = PolynomialModel.CreateStub("PIL_ContrastFactor", 0.5, 3.0);
        _pilMedianModel = PolynomialModel.CreateStub("PIL_MedianSize", 1.0, 7.0);

        // OpenCV parameter models
        _opencvDenoiseModel = PolynomialModel.CreateStub("OpenCV_DenoiseH", 1.0, 20.0);
        _opencvClaheModel = PolynomialModel.CreateStub("OpenCV_ClaheClip", 1.0, 8.0);
        _opencvBilateralDModel = PolynomialModel.CreateStub("OpenCV_BilateralD", 3.0, 15.0);
        _opencvSigmaColorModel = PolynomialModel.CreateStub("OpenCV_SigmaColor", 10.0, 150.0);
        _opencvSigmaSpaceModel = PolynomialModel.CreateStub("OpenCV_SigmaSpace", 10.0, 150.0);
        _opencvUnsharpAmountModel = PolynomialModel.CreateStub("OpenCV_UnsharpAmount", 0.0, 3.0);
        _opencvUnsharpRadiusModel = PolynomialModel.CreateStub("OpenCV_UnsharpRadius", 0.5, 5.0);

        // Filter type classification models (softmax scores)
        _filterTypeNoneScore = PolynomialModel.CreateStub("FilterType_None_Score", 0.0, 1.0);
        _filterTypePilScore = PolynomialModel.CreateStub("FilterType_PIL_Score", 0.0, 1.0);
        _filterTypeOpenCvScore = PolynomialModel.CreateStub("FilterType_OpenCV_Score", 0.0, 1.0);
    }

    /// <inheritdoc />
    public ImageFilterConfig SelectFilter(ImageQualityAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        try
        {
            // 1. Normalize features to [0, 1] range
            var features = _normalizer.Normalize(assessment);

            _logger.LogDebug(
                "Normalized features: blur={Blur:F3}, noise={Noise:F3}, contrast={Contrast:F3}, sharpness={Sharpness:F3}",
                features[0], features[1], features[2], features[3]);

            // 2. Select filter type using polynomial classification
            var filterType = SelectFilterType(features);

            _logger.LogInformation(
                "Polynomial strategy selected filter: {FilterType} (Quality={Quality}, Blur={Blur:F1}, Noise={Noise:F2})",
                filterType, assessment.QualityLevel, assessment.BlurScore, assessment.NoiseLevel);

            // 3. Predict optimal parameters for selected filter
            var config = filterType.Value switch
            {
                0 => CreateNoneConfig(),
                1 => CreatePilConfig(features),
                2 => CreateOpenCvConfig(features),
                3 => CreateAdaptiveConfig(features),
                _ => CreatePilConfig(features) // Fallback to PIL
            };

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in polynomial filter selection, falling back to Q2 optimized config");
            return ImageFilterConfig.CreateQ2Optimized();
        }
    }

    /// <inheritdoc />
    public ImageFilterConfig SelectFilterByQuality(ImageQualityLevel qualityLevel)
    {
        // For direct quality level selection, fall back to analytical strategy rules
        // TODO: Could train separate models for quality-based selection
        return qualityLevel.Value switch
        {
            5 => CreateNoneConfig(),
            1 => CreateOpenCvConfigDefault(),
            2 => CreatePilConfigDefault(),
            3 => CreatePilConfigDefault(),
            4 => CreateNoneConfig(),
            _ => CreatePilConfigDefault()
        };
    }

    /// <inheritdoc />
    public ImageFilterConfig GetFilterConfig(ImageFilterType filterType)
    {
        return filterType.Value switch
        {
            0 => CreateNoneConfig(),
            1 => CreatePilConfigDefault(),
            2 => CreateOpenCvConfigDefault(),
            3 => CreateAdaptiveConfig(new double[] { 0.5, 0.5, 0.5, 0.5 }),
            _ => CreatePilConfigDefault()
        };
    }

    /// <summary>
    /// Selects filter type using polynomial logistic regression with softmax.
    /// </summary>
    /// <param name="features">Normalized feature vector.</param>
    /// <returns>Selected filter type.</returns>
    private ImageFilterType SelectFilterType(double[] features)
    {
        // TODO: Replace with trained polynomial logistic regression models
        // For now, use simple heuristic based on quality metrics

        double blur = features[0];      // High = sharp
        double noise = features[1];     // High = noisy
        double contrast = features[2];  // High = good contrast
        double sharpness = features[3]; // High = sharp edges

        // Pristine detection (use None filter)
        if (blur > 0.7 && noise < 0.1 && contrast > 0.6)
        {
            return ImageFilterType.None;
        }

        // Heavy degradation (use OpenCV for aggressive enhancement)
        if (blur < 0.3 || noise > 0.5)
        {
            return ImageFilterType.OpenCvAdvanced;
        }

        // Moderate degradation (use PIL - best ROI from NSGA-II results)
        return ImageFilterType.PilSimple;
    }

    /// <summary>
    /// Creates configuration with no filtering.
    /// </summary>
    private static ImageFilterConfig CreateNoneConfig()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.None,
            EnableEnhancement = false
        };
    }

    /// <summary>
    /// Creates PIL configuration with polynomial-predicted parameters.
    /// </summary>
    /// <param name="features">Normalized feature vector.</param>
    /// <returns>PIL configuration with optimized parameters.</returns>
    private ImageFilterConfig CreatePilConfig(double[] features)
    {
        var contrastFactor = (float)_pilContrastModel.Predict(features);
        var medianSize = (int)Math.Round(_pilMedianModel.Predict(features));

        // Ensure median size is odd
        if (medianSize % 2 == 0) medianSize++;
        medianSize = Math.Clamp(medianSize, 1, 7);

        _logger.LogDebug(
            "PIL parameters predicted: contrast={Contrast:F3}, median={Median}",
            contrastFactor, medianSize);

        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.PilSimple,
            EnableEnhancement = true,
            PilParams = new PilFilterParams
            {
                ContrastFactor = contrastFactor,
                MedianSize = medianSize
            }
        };
    }

    /// <summary>
    /// Creates OpenCV configuration with polynomial-predicted parameters.
    /// </summary>
    /// <param name="features">Normalized feature vector.</param>
    /// <returns>OpenCV configuration with optimized parameters.</returns>
    private ImageFilterConfig CreateOpenCvConfig(double[] features)
    {
        var denoiseH = (float)_opencvDenoiseModel.Predict(features);
        var claheClip = (float)_opencvClaheModel.Predict(features);
        var bilateralD = (int)Math.Round(_opencvBilateralDModel.Predict(features));
        var sigmaColor = (float)_opencvSigmaColorModel.Predict(features);
        var sigmaSpace = (float)_opencvSigmaSpaceModel.Predict(features);
        var unsharpAmount = (float)_opencvUnsharpAmountModel.Predict(features);
        var unsharpRadius = (float)_opencvUnsharpRadiusModel.Predict(features);

        // Ensure bilateral diameter is odd
        if (bilateralD % 2 == 0) bilateralD++;
        bilateralD = Math.Clamp(bilateralD, 3, 15);

        _logger.LogDebug(
            "OpenCV parameters predicted: denoise={Denoise:F1}, clahe={Clahe:F2}, bilateral={Bilateral}",
            denoiseH, claheClip, bilateralD);

        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.OpenCvAdvanced,
            EnableEnhancement = true,
            OpenCvParams = new OpenCvFilterParams
            {
                DenoiseH = denoiseH,
                ClaheClip = claheClip,
                BilateralD = bilateralD,
                SigmaColor = sigmaColor,
                SigmaSpace = sigmaSpace,
                UnsharpAmount = unsharpAmount,
                UnsharpRadius = unsharpRadius
            }
        };
    }

    /// <summary>
    /// Creates adaptive configuration (delegates to quality analyzer).
    /// </summary>
    /// <param name="features">Normalized feature vector.</param>
    /// <returns>Adaptive configuration.</returns>
    private static ImageFilterConfig CreateAdaptiveConfig(double[] features)
    {
        return ImageFilterConfig.CreateAdaptive();
    }

    /// <summary>
    /// Creates default PIL configuration (Q2 optimized).
    /// </summary>
    private static ImageFilterConfig CreatePilConfigDefault()
    {
        return ImageFilterConfig.CreateQ2Optimized();
    }

    /// <summary>
    /// Creates default OpenCV configuration.
    /// </summary>
    private static ImageFilterConfig CreateOpenCvConfigDefault()
    {
        return new ImageFilterConfig
        {
            FilterType = ImageFilterType.OpenCvAdvanced,
            EnableEnhancement = true,
            OpenCvParams = OpenCvFilterParams.CreateDefault()
        };
    }

    // TODO: Add method to load trained polynomial coefficients from JSON/config file
    // public void LoadTrainedModels(string coefficientsFilePath) { ... }

    // TODO: Add method to update models from new training data
    // public void UpdateModels(TrainingData data) { ... }
}
