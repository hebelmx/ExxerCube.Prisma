"""
Quality Threshold Calibration

Correlates image quality metrics with actual OCR baseline performance
to determine production thresholds for filter routing.

Analyzes:
- Baseline OCR performance per quality level
- Quality metrics (blur, noise, contrast) per level
- Correlation between metrics and performance
- Production thresholds for filter selection

Output:
- Calibrated quality thresholds
- Production decision tree
- Correlation analysis
"""

import json
import pandas as pd
from pathlib import Path
import numpy as np

FIXTURES_DIR = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures")

# Load baseline performance
baseline_df = pd.read_csv(FIXTURES_DIR / "comprehensive_with_baseline_matrix.csv")
baseline_only = baseline_df[baseline_df['filter_type'] == 'BASELINE']

# Load quality metrics
quality_levels = ['Q0', 'Q05', 'Q1', 'Q15', 'Q2']
quality_files = {
    'Q0_Pristine': 'quality_metrics_Q0.json',
    'Q05_VeryGood': 'quality_metrics_Q05.json',
    'Q1_Poor': 'quality_metrics_Q1.json',
    'Q15_Medium': 'quality_metrics_Q15.json',
    'Q2_MediumPoor': 'quality_metrics_Q2.json'
}

print("=" * 80)
print("QUALITY THRESHOLD CALIBRATION")
print("=" * 80)
print()

# Build correlation table
correlation_data = []

for level_name, metrics_file in quality_files.items():
    # Get baseline OCR performance
    level_baseline = baseline_only[baseline_only['degradation_level'] == level_name]

    if len(level_baseline) == 0:
        continue

    baseline_row = level_baseline.iloc[0]
    total_edits = baseline_row['total_edits']

    # Load quality metrics
    with open(FIXTURES_DIR / metrics_file, 'r') as f:
        metrics_data = json.load(f)

    # Calculate average metrics across all documents in this level
    avg_blur = np.mean([m['metrics']['blur'] for m in metrics_data])
    avg_noise = np.mean([m['metrics']['noise'] for m in metrics_data])
    avg_contrast = np.mean([m['metrics']['contrast_rms'] for m in metrics_data])
    avg_quality_score = np.mean([m['quality_score'] for m in metrics_data])

    # Per-document details
    doc_details = {}
    for m in metrics_data:
        doc_name = Path(m['image_path']).name.split('-')[0]
        doc_details[doc_name] = {
            'blur': m['metrics']['blur'],
            'noise': m['metrics']['noise'],
            'contrast': m['metrics']['contrast_rms'],
            'quality_score': m['quality_score']
        }

    correlation_data.append({
        'level': level_name,
        'baseline_edits': int(total_edits),
        'avg_blur': round(avg_blur, 1),
        'avg_noise': round(avg_noise, 1),
        'avg_contrast': round(avg_contrast, 1),
        'avg_quality_score': round(avg_quality_score, 1),
        'doc_details': doc_details
    })

# Create DataFrame
df = pd.DataFrame(correlation_data)

print("BASELINE OCR PERFORMANCE vs QUALITY METRICS:")
print("-" * 80)
print(df[['level', 'baseline_edits', 'avg_blur', 'avg_noise', 'avg_contrast', 'avg_quality_score']].to_string(index=False))
print()

# Analyze correlations
print("=" * 80)
print("CORRELATION ANALYSIS:")
print("=" * 80)
print()

print("Baseline Edits vs Blur:")
corr_blur = df[['baseline_edits', 'avg_blur']].corr().iloc[0, 1]
print(f"  Correlation: {corr_blur:.3f}")
print(f"  Higher blur = {'BETTER' if corr_blur < 0 else 'WORSE'} OCR")
print()

print("Baseline Edits vs Noise:")
corr_noise = df[['baseline_edits', 'avg_noise']].corr().iloc[0, 1]
print(f"  Correlation: {corr_noise:.3f}")
print(f"  Higher noise = {'BETTER' if corr_noise < 0 else 'WORSE'} OCR")
print()

print("Baseline Edits vs Contrast:")
corr_contrast = df[['baseline_edits', 'avg_contrast']].corr().iloc[0, 1]
print(f"  Correlation: {corr_contrast:.3f}")
print(f"  Higher contrast = {'BETTER' if corr_contrast < 0 else 'WORSE'} OCR")
print()

# Define production thresholds based on actual OCR performance
print("=" * 80)
print("PRODUCTION THRESHOLDS (Calibrated to Baseline OCR Performance):")
print("=" * 80)
print()

# Find threshold values
q0_data = df[df['level'] == 'Q0_Pristine'].iloc[0]
q05_data = df[df['level'] == 'Q05_VeryGood'].iloc[0]
q1_data = df[df['level'] == 'Q1_Poor'].iloc[0]
q15_data = df[df['level'] == 'Q15_Medium'].iloc[0]
q2_data = df[df['level'] == 'Q2_MediumPoor'].iloc[0]

print("Quality Level Definitions:")
print("-" * 80)
print()

print(f"Q0 PRISTINE (NO FILTER):")
print(f"  Baseline Performance: {q0_data['baseline_edits']} edits (EXCELLENT)")
print(f"  Blur: {q0_data['avg_blur']:.0f}")
print(f"  Noise: {q0_data['avg_noise']:.1f}")
print(f"  Contrast: {q0_data['avg_contrast']:.1f}")
print(f"  → Use NO FILTER (filtering degrades quality!)")
print()

print(f"Q05-Q1 GOOD (OpenCV Filter):")
print(f"  Q05 Baseline: {q05_data['baseline_edits']} edits")
print(f"  Q1 Baseline: {q1_data['baseline_edits']} edits")
print(f"  Blur range: {q1_data['avg_blur']:.0f} - {q05_data['avg_blur']:.0f}")
print(f"  Noise range: {q05_data['avg_noise']:.1f} - {q1_data['avg_noise']:.1f}")
print(f"  Contrast range: {q1_data['avg_contrast']:.1f} - {q05_data['avg_contrast']:.1f}")
print(f"  → Use OpenCV filter (20-35% improvement)")
print()

print(f"Q15-Q2 DEGRADED (PIL Filter):")
print(f"  Q15 Baseline: {q15_data['baseline_edits']} edits")
print(f"  Q2 Baseline: {q2_data['baseline_edits']} edits")
print(f"  Blur range: {q15_data['avg_blur']:.0f} - {q2_data['avg_blur']:.0f}")
print(f"  Noise range: {q15_data['avg_noise']:.1f} - {q2_data['avg_noise']:.1f}")
print(f"  Contrast range: {q2_data['avg_contrast']:.1f} - {q15_data['avg_contrast']:.1f}")
print(f"  → Use PIL filter (75-80% improvement!)")
print()

# Production decision thresholds
print("=" * 80)
print("PRODUCTION DECISION TREE:")
print("=" * 80)
print()

# Use baseline_edits as the PRIMARY indicator
# Noise is the best secondary indicator (clear progression)

print("When a new document arrives, measure:")
print("  1. Run baseline OCR on small sample region")
print("  2. Measure noise level")
print("  3. Measure blur")
print()
print("Decision Logic:")
print("-" * 80)
print()

# Calculate decision thresholds (midpoints between levels)
noise_threshold_1 = (q0_data['avg_noise'] + q05_data['avg_noise']) / 2
noise_threshold_2 = (q1_data['avg_noise'] + q15_data['avg_noise']) / 2

print(f"IF noise < {noise_threshold_1:.1f}:")
print(f"  → Quality Level: PRISTINE (Q0)")
print(f"  → Filter Strategy: NO FILTER")
print(f"  → Expected: < 100 edits on baseline OCR")
print()

print(f"ELSE IF noise < {noise_threshold_2:.1f}:")
print(f"  → Quality Level: GOOD (Q05-Q1)")
print(f"  → Filter Strategy: OpenCV (best: h=6, CLAHE=1.10)")
print(f"  → Expected: 400-600 edits → 300-400 with filter")
print()

print(f"ELSE:")
print(f"  → Quality Level: DEGRADED (Q15-Q2)")
print(f"  → Filter Strategy: PIL (best: contrast=1.16, median=3)")
print(f"  → Expected: 4000-6000 edits → 900-1500 with filter")
print()

# Alternative: Use contrast as primary
contrast_threshold_1 = (q0_data['avg_contrast'] + q05_data['avg_contrast']) / 2
contrast_threshold_2 = (q1_data['avg_contrast'] + q15_data['avg_contrast']) / 2

print("=" * 80)
print("ALTERNATIVE: Contrast-Based Decision Tree:")
print("=" * 80)
print()

print(f"IF contrast > {contrast_threshold_1:.1f}:")
print(f"  → PRISTINE → NO FILTER")
print()

print(f"ELSE IF contrast > {contrast_threshold_2:.1f}:")
print(f"  → GOOD → OpenCV filter")
print()

print(f"ELSE:")
print(f"  → DEGRADED → PIL filter")
print()

# Save calibrated thresholds
thresholds = {
    'noise_based': {
        'pristine_threshold': float(noise_threshold_1),
        'degraded_threshold': float(noise_threshold_2)
    },
    'contrast_based': {
        'pristine_threshold': float(contrast_threshold_1),
        'degraded_threshold': float(contrast_threshold_2)
    },
    'filters': {
        'pristine': 'NO_FILTER',
        'good': 'OpenCV (h=6, CLAHE=1.10)',
        'degraded': 'PIL (contrast=1.16, median=3)'
    },
    'performance_targets': {
        'pristine': '< 100 edits',
        'good': '300-400 edits (vs 400-600 baseline)',
        'degraded': '900-1500 edits (vs 4000-6000 baseline)'
    }
}

output_file = FIXTURES_DIR / "production_quality_thresholds.json"
with open(output_file, 'w') as f:
    json.dump(thresholds, f, indent=2)

print("=" * 80)
print(f"✓ Saved production thresholds to: {output_file}")
print("=" * 80)

# Save full correlation data
correlation_output = FIXTURES_DIR / "quality_metric_correlation.json"
with open(correlation_output, 'w') as f:
    json.dump(correlation_data, f, indent=2)

print(f"✓ Saved correlation data to: {correlation_output}")
print()
