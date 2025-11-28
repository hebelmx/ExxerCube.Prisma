#!/usr/bin/env python3
"""
GA Single Cluster Optimizer

Run as: python ga_single_cluster.py <cluster_id>

Optimizes filters for ONE cluster only.
Launch multiple instances in parallel for all clusters.
"""

import subprocess
import json
import random
import tempfile
import sys
from pathlib import Path
from typing import Dict, Tuple

import numpy as np
from PIL import Image, ImageFilter, ImageEnhance
import cv2
from deap import base, creator, tools
import Levenshtein


# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
PRISTINE_DIR = BASE_PATH / "PRP1"
DEGRADED_DIR = BASE_PATH / "PRP1_Degraded_v6"
OUTPUT_DIR = BASE_PATH / "PRP1_GA_Results_v6"

CLUSTER_FILE = DEGRADED_DIR / "cluster_assignments.json"

PRISTINE_DOCS = {
    "222AAA": "222AAA-44444444442025_page-1.png",
    "333BBB": "333BBB-44444444442025_page1.png",
    "333ccc": "333ccc-6666666662025_page1.png",
    "555CCC": "555CCC-66666662025_page-0001.png",
}

# GA parameters
POPULATION_SIZE = 30
GENERATIONS = 25
CROSSOVER_PROB = 0.7
MUTATION_PROB = 0.3
TOURNAMENT_SIZE = 3

# Filter parameter bounds
PARAM_BOUNDS = [
    (0.8, 1.5),     # 0: contrast
    (0.9, 1.2),     # 1: brightness
    (0.8, 2.5),     # 2: sharpness
    (1, 5),         # 3: median_size (int)
    (0.5, 3.0),     # 4: unsharp_radius
    (50, 200),      # 5: unsharp_percent
    (1, 5),         # 6: unsharp_threshold (int)
    (0, 1),         # 7: scan_removal_method (0=none, 1=morph)
    (1, 15),        # 8: morph_kernel_width (int)
    (1, 3),         # 9: morph_kernel_height (int)
    (0, 1),         # 10: apply_bilateral (0=no, 1=yes)
]


# ============================================================================
# Filter Functions
# ============================================================================

def remove_scan_lines_morph(img: Image.Image, kernel_width: int, kernel_height: int) -> Image.Image:
    """Remove horizontal scan lines using morphological opening."""
    # Ensure valid kernel size (minimum 1x1)
    kernel_width = max(1, int(kernel_width))
    kernel_height = max(1, int(kernel_height))

    img_array = np.array(img)
    if len(img_array.shape) == 3:
        gray = cv2.cvtColor(img_array, cv2.COLOR_RGB2GRAY)
    else:
        gray = img_array

    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (kernel_width, kernel_height))
    inverted = 255 - gray
    opened = cv2.morphologyEx(inverted, cv2.MORPH_OPEN, kernel)
    result = 255 - opened

    if len(img_array.shape) == 3:
        diff = result.astype(np.float32) - gray.astype(np.float32)
        result_rgb = img_array.astype(np.float32)
        for c in range(3):
            result_rgb[:, :, c] = np.clip(result_rgb[:, :, c] + diff * 0.7, 0, 255)
        return Image.fromarray(result_rgb.astype(np.uint8))
    return Image.fromarray(result)


def apply_bilateral_filter(img: Image.Image) -> Image.Image:
    """Apply bilateral filter."""
    img_array = np.array(img)
    filtered = cv2.bilateralFilter(img_array, 9, 75, 75)
    return Image.fromarray(filtered)


def apply_filters(image: Image.Image, params: list) -> Image.Image:
    """Apply filter chain."""
    img = image.copy()
    if img.mode != 'RGB':
        img = img.convert('RGB')

    contrast = params[0]
    brightness = params[1]
    sharpness = params[2]
    median_size = int(params[3])
    unsharp_radius = params[4]
    unsharp_percent = int(params[5])
    unsharp_threshold = int(params[6])
    scan_removal = int(round(params[7]))
    morph_w = int(params[8])
    morph_h = int(params[9])
    bilateral = int(round(params[10]))

    if scan_removal == 1 and morph_w > 1:
        img = remove_scan_lines_morph(img, morph_w, morph_h)

    if bilateral == 1:
        img = apply_bilateral_filter(img)

    if brightness != 1.0:
        img = ImageEnhance.Brightness(img).enhance(brightness)

    if contrast != 1.0:
        img = ImageEnhance.Contrast(img).enhance(contrast)

    if median_size > 1:
        img = img.filter(ImageFilter.MedianFilter(size=2*median_size+1))

    if sharpness != 1.0:
        img = ImageEnhance.Sharpness(img).enhance(sharpness)

    if unsharp_radius > 0.5 and unsharp_percent > 50:
        img = img.filter(ImageFilter.UnsharpMask(
            radius=unsharp_radius, percent=unsharp_percent, threshold=unsharp_threshold
        ))

    return img


# ============================================================================
# OCR
# ============================================================================

def run_tesseract(image: Image.Image) -> str:
    with tempfile.NamedTemporaryFile(suffix='.png', delete=True) as tmp:
        image.save(tmp.name, 'PNG')
        result = subprocess.run(
            ["tesseract", tmp.name, "stdout", "-l", "spa", "--psm", "6"],
            capture_output=True, text=True, timeout=60
        )
        return result.stdout


_ground_truth_cache = {}

def get_ground_truth(doc_id: str) -> str:
    if doc_id not in _ground_truth_cache:
        path = PRISTINE_DIR / PRISTINE_DOCS[doc_id]
        if path.exists():
            _ground_truth_cache[doc_id] = run_tesseract(Image.open(path))
        else:
            _ground_truth_cache[doc_id] = ""
    return _ground_truth_cache[doc_id]


# ============================================================================
# Fitness
# ============================================================================

def evaluate(individual: list, cluster_images: list) -> Tuple[float, float]:
    total_edit = 0
    count = 0

    for img_info in cluster_images:
        path = DEGRADED_DIR / img_info["filename"]
        if not path.exists():
            continue

        degraded = Image.open(path)
        filtered = apply_filters(degraded, individual)
        text = run_tesseract(filtered)
        truth = get_ground_truth(img_info["doc_id"])

        total_edit += Levenshtein.distance(text, truth)
        count += 1

    avg_edit = total_edit / count if count > 0 else float('inf')

    complexity = (
        abs(individual[0] - 1.0) +
        abs(individual[1] - 1.0) +
        abs(individual[2] - 1.0) * 0.5 +
        (individual[3] - 1) * 0.3 +
        individual[7] * 0.5 +
        individual[10] * 0.3
    )

    return (avg_edit, complexity)


# ============================================================================
# GA
# ============================================================================

def setup_ga(cluster_images):
    if hasattr(creator, "FitnessMin"):
        del creator.FitnessMin
    if hasattr(creator, "Individual"):
        del creator.Individual

    creator.create("FitnessMin", base.Fitness, weights=(-1.0, -0.1))
    creator.create("Individual", list, fitness=creator.FitnessMin)

    toolbox = base.Toolbox()

    def random_ind():
        return [
            random.uniform(PARAM_BOUNDS[0][0], PARAM_BOUNDS[0][1]),
            random.uniform(PARAM_BOUNDS[1][0], PARAM_BOUNDS[1][1]),
            random.uniform(PARAM_BOUNDS[2][0], PARAM_BOUNDS[2][1]),
            random.randint(PARAM_BOUNDS[3][0], PARAM_BOUNDS[3][1]),
            random.uniform(PARAM_BOUNDS[4][0], PARAM_BOUNDS[4][1]),
            random.uniform(PARAM_BOUNDS[5][0], PARAM_BOUNDS[5][1]),
            random.randint(PARAM_BOUNDS[6][0], PARAM_BOUNDS[6][1]),
            random.randint(PARAM_BOUNDS[7][0], PARAM_BOUNDS[7][1]),
            random.randint(PARAM_BOUNDS[8][0], PARAM_BOUNDS[8][1]),
            random.randint(PARAM_BOUNDS[9][0], PARAM_BOUNDS[9][1]),
            random.randint(PARAM_BOUNDS[10][0], PARAM_BOUNDS[10][1]),
        ]

    toolbox.register("individual", tools.initIterate, creator.Individual, random_ind)
    toolbox.register("population", tools.initRepeat, list, toolbox.individual)
    toolbox.register("select", tools.selTournament, tournsize=TOURNAMENT_SIZE)
    toolbox.register("mate", tools.cxBlend, alpha=0.5)

    def mutate(ind):
        for i in range(len(ind)):
            if random.random() < 0.25:
                if i in [3, 6, 7, 8, 9, 10]:
                    ind[i] = random.randint(PARAM_BOUNDS[i][0], PARAM_BOUNDS[i][1])
                else:
                    delta = (PARAM_BOUNDS[i][1] - PARAM_BOUNDS[i][0]) * 0.2
                    ind[i] += random.gauss(0, delta)
                    ind[i] = max(PARAM_BOUNDS[i][0], min(PARAM_BOUNDS[i][1], ind[i]))
        return ind,

    toolbox.register("mutate", mutate)
    toolbox.register("evaluate", lambda ind: evaluate(ind, cluster_images))

    return toolbox


def run_ga(cluster_id: int, cluster_images: list) -> dict:
    """Run GA for a single cluster."""
    random.seed(42 + cluster_id)
    np.random.seed(42 + cluster_id)

    print(f"[Cluster {cluster_id}] Starting GA with {len(cluster_images)} images")

    if len(cluster_images) < 3:
        print(f"[Cluster {cluster_id}] Skipped - too few images")
        return {"status": "skipped", "cluster_id": cluster_id}

    toolbox = setup_ga(cluster_images)
    pop = toolbox.population(n=POPULATION_SIZE)

    # Evaluate initial
    fitnesses = list(map(toolbox.evaluate, pop))
    for ind, fit in zip(pop, fitnesses):
        ind.fitness.values = fit

    hof = tools.HallOfFame(3)
    hof.update(pop)

    print(f"[Cluster {cluster_id}] Initial best: {hof[0].fitness.values[0]:.1f}")

    for gen in range(GENERATIONS):
        offspring = toolbox.select(pop, len(pop))
        offspring = list(map(toolbox.clone, offspring))

        for c1, c2 in zip(offspring[::2], offspring[1::2]):
            if random.random() < CROSSOVER_PROB:
                toolbox.mate(c1, c2)
                del c1.fitness.values
                del c2.fitness.values

        for mut in offspring:
            if random.random() < MUTATION_PROB:
                toolbox.mutate(mut)
                del mut.fitness.values

        invalid = [ind for ind in offspring if not ind.fitness.valid]
        fitnesses = list(map(toolbox.evaluate, invalid))
        for ind, fit in zip(invalid, fitnesses):
            ind.fitness.values = fit

        pop[:] = offspring
        hof.update(pop)

        if (gen + 1) % 5 == 0:
            print(f"[Cluster {cluster_id}] Gen {gen+1}: best={hof[0].fitness.values[0]:.1f}")

    best = hof[0]
    result = {
        "status": "optimized",
        "cluster_id": cluster_id,
        "n_images": len(cluster_images),
        "best_edit_distance": round(best.fitness.values[0], 2),
        "best_params": {
            "contrast": round(best[0], 3),
            "brightness": round(best[1], 3),
            "sharpness": round(best[2], 3),
            "median_size": int(best[3]),
            "unsharp_radius": round(best[4], 3),
            "unsharp_percent": int(best[5]),
            "unsharp_threshold": int(best[6]),
            "scan_removal": int(round(best[7])),
            "morph_kernel_w": int(best[8]),
            "morph_kernel_h": int(best[9]),
            "bilateral": int(round(best[10])),
        }
    }

    print(f"[Cluster {cluster_id}] DONE - Best edit distance: {result['best_edit_distance']}")
    print(f"[Cluster {cluster_id}] Params: {result['best_params']}")

    return result


# ============================================================================
# Main
# ============================================================================

def main():
    if len(sys.argv) < 2:
        print("Usage: python ga_single_cluster.py <cluster_id>")
        print("       python ga_single_cluster.py all  # run all sequentially")
        sys.exit(1)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    with open(CLUSTER_FILE) as f:
        data = json.load(f)

    n_clusters = data["clustering"]["n_clusters"]
    assignments = data["cluster_assignments"]

    # Load ground truths
    print("Loading ground truths...")
    for doc_id in PRISTINE_DOCS:
        get_ground_truth(doc_id)

    # Group by cluster
    clusters = {i: [] for i in range(n_clusters)}
    for item in assignments:
        clusters[item["cluster"]].append(item)

    if sys.argv[1] == "all":
        # Run all clusters sequentially
        all_results = {}
        for cid in range(n_clusters):
            result = run_ga(cid, clusters[cid])
            all_results[cid] = result

            # Save intermediate
            with open(OUTPUT_DIR / f"cluster_{cid}_result.json", 'w') as f:
                json.dump(result, f, indent=2)

        # Save combined
        with open(OUTPUT_DIR / "all_clusters_results.json", 'w') as f:
            json.dump(all_results, f, indent=2)
    else:
        cluster_id = int(sys.argv[1])
        if cluster_id not in clusters:
            print(f"Invalid cluster_id: {cluster_id}. Valid: 0-{n_clusters-1}")
            sys.exit(1)

        result = run_ga(cluster_id, clusters[cluster_id])

        # Save result
        with open(OUTPUT_DIR / f"cluster_{cluster_id}_result.json", 'w') as f:
            json.dump(result, f, indent=2)


if __name__ == "__main__":
    main()
