"""
Determine Image Cluster Assignments

Loads the production clustering parameters and classifies all test images
into their respective clusters for cluster-specific GA optimization.

Output: Mapping of image_id → cluster_id for all 20 test images
"""

import json
import numpy as np
from pathlib import Path
from sklearn.preprocessing import StandardScaler

FIXTURES_DIR = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures")

print("="*80)
print("IMAGE CLUSTER ASSIGNMENT")
print("="*80)
print()

# Load production catalog with clustering parameters
with open(FIXTURES_DIR / "production_correlation_catalog.json", 'r') as f:
    catalog = json.load(f)

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

# Extract clustering parameters
scaler_mean = np.array(catalog['image_clustering']['scaler_mean'])
scaler_scale = np.array(catalog['image_clustering']['scaler_scale'])
centroids = np.array(catalog['image_clustering']['centroids'])
n_clusters = catalog['image_clustering']['n_clusters']

print(f"Loaded clustering parameters: {n_clusters} clusters")
print()

# Build feature matrix for all images
image_data = []

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

        image_data.append({
            'image_id': f"{doc_name}_{level}",
            'doc': doc_name,
            'level': level,
            'features': features
        })

# Classify each image into clusters
cluster_assignments = {i: [] for i in range(n_clusters)}

for img_data in image_data:
    features = np.array(img_data['features'])

    # Standardize using production scaler
    features_scaled = (features - scaler_mean) / scaler_scale

    # Find nearest centroid
    distances = np.linalg.norm(centroids - features_scaled, axis=1)
    cluster_id = int(np.argmin(distances))

    cluster_assignments[cluster_id].append(img_data)

# Display cluster assignments
print("CLUSTER ASSIGNMENTS")
print("-"*80)
print()

for cluster_id in range(n_clusters):
    print(f"Cluster {cluster_id}: {len(cluster_assignments[cluster_id])} images")

    # Group by document
    docs_in_cluster = {}
    for img in cluster_assignments[cluster_id]:
        doc = img['doc']
        if doc not in docs_in_cluster:
            docs_in_cluster[doc] = []
        docs_in_cluster[doc].append(img['level'])

    for doc, levels in sorted(docs_in_cluster.items()):
        print(f"  {doc}: {', '.join(levels)}")

    # Show average characteristics
    all_features = np.array([img['features'] for img in cluster_assignments[cluster_id]])
    avg_blur = all_features[:, 0].mean()
    avg_noise = all_features[:, 1].mean()
    avg_contrast = all_features[:, 2].mean()
    avg_brightness = all_features[:, 3].mean()
    avg_entropy = all_features[:, 4].mean()

    print(f"  Avg: blur={avg_blur:.1f}, noise={avg_noise:.2f}, contrast={avg_contrast:.1f}, brightness={avg_brightness:.1f}, entropy={avg_entropy:.2f}")
    print()

# Save detailed mapping for GA script generation
output_mapping = {
    'clusters': {
        str(cluster_id): {
            'images': [img['image_id'] for img in cluster_assignments[cluster_id]],
            'documents': list(set(img['doc'] for img in cluster_assignments[cluster_id])),
            'characteristics': {
                'description': f"Cluster {cluster_id}",
                'avg_blur': float(np.array([img['features'] for img in cluster_assignments[cluster_id]])[:, 0].mean()),
                'avg_noise': float(np.array([img['features'] for img in cluster_assignments[cluster_id]])[:, 1].mean()),
                'avg_contrast': float(np.array([img['features'] for img in cluster_assignments[cluster_id]])[:, 2].mean())
            }
        }
        for cluster_id in range(n_clusters)
    }
}

output_file = FIXTURES_DIR / "cluster_image_assignments.json"
with open(output_file, 'w') as f:
    json.dump(output_mapping, f, indent=2)

print(f"✓ Saved cluster assignments to: {output_file}")
print()

# Generate GA script configuration
print("="*80)
print("GA OPTIMIZATION CONFIGURATION")
print("="*80)
print()

for cluster_id in range(n_clusters):
    images = cluster_assignments[cluster_id]
    docs = list(set(img['doc'] for img in images))

    print(f"Cluster {cluster_id} GA:")
    print(f"  Documents: {', '.join(docs)}")
    print(f"  Total images: {len(images)}")
    print(f"  Script names:")
    print(f"    - optimize_cluster{cluster_id}_pil.py")
    print(f"    - optimize_cluster{cluster_id}_opencv.py")
    print()

print("="*80)
print("✓ COMPLETE")
print("="*80)
