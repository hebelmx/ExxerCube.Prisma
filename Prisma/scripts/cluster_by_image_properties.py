#!/usr/bin/env python3
"""
Cluster Degraded Documents by Image Properties

Goal: Group images by their visual properties (blur_score, contrast, noise, edge_density)
NOT by OCR confidence - the hypothesis is that similar-looking images need similar filters.

Uses K-Means clustering with automatic k selection via elbow method.
"""

import json
from pathlib import Path
from typing import List, Dict

import numpy as np
from sklearn.cluster import KMeans
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import silhouette_score
import matplotlib.pyplot as plt


# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
INPUT_DIR = BASE_PATH / "PRP1_Degraded_v6"
RESULTS_FILE = INPUT_DIR / "degradation_results_v6.json"

# Features to use for clustering
FEATURES = ["blur_score", "contrast", "noise_estimate", "edge_density"]

# Cluster range to test
MIN_CLUSTERS = 2
MAX_CLUSTERS = 6


# ============================================================================
# Main
# ============================================================================

def main():
    print("=" * 70)
    print("DOCUMENT CLUSTERING BY IMAGE PROPERTIES")
    print("=" * 70)
    print()

    # Load results
    with open(RESULTS_FILE) as f:
        results = json.load(f)

    print(f"Loaded {len(results)} degraded images")
    print(f"Features: {FEATURES}")
    print()

    # Extract feature matrix
    feature_matrix = []
    filenames = []
    valid_results = []

    for item in results:
        props = item.get("image_properties", {})
        if all(f in props for f in FEATURES):
            feature_vector = [props[f] for f in FEATURES]
            feature_matrix.append(feature_vector)
            filenames.append(item["filename"])
            valid_results.append(item)

    X = np.array(feature_matrix)
    print(f"Valid images with all features: {len(X)}")
    print()

    # Standardize features
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)

    # Find optimal number of clusters using silhouette score
    print("Finding optimal cluster count...")
    print("-" * 40)

    silhouette_scores = []
    inertias = []

    for k in range(MIN_CLUSTERS, MAX_CLUSTERS + 1):
        kmeans = KMeans(n_clusters=k, random_state=42, n_init=10)
        labels = kmeans.fit_predict(X_scaled)
        sil_score = silhouette_score(X_scaled, labels)
        silhouette_scores.append(sil_score)
        inertias.append(kmeans.inertia_)
        print(f"  k={k}: silhouette={sil_score:.3f}, inertia={kmeans.inertia_:.1f}")

    # Best k by silhouette score
    best_k = MIN_CLUSTERS + np.argmax(silhouette_scores)
    print()
    print(f"Best k by silhouette: {best_k}")
    print()

    # Fit final model
    kmeans = KMeans(n_clusters=best_k, random_state=42, n_init=10)
    labels = kmeans.fit_predict(X_scaled)

    # Analyze clusters
    print("=" * 70)
    print(f"CLUSTER ANALYSIS (k={best_k})")
    print("=" * 70)

    # Group results by cluster
    clusters = {i: [] for i in range(best_k)}
    for idx, label in enumerate(labels):
        clusters[label].append(valid_results[idx])

    # Statistics per cluster
    for cluster_id in range(best_k):
        cluster_items = clusters[cluster_id]
        print(f"\n{'='*40}")
        print(f"CLUSTER {cluster_id}: {len(cluster_items)} images")
        print(f"{'='*40}")

        # Feature statistics
        cluster_features = np.array([
            [item["image_properties"][f] for f in FEATURES]
            for item in cluster_items
        ])

        print("\nImage property statistics:")
        for i, feat in enumerate(FEATURES):
            values = cluster_features[:, i]
            print(f"  {feat}: mean={np.mean(values):.2f}, std={np.std(values):.2f}, "
                  f"range=[{np.min(values):.2f}, {np.max(values):.2f}]")

        # OCR confidence distribution
        confs = [item["actual_conf"] for item in cluster_items]
        print(f"\nOCR confidence: mean={np.mean(confs):.1f}%, "
              f"range=[{np.min(confs):.1f}%, {np.max(confs):.1f}%]")

        # Document source distribution
        doc_counts = {}
        for item in cluster_items:
            doc_id = item["doc_id"]
            doc_counts[doc_id] = doc_counts.get(doc_id, 0) + 1
        print(f"\nSource documents: {doc_counts}")

        # Artifacts distribution
        artifacts = {}
        for item in cluster_items:
            art_key = "+".join(item.get("artifacts", [])) if item.get("artifacts") else "clean"
            artifacts[art_key] = artifacts.get(art_key, 0) + 1
        print(f"Artifacts: {artifacts}")

        # Target confidence distribution
        target_counts = {}
        for item in cluster_items:
            t = item["target_conf"]
            target_counts[t] = target_counts.get(t, 0) + 1
        print(f"Target confidences: {target_counts}")

        # List a few examples
        print(f"\nExamples:")
        for item in cluster_items[:3]:
            print(f"  - {item['filename']}: {item['actual_conf']}%")

    # Save cluster assignments
    output_data = {
        "clustering": {
            "method": "kmeans",
            "features": FEATURES,
            "n_clusters": best_k,
            "silhouette_score": silhouette_scores[best_k - MIN_CLUSTERS],
        },
        "cluster_assignments": [
            {
                "filename": valid_results[idx]["filename"],
                "cluster": int(labels[idx]),
                "doc_id": valid_results[idx]["doc_id"],
                "target_conf": valid_results[idx]["target_conf"],
                "actual_conf": valid_results[idx]["actual_conf"],
                "image_properties": valid_results[idx]["image_properties"],
            }
            for idx in range(len(labels))
        ],
        "cluster_summaries": {}
    }

    # Add cluster summaries
    for cluster_id in range(best_k):
        cluster_items = clusters[cluster_id]
        cluster_features = np.array([
            [item["image_properties"][f] for f in FEATURES]
            for item in cluster_items
        ])
        confs = [item["actual_conf"] for item in cluster_items]

        output_data["cluster_summaries"][str(cluster_id)] = {
            "count": len(cluster_items),
            "feature_means": {
                FEATURES[i]: round(np.mean(cluster_features[:, i]), 2)
                for i in range(len(FEATURES))
            },
            "confidence_range": [min(confs), max(confs)],
            "confidence_mean": round(np.mean(confs), 1),
        }

    # Convert numpy types to Python types for JSON serialization
    def convert_numpy(obj):
        if isinstance(obj, np.integer):
            return int(obj)
        elif isinstance(obj, np.floating):
            return float(obj)
        elif isinstance(obj, np.ndarray):
            return obj.tolist()
        elif isinstance(obj, dict):
            return {k: convert_numpy(v) for k, v in obj.items()}
        elif isinstance(obj, list):
            return [convert_numpy(i) for i in obj]
        return obj

    output_data = convert_numpy(output_data)

    output_path = INPUT_DIR / "cluster_assignments.json"
    with open(output_path, 'w') as f:
        json.dump(output_data, f, indent=2)

    print()
    print("=" * 70)
    print(f"Cluster assignments saved to: {output_path}")
    print("=" * 70)

    # Create visualization
    fig, axes = plt.subplots(2, 2, figsize=(12, 10))

    # 1. Silhouette scores
    axes[0, 0].plot(range(MIN_CLUSTERS, MAX_CLUSTERS + 1), silhouette_scores, 'bo-')
    axes[0, 0].axvline(x=best_k, color='r', linestyle='--', label=f'Best k={best_k}')
    axes[0, 0].set_xlabel('Number of clusters')
    axes[0, 0].set_ylabel('Silhouette Score')
    axes[0, 0].set_title('Silhouette Score vs Number of Clusters')
    axes[0, 0].legend()

    # 2. Elbow plot
    axes[0, 1].plot(range(MIN_CLUSTERS, MAX_CLUSTERS + 1), inertias, 'go-')
    axes[0, 1].axvline(x=best_k, color='r', linestyle='--', label=f'Best k={best_k}')
    axes[0, 1].set_xlabel('Number of clusters')
    axes[0, 1].set_ylabel('Inertia')
    axes[0, 1].set_title('Elbow Plot')
    axes[0, 1].legend()

    # 3. Blur score vs Edge density (colored by cluster)
    for cluster_id in range(best_k):
        mask = labels == cluster_id
        axes[1, 0].scatter(
            X[mask, 0], X[mask, 3],  # blur_score vs edge_density
            label=f'Cluster {cluster_id}',
            alpha=0.7
        )
    axes[1, 0].set_xlabel('Blur Score')
    axes[1, 0].set_ylabel('Edge Density')
    axes[1, 0].set_title('Clusters by Blur Score vs Edge Density')
    axes[1, 0].legend()

    # 4. Contrast vs Noise (colored by cluster)
    for cluster_id in range(best_k):
        mask = labels == cluster_id
        axes[1, 1].scatter(
            X[mask, 1], X[mask, 2],  # contrast vs noise_estimate
            label=f'Cluster {cluster_id}',
            alpha=0.7
        )
    axes[1, 1].set_xlabel('Contrast')
    axes[1, 1].set_ylabel('Noise Estimate')
    axes[1, 1].set_title('Clusters by Contrast vs Noise')
    axes[1, 1].legend()

    plt.tight_layout()
    plot_path = INPUT_DIR / "cluster_visualization.png"
    plt.savefig(plot_path, dpi=150)
    print(f"Visualization saved to: {plot_path}")

    return output_data


if __name__ == "__main__":
    main()
