"""
Polynomial Model Retraining Script
===================================

Retrains polynomial regression models using production OCR session data.
Exports new coefficients to appsettings.json for hot-reload via IOptionsMonitor.

Usage:
    python retrain_polynomial_model.py --input sessions.json --output appsettings.PolynomialCoefficients.json

Requirements:
    pip install pandas scikit-learn numpy

Workflow:
    1. Load production OCR sessions from database/JSON export
    2. Filter for high-quality reviewed sessions (quality_rating >= 3)
    3. Extract features and optimal parameters
    4. Train separate polynomial models for each parameter
    5. Validate using cross-validation
    6. Export coefficients to JSON for C# hot-reload
    7. Log model performance metrics

Model Performance Targets:
    - R² > 0.85 for all parameters
    - Cross-validation RMSE improvement over baseline
    - Validation set improvement > 15% OCR quality
"""

import argparse
import json
import sys
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np
import pandas as pd
from sklearn.linear_model import Ridge
from sklearn.model_selection import cross_val_score, train_test_split
from sklearn.preprocessing import PolynomialFeatures, StandardScaler


def load_sessions(input_path: str, min_quality_rating: int = 3) -> pd.DataFrame:
    """
    Load OCR sessions from JSON export.

    Args:
        input_path: Path to sessions JSON file
        min_quality_rating: Minimum quality rating to include

    Returns:
        DataFrame with training data
    """
    print(f"📂 Loading sessions from {input_path}")

    with open(input_path, 'r', encoding='utf-8') as f:
        sessions = json.load(f)

    df = pd.DataFrame(sessions)

    print(f"   Total sessions loaded: {len(df)}")

    # Filter for reviewed sessions with ground truth
    df = df[df['isReviewed'] == True].copy()
    df = df[df['groundTruth'].notna()].copy()
    df = df[df['qualityRating'] >= min_quality_rating].copy()

    print(f"   Reviewed sessions (quality >= {min_quality_rating}): {len(df)}")

    # Filter for sessions with optimal parameters
    required_cols = ['optimalContrast', 'optimalBrightness', 'optimalSharpness',
                     'optimalUnsharpRadius', 'optimalUnsharpPercent']
    df = df[df[required_cols].notna().all(axis=1)].copy()

    print(f"   Sessions with optimal parameters: {len(df)}")

    if len(df) < 100:
        print("⚠️  WARNING: Less than 100 training samples. Results may not be reliable.")

    return df


def prepare_features_and_targets(df: pd.DataFrame) -> Tuple[np.ndarray, Dict[str, np.ndarray]]:
    """
    Extract features and target parameters from session data.

    Args:
        df: DataFrame with session data

    Returns:
        Tuple of (features, targets_dict)
    """
    print("\n🔧 Preparing features and targets")

    # Extract features (4D vector)
    X = df[['blurScore', 'contrast', 'noiseEstimate', 'edgeDensity']].values

    print(f"   Feature shape: {X.shape}")
    print(f"   Feature ranges:")
    print(f"     BlurScore:     [{X[:, 0].min():.2f}, {X[:, 0].max():.2f}]")
    print(f"     Contrast:      [{X[:, 1].min():.2f}, {X[:, 1].max():.2f}]")
    print(f"     NoiseEstimate: [{X[:, 2].min():.2f}, {X[:, 2].max():.2f}]")
    print(f"     EdgeDensity:   [{X[:, 3].min():.4f}, {X[:, 3].max():.4f}]")

    # Extract target parameters (5 outputs)
    targets = {
        'contrast': df['optimalContrast'].values,
        'brightness': df['optimalBrightness'].values,
        'sharpness': df['optimalSharpness'].values,
        'unsharp_radius': df['optimalUnsharpRadius'].values,
        'unsharp_percent': df['optimalUnsharpPercent'].values
    }

    print(f"\n   Target ranges:")
    for name, values in targets.items():
        print(f"     {name:15s}: [{values.min():.2f}, {values.max():.2f}]")

    return X, targets


def train_polynomial_model(
    X: np.ndarray,
    y: np.ndarray,
    param_name: str,
    degree: int = 2,
    alpha: float = 1.0
) -> Dict:
    """
    Train polynomial regression model for a single parameter.

    Args:
        X: Feature matrix (N x 4)
        y: Target values (N,)
        param_name: Parameter name for logging
        degree: Polynomial degree
        alpha: Ridge regularization parameter

    Returns:
        Dict with coefficients and performance metrics
    """
    print(f"\n🎯 Training model for {param_name}")

    # Split data
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42
    )

    # Normalize features using StandardScaler
    scaler = StandardScaler()
    X_train_scaled = scaler.fit_transform(X_train)
    X_test_scaled = scaler.transform(X_test)

    # Generate polynomial features
    poly = PolynomialFeatures(degree=degree, include_bias=False)
    X_train_poly = poly.fit_transform(X_train_scaled)
    X_test_poly = poly.transform(X_test_scaled)

    # Train Ridge regression
    model = Ridge(alpha=alpha)
    model.fit(X_train_poly, y_train)

    # Cross-validation on training set
    cv_scores = cross_val_score(
        model, X_train_poly, y_train,
        cv=5, scoring='neg_mean_squared_error'
    )
    cv_rmse = np.sqrt(-cv_scores.mean())

    # Test set evaluation
    y_pred_test = model.predict(X_test_poly)
    test_rmse = np.sqrt(np.mean((y_test - y_pred_test) ** 2))
    test_mae = np.mean(np.abs(y_test - y_pred_test))

    # R² score on test set
    r2_score = model.score(X_test_poly, y_test)

    print(f"   Training samples: {len(X_train)}")
    print(f"   Test samples:     {len(X_test)}")
    print(f"   CV RMSE:          {cv_rmse:.4f}")
    print(f"   Test RMSE:        {test_rmse:.4f}")
    print(f"   Test MAE:         {test_mae:.4f}")
    print(f"   R² Score:         {r2_score:.4f}")

    if r2_score < 0.85:
        print(f"   ⚠️  WARNING: R² < 0.85 for {param_name}")

    return {
        'param_name': param_name,
        'intercept': float(model.intercept_),
        'coefficients': model.coef_.tolist(),
        'scaler_mean': scaler.mean_.tolist(),
        'scaler_scale': scaler.scale_.tolist(),
        'r2_score': float(r2_score),
        'cv_rmse': float(cv_rmse),
        'test_rmse': float(test_rmse),
        'test_mae': float(test_mae),
        'min_value': float(y.min()),
        'max_value': float(y.max()),
        'n_features': X_train_poly.shape[1]
    }


def train_all_models(X: np.ndarray, targets: Dict[str, np.ndarray]) -> Dict:
    """
    Train models for all parameters.

    Returns:
        Dict with all model coefficients and metadata
    """
    models = {}

    for param_name, y in targets.items():
        result = train_polynomial_model(X, y, param_name)
        models[param_name] = result

    return models


def export_to_appsettings(models: Dict, output_path: str, df: pd.DataFrame):
    """
    Export trained models to appsettings.json format for C# hot-reload.

    Args:
        models: Dict of trained model coefficients
        output_path: Path to output JSON file
        df: Original DataFrame for metadata
    """
    print(f"\n💾 Exporting to {output_path}")

    # Use first model's scaler (same for all)
    first_model = next(iter(models.values()))

    # Calculate performance summary
    avg_r2 = np.mean([m['r2_score'] for m in models.values()])
    avg_mae = np.mean([m['test_mae'] for m in models.values()])

    # Determine parameter bounds (from data or defaults)
    param_bounds = {
        'contrast': (0.5, 2.0),
        'brightness': (0.8, 1.3),
        'sharpness': (0.5, 3.0),
        'unsharp_radius': (0.0, 5.0),
        'unsharp_percent': (0.0, 250.0)
    }

    # Build appsettings structure
    output = {
        "PolynomialModelOptions": {
            "ModelVersion": f"production_{datetime.now().strftime('%Y%m%d_%H%M%S')}",
            "TrainedDate": datetime.now().isoformat(),
            "TrainingDataSize": len(df),
            "PolynomialDegree": 2,
            "ScalerMean": first_model['scaler_mean'],
            "ScalerScale": first_model['scaler_scale'],

            # Model performance summary
            "AverageR2Score": avg_r2,
            "AverageMeanAbsoluteError": avg_mae,

            # Individual parameter models
            "ContrastModel": {
                "ParameterName": "Contrast",
                "Intercept": models['contrast']['intercept'],
                "Coefficients": models['contrast']['coefficients'],
                "MinValue": param_bounds['contrast'][0],
                "MaxValue": param_bounds['contrast'][1],
                "R2Score": models['contrast']['r2_score'],
                "MeanAbsoluteError": models['contrast']['test_mae']
            },

            "BrightnessModel": {
                "ParameterName": "Brightness",
                "Intercept": models['brightness']['intercept'],
                "Coefficients": models['brightness']['coefficients'],
                "MinValue": param_bounds['brightness'][0],
                "MaxValue": param_bounds['brightness'][1],
                "R2Score": models['brightness']['r2_score'],
                "MeanAbsoluteError": models['brightness']['test_mae']
            },

            "SharpnessModel": {
                "ParameterName": "Sharpness",
                "Intercept": models['sharpness']['intercept'],
                "Coefficients": models['sharpness']['coefficients'],
                "MinValue": param_bounds['sharpness'][0],
                "MaxValue": param_bounds['sharpness'][1],
                "R2Score": models['sharpness']['r2_score'],
                "MeanAbsoluteError": models['sharpness']['test_mae']
            },

            "UnsharpRadiusModel": {
                "ParameterName": "UnsharpRadius",
                "Intercept": models['unsharp_radius']['intercept'],
                "Coefficients": models['unsharp_radius']['coefficients'],
                "MinValue": param_bounds['unsharp_radius'][0],
                "MaxValue": param_bounds['unsharp_radius'][1],
                "R2Score": models['unsharp_radius']['r2_score'],
                "MeanAbsoluteError": models['unsharp_radius']['test_mae']
            },

            "UnsharpPercentModel": {
                "ParameterName": "UnsharpPercent",
                "Intercept": models['unsharp_percent']['intercept'],
                "Coefficients": models['unsharp_percent']['coefficients'],
                "MinValue": param_bounds['unsharp_percent'][0],
                "MaxValue": param_bounds['unsharp_percent'][1],
                "R2Score": models['unsharp_percent']['r2_score'],
                "MeanAbsoluteError": models['unsharp_percent']['test_mae']
            }
        }
    }

    # Write JSON
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(output, f, indent=2, ensure_ascii=False)

    print(f"✅ Exported successfully")
    print(f"\n📊 Summary:")
    print(f"   Training Data Size:  {len(df)}")
    print(f"   Average R² Score:    {avg_r2:.4f}")
    print(f"   Average MAE:         {avg_mae:.4f}")
    print(f"\n🔄 To apply in production:")
    print(f"   1. Copy {output_path} to production server")
    print(f"   2. Merge with appsettings.json or use as override file")
    print(f"   3. IOptionsMonitor will auto-reload within seconds")
    print(f"   4. Check logs for model reload confirmation")


def main():
    parser = argparse.ArgumentParser(
        description='Retrain polynomial filter models from production data'
    )
    parser.add_argument(
        '--input', '-i',
        required=True,
        help='Path to sessions JSON file'
    )
    parser.add_argument(
        '--output', '-o',
        default='appsettings.PolynomialCoefficients.json',
        help='Path to output appsettings JSON file'
    )
    parser.add_argument(
        '--min-quality', '-q',
        type=int,
        default=3,
        help='Minimum quality rating to include (1-5)'
    )

    args = parser.parse_args()

    print("╔════════════════════════════════════════════════════════════════╗")
    print("║      POLYNOMIAL MODEL RETRAINING - PRODUCTION PIPELINE         ║")
    print("╚════════════════════════════════════════════════════════════════╝")

    try:
        # Load data
        df = load_sessions(args.input, args.min_quality)

        if len(df) < 50:
            print("\n❌ ERROR: Need at least 50 training samples")
            return 1

        # Prepare features and targets
        X, targets = prepare_features_and_targets(df)

        # Train all models
        models = train_all_models(X, targets)

        # Export to appsettings format
        export_to_appsettings(models, args.output, df)

        print("\n✅ Retraining completed successfully!")
        return 0

    except Exception as e:
        print(f"\n❌ ERROR: {e}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == '__main__':
    sys.exit(main())
