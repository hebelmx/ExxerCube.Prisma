"""
Production Quality Predictor

Auto-generated from Random Forest model (R² = 0.9569)

Usage:
    predictor = QualityPredictor()
    recommendation = predictor.predict(blur, noise, contrast, brightness, entropy)
"""

import numpy as np

class QualityPredictor:
    def __init__(self):
        # Scaler parameters
        self.scaler_mean = np.array([1904.2562172697646, 3.285677411865784, 31.644653767543083, 230.63537173499702, 4.310192376375198])
        self.scaler_scale = np.array([1830.7360700699362, 2.9534615971944884, 8.835931827536946, 8.92396584132394, 1.0499677944474157])

        # Model parameters

    def standardize(self, features):
        """Standardize features using fitted scaler."""
        return (features - self.scaler_mean) / self.scaler_scale

    def predict_edits(self, blur, noise, contrast, brightness=230, entropy=5.0):
        """
        Predict baseline OCR edit distance from quality metrics.

        Args:
            blur: Laplacian variance (higher = sharper)
            noise: Noise level (std in uniform regions)
            contrast: RMS contrast
            brightness: Mean intensity (default 230)
            entropy: Histogram entropy (default 5.0)

        Returns:
            Predicted edit distance
        """
        features = np.array([blur, noise, contrast, brightness, entropy])
        features_scaled = self.standardize(features)

    def recommend_filter(self, blur, noise, contrast, brightness=230, entropy=5.0):
        """
        Recommend filter based on predicted OCR performance.

        Returns:
            ("NO_FILTER" | "OPENCV" | "PIL", predicted_edits)
        """
        predicted_edits = self.predict_edits(blur, noise, contrast, brightness, entropy)

        if predicted_edits < 200:
            return "NO_FILTER", predicted_edits
        elif predicted_edits < 1000:
            return "OPENCV", predicted_edits
        else:
            return "PIL", predicted_edits

# Example usage
if __name__ == "__main__":
    predictor = QualityPredictor()

    # Test cases
    print("Test Cases:")
    print("-" * 60)

    # Pristine
    rec, edits = predictor.recommend_filter(blur=3500, noise=0.2, contrast=38.5)
    print(f"Pristine (blur=3500, noise=0.2, contrast=38.5):")
    print(f"  → {rec} (predicted: {edits:.0f} edits)")

    # Good
    rec, edits = predictor.recommend_filter(blur=1500, noise=1.2, contrast=34.0)
    print(f"Good (blur=1500, noise=1.2, contrast=34.0):")
    print(f"  → {rec} (predicted: {edits:.0f} edits)")

    # Degraded
    rec, edits = predictor.recommend_filter(blur=1200, noise=7.0, contrast=24.0)
    print(f"Degraded (blur=1200, noise=7.0, contrast=24.0):")
    print(f"  → {rec} (predicted: {edits:.0f} edits)")
