#!/usr/bin/env python3
"""
GA Per-Cluster Filter Optimization v6

ADDED: Scan line removal filters
- Morphological opening (removes horizontal lines)
- Horizontal line detection + median replacement
- Adaptive thresholding options

Uses DEAP for genetic algorithm.
"""

import subprocess
import json
import random
import tempfile
from pathlib import Path
from typing import Dict, Tuple
from functools import partial

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
POPULATION_SIZE = 25
GENERATIONS = 20
CROSSOVER_PROB = 0.7
MUTATION_PROB = 0.3
TOURNAMENT_SIZE = 3

# Filter parameter bounds [min, max]
# Indices: 0-6 original, 7-10 scan line removal
PARAM_BOUNDS = [
    (0.8, 1.5),     # 0: contrast
    (0.9, 1.2),     # 1: brightness
    (0.8, 2.5),     # 2: sharpness
    (1, 5),         # 3: median_size (int)
    (0.5, 3.0),     # 4: unsharp_radius
    (50, 200),      # 5: unsharp_percent
    (1, 5),         # 6: unsharp_threshold (int)
    (0, 1),         # 7: scan_removal_method (0=none, 1=morph)
    (1, 15),        # 8: morph_kernel_width (int, for horizontal lines)
    (1, 3),         # 9: morph_kernel_height (int)
    (0, 1),         # 10: apply_bilateral (0=no, 1=yes)
]


# ============================================================================
# Scan Line Removal Functions
# ============================================================================

def remove_scan_lines_morph(img: Image.Image, kernel_width: int, kernel_height: int) -> Image.Image:
    """
    Remove horizontal scan lines using morphological opening.
    Uses a horizontal kernel to selectively remove thin horizontal lines.
    """
    img_array = np.array(img)

    # Convert to grayscale for morphology
    if len(img_array.shape) == 3:
        gray = cv2.cvtColor(img_array, cv2.COLOR_RGB2GRAY)
    else:
        gray = img_array

    # Create horizontal kernel
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (kernel_width, kernel_height))

    # Morphological opening removes small bright features (scan lines are darker)
    # We need to invert, apply, invert back
    inverted = 255 - gray
    opened = cv2.morphologyEx(inverted, cv2.MORPH_OPEN, kernel)
    result = 255 - opened

    # Convert back to RGB if needed
    if len(img_array.shape) == 3:
        # Apply the change as a blend
        diff = result.astype(np.float32) - gray.astype(np.float32)
        result_rgb = img_array.astype(np.float32)
        for c in range(3):
            result_rgb[:, :, c] = np.clip(result_rgb[:, :, c] + diff * 0.7, 0, 255)
        result_rgb = result_rgb.astype(np.uint8)
        return Image.fromarray(result_rgb)
    else:
        return Image.fromarray(result)


def apply_bilateral_filter(img: Image.Image, d: int = 9, sigma_color: int = 75, sigma_space: int = 75) -> Image.Image:
    """Apply bilateral filter to smooth while preserving edges."""
    img_array = np.array(img)
    if len(img_array.shape) == 3:
        filtered = cv2.bilateralFilter(img_array, d, sigma_color, sigma_space)
    else:
        filtered = cv2.bilateralFilter(img_array, d, sigma_color, sigma_space)
    return Image.fromarray(filtered)


# ============================================================================
# Main Filter Application
# ============================================================================

def apply_filters(image: Image.Image, params: list) -> Image.Image:
    """Apply filter chain with given parameters."""
    img = image.copy()
    if img.mode != 'RGB':
        img = img.convert('RGB')

    # Decode parameters
    contrast = params[0]
    brightness = params[1]
    sharpness = params[2]
    median_size = int(params[3])
    unsharp_radius = params[4]
    unsharp_percent = int(params[5])
    unsharp_threshold = int(params[6])
    scan_removal_method = int(round(params[7]))
    morph_kernel_width = int(params[8])
    morph_kernel_height = int(params[9])
    apply_bilateral = int(round(params[10]))

    # 1. Scan line removal (first, to clean up before other filters)
    if scan_removal_method == 1 and morph_kernel_width > 1:
        img = remove_scan_lines_morph(img, morph_kernel_width, morph_kernel_height)

    # 2. Bilateral filter (edge-preserving smoothing)
    if apply_bilateral == 1:
        img = apply_bilateral_filter(img)

    # 3. Brightness adjustment
    if brightness != 1.0:
        img = ImageEnhance.Brightness(img).enhance(brightness)

    # 4. Contrast enhancement
    if contrast != 1.0:
        img = ImageEnhance.Contrast(img).enhance(contrast)

    # 5. Median filter (denoise)
    if median_size > 1:
        kernel_size = 2 * median_size + 1
        img = img.filter(ImageFilter.MedianFilter(size=kernel_size))

    # 6. Sharpness
    if sharpness != 1.0:
        img = ImageEnhance.Sharpness(img).enhance(sharpness)

    # 7. Unsharp mask
    if unsharp_radius > 0.5 and unsharp_percent > 50:
        img = img.filter(ImageFilter.UnsharpMask(
            radius=unsharp_radius,
            percent=unsharp_percent,
            threshold=unsharp_threshold
        ))

    return img


# ============================================================================
# OCR Functions
# ============================================================================

def run_tesseract(image: Image.Image) -> str:
    """Run Tesseract OCR on a PIL image."""
    with tempfile.NamedTemporaryFile(suffix='.png', delete=True) as tmp:
        image.save(tmp.name, 'PNG')
        result = subprocess.run(
            ["tesseract", tmp.name, "stdout", "-l", "spa", "--psm", "6"],
            capture_output=True, text=True, timeout=60
        )
        return result.stdout


_ground_truth_cache = {}

def get_ground_truth(doc_id: str) -> str:
    """Get ground truth OCR text from pristine document."""
    if doc_id not in _ground_truth_cache:
        pristine_path = PRISTINE_DIR / PRISTINE_DOCS[doc_id]
        if pristine_path.exists():
            img = Image.open(pristine_path)
            _ground_truth_cache[doc_id] = run_tesseract(img)
        else:
            _ground_truth_cache[doc_id] = ""
    return _ground_truth_cache[doc_id]


# ============================================================================
# Fitness Function
# ============================================================================

def evaluate_individual(individual: list, cluster_images: list) -> Tuple[float, float]:
    """Evaluate a filter configuration."""
    total_edit_distance = 0
    count = 0

    for img_info in cluster_images:
        filename = img_info["filename"]
        doc_id = img_info["doc_id"]
        img_path = DEGRADED_DIR / filename

        if not img_path.exists():
            continue

        degraded = Image.open(img_path)
        filtered = apply_filters(degraded, individual)
        filtered_text = run_tesseract(filtered)
        ground_truth = get_ground_truth(doc_id)

        edit_dist = Levenshtein.distance(filtered_text, ground_truth)
        total_edit_distance += edit_dist
        count += 1

    avg_edit_distance = total_edit_distance / count if count > 0 else float('inf')

    # Calculate filter complexity
    complexity = (
        abs(individual[0] - 1.0) +  # contrast
        abs(individual[1] - 1.0) +  # brightness
        abs(individual[2] - 1.0) * 0.5 +  # sharpness
        (individual[3] - 1) * 0.3 +  # median_size
        max(0, individual[4] - 0.5) * 0.2 +  # unsharp
        individual[7] * 0.5 +  # scan removal active
        individual[10] * 0.3  # bilateral active
    )

    return (avg_edit_distance, complexity)


# ============================================================================
# GA Setup
# ============================================================================

def setup_ga():
    """Setup DEAP genetic algorithm."""
    if hasattr(creator, "FitnessMin"):
        del creator.FitnessMin
    if hasattr(creator, "Individual"):
        del creator.Individual

    creator.create("FitnessMin", base.Fitness, weights=(-1.0, -0.1))
    creator.create("Individual", list, fitness=creator.FitnessMin)

    toolbox = base.Toolbox()

    def random_individual():
        return [
            random.uniform(PARAM_BOUNDS[0][0], PARAM_BOUNDS[0][1]),  # contrast
            random.uniform(PARAM_BOUNDS[1][0], PARAM_BOUNDS[1][1]),  # brightness
            random.uniform(PARAM_BOUNDS[2][0], PARAM_BOUNDS[2][1]),  # sharpness
            random.randint(PARAM_BOUNDS[3][0], PARAM_BOUNDS[3][1]),  # median_size
            random.uniform(PARAM_BOUNDS[4][0], PARAM_BOUNDS[4][1]),  # unsharp_radius
            random.uniform(PARAM_BOUNDS[5][0], PARAM_BOUNDS[5][1]),  # unsharp_percent
            random.randint(PARAM_BOUNDS[6][0], PARAM_BOUNDS[6][1]),  # unsharp_threshold
            random.randint(PARAM_BOUNDS[7][0], PARAM_BOUNDS[7][1]),  # scan_removal_method
            random.randint(PARAM_BOUNDS[8][0], PARAM_BOUNDS[8][1]),  # morph_kernel_width
            random.randint(PARAM_BOUNDS[9][0], PARAM_BOUNDS[9][1]),  # morph_kernel_height
            random.randint(PARAM_BOUNDS[10][0], PARAM_BOUNDS[10][1]),  # apply_bilateral
        ]

    toolbox.register("individual", tools.initIterate, creator.Individual, random_individual)
    toolbox.register("population", tools.initRepeat, list, toolbox.individual)
    toolbox.register("select", tools.selTournament, tournsize=TOURNAMENT_SIZE)
    toolbox.register("mate", tools.cxBlend, alpha=0.5)

    def mutate(individual):
        for i in range(len(individual)):
            if random.random() < 0.25:
                if i in [3, 6, 7, 8, 9, 10]:  # Integer parameters
                    individual[i] = random.randint(PARAM_BOUNDS[i][0], PARAM_BOUNDS[i][1])
                else:
                    delta = (PARAM_BOUNDS[i][1] - PARAM_BOUNDS[i][0]) * 0.2
                    individual[i] += random.gauss(0, delta)
                    individual[i] = max(PARAM_BOUNDS[i][0], min(PARAM_BOUNDS[i][1], individual[i]))
        return individual,

    toolbox.register("mutate", mutate)

    return toolbox


# ============================================================================
# Main
# ============================================================================

def main():
    random.seed(42)
    np.random.seed(42)

    print("=" * 70)
    print("GA PER-CLUSTER FILTER OPTIMIZATION v6")
    print("=" * 70)
    print()
    print("NEW: Scan line removal filters (morphological, bilateral)")
    print()

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    with open(CLUSTER_FILE) as f:
        cluster_data = json.load(f)

    n_clusters = cluster_data["clustering"]["n_clusters"]
    assignments = cluster_data["cluster_assignments"]

    print(f"Loaded {len(assignments)} images in {n_clusters} clusters")

    # Load ground truths
    print("\nLoading ground truth OCR texts...")
    for doc_id in PRISTINE_DOCS:
        get_ground_truth(doc_id)
        print(f"  {doc_id}: {len(_ground_truth_cache[doc_id])} chars")

    clusters = {i: [] for i in range(n_clusters)}
    for item in assignments:
        clusters[item["cluster"]].append(item)

    results = {}

    for cluster_id in range(n_clusters):
        cluster_images = clusters[cluster_id]

        print(f"\n{'='*60}")
        print(f"CLUSTER {cluster_id}: {len(cluster_images)} images")
        print(f"{'='*60}")

        if len(cluster_images) < 3:
            print("Skipping - too few images")
            results[cluster_id] = {"status": "skipped", "reason": "too_few_images"}
            continue

        toolbox = setup_ga()
        eval_func = partial(evaluate_individual, cluster_images=cluster_images)
        toolbox.register("evaluate", eval_func)

        pop = toolbox.population(n=POPULATION_SIZE)

        print(f"\nEvaluating initial population ({POPULATION_SIZE})...")
        fitnesses = list(map(toolbox.evaluate, pop))
        for ind, fit in zip(pop, fitnesses):
            ind.fitness.values = fit

        best_fitness = min(f[0] for f in fitnesses)
        print(f"Initial best edit distance: {best_fitness:.1f}")

        hof = tools.HallOfFame(3)

        for gen in range(GENERATIONS):
            offspring = toolbox.select(pop, len(pop))
            offspring = list(map(toolbox.clone, offspring))

            for child1, child2 in zip(offspring[::2], offspring[1::2]):
                if random.random() < CROSSOVER_PROB:
                    toolbox.mate(child1, child2)
                    del child1.fitness.values
                    del child2.fitness.values

            for mutant in offspring:
                if random.random() < MUTATION_PROB:
                    toolbox.mutate(mutant)
                    del mutant.fitness.values

            invalid_ind = [ind for ind in offspring if not ind.fitness.valid]
            fitnesses = list(map(toolbox.evaluate, invalid_ind))
            for ind, fit in zip(invalid_ind, fitnesses):
                ind.fitness.values = fit

            pop[:] = offspring
            hof.update(pop)

            if (gen + 1) % 5 == 0 or gen == 0:
                best = hof[0].fitness.values[0]
                print(f"  Gen {gen+1:3d}: best={best:.1f}")

        best_individual = hof[0]
        best_params = {
            "contrast": round(best_individual[0], 3),
            "brightness": round(best_individual[1], 3),
            "sharpness": round(best_individual[2], 3),
            "median_size": int(best_individual[3]),
            "unsharp_radius": round(best_individual[4], 3),
            "unsharp_percent": int(best_individual[5]),
            "unsharp_threshold": int(best_individual[6]),
            "scan_removal_method": int(round(best_individual[7])),
            "morph_kernel_width": int(best_individual[8]),
            "morph_kernel_height": int(best_individual[9]),
            "apply_bilateral": int(round(best_individual[10])),
        }

        print(f"\nBest parameters for cluster {cluster_id}:")
        for k, v in best_params.items():
            print(f"  {k}: {v}")
        print(f"\nFinal edit distance: {best_individual.fitness.values[0]:.1f}")

        results[cluster_id] = {
            "status": "optimized",
            "n_images": len(cluster_images),
            "best_params": best_params,
            "best_edit_distance": round(best_individual.fitness.values[0], 2),
            "best_complexity": round(best_individual.fitness.values[1], 4),
        }

    results_path = OUTPUT_DIR / "ga_cluster_results_v6.json"
    with open(results_path, 'w') as f:
        json.dump(results, f, indent=2)

    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)

    for cluster_id, result in results.items():
        print(f"\nCluster {cluster_id}:")
        if result["status"] == "optimized":
            print(f"  Images: {result['n_images']}")
            print(f"  Best edit distance: {result['best_edit_distance']}")
            p = result['best_params']
            print(f"  Scan removal: {'Yes (morph)' if p['scan_removal_method']==1 else 'No'}")
            print(f"  Bilateral: {'Yes' if p['apply_bilateral']==1 else 'No'}")
        else:
            print(f"  Status: {result['status']}")

    print(f"\nResults saved to: {results_path}")
    print("=" * 70)

    return results


if __name__ == "__main__":
    main()
