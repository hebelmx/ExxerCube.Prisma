"""
Complete Three-Way Clustering and Correlation Matrix

1. Image Property Clustering: Group images by (blur, noise, contrast, brightness, entropy)
2. Filter Parameter Clustering: Group filters by their parameters
3. Correlation Matrix: image_cluster × quality_level → best_filter_cluster

Output:
- Production lookup table: measure_properties() → filter_parameters
- Complete correlation matrix for all combinations
- Optimal filter per (property_cluster, quality_level)
"""

import json
import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.cluster import KMeans
from sklearn.preprocessing import StandardScaler

FIXTURES_DIR = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures")

print("=" * 80)
print("THREE-WAY CLUSTERING AND CORRELATION MATRIX")
print("=" * 80)
print()

# ============================================================================
# STEP 1: Load All Data
# ============================================================================

print("STEP 1: Loading Data")
print("-" * 80)

# Load performance matrix (with baseline)
with open(FIXTURES_DIR / "comprehensive_with_baseline_matrix.json", 'r') as f:
    all_results = json.load(f)

# Load quality metrics for all degradation levels
quality_files = {
    'Q0_Pristine': 'quality_metrics_Q0.json',
    'Q05_VeryGood': 'quality_metrics_Q05.json',
    'Q1_Poor': 'quality_metrics_Q1.json',
    'Q15_Medium': 'quality_metrics_Q15.json',
    'Q2_MediumPoor': 'quality_metrics_Q2.json'
}

all_quality_metrics = {}
for level, filename in quality_files.items():
    with open(FIXTURES_DIR / filename, 'r') as f:
        all_quality_metrics[level] = json.load(f)

print(f"✓ Loaded {len(all_results)} filter results")
print(f"✓ Loaded quality metrics for {len(all_quality_metrics)} degradation levels")
print()

# ============================================================================
# STEP 2: Cluster Images by Properties
# ============================================================================

print("STEP 2: Clustering Images by Properties")
print("-" * 80)

# Build feature matrix: each image at each degradation level
image_features = []
image_labels = []

for level, metrics_list in all_quality_metrics.items():
    for metric in metrics_list:
        doc_name = Path(metric['image_path']).name.split('-')[0]

        features = [
            metric['metrics']['blur'],
            metric['metrics']['noise'],
            metric['metrics']['contrast_rms'],
            metric['metrics']['brightness_mean'],
            metric['metrics']['histogram']['entropy']
        ]

        image_features.append(features)
        image_labels.append({
            'level': level,
            'doc': doc_name,
            'image_id': f"{doc_name}_{level}"
        })

image_features = np.array(image_features)

# Standardize and cluster
scaler_images = StandardScaler()
image_features_scaled = scaler_images.fit_transform(image_features)

# Try different cluster counts
print("Testing image cluster counts...")
for n_clusters in range(2, 6):
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    labels = kmeans.fit_predict(image_features_scaled)
    inertia = kmeans.inertia_
    print(f"  {n_clusters} clusters: inertia = {inertia:.1f}")

print()

# Use 3 clusters
n_image_clusters = 3
kmeans_images = KMeans(n_clusters=n_image_clusters, random_state=42, n_init=10)
image_cluster_labels = kmeans_images.fit_predict(image_features_scaled)

print(f"Image Clustering (k={n_image_clusters}):")
image_cluster_map = {}
for i, label_info in enumerate(image_labels):
    cluster = image_cluster_labels[i]
    image_id = label_info['image_id']
    image_cluster_map[image_id] = cluster
    print(f"  {image_id:30s} → Cluster {cluster}")

print()

# Analyze cluster characteristics
print("Image Cluster Characteristics:")
for cluster_id in range(n_image_clusters):
    cluster_indices = image_cluster_labels == cluster_id
    cluster_features = image_features[cluster_indices]

    avg_blur = cluster_features[:, 0].mean()
    avg_noise = cluster_features[:, 1].mean()
    avg_contrast = cluster_features[:, 2].mean()
    avg_brightness = cluster_features[:, 3].mean()
    avg_entropy = cluster_features[:, 4].mean()

    print(f"  Cluster {cluster_id}:")
    print(f"    Blur: {avg_blur:.1f}, Noise: {avg_noise:.2f}, Contrast: {avg_contrast:.1f}")
    print(f"    Brightness: {avg_brightness:.1f}, Entropy: {avg_entropy:.2f}")
    print(f"    Size: {cluster_indices.sum()} images")
    print()

# ============================================================================
# STEP 3: Cluster Filters by Parameters
# ============================================================================

print("STEP 3: Clustering Filters by Parameters")
print("-" * 80)

# Extract filter parameters (exclude BASELINE)
filter_features = []
filter_labels = []

for result in all_results:
    filter_id = result['filter_id']
    filter_type = result['filter_type']

    if filter_type == 'BASELINE':
        continue

    params = result['params']

    if filter_type == 'PIL':
        # PIL: 2 parameters
        features = [
            params.get('contrast_factor', 1.0),
            params.get('median_size', 3),
            0, 0, 0, 0, 0  # Pad to match OpenCV size
        ]
    else:  # OpenCV
        # OpenCV: 7 parameters
        features = [
            params.get('denoise_h', 10),
            params.get('clahe_clip', 2.0),
            params.get('bilateral_d', 5),
            params.get('bilateral_sigma_color', 75),
            params.get('bilateral_sigma_space', 75),
            params.get('unsharp_amount', 1.0),
            params.get('unsharp_radius', 1.0)
        ]

    filter_features.append(features)
    filter_labels.append({
        'filter_id': filter_id,
        'filter_type': filter_type,
        'params': params
    })

filter_features = np.array(filter_features)

# Standardize and cluster
scaler_filters = StandardScaler()
filter_features_scaled = scaler_filters.fit_transform(filter_features)

# Try different cluster counts
print("Testing filter cluster counts...")
for n_clusters in range(3, 8):
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    labels = kmeans.fit_predict(filter_features_scaled)
    inertia = kmeans.inertia_
    print(f"  {n_clusters} clusters: inertia = {inertia:.1f}")

print()

# Use 5 filter clusters
n_filter_clusters = 5
kmeans_filters = KMeans(n_clusters=n_filter_clusters, random_state=42, n_init=10)
filter_cluster_labels = kmeans_filters.fit_predict(filter_features_scaled)

print(f"Filter Clustering (k={n_filter_clusters}):")
filter_cluster_map = {}
for i, label_info in enumerate(filter_labels):
    cluster = filter_cluster_labels[i]
    filter_id = label_info['filter_id']
    filter_cluster_map[filter_id] = cluster
    print(f"  {filter_id:15s} → Filter Cluster {cluster} ({label_info['filter_type']})")

print()

# Analyze filter cluster characteristics
print("Filter Cluster Characteristics:")
for cluster_id in range(n_filter_clusters):
    cluster_indices = filter_cluster_labels == cluster_id
    cluster_filters = [filter_labels[i] for i in range(len(filter_labels)) if cluster_indices[i]]

    print(f"  Filter Cluster {cluster_id}:")
    print(f"    Size: {sum(cluster_indices)} filters")

    # Analyze by type
    pil_count = sum(1 for f in cluster_filters if f['filter_type'] == 'PIL')
    opencv_count = sum(1 for f in cluster_filters if f['filter_type'] == 'OpenCV')
    print(f"    Types: {pil_count} PIL, {opencv_count} OpenCV")

    # Show representative parameters
    if cluster_filters:
        rep_filter = cluster_filters[0]
        print(f"    Representative: {rep_filter['filter_id']}")
        params_str = ', '.join([f"{k}={v}" for k, v in list(rep_filter['params'].items())[:3]])
        print(f"    Params: {params_str}...")
    print()

# ============================================================================
# STEP 4: Build Correlation Matrix
# ============================================================================

print("STEP 4: Building Correlation Matrix")
print("-" * 80)

# For each (image_cluster, quality_level), find best filter_cluster
correlation_matrix = {}

degradation_levels = ['Q0_Pristine', 'Q05_VeryGood', 'Q1_Poor', 'Q15_Medium', 'Q2_MediumPoor']

for img_cluster in range(n_image_clusters):
    correlation_matrix[img_cluster] = {}

    for level in degradation_levels:
        # Find images in this cluster at this quality level
        images_in_cluster = [
            label_info['image_id']
            for i, label_info in enumerate(image_labels)
            if image_cluster_labels[i] == img_cluster and label_info['level'] == level
        ]

        if not images_in_cluster:
            continue

        # Test each filter cluster
        filter_cluster_performance = {}

        for filt_cluster in range(n_filter_clusters):
            # Get filters in this cluster
            filters_in_cluster = [
                filter_labels[i]['filter_id']
                for i in range(len(filter_labels))
                if filter_cluster_labels[i] == filt_cluster
            ]

            # Calculate average performance of this filter cluster on these images
            total_edits = 0
            count = 0

            for filter_id in filters_in_cluster:
                # Find filter result
                filter_result = next((r for r in all_results if r['filter_id'] == filter_id), None)
                if not filter_result:
                    continue

                if level not in filter_result['performance']:
                    continue

                # Sum edits across all images in this cluster at this level
                for image_id in images_in_cluster:
                    doc = image_id.split('_')[0]
                    if doc in filter_result['performance'][level]:
                        total_edits += filter_result['performance'][level][doc]
                        count += 1

            if count > 0:
                avg_edits = total_edits / count
                filter_cluster_performance[filt_cluster] = {
                    'avg_edits': avg_edits,
                    'filter_count': len(filters_in_cluster)
                }

        # Find best filter cluster
        if filter_cluster_performance:
            best_filter_cluster = min(filter_cluster_performance.items(),
                                     key=lambda x: x[1]['avg_edits'])

            correlation_matrix[img_cluster][level] = {
                'best_filter_cluster': best_filter_cluster[0],
                'avg_edits': best_filter_cluster[1]['avg_edits'],
                'image_count': len(images_in_cluster),
                'all_clusters': filter_cluster_performance
            }

# Display correlation matrix
print("CORRELATION MATRIX: Image_Cluster × Quality_Level → Best_Filter_Cluster")
print("-" * 80)
print()

for img_cluster in range(n_image_clusters):
    print(f"Image Cluster {img_cluster}:")
    for level in degradation_levels:
        if level in correlation_matrix[img_cluster]:
            info = correlation_matrix[img_cluster][level]
            best_fc = info['best_filter_cluster']
            edits = info['avg_edits']
            print(f"  {level:15s} → Filter Cluster {best_fc} ({edits:.1f} avg edits)")
    print()

# ============================================================================
# STEP 5: Save Production Catalog
# ============================================================================

print("STEP 5: Generating Production Catalog")
print("-" * 80)

production_catalog = {
    'image_clustering': {
        'n_clusters': n_image_clusters,
        'scaler_mean': scaler_images.mean_.tolist(),
        'scaler_scale': scaler_images.scale_.tolist(),
        'centroids': kmeans_images.cluster_centers_.tolist()
    },
    'filter_clustering': {
        'n_clusters': n_filter_clusters,
        'scaler_mean': scaler_filters.mean_.tolist(),
        'scaler_scale': scaler_filters.scale_.tolist(),
        'centroids': kmeans_filters.cluster_centers_.tolist()
    },
    'correlation_matrix': {},
    'filter_catalog': {}
}

# Build filter catalog (filter_cluster → representative parameters)
for filt_cluster in range(n_filter_clusters):
    cluster_filters = [
        filter_labels[i]
        for i in range(len(filter_labels))
        if filter_cluster_labels[i] == filt_cluster
    ]

    if cluster_filters:
        # Use first filter as representative (could average parameters instead)
        rep_filter = cluster_filters[0]
        production_catalog['filter_catalog'][filt_cluster] = {
            'filter_type': rep_filter['filter_type'],
            'parameters': rep_filter['params'],
            'cluster_size': len(cluster_filters)
        }

# Add correlation matrix
for img_cluster in range(n_image_clusters):
    production_catalog['correlation_matrix'][img_cluster] = {}
    for level in degradation_levels:
        if level in correlation_matrix[img_cluster]:
            info = correlation_matrix[img_cluster][level]
            production_catalog['correlation_matrix'][img_cluster][level] = {
                'best_filter_cluster': info['best_filter_cluster'],
                'expected_edits': info['avg_edits']
            }

# Save catalog
output_file = FIXTURES_DIR / "production_correlation_catalog.json"
with open(output_file, 'w') as f:
    json.dump(production_catalog, f, indent=2)

print(f"✓ Saved production catalog: {output_file}")
print()

print("=" * 80)
print("✓ COMPLETE!")
print("=" * 80)
print()
print(f"Image Clusters: {n_image_clusters}")
print(f"Filter Clusters: {n_filter_clusters}")
print(f"Correlation Matrix: {n_image_clusters} × {len(degradation_levels)} = {n_image_clusters * len(degradation_levels)} mappings")
print()
print("Production Usage:")
print("  1. Measure image properties (blur, noise, contrast, brightness, entropy)")
print("  2. Classify to image_cluster using K-means")
print("  3. Determine quality_level from metrics")
print("  4. Lookup correlation_matrix[image_cluster][quality_level]")
print("  5. Get filter parameters from filter_catalog[best_filter_cluster]")
print("  6. Apply filter with those parameters")
