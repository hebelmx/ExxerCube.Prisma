# Polynomial Filter Implementation Guide for C#

## Overview

This guide details how to implement a new `PolynomialImageQualityAnalyzer` that uses the polynomial regression model from our GA optimization to predict optimal filter parameters based on image properties.

**Result Summary:**
- Polynomial model achieves **18.4% OCR improvement** (vs 12.3% for lookup table)
- Wins 21 vs 11 on validation dataset
- R² > 0.89 for all filter parameters

---

## 1. Architecture Integration

### Current Architecture

```
IImageQualityAnalyzer (interface)
    └── EmguCVImageQualityAnalyzer (existing - if implemented)
    └── PolynomialImageQualityAnalyzer (NEW - to implement)

IFilterSelectionStrategy (interface)
    └── DefaultFilterSelectionStrategy (existing)
    └── PolynomialFilterSelectionStrategy (NEW - to implement)

IImageEnhancementFilter (interface)
    └── PilSimpleEnhancementFilter (existing - uses ContrastFactor + MedianSize)
    └── PolynomialEnhancementFilter (NEW - uses 5 continuous parameters)
```

### Key Interfaces (from Domain/Interfaces/)

```csharp
// IImageQualityAnalyzer.cs - already exists
public interface IImageQualityAnalyzer
{
    Task<Result<ImageQualityAssessment>> AnalyzeAsync(ImageData imageData);
    Task<Result<ImageQualityLevel>> GetQualityLevelAsync(ImageData imageData);
}

// ImageQualityAssessment - already exists
public class ImageQualityAssessment
{
    public ImageQualityLevel QualityLevel { get; set; }
    public float Confidence { get; set; }
    public float NoiseLevel { get; set; }      // We'll populate this
    public float ContrastLevel { get; set; }   // We'll populate this
    public float SharpnessLevel { get; set; }  // We'll populate this
    public Dictionary<string, object> Diagnostics { get; set; }  // Store raw features here
}
```

---

## 2. New Domain Models

### 2.1 PolynomialFilterParams (add to ImageFilterConfig.cs)

```csharp
/// <summary>
/// Parameters for polynomial-predicted image enhancement.
/// Predicts 5 continuous parameters from image properties.
/// Based on GA per-cluster optimization + polynomial fitting.
/// Achieves 18.4% OCR improvement on degraded documents.
/// </summary>
public class PolynomialFilterParams
{
    /// <summary>
    /// Contrast enhancement factor. Range: [0.5, 2.0]
    /// </summary>
    public float Contrast { get; set; } = 1.0f;

    /// <summary>
    /// Brightness adjustment factor. Range: [0.8, 1.3]
    /// </summary>
    public float Brightness { get; set; } = 1.0f;

    /// <summary>
    /// Sharpness enhancement factor. Range: [0.5, 3.0]
    /// </summary>
    public float Sharpness { get; set; } = 1.0f;

    /// <summary>
    /// Unsharp mask radius in pixels. Range: [0.0, 5.0]
    /// </summary>
    public float UnsharpRadius { get; set; } = 0.0f;

    /// <summary>
    /// Unsharp mask strength percentage. Range: [0, 250]
    /// </summary>
    public int UnsharpPercent { get; set; } = 0;

    /// <summary>
    /// Fixed unsharp threshold (not predicted).
    /// </summary>
    public int UnsharpThreshold { get; set; } = 2;
}
```

### 2.2 ImagePropertyFeatures (new class)

```csharp
/// <summary>
/// Image properties extracted for polynomial model prediction.
/// These 4 features are INPUT to the polynomial regression.
/// </summary>
public class ImagePropertyFeatures
{
    /// <summary>
    /// Laplacian variance - higher = sharper image.
    /// Typical range: 0-10000+
    /// </summary>
    public double BlurScore { get; set; }

    /// <summary>
    /// Standard deviation of grayscale intensity.
    /// Typical range: 0-80
    /// </summary>
    public double Contrast { get; set; }

    /// <summary>
    /// Mean absolute Laplacian - high frequency energy.
    /// Typical range: 0-100+
    /// </summary>
    public double NoiseEstimate { get; set; }

    /// <summary>
    /// Ratio of Canny edge pixels to total pixels.
    /// Typical range: 0-0.15
    /// </summary>
    public double EdgeDensity { get; set; }

    /// <summary>
    /// Returns features as array in model order.
    /// </summary>
    public double[] ToArray() => new[] { BlurScore, Contrast, NoiseEstimate, EdgeDensity };
}
```

---

## 3. PolynomialImageQualityAnalyzer Implementation

### 3.1 Feature Extraction (EmguCV)

```csharp
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace ExxerCube.Prisma.Infrastructure.Imaging.Analyzers;

/// <summary>
/// Image quality analyzer using polynomial regression for optimal filter prediction.
/// Extracts 4 image properties and uses trained polynomial model.
/// </summary>
public class PolynomialImageQualityAnalyzer : IImageQualityAnalyzer
{
    private readonly ILogger<PolynomialImageQualityAnalyzer> _logger;
    private readonly PolynomialModel _model;

    public PolynomialImageQualityAnalyzer(
        ILogger<PolynomialImageQualityAnalyzer> logger,
        IOptions<PolynomialModelOptions> modelOptions)
    {
        _logger = logger;
        _model = PolynomialModel.Load(modelOptions.Value.ModelPath);
    }

    /// <summary>
    /// Extracts image properties for polynomial prediction.
    /// </summary>
    public ImagePropertyFeatures ExtractFeatures(Mat image)
    {
        // Convert to grayscale
        using var gray = new Mat();
        if (image.NumberOfChannels > 1)
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);
        else
            image.CopyTo(gray);

        // 1. Blur Score (Laplacian variance)
        using var laplacian = new Mat();
        CvInvoke.Laplacian(gray, laplacian, DepthType.Cv64F);
        var laplacianArray = (double[,])laplacian.GetData();
        var blurScore = ComputeVariance(laplacianArray);

        // 2. Contrast (standard deviation of intensity)
        var contrast = ComputeStdDev(gray);

        // 3. Noise Estimate (mean absolute Laplacian)
        var noiseEstimate = ComputeMeanAbsolute(laplacianArray);

        // 4. Edge Density (Canny edge ratio)
        using var edges = new Mat();
        CvInvoke.Canny(gray, edges, 100, 200);
        var edgeArray = (byte[,])edges.GetData();
        var edgePixels = CountNonZero(edgeArray);
        var edgeDensity = (double)edgePixels / (edges.Rows * edges.Cols);

        return new ImagePropertyFeatures
        {
            BlurScore = blurScore,
            Contrast = contrast,
            NoiseEstimate = noiseEstimate,
            EdgeDensity = edgeDensity
        };
    }

    public async Task<Result<ImageQualityAssessment>> AnalyzeAsync(ImageData imageData)
    {
        try
        {
            using var inputMat = new Mat();
            CvInvoke.Imdecode(imageData.Data, ImreadModes.Color, inputMat);

            if (inputMat.IsEmpty)
                return Result<ImageQualityAssessment>.Failure("Failed to decode image");

            var features = ExtractFeatures(inputMat);

            // Predict filter parameters using polynomial model
            var predictedParams = _model.Predict(features);

            // Map features to assessment
            var assessment = new ImageQualityAssessment
            {
                // Normalize features to 0-1 range for assessment
                NoiseLevel = (float)Math.Min(1.0, features.NoiseEstimate / 50.0),
                ContrastLevel = (float)Math.Min(1.0, features.Contrast / 60.0),
                SharpnessLevel = (float)Math.Min(1.0, features.BlurScore / 2000.0),
                Confidence = 0.9f,  // High confidence from polynomial model

                // Store raw features in diagnostics
                Diagnostics = new Dictionary<string, object>
                {
                    ["blur_score"] = features.BlurScore,
                    ["contrast"] = features.Contrast,
                    ["noise_estimate"] = features.NoiseEstimate,
                    ["edge_density"] = features.EdgeDensity,
                    ["predicted_params"] = predictedParams
                }
            };

            // Determine quality level based on blur score
            assessment.QualityLevel = DetermineQualityLevel(features);
            assessment.RecommendedFilter = ImageFilterType.Polynomial;

            return Result<ImageQualityAssessment>.Success(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image properties");
            return Result<ImageQualityAssessment>.Failure(ex.Message);
        }
    }

    private static ImageQualityLevel DetermineQualityLevel(ImagePropertyFeatures features)
    {
        // Higher blur score = sharper image = better quality
        return features.BlurScore switch
        {
            < 200 => ImageQualityLevel.Q1_Poor,
            < 500 => ImageQualityLevel.Q2_MediumPoor,
            < 1500 => ImageQualityLevel.Q3_Low,
            < 3000 => ImageQualityLevel.Q4_VeryLow,
            _ => ImageQualityLevel.Pristine
        };
    }

    // Helper methods
    private static double ComputeVariance(double[,] data)
    {
        var sum = 0.0;
        var sumSq = 0.0;
        var count = data.Length;
        foreach (var val in data)
        {
            sum += val;
            sumSq += val * val;
        }
        var mean = sum / count;
        return (sumSq / count) - (mean * mean);
    }

    private static double ComputeStdDev(Mat gray)
    {
        var stdDev = new MCvScalar();
        var mean = new MCvScalar();
        CvInvoke.MeanStdDev(gray, ref mean, ref stdDev);
        return stdDev.V0;
    }

    private static double ComputeMeanAbsolute(double[,] data)
    {
        var sum = 0.0;
        foreach (var val in data)
            sum += Math.Abs(val);
        return sum / data.Length;
    }

    private static int CountNonZero(byte[,] data)
    {
        var count = 0;
        foreach (var val in data)
            if (val > 0) count++;
        return count;
    }
}
```

---

## 4. Polynomial Model Implementation

### 4.1 Model Coefficients (from polynomial_model_v2.json)

```csharp
/// <summary>
/// Polynomial regression model for filter parameter prediction.
/// Trained on GA-optimized cluster data.
/// </summary>
public class PolynomialModel
{
    // Scaler parameters (StandardScaler normalization)
    private static readonly double[] ScalerMean = { 565.758125, 29.1111, 15.2173, 4.3990 };
    private static readonly double[] ScalerScale = { 1225.0172, 5.8784, 18.2808, 2.5322 };

    // Polynomial coefficients for each parameter (degree 2, 15 features)
    // Feature order: 1, x0, x1, x2, x3, x0², x0x1, x0x2, x0x3, x1², x1x2, x1x3, x2², x2x3, x3²

    private static readonly double[] ContrastCoef = {
        0.0, 0.1096, -0.4311, 0.0875, 0.1243,
        0.0147, 0.2337, -0.3083, -0.4505,
        0.1190, -0.1537, 0.0157,
        0.2605, 0.3578, 0.0455
    };
    private static readonly double ContrastIntercept = 1.0011;

    private static readonly double[] BrightnessCoef = {
        0.0, 0.0030, 0.0185, 0.0022, -0.0428,
        -0.0066, -0.0039, 0.0132, -0.0546,
        -0.0031, 0.0135, 0.0006,
        0.0068, -0.0222, 0.0009
    };
    private static readonly double BrightnessIntercept = 1.0735;

    private static readonly double[] SharpnessCoef = {
        0.0, 0.0314, 0.3771, 0.0752, -0.1629,
        0.0101, -0.1100, 0.1108, -0.2895,
        0.1683, -0.1517, -0.2783,
        -0.0136, -0.0722, -0.0408
    };
    private static readonly double SharpnessIntercept = 2.2279;

    private static readonly double[] UnsharpRadiusCoef = {
        0.0, -0.1262, -0.0350, -0.1980, -0.6791,
        -0.0582, -0.2502, 0.3043, -0.1707,
        -0.4983, 0.8135, 0.5182,
        -0.1420, -0.6486, 0.0650
    };
    private static readonly double UnsharpRadiusIntercept = 2.5032;

    private static readonly double[] UnsharpPercentCoef = {
        0.0, -59.83, 42.22, -57.19, 20.20,
        21.89, -107.31, 80.69, 257.05,
        -62.80, 111.17, 24.40,
        -124.69, -111.20, -3.62
    };
    private static readonly double UnsharpPercentIntercept = 173.38;

    /// <summary>
    /// Predicts optimal filter parameters from image features.
    /// </summary>
    public PolynomialFilterParams Predict(ImagePropertyFeatures features)
    {
        // Step 1: Normalize features
        var normalized = NormalizeFeatures(features.ToArray());

        // Step 2: Generate polynomial features (degree 2)
        var polyFeatures = GeneratePolynomialFeatures(normalized);

        // Step 3: Predict each parameter
        var contrast = Clamp(DotProduct(polyFeatures, ContrastCoef) + ContrastIntercept, 0.5, 2.0);
        var brightness = Clamp(DotProduct(polyFeatures, BrightnessCoef) + BrightnessIntercept, 0.8, 1.3);
        var sharpness = Clamp(DotProduct(polyFeatures, SharpnessCoef) + SharpnessIntercept, 0.5, 3.0);
        var unsharpRadius = Clamp(DotProduct(polyFeatures, UnsharpRadiusCoef) + UnsharpRadiusIntercept, 0.0, 5.0);
        var unsharpPercent = (int)Clamp(DotProduct(polyFeatures, UnsharpPercentCoef) + UnsharpPercentIntercept, 0, 250);

        return new PolynomialFilterParams
        {
            Contrast = (float)contrast,
            Brightness = (float)brightness,
            Sharpness = (float)sharpness,
            UnsharpRadius = (float)unsharpRadius,
            UnsharpPercent = unsharpPercent,
            UnsharpThreshold = 2  // Fixed
        };
    }

    private double[] NormalizeFeatures(double[] features)
    {
        var normalized = new double[4];
        for (int i = 0; i < 4; i++)
            normalized[i] = (features[i] - ScalerMean[i]) / ScalerScale[i];
        return normalized;
    }

    /// <summary>
    /// Generates degree-2 polynomial features with bias.
    /// Order: [1, x0, x1, x2, x3, x0², x0x1, x0x2, x0x3, x1², x1x2, x1x3, x2², x2x3, x3²]
    /// </summary>
    private double[] GeneratePolynomialFeatures(double[] x)
    {
        return new double[]
        {
            1.0,           // bias
            x[0], x[1], x[2], x[3],  // linear terms
            x[0]*x[0],     // x0²
            x[0]*x[1],     // x0*x1
            x[0]*x[2],     // x0*x2
            x[0]*x[3],     // x0*x3
            x[1]*x[1],     // x1²
            x[1]*x[2],     // x1*x2
            x[1]*x[3],     // x1*x3
            x[2]*x[2],     // x2²
            x[2]*x[3],     // x2*x3
            x[3]*x[3]      // x3²
        };
    }

    private static double DotProduct(double[] a, double[] b)
    {
        var sum = 0.0;
        for (int i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
```

---

## 5. Polynomial Enhancement Filter

```csharp
/// <summary>
/// Image enhancement filter using polynomial-predicted parameters.
/// Applies: Brightness -> Contrast -> Sharpness -> UnsharpMask
/// </summary>
public class PolynomialEnhancementFilter : IImageEnhancementFilter
{
    private readonly ILogger<PolynomialEnhancementFilter> _logger;
    private readonly PolynomialImageQualityAnalyzer _analyzer;

    public ImageFilterType FilterType => ImageFilterType.Polynomial;
    public string FilterName => "Polynomial Enhancement";

    public async Task<Result<ImageData>> EnhanceAsync(ImageData imageData, ImageFilterConfig config)
    {
        try
        {
            using var inputMat = new Mat();
            CvInvoke.Imdecode(imageData.Data, ImreadModes.Color, inputMat);

            if (inputMat.IsEmpty)
                return Result<ImageData>.Failure("Failed to decode image");

            // Get predicted parameters
            var features = _analyzer.ExtractFeatures(inputMat);
            var polyParams = config.PolynomialParams ?? _analyzer.Model.Predict(features);

            _logger.LogInformation(
                "Applying polynomial enhancement: contrast={Contrast}, brightness={Brightness}, " +
                "sharpness={Sharpness}, unsharp_radius={Radius}, unsharp_percent={Percent}",
                polyParams.Contrast, polyParams.Brightness, polyParams.Sharpness,
                polyParams.UnsharpRadius, polyParams.UnsharpPercent);

            // Apply filter chain
            using var enhanced = ApplyFilters(inputMat, polyParams);

            var outputBytes = CvInvoke.Imencode(".png", enhanced);
            return Result<ImageData>.Success(new ImageData(
                outputBytes, imageData.SourcePath, imageData.PageNumber, imageData.TotalPages));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in polynomial enhancement");
            return Result<ImageData>.Failure(ex.Message);
        }
    }

    private Mat ApplyFilters(Mat input, PolynomialFilterParams p)
    {
        var current = input.Clone();

        // 1. Brightness adjustment
        if (Math.Abs(p.Brightness - 1.0f) > 0.01f)
        {
            using var temp = current;
            current = ApplyBrightness(temp, p.Brightness);
        }

        // 2. Contrast adjustment
        if (Math.Abs(p.Contrast - 1.0f) > 0.01f)
        {
            using var temp = current;
            current = ApplyContrast(temp, p.Contrast);
        }

        // 3. Sharpness adjustment (via unsharp mask with fixed params)
        if (Math.Abs(p.Sharpness - 1.0f) > 0.01f)
        {
            using var temp = current;
            current = ApplySharpness(temp, p.Sharpness);
        }

        // 4. Unsharp mask (if radius and percent are significant)
        if (p.UnsharpRadius > 0.5f && p.UnsharpPercent > 50)
        {
            using var temp = current;
            current = ApplyUnsharpMask(temp, p.UnsharpRadius, p.UnsharpPercent, p.UnsharpThreshold);
        }

        return current;
    }

    private Mat ApplyBrightness(Mat input, float factor)
    {
        var output = new Mat();
        // brightness = alpha * pixel + beta, where alpha=1, beta=127.5*(factor-1)
        input.ConvertTo(output, DepthType.Cv8U, 1.0, 127.5 * (factor - 1));
        return output;
    }

    private Mat ApplyContrast(Mat input, float factor)
    {
        var output = new Mat();
        // contrast = alpha * (pixel - 127.5) + 127.5 = alpha * pixel + 127.5 * (1 - alpha)
        input.ConvertTo(output, DepthType.Cv8U, factor, 127.5 * (1 - factor));
        return output;
    }

    private Mat ApplySharpness(Mat input, float factor)
    {
        // Create sharpening kernel
        // sharpened = original + factor * (original - blurred)
        using var blurred = new Mat();
        CvInvoke.GaussianBlur(input, blurred, new Size(0, 0), 1.0);

        var output = new Mat();
        CvInvoke.AddWeighted(input, 1.0 + factor, blurred, -factor, 0, output);
        return output;
    }

    private Mat ApplyUnsharpMask(Mat input, float radius, int percent, int threshold)
    {
        // Standard unsharp mask: output = original + amount * (original - blurred)
        using var blurred = new Mat();
        var sigma = radius;
        CvInvoke.GaussianBlur(input, blurred, new Size(0, 0), sigma);

        var amount = percent / 100.0;
        var output = new Mat();
        CvInvoke.AddWeighted(input, 1.0 + amount, blurred, -amount, 0, output);
        return output;
    }
}
```

---

## 6. Add to ImageFilterType Enum

```csharp
// In Domain/Enums/ImageFilterType.cs (or wherever the enum is defined)
public enum ImageFilterType
{
    None = 0,
    PilSimple = 1,
    OpenCvAdvanced = 2,
    Adaptive = 3,
    Polynomial = 4  // NEW
}
```

---

## 7. Dependency Injection Registration

```csharp
// In Infrastructure.Imaging/DependencyInjection.cs
public static IServiceCollection AddImagingInfrastructure(this IServiceCollection services)
{
    // ... existing registrations ...

    // Register polynomial analyzer
    services.AddSingleton<PolynomialImageQualityAnalyzer>();
    services.AddSingleton<IImageQualityAnalyzer, PolynomialImageQualityAnalyzer>();

    // Register polynomial filter
    services.AddSingleton<PolynomialEnhancementFilter>();
    services.AddKeyedSingleton<IImageEnhancementFilter, PolynomialEnhancementFilter>(
        ImageFilterType.Polynomial);

    return services;
}
```

---

## 8. Model Performance Reference

| Parameter | R² Score | MAE | Bounds |
|-----------|----------|-----|--------|
| contrast | 0.949 | 0.052 | [0.5, 2.0] |
| brightness | 0.987 | 0.004 | [0.8, 1.3] |
| sharpness | 0.947 | 0.089 | [0.5, 3.0] |
| unsharp_radius | 0.938 | 0.197 | [0.0, 5.0] |
| unsharp_percent | 0.897 | 16.4 | [0, 250] |

**Validation Results (32 unseen images):**
- No filter: 755.0 avg edit distance
- Lookup table: 661.9 (-12.3%)
- **Polynomial: 616.4 (-18.4%)** - Winner!

---

## 9. Usage Example

```csharp
// In your OCR processing pipeline
public async Task<string> ProcessDocumentAsync(ImageData imageData)
{
    // Analyze image and get optimal parameters
    var analyzer = _serviceProvider.GetRequiredService<PolynomialImageQualityAnalyzer>();
    var assessment = await analyzer.AnalyzeAsync(imageData);

    // Get polynomial filter
    var filter = _serviceProvider.GetRequiredKeyedService<IImageEnhancementFilter>(
        ImageFilterType.Polynomial);

    // Enhance image
    var config = new ImageFilterConfig
    {
        FilterType = ImageFilterType.Polynomial,
        EnableEnhancement = true,
        PolynomialParams = (PolynomialFilterParams)assessment.Value.Diagnostics["predicted_params"]
    };

    var enhanced = await filter.EnhanceAsync(imageData, config);

    // Run OCR on enhanced image
    return await _ocrEngine.RecognizeAsync(enhanced.Value);
}
```

---

## 10. Testing Checklist

- [ ] Feature extraction produces values in expected ranges
- [ ] Polynomial prediction matches Python reference implementation
- [ ] Filter application produces visually similar results to PIL
- [ ] End-to-end OCR improvement on test documents
- [ ] Performance acceptable (< 100ms per image for feature extraction + prediction)

---

## References

- `scripts/production_filter_inference.py` - Python reference implementation
- `Fixtures/polynomial_model_v2.json` - Trained model coefficients
- `scripts/OCR_FILTER_OPTIMIZATION_RETROSPECTIVE.md` - Methodology documentation
- `scripts/NewFilteringStrategy.md` - Strategy overview
