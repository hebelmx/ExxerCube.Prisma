"""
Advanced Quality Model: Clustering + Regression

1. Cluster documents based on filter performance patterns
2. Correlate quality metrics (blur, noise, contrast) with clusters
3. Fit regression model: quality metrics → predicted OCR performance
4. Generate production quality predictor function

Output:
- Natural clusters from performance data
- Multi-parameter regression model
- Production predictor: f(blur, noise, contrast) → filter recommendation
"""

import json
import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.cluster import KMeans
from sklearn.preprocessing import StandardScaler
from sklearn.linear_model import LinearRegression, Ridge
from sklearn.ensemble import RandomForestRegressor
from sklearn.metrics import r2_score, mean_absolute_error
import matplotlib
matplotlib.use('Agg')  # Non-interactive backend
import matplotlib.pyplot as plt

FIXTURES_DIR = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures")

print("=" * 80)
print("ADVANCED QUALITY MODEL: CLUSTERING + REGRESSION")
print("=" * 80)
print()

# ============================================================================
# Step 1: Load Performance Matrix and Cluster Documents
# ============================================================================

print("STEP 1: Clustering Analysis")
print("-" * 80)
print()

# Load baseline performance data
with open(FIXTURES_DIR / "comprehensive_with_baseline_matrix.json", 'r') as f:
    all_results = json.load(f)

# Extract baseline performance per document per degradation level
baseline_result = [r for r in all_results if r['filter_id'] == 'BASELINE'][0]

# Build document-level feature vectors (performance across all degradation levels)
documents = ['222AAA', '333BBB', '333ccc', '555CCC']
degradation_levels = ['Q0_Pristine', 'Q05_VeryGood', 'Q1_Poor', 'Q15_Medium', 'Q2_MediumPoor']

# Create performance matrix: rows = documents, cols = degradation levels
doc_performance = []
doc_labels = []

for doc in documents:
    performance_vector = []
    for level in degradation_levels:
        if level in baseline_result['performance']:
            edits = baseline_result['performance'][level].get(doc, 0)
            performance_vector.append(edits)
        else:
            performance_vector.append(0)

    doc_performance.append(performance_vector)
    doc_labels.append(doc)

doc_performance = np.array(doc_performance)

print("Document Performance Matrix (Baseline OCR edits):")
print(f"  Shape: {doc_performance.shape} (documents × degradation levels)")
print()
for i, doc in enumerate(doc_labels):
    print(f"  {doc}: {doc_performance[i]}")
print()

# Try different cluster counts
print("Testing cluster counts...")
inertias = []
silhouette_scores = []

for n_clusters in range(2, 5):
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    labels = kmeans.fit_predict(doc_performance)
    inertias.append(kmeans.inertia_)

    # Calculate silhouette score manually (simple version)
    # For now just use inertia
    print(f"  {n_clusters} clusters: inertia = {kmeans.inertia_:.1f}")

print()

# Use 2 clusters (based on our hypothesis: high-quality vs degraded)
n_clusters = 2
kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
cluster_labels = kmeans.fit_predict(doc_performance)

print(f"Clustering Result (k={n_clusters}):")
for i, doc in enumerate(doc_labels):
    print(f"  {doc}: Cluster {cluster_labels[i]}")
print()

# ============================================================================
# Step 2: Load Quality Metrics and Build Training Dataset
# ============================================================================

print("STEP 2: Building Training Dataset")
print("-" * 80)
print()

# Load quality metrics
quality_files = {
    'Q0_Pristine': 'quality_metrics_Q0.json',
    'Q05_VeryGood': 'quality_metrics_Q05.json',
    'Q1_Poor': 'quality_metrics_Q1.json',
    'Q15_Medium': 'quality_metrics_Q15.json',
    'Q2_MediumPoor': 'quality_metrics_Q2.json'
}

# Build training dataset: (blur, noise, contrast) → baseline_edits
training_data = []

for level_name, metrics_file in quality_files.items():
    with open(FIXTURES_DIR / metrics_file, 'r') as f:
        metrics_data = json.load(f)

    # Get baseline performance for this level
    if level_name not in baseline_result['performance']:
        continue

    for metric_entry in metrics_data:
        doc_name = Path(metric_entry['image_path']).name.split('-')[0]

        if doc_name in baseline_result['performance'][level_name]:
            blur = metric_entry['metrics']['blur']
            noise = metric_entry['metrics']['noise']
            contrast = metric_entry['metrics']['contrast_rms']
            brightness = metric_entry['metrics']['brightness_mean']
            entropy = metric_entry['metrics']['histogram']['entropy']

            baseline_edits = baseline_result['performance'][level_name][doc_name]

            # Find cluster for this document
            doc_idx = doc_labels.index(doc_name)
            cluster = cluster_labels[doc_idx]

            training_data.append({
                'doc': doc_name,
                'level': level_name,
                'blur': blur,
                'noise': noise,
                'contrast': contrast,
                'brightness': brightness,
                'entropy': entropy,
                'baseline_edits': baseline_edits,
                'cluster': cluster
            })

df = pd.DataFrame(training_data)

print(f"Training Dataset: {len(df)} samples")
print(f"  Documents: {df['doc'].nunique()}")
print(f"  Degradation levels: {df['level'].nunique()}")
print()

print("Sample data:")
print(df.head(10).to_string())
print()

# ============================================================================
# Step 3: Fit Regression Models
# ============================================================================

print("STEP 3: Fitting Regression Models")
print("-" * 80)
print()

# Features: blur, noise, contrast, brightness, entropy
X = df[['blur', 'noise', 'contrast', 'brightness', 'entropy']].values
y = df['baseline_edits'].values

# Standardize features
scaler = StandardScaler()
X_scaled = scaler.fit_transform(X)

print("Feature statistics:")
print(f"  Blur: mean={df['blur'].mean():.1f}, std={df['blur'].std():.1f}")
print(f"  Noise: mean={df['noise'].mean():.2f}, std={df['noise'].std():.2f}")
print(f"  Contrast: mean={df['contrast'].mean():.1f}, std={df['contrast'].std():.1f}")
print(f"  Brightness: mean={df['brightness'].mean():.1f}, std={df['brightness'].std():.1f}")
print(f"  Entropy: mean={df['entropy'].mean():.2f}, std={df['entropy'].std():.2f}")
print()

# Try multiple regression models
models = {
    'Linear Regression': LinearRegression(),
    'Ridge Regression': Ridge(alpha=1.0),
    'Random Forest': RandomForestRegressor(n_estimators=100, random_state=42, max_depth=5)
}

best_model = None
best_score = -np.inf
best_name = None

for name, model in models.items():
    model.fit(X_scaled, y)
    y_pred = model.predict(X_scaled)

    r2 = r2_score(y, y_pred)
    mae = mean_absolute_error(y, y_pred)

    print(f"{name}:")
    print(f"  R² score: {r2:.4f}")
    print(f"  MAE: {mae:.1f} edits")

    if r2 > best_score:
        best_score = r2
        best_model = model
        best_name = name

    print()

print(f"✓ Best model: {best_name} (R² = {best_score:.4f})")
print()

# ============================================================================
# Step 4: Analyze Feature Importance
# ============================================================================

print("STEP 4: Feature Importance Analysis")
print("-" * 80)
print()

if hasattr(best_model, 'coef_'):
    # Linear model coefficients
    feature_names = ['blur', 'noise', 'contrast', 'brightness', 'entropy']
    coefficients = best_model.coef_

    print("Linear coefficients (standardized):")
    for name, coef in zip(feature_names, coefficients):
        print(f"  {name:12s}: {coef:+8.2f}")
    print()

elif hasattr(best_model, 'feature_importances_'):
    # Tree-based feature importance
    feature_names = ['blur', 'noise', 'contrast', 'brightness', 'entropy']
    importances = best_model.feature_importances_

    print("Feature importances:")
    for name, imp in sorted(zip(feature_names, importances), key=lambda x: -x[1]):
        print(f"  {name:12s}: {imp:.4f}")
    print()

# ============================================================================
# Step 5: Generate Production Predictor Function
# ============================================================================

print("STEP 5: Production Predictor Function")
print("-" * 80)
print()

# Determine decision boundaries from model predictions
test_points = []
for blur_val in [500, 1000, 2000, 3500]:
    for noise_val in [0.2, 1.0, 4.0, 7.0]:
        for contrast_val in [25, 30, 35, 40]:
            brightness_val = 230  # typical
            entropy_val = 5.0  # typical

            X_test = scaler.transform([[blur_val, noise_val, contrast_val, brightness_val, entropy_val]])
            predicted_edits = best_model.predict(X_test)[0]

            # Classify based on predicted performance
            if predicted_edits < 200:
                recommendation = "NO_FILTER"
            elif predicted_edits < 1000:
                recommendation = "OPENCV"
            else:
                recommendation = "PIL"

            test_points.append({
                'blur': blur_val,
                'noise': noise_val,
                'contrast': contrast_val,
                'predicted_edits': predicted_edits,
                'recommendation': recommendation
            })

test_df = pd.DataFrame(test_points)

print("Decision boundary samples:")
print(test_df.groupby('recommendation').agg({
    'blur': ['min', 'max', 'mean'],
    'noise': ['min', 'max', 'mean'],
    'contrast': ['min', 'max', 'mean'],
    'predicted_edits': ['min', 'max', 'mean']
}).round(1))
print()

# ============================================================================
# Step 6: Save Production Model
# ============================================================================

print("STEP 6: Saving Production Model")
print("-" * 80)
print()

# Save model parameters
production_model = {
    'model_type': best_name,
    'r2_score': float(best_score),
    'scaler_mean': scaler.mean_.tolist(),
    'scaler_scale': scaler.scale_.tolist(),
    'feature_names': ['blur', 'noise', 'contrast', 'brightness', 'entropy'],
    'decision_thresholds': {
        'NO_FILTER': 'predicted_edits < 200',
        'OPENCV': '200 <= predicted_edits < 1000',
        'PIL': 'predicted_edits >= 1000'
    },
    'clusters': {
        f"cluster_{i}": {
            'documents': [doc for j, doc in enumerate(doc_labels) if cluster_labels[j] == i],
            'characteristics': doc_performance[cluster_labels == i].mean(axis=0).tolist()
        }
        for i in range(n_clusters)
    }
}

# Add model-specific parameters
if hasattr(best_model, 'coef_'):
    production_model['coefficients'] = best_model.coef_.tolist()
    production_model['intercept'] = float(best_model.intercept_)
elif hasattr(best_model, 'feature_importances_'):
    production_model['feature_importances'] = best_model.feature_importances_.tolist()

output_file = FIXTURES_DIR / "production_quality_model.json"
with open(output_file, 'w') as f:
    json.dump(production_model, f, indent=2)

print(f"✓ Saved production model to: {output_file}")
print()

# ============================================================================
# Step 7: Generate Predictor Code
# ============================================================================

print("STEP 7: Production Predictor Code")
print("-" * 80)
print()

predictor_code = f'''"""
Production Quality Predictor

Auto-generated from {best_name} model (R² = {best_score:.4f})

Usage:
    predictor = QualityPredictor()
    recommendation = predictor.predict(blur, noise, contrast, brightness, entropy)
"""

import numpy as np

class QualityPredictor:
    def __init__(self):
        # Scaler parameters
        self.scaler_mean = np.array({scaler.mean_.tolist()})
        self.scaler_scale = np.array({scaler.scale_.tolist()})

        # Model parameters
'''

if hasattr(best_model, 'coef_'):
    predictor_code += f'''        self.coefficients = np.array({best_model.coef_.tolist()})
        self.intercept = {best_model.intercept_}
'''

predictor_code += f'''
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
'''

if hasattr(best_model, 'coef_'):
    predictor_code += f'''
        # Linear prediction
        prediction = np.dot(features_scaled, self.coefficients) + self.intercept
        return max(0, prediction)  # Edits can't be negative
'''

predictor_code += f'''
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
    print(f"  → {{rec}} (predicted: {{edits:.0f}} edits)")

    # Good
    rec, edits = predictor.recommend_filter(blur=1500, noise=1.2, contrast=34.0)
    print(f"Good (blur=1500, noise=1.2, contrast=34.0):")
    print(f"  → {{rec}} (predicted: {{edits:.0f}} edits)")

    # Degraded
    rec, edits = predictor.recommend_filter(blur=1200, noise=7.0, contrast=24.0)
    print(f"Degraded (blur=1200, noise=7.0, contrast=24.0):")
    print(f"  → {{rec}} (predicted: {{edits:.0f}} edits)")
'''

predictor_file = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/Python/quality_predictor.py")
predictor_file.parent.mkdir(parents=True, exist_ok=True)

with open(predictor_file, 'w') as f:
    f.write(predictor_code)

print(f"✓ Generated predictor code: {predictor_file}")
print()

print("=" * 80)
print("✓ COMPLETE!")
print("=" * 80)
print()
print(f"Model Performance: R² = {best_score:.4f}")
print(f"Clusters Found: {n_clusters}")
print(f"Production files:")
print(f"  - {output_file}")
print(f"  - {predictor_file}")
