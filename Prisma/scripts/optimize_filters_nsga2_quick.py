#!/usr/bin/env python3
"""
QUICK TEST VERSION - NSGA-II Filter Optimization

Validation run with minimal population/generations:
- Population: 5
- Generations: 2
- Total evaluations: 10
- Estimated time: ~5 minutes

Usage:
    python optimize_filters_nsga2_quick.py
"""

import sys
from pathlib import Path

# Import the full optimizer
sys.path.insert(0, str(Path(__file__).parent))
from optimize_filters_nsga2 import (
    OCRFilterOptimizationProblem,
    load_ground_truth,
    NSGA2, SBX, PM, FloatRandomSampling,
    minimize, get_termination,
    json, np, pickle, time
)


def main():
    """Run QUICK TEST optimization."""

    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*80)
    print("NSGA-II QUICK TEST - ENVIRONMENT VALIDATION")
    print("="*80)
    print()
    print("Configuration:")
    print("  Population: 5")
    print("  Generations: 2")
    print("  Total evaluations: 10")
    print("  Estimated time: ~5 minutes")
    print()
    print("Goal: Validate environment, libraries, OCR pipeline")
    print("="*80)
    print()

    # Step 1: Load ground truth
    print("Loading ground truth from pristine documents...")
    ground_truth = load_ground_truth(base_path)
    print(f"  Loaded {len(ground_truth)} ground truth documents")
    print()

    # Step 2: Setup optimization problem
    print("Setting up NSGA-II optimization problem...")
    problem = OCRFilterOptimizationProblem(base_path, ground_truth)
    print(f"  Decision variables: {problem.n_var}")
    print(f"  Objectives: {problem.n_obj}")
    print()

    # Step 3: Configure NSGA-II algorithm (QUICK SETTINGS)
    print("Configuring NSGA-II algorithm (QUICK TEST)...")
    algorithm = NSGA2(
        pop_size=5,  # VERY SMALL for quick test
        sampling=FloatRandomSampling(),
        crossover=SBX(prob=0.9, eta=15),
        mutation=PM(prob=0.1, eta=20),
        eliminate_duplicates=True
    )
    print("  Population size: 5")
    print("  Generations: 2")
    print()

    # Step 4: Define termination criterion
    termination = get_termination("n_gen", 2)  # ONLY 2 generations
    print()

    # Step 5: Run optimization
    print("="*80)
    print("STARTING QUICK TEST OPTIMIZATION")
    print("="*80)
    print()

    start_time = time.time()

    res = minimize(
        problem,
        algorithm,
        termination,
        seed=42,
        verbose=True,
        save_history=True
    )

    elapsed = time.time() - start_time

    print()
    print("="*80)
    print(f"QUICK TEST COMPLETE ({elapsed/60:.2f} minutes)")
    print("="*80)
    print()

    # Step 6: Extract results
    print("Extracting results...")
    pareto_X = res.X
    pareto_F = res.F

    print(f"  Pareto front size: {len(pareto_X)} solutions")
    print()

    # Step 7: Save quick test results
    results_file = base_path / "nsga2_quick_test_results.json"

    pareto_catalog = []
    for i, (x, f) in enumerate(zip(pareto_X, pareto_F)):
        solution = {
            "id": i,
            "genome": {
                "denoise_h": int(x[0]),
                "clahe_clip": float(x[1]),
                "bilateral_d": int(x[2]) | 1,
                "bilateral_sigma_color": int(x[3]),
                "bilateral_sigma_space": int(x[4]),
                "unsharp_amount": float(x[5]),
                "unsharp_radius": float(x[6])
            },
            "objectives": {
                "Q1_222AAA": int(f[0]),
                "Q1_333BBB": int(f[1]),
                "Q1_333ccc": int(f[2]),
                "Q1_555CCC": int(f[3]),
                "Q2_222AAA": int(f[4]),
                "Q2_333BBB": int(f[5]),
                "Q2_333ccc": int(f[6]),
                "Q2_555CCC": int(f[7])
            },
            "total_edits_Q1": int(sum(f[0:4])),
            "total_edits_Q2": int(sum(f[4:8])),
            "total_edits_all": int(sum(f))
        }
        pareto_catalog.append(solution)

    with open(results_file, 'w') as f:
        json.dump(pareto_catalog, f, indent=2)

    print(f"Quick test results saved to: {results_file}")
    print()

    # Step 8: Print best solution
    print("="*80)
    print("BEST SOLUTION FROM QUICK TEST")
    print("="*80)
    print()

    sorted_catalog = sorted(pareto_catalog, key=lambda x: x["total_edits_all"])

    if sorted_catalog:
        best = sorted_catalog[0]
        print(f"Total edits: {best['total_edits_all']}")
        print(f"  Q1 total: {best['total_edits_Q1']} edits")
        print(f"  Q2 total: {best['total_edits_Q2']} edits")
        print()
        print("Parameters:")
        for param, value in best['genome'].items():
            print(f"  {param}: {value}")
        print()

    print("="*80)
    print("ENVIRONMENT VALIDATION: SUCCESS")
    print("="*80)
    print()
    print("Next step: Run medium optimization (8 hours)")
    print("  python optimize_filters_nsga2_medium.py")
    print()


if __name__ == "__main__":
    main()
