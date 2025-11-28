#!/usr/bin/env python3
"""
GA Per-Cluster Filter Optimization v5

For each cluster, find optimal filter parameters using:
- Fitness: Edit distance (lower is better)
- Expanded filter space: Contrast, Median, Sharpness, Brightness, UnsharpMask
- Multi-objective: minimize edit distance, minimize complexity

Uses DEAP for genetic algorithm.
"""

import subprocess
import json
import io
import random
import tempfile
from pathlib import Path
from typing import List, Dict, Tuple
from functools import partial

import numpy as np
from PIL import Image, ImageFilter, ImageEnhance
from deap import base, creator, tools, algorithms
import Levenshtein


# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
PRISTINE_DIR = BASE_PATH / "PRP1"
DEGRADED_DIR = BASE_PATH / "PRP1_Degraded_v6"
OUTPUT_DIR = BASE_PATH / "PRP1_GA_Results_v6"

CLUSTER_FILE = DEGRADED_DIR / "cluster_assignments.json"

# Pristine document files (for ground truth OCR)
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

# Filter parameter bounds [min, max]
PARAM_BOUNDS = {
    "contrast": (0.8, 1.5),
    "brightness": (0.9, 1.2),
    "sharpness": (0.8, 2.5),
    "median_size": (1, 5),      # Will use 2*n+1 for odd values
    "unsharp_radius": (0.5, 3.0),
    "unsharp_percent": (50, 200),
    "unsharp_threshold": (1, 5),
}


# ============================================================================
# Filter Functions
# ============================================================================

def apply_filters(image: Image.Image, params: Dict) -> Image.Image:
    """Apply filter chain with given parameters."""
    img = image.copy()
    if img.mode != 'RGB':
        img = img.convert('RGB')

    # 1. Brightness adjustment
    brightness = params.get("brightness", 1.0)
    if brightness != 1.0:
        img = ImageEnhance.Brightness(img).enhance(brightness)

    # 2. Contrast enhancement
    contrast = params.get("contrast", 1.0)
    if contrast != 1.0:
        img = ImageEnhance.Contrast(img).enhance(contrast)

    # 3. Median filter (denoise)
    median_size = int(params.get("median_size", 1))
    if median_size > 1:
        kernel_size = 2 * median_size + 1  # Ensure odd
        img = img.filter(ImageFilter.MedianFilter(size=kernel_size))

    # 4. Sharpness
    sharpness = params.get("sharpness", 1.0)
    if sharpness != 1.0:
        img = ImageEnhance.Sharpness(img).enhance(sharpness)

    # 5. Unsharp mask (if radius > threshold)
    unsharp_radius = params.get("unsharp_radius", 0.5)
    unsharp_percent = int(params.get("unsharp_percent", 50))
    unsharp_threshold = int(params.get("unsharp_threshold", 1))
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


# Cache for ground truth texts
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

def evaluate_individual(individual: List[float], cluster_images: List[Dict]) -> Tuple[float, float]:
    """
    Evaluate a filter configuration on all images in a cluster.

    Returns:
        (avg_edit_distance, filter_complexity)
    """
    # Decode individual to parameters
    params = {
        "contrast": individual[0],
        "brightness": individual[1],
        "sharpness": individual[2],
        "median_size": individual[3],
        "unsharp_radius": individual[4],
        "unsharp_percent": individual[5],
        "unsharp_threshold": individual[6],
    }

    total_edit_distance = 0
    count = 0

    for img_info in cluster_images:
        filename = img_info["filename"]
        doc_id = img_info["doc_id"]
        img_path = DEGRADED_DIR / filename

        if not img_path.exists():
            continue

        # Load degraded image
        degraded = Image.open(img_path)

        # Apply filters
        filtered = apply_filters(degraded, params)

        # OCR
        filtered_text = run_tesseract(filtered)

        # Get ground truth
        ground_truth = get_ground_truth(doc_id)

        # Calculate edit distance
        edit_dist = Levenshtein.distance(filtered_text, ground_truth)
        total_edit_distance += edit_dist
        count += 1

    avg_edit_distance = total_edit_distance / count if count > 0 else float('inf')

    # Calculate filter complexity (how much filtering is being applied)
    # Low complexity = parameters close to neutral (1.0, 1.0, 1.0, 1, 0.5, 50, 1)
    complexity = (
        abs(params["contrast"] - 1.0) +
        abs(params["brightness"] - 1.0) +
        abs(params["sharpness"] - 1.0) * 0.5 +
        (params["median_size"] - 1) * 0.3 +
        max(0, params["unsharp_radius"] - 0.5) * 0.2 +
        max(0, (params["unsharp_percent"] - 50) / 100) * 0.2
    )

    return (avg_edit_distance, complexity)


# ============================================================================
# GA Setup
# ============================================================================

def setup_ga():
    """Setup DEAP genetic algorithm."""
    # Create fitness and individual types
    if hasattr(creator, "FitnessMin"):
        del creator.FitnessMin
    if hasattr(creator, "Individual"):
        del creator.Individual

    # Minimize both objectives (edit distance, complexity)
    creator.create("FitnessMin", base.Fitness, weights=(-1.0, -0.1))
    creator.create("Individual", list, fitness=creator.FitnessMin)

    toolbox = base.Toolbox()

    # Attribute generators for each parameter
    bounds = list(PARAM_BOUNDS.values())

    def random_individual():
        return [
            random.uniform(bounds[0][0], bounds[0][1]),  # contrast
            random.uniform(bounds[1][0], bounds[1][1]),  # brightness
            random.uniform(bounds[2][0], bounds[2][1]),  # sharpness
            random.randint(bounds[3][0], bounds[3][1]),  # median_size
            random.uniform(bounds[4][0], bounds[4][1]),  # unsharp_radius
            random.uniform(bounds[5][0], bounds[5][1]),  # unsharp_percent
            random.randint(bounds[6][0], bounds[6][1]),  # unsharp_threshold
        ]

    toolbox.register("individual", tools.initIterate, creator.Individual, random_individual)
    toolbox.register("population", tools.initRepeat, list, toolbox.individual)

    # Genetic operators
    toolbox.register("select", tools.selTournament, tournsize=TOURNAMENT_SIZE)
    toolbox.register("mate", tools.cxBlend, alpha=0.5)

    def mutate(individual):
        for i in range(len(individual)):
            if random.random() < 0.3:  # Per-gene mutation probability
                if i == 3 or i == 6:  # Integer parameters
                    individual[i] = random.randint(bounds[i][0], bounds[i][1])
                else:
                    delta = (bounds[i][1] - bounds[i][0]) * 0.2
                    individual[i] += random.gauss(0, delta)
                    individual[i] = max(bounds[i][0], min(bounds[i][1], individual[i]))
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
    print("GA PER-CLUSTER FILTER OPTIMIZATION v5")
    print("=" * 70)
    print()

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # Load cluster assignments
    with open(CLUSTER_FILE) as f:
        cluster_data = json.load(f)

    n_clusters = cluster_data["clustering"]["n_clusters"]
    assignments = cluster_data["cluster_assignments"]

    print(f"Loaded {len(assignments)} images in {n_clusters} clusters")
    print()

    # Get ground truth for all documents first
    print("Loading ground truth OCR texts...")
    for doc_id in PRISTINE_DOCS:
        get_ground_truth(doc_id)
        print(f"  {doc_id}: {len(_ground_truth_cache[doc_id])} chars")
    print()

    # Group by cluster
    clusters = {i: [] for i in range(n_clusters)}
    for item in assignments:
        clusters[item["cluster"]].append(item)

    results = {}

    # Optimize each cluster
    for cluster_id in range(n_clusters):
        cluster_images = clusters[cluster_id]

        print(f"{'='*60}")
        print(f"CLUSTER {cluster_id}: {len(cluster_images)} images")
        print(f"{'='*60}")

        if len(cluster_images) < 3:
            print("Skipping - too few images for optimization")
            results[cluster_id] = {"status": "skipped", "reason": "too_few_images"}
            continue

        # Setup GA
        toolbox = setup_ga()
        eval_func = partial(evaluate_individual, cluster_images=cluster_images)
        toolbox.register("evaluate", eval_func)

        # Create population
        pop = toolbox.population(n=POPULATION_SIZE)

        # Evaluate initial population
        print(f"\nEvaluating initial population ({POPULATION_SIZE} individuals)...")
        fitnesses = list(map(toolbox.evaluate, pop))
        for ind, fit in zip(pop, fitnesses):
            ind.fitness.values = fit

        best_fitness = min(f[0] for f in fitnesses)
        print(f"Initial best edit distance: {best_fitness:.1f}")

        # Statistics
        stats = tools.Statistics(lambda ind: ind.fitness.values[0])
        stats.register("min", np.min)
        stats.register("avg", np.mean)
        stats.register("std", np.std)

        # Hall of fame
        hof = tools.HallOfFame(3)

        # Run GA
        print(f"\nRunning GA for {GENERATIONS} generations...")

        for gen in range(GENERATIONS):
            # Select offspring
            offspring = toolbox.select(pop, len(pop))
            offspring = list(map(toolbox.clone, offspring))

            # Apply crossover
            for child1, child2 in zip(offspring[::2], offspring[1::2]):
                if random.random() < CROSSOVER_PROB:
                    toolbox.mate(child1, child2)
                    del child1.fitness.values
                    del child2.fitness.values

            # Apply mutation
            for mutant in offspring:
                if random.random() < MUTATION_PROB:
                    toolbox.mutate(mutant)
                    del mutant.fitness.values

            # Evaluate offspring
            invalid_ind = [ind for ind in offspring if not ind.fitness.valid]
            fitnesses = list(map(toolbox.evaluate, invalid_ind))
            for ind, fit in zip(invalid_ind, fitnesses):
                ind.fitness.values = fit

            # Replace population
            pop[:] = offspring

            # Update hall of fame
            hof.update(pop)

            # Get stats
            record = stats.compile(pop)
            best = hof[0].fitness.values[0]

            if (gen + 1) % 5 == 0 or gen == 0:
                print(f"  Gen {gen+1:3d}: best={best:.1f}, avg={record['avg']:.1f}, std={record['std']:.1f}")

        # Results for this cluster
        best_individual = hof[0]
        best_params = {
            "contrast": round(best_individual[0], 3),
            "brightness": round(best_individual[1], 3),
            "sharpness": round(best_individual[2], 3),
            "median_size": int(best_individual[3]),
            "unsharp_radius": round(best_individual[4], 3),
            "unsharp_percent": int(best_individual[5]),
            "unsharp_threshold": int(best_individual[6]),
        }

        print(f"\nBest parameters for cluster {cluster_id}:")
        for k, v in best_params.items():
            print(f"  {k}: {v}")
        print(f"\nFinal edit distance: {best_individual.fitness.values[0]:.1f}")
        print(f"Filter complexity: {best_individual.fitness.values[1]:.3f}")

        results[cluster_id] = {
            "status": "optimized",
            "n_images": len(cluster_images),
            "best_params": best_params,
            "best_edit_distance": round(best_individual.fitness.values[0], 2),
            "best_complexity": round(best_individual.fitness.values[1], 4),
            "generations": GENERATIONS,
            "population_size": POPULATION_SIZE,
        }

    # Save results
    results_path = OUTPUT_DIR / "ga_cluster_results.json"
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
            print(f"  Best params: {result['best_params']}")
        else:
            print(f"  Status: {result['status']}")

    print(f"\nResults saved to: {results_path}")
    print("=" * 70)

    return results


if __name__ == "__main__":
    main()
