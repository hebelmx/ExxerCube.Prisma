#!/usr/bin/env python3
"""
MEDIUM RUN - NSGA-II Filter Optimization (8 Hours)

Balanced optimization run:
- Population: 40
- Generations: 30
- Total evaluations: 1,200
- Estimated time: ~8 hours

Produces useful Pareto front for production decision.

Usage:
    python optimize_filters_nsga2_medium.py
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
    """Run MEDIUM (8-hour) optimization."""

    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*80)
    print("NSGA-II MEDIUM RUN - 8 HOUR OPTIMIZATION")
    print("="*80)
    print()
    print("Configuration:")
    print("  Population: 40")
    print("  Generations: 30")
    print("  Total evaluations: 1,200")
    print("  Estimated time: ~8 hours")
    print()
    print("Goal: Find useful Pareto front for production deployment")
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

    # Step 3: Configure NSGA-II algorithm (MEDIUM SETTINGS)
    print("Configuring NSGA-II algorithm (MEDIUM RUN)...")
    algorithm = NSGA2(
        pop_size=40,  # Balanced population size
        sampling=FloatRandomSampling(),
        crossover=SBX(prob=0.9, eta=15),
        mutation=PM(prob=0.1, eta=20),
        eliminate_duplicates=True
    )
    print("  Population size: 40")
    print("  Crossover: SBX (prob=0.9, eta=15)")
    print("  Mutation: PM (prob=0.1, eta=20)")
    print()

    # Step 4: Define termination criterion
    termination = get_termination("n_gen", 30)
    print("  Termination: 30 generations")
    print()

    # Step 5: Run optimization
    print("="*80)
    print("STARTING 8-HOUR OPTIMIZATION")
    print("="*80)
    print()
    print("Progress will be logged to: nsga2_progress.log")
    print()
    print("Estimated completion time: ~8 hours from now")
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
    print(f"MEDIUM RUN COMPLETE ({elapsed/3600:.2f} hours)")
    print("="*80)
    print()

    # Step 6: Extract Pareto front
    print("Extracting Pareto-optimal solutions...")
    pareto_X = res.X
    pareto_F = res.F

    print(f"  Pareto front size: {len(pareto_X)} solutions")
    print()

    # Step 7: Save results
    results_file = base_path / "nsga2_medium_pareto_front.json"

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

    print(f"Pareto front catalog saved to: {results_file}")
    print()

    # Step 8: Print top 10 solutions
    print("="*80)
    print("TOP 10 SOLUTIONS FROM PARETO FRONT")
    print("="*80)
    print()

    sorted_catalog = sorted(pareto_catalog, key=lambda x: x["total_edits_all"])

    for i, sol in enumerate(sorted_catalog[:10], 1):
        print(f"Solution #{i} (Total: {sol['total_edits_all']} edits, "
              f"Q1: {sol['total_edits_Q1']}, Q2: {sol['total_edits_Q2']})")
        print(f"  Parameters: h={sol['genome']['denoise_h']}, "
              f"CLAHE={sol['genome']['clahe_clip']:.2f}, "
              f"bilateral_d={sol['genome']['bilateral_d']}, "
              f"unsharp={sol['genome']['unsharp_amount']:.2f}")
        print(f"  Q2 333BBB (heartbreaker): {sol['objectives']['Q2_333BBB']} edits")
        print()

    # Step 9: Analysis - Find solution that rescues 333BBB
    print("="*80)
    print("SPECIAL ANALYSIS: 333BBB RESCUE POTENTIAL")
    print("="*80)
    print()

    # Current fixed enhancement: 333BBB Q2 = 431 edits
    baseline = 431

    best_333bbb = min(pareto_catalog, key=lambda x: x['objectives']['Q2_333BBB'])

    print(f"Best solution for Q2 333BBB:")
    print(f"  Edit distance: {best_333bbb['objectives']['Q2_333BBB']} "
          f"(baseline: {baseline})")
    print(f"  Improvement: {baseline - best_333bbb['objectives']['Q2_333BBB']} edits")
    print(f"  Parameters:")
    for param, value in best_333bbb['genome'].items():
        print(f"    {param}: {value}")
    print()

    # Step 10: Save checkpoint
    checkpoint_file = base_path / "nsga2_medium_checkpoint.pkl"
    with open(checkpoint_file, 'wb') as f:
        pickle.dump(res, f)

    print(f"Checkpoint saved to: {checkpoint_file}")
    print()

    # Step 11: Decision point
    print("="*80)
    print("DECISION POINT: FULL RUN NEEDED?")
    print("="*80)
    print()

    print("Evaluate the Pareto front quality:")
    print(f"  1. Pareto front size: {len(pareto_X)} solutions")
    print(f"  2. Best total edits: {sorted_catalog[0]['total_edits_all']}")
    print(f"  3. 333BBB rescue: {best_333bbb['objectives']['Q2_333BBB']} edits")
    print()
    print("If Pareto front is well-distributed and 333BBB is rescued,")
    print("the medium run may be sufficient!")
    print()
    print("Otherwise, run full optimization on powerful server:")
    print("  - Population: 100")
    print("  - Generations: 50")
    print("  - Time: ~33 hours")
    print()


if __name__ == "__main__":
    main()
