# MLOps Workflow for Continuous Model Improvement

## Overview

This document describes the complete MLOps workflow for continuously improving the polynomial filter enhancement model using production data.

```
┌──────────────┐      ┌──────────────┐      ┌──────────────┐      ┌──────────────┐
│  Production  │─────▶│  OCR Session │─────▶│    Manual    │─────▶│  Retrain     │
│  OCR Pipeline│      │  Repository  │      │    Review    │      │  Model       │
└──────────────┘      └──────────────┘      └──────────────┘      └──────────────┘
                              │                     │                      │
                              │                     │                      │
                              ▼                     ▼                      ▼
                      ┌──────────────┐      ┌──────────────┐      ┌──────────────┐
                      │   Database   │      │  Admin UI    │      │ appsettings  │
                      │   Storage    │      │   Labeling   │      │   .json      │
                      └──────────────┘      └──────────────┘      └──────────────┘
                                                                           │
                                                                           │
                                                                           ▼
                                                                   ┌──────────────┐
                                                                   │ IOptions     │
                                                                   │ Monitor      │
                                                                   │ Hot-Reload   │
                                                                   └──────────────┘
```

## Phase 1: Data Collection (Implemented)

### 1.1 Store OCR Sessions

Every OCR processing operation stores comprehensive session data:

```csharp
public async Task ProcessDocument(ImageData imageData)
{
    var session = new OcrSession
    {
        // Image data
        ImageHash = ComputeHash(imageData),
        ImagePath = await StoreImage(imageData),

        // Extracted features
        BlurScore = features.BlurScore,
        Contrast = features.Contrast,
        NoiseEstimate = features.NoiseEstimate,
        EdgeDensity = features.EdgeDensity,

        // Predicted parameters
        PredictedContrast = params.Contrast,
        PredictedBrightness = params.Brightness,
        // ... etc

        // OCR results
        BaselineOcrText = baselineResult.Text,
        EnhancedOcrText = enhancedResult.Text,

        // Metadata
        ProcessedAt = DateTime.UtcNow,
        ModelVersion = "polynomial_v1"
    };

    await _repository.StoreSessionAsync(session);
}
```

### 1.2 Collect Metrics

- **Input Features**: BlurScore, Contrast, NoiseEstimate, EdgeDensity
- **Predicted Parameters**: Contrast, Brightness, Sharpness, UnsharpRadius, UnsharpPercent
- **OCR Results**: Baseline text, Enhanced text, Processing time
- **Quality Metrics**: Levenshtein distance (if ground truth available)

## Phase 2: Manual Review & Labeling

### 2.1 Review Workflow

Admin UI allows reviewers to:

1. **View Unreviewed Sessions**
   - Fetch sessions needing review: `GetUnreviewedSessionsAsync()`
   - Display side-by-side: Original vs Enhanced image
   - Show OCR results: Baseline vs Enhanced text

2. **Provide Ground Truth**
   - Manually correct OCR text
   - Rate quality (1-5 stars)
   - Add review notes

3. **Store Review Data**
   ```csharp
   await _repository.UpdateReviewAsync(
       sessionId: session.Id,
       groundTruth: correctedText,
       qualityRating: 4,
       reviewNotes: "Good improvement on degraded scan",
       reviewedBy: currentUser.Id
   );
   ```

### 2.2 Optimal Parameter Search (Optional)

For critical documents, perform grid search to find optimal parameters:

```csharp
// Try different parameter combinations
var bestParams = await GridSearchOptimalParameters(
    session.ImagePath,
    session.GroundTruth
);

await _repository.UpdateOptimalParametersAsync(
    session.Id,
    bestParams
);
```

## Phase 3: Model Retraining

### 3.1 Export Training Data

```bash
# Export reviewed sessions to JSON
dotnet run --project Tools.DataExport -- \
    --min-quality 3 \
    --output sessions.json
```

Or use repository API:

```csharp
await _repository.ExportTrainingDataAsync(
    outputPath: "sessions.json",
    format: "json",
    minQualityRating: 3
);
```

### 3.2 Run Retraining Script

```bash
cd scripts

# Install dependencies (first time only)
pip install pandas scikit-learn numpy

# Retrain model
python retrain_polynomial_model.py \
    --input ../data/sessions.json \
    --output appsettings.PolynomialCoefficients.json \
    --min-quality 3
```

### 3.3 Script Output

```
╔════════════════════════════════════════════════════════════════╗
║      POLYNOMIAL MODEL RETRAINING - PRODUCTION PIPELINE         ║
╚════════════════════════════════════════════════════════════════╝

📂 Loading sessions from sessions.json
   Total sessions loaded: 2,450
   Reviewed sessions (quality >= 3): 1,247
   Sessions with optimal parameters: 1,247

🔧 Preparing features and targets
   Feature shape: (1247, 4)
   Feature ranges:
     BlurScore:     [45.32, 8521.45]
     Contrast:      [12.34, 72.18]
     NoiseEstimate: [2.15, 95.67]
     EdgeDensity:   [0.0012, 0.1456]

🎯 Training model for contrast
   Training samples: 997
   Test samples:     250
   CV RMSE:          0.0485
   Test RMSE:        0.0512
   Test MAE:         0.0401
   R² Score:         0.9523

🎯 Training model for brightness
   ... (similar output for each parameter)

💾 Exporting to appsettings.PolynomialCoefficients.json
✅ Exported successfully

📊 Summary:
   Training Data Size:  1,247
   Average R² Score:    0.9384
   Average MAE:         5.2341

🔄 To apply in production:
   1. Copy appsettings.PolynomialCoefficients.json to production
   2. Merge with appsettings.json or use as override
   3. IOptionsMonitor will auto-reload within seconds
   4. Check logs for model reload confirmation

✅ Retraining completed successfully!
```

## Phase 4: Production Deployment

### 4.1 Update Configuration

Copy generated coefficients to production:

```bash
# Development
cp appsettings.PolynomialCoefficients.json \
   ../appsettings.Production.json

# Or use as override file (recommended)
cp appsettings.PolynomialCoefficients.json \
   /app/config/appsettings.PolynomialCoefficients.json
```

### 4.2 Hot-Reload Detection

Application automatically detects changes:

```csharp
// TrainedPolynomialModel constructor
_options.OnChange(newOptions =>
{
    _logger.LogInformation(
        "🔄 Polynomial model reloaded: version={Version}, " +
        "trained on {Size} sessions, trained={TrainedDate}",
        newOptions.ModelVersion,
        newOptions.TrainingDataSize,
        newOptions.TrainedDate);

    _logger.LogInformation(
        "  Model Performance: " +
        "Contrast R²={ContrastR2:F3}, " +
        "Brightness R²={BrightnessR2:F3}, ...",
        newOptions.ContrastModel.R2Score,
        newOptions.BrightnessModel.R2Score);
});
```

### 4.3 Production Logs

```
[2025-01-28 15:30:15] INFO: 🔄 Polynomial model reloaded: version=production_20250128_153000, trained on 1247 sessions, trained=2025-01-28
[2025-01-28 15:30:15] INFO:   Model Performance: Contrast R²=0.952, Brightness R²=0.987, Sharpness R²=0.941, UnsharpRadius R²=0.933, UnsharpPercent R²=0.901
[2025-01-28 15:30:16] INFO: Next prediction will use new model
```

## Phase 5: Monitoring & Iteration

### 5.1 Track Performance

```csharp
var stats = await _repository.GetStatisticsAsync();

_logger.LogInformation(
    "OCR Session Statistics:\n" +
    "  Total Sessions: {Total}\n" +
    "  Reviewed: {Reviewed}\n" +
    "  Training Ready: {TrainingReady}\n" +
    "  Avg Improvement: {Improvement:F2}%",
    stats.TotalSessions,
    stats.ReviewedSessions,
    stats.TrainingReadySessions,
    stats.AverageImprovementPercent);
```

### 5.2 A/B Testing

Compare model versions:

```csharp
// Flag to test new model vs old
if (_featureFlags.IsEnabled("UseNewPolynomialModel"))
{
    var newModel = new TrainedPolynomialModel(
        _newModelOptions, _logger);
}
```

### 5.3 Retraining Schedule

**Recommended Schedule:**
- **Weekly**: Export and analyze new sessions
- **Monthly**: Retrain if > 500 new reviewed sessions
- **Quarterly**: Full model evaluation and comparison

## Configuration Examples

### Development (appsettings.Development.json)

```json
{
  "PolynomialModelOptions": {
    "ModelVersion": "dev_baseline",
    "TrainedDate": "2025-01-15T00:00:00Z",
    "TrainingDataSize": 820,
    // ... baseline coefficients
  }
}
```

### Production (appsettings.Production.json)

```json
{
  "PolynomialModelOptions": {
    "ModelVersion": "production_20250128_153000",
    "TrainedDate": "2025-01-28T15:30:00Z",
    "TrainingDataSize": 1247,
    // ... retrained coefficients
  }
}
```

## Database Schema

### OcrSessions Table

```sql
CREATE TABLE OcrSessions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessedAt DATETIME2 NOT NULL,

    -- Image Data
    ImageHash VARCHAR(64) NOT NULL,
    ImagePath NVARCHAR(500),
    ImageSizeBytes BIGINT,

    -- Features (4D input)
    BlurScore FLOAT NOT NULL,
    Contrast FLOAT NOT NULL,
    NoiseEstimate FLOAT NOT NULL,
    EdgeDensity FLOAT NOT NULL,
    QualityLevel NVARCHAR(50),

    -- Predicted Parameters (5D output)
    FilterType NVARCHAR(50),
    PredictedContrast FLOAT,
    PredictedBrightness FLOAT,
    PredictedSharpness FLOAT,
    PredictedUnsharpRadius FLOAT,
    PredictedUnsharpPercent FLOAT,
    ModelVersion NVARCHAR(100),

    -- OCR Results
    OcrEngine NVARCHAR(50),
    BaselineOcrText NVARCHAR(MAX),
    EnhancedOcrText NVARCHAR(MAX),
    OcrProcessingTimeMs BIGINT,

    -- Quality Metrics (for training)
    GroundTruth NVARCHAR(MAX),
    BaselineLevenshteinDistance INT,
    EnhancedLevenshteinDistance INT,
    ImprovementPercent FLOAT,

    -- Review Data
    IsReviewed BIT DEFAULT 0,
    QualityRating INT,
    ReviewedBy NVARCHAR(100),
    ReviewedAt DATETIME2,

    -- Optimal Parameters (from search/review)
    OptimalContrast FLOAT,
    OptimalBrightness FLOAT,
    OptimalSharpness FLOAT,
    OptimalUnsharpRadius FLOAT,
    OptimalUnsharpPercent FLOAT,

    -- Metadata
    IncludeInTraining BIT DEFAULT 1,
    MetadataJson NVARCHAR(MAX),

    INDEX IX_ProcessedAt (ProcessedAt),
    INDEX IX_IsReviewed (IsReviewed),
    INDEX IX_QualityRating (QualityRating),
    INDEX IX_ModelVersion (ModelVersion)
);
```

## Next Steps

### Phase 6: Neural Network (Future)

When dataset reaches 10,000+ sessions:

1. Train small neural network (see `train_neural_network.py`)
2. Export to ONNX format
3. Use Microsoft.ML.OnnxRuntime for inference
4. Compare performance vs polynomial model

### Phase 7: Custom Tesseract (Future)

When domain-specific corpus is ready:

1. Collect enhanced images + ground truth
2. Generate Tesseract box files
3. Train custom language model
4. Deploy as `spa_banamex` language

## Troubleshooting

### Model Not Reloading

1. Check file watcher is enabled:
   ```json
   {
     "PolynomialModelOptions": {
       "ReloadOnChange": true
     }
   }
   ```

2. Verify appsettings location:
   ```bash
   ls /app/appsettings*.json
   ```

3. Check application logs for reload events

### Poor Model Performance

1. Check training data quality:
   - Minimum 500+ reviewed sessions
   - Quality rating >= 3
   - Diverse document types

2. Validate R² scores:
   - All parameters R² > 0.85
   - If lower, collect more data or tune regularization

3. Compare with baseline:
   - Test on validation set
   - Measure Levenshtein distance improvement

## Resources

- **Scripts**: `scripts/retrain_polynomial_model.py`
- **Configuration**: `appsettings.PolynomialModel.Example.json`
- **Domain Models**: `Domain/Models/OcrSession.cs`, `PolynomialModelOptions.cs`
- **Repository**: `Domain/Interfaces/IOcrSessionRepository.cs`
- **Model**: `Infrastructure.Imaging/Strategies/TrainedPolynomialModel.cs`

## Contact

For questions about the MLOps pipeline, contact the ML/OCR team.
