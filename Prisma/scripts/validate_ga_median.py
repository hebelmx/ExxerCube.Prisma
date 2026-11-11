#!/usr/bin/env python3
"""
Validation GA: Verify median_size varies in search space

Small GA run to confirm:
1. median_size is being explored (3, 5, 7)
2. Parameters are logged at each evaluation
3. Checkpoints are saved periodically

Configuration:
    Population: 10
    Generations: 5
    Total Evaluations: ~50
    Runtime: ~5 minutes
"""

import numpy as np
import subprocess
import json
import time
from pathlib import Path
from dataclasses import dataclass
from PIL import Image, ImageEnhance, ImageFilter

from pymoo.core.problem import Problem
from pymoo.core.callback import Callback
from pymoo.algorithms.moo.nsga2 import NSGA2
from pymoo.operators.crossover.sbx import SBX
from pymoo.operators.mutation.pm import PM
from pymoo.operators.sampling.rnd import FloatRandomSampling
from pymoo.optimize import minimize
from pymoo.termination import get_termination


@dataclass
class PILFilterGenome:
    contrast_factor: float
    median_size: int


def levenshtein_distance(s1: str, s2: str) -> int:
    if len(s1) < len(s2):
        return levenshtein_distance(s2, s1)
    if len(s2) == 0:
        return len(s1)
    prev = range(len(s2) + 1)
    for i, c1 in enumerate(s1):
        curr = [i + 1]
        for j, c2 in enumerate(s2):
            curr.append(min(prev[j + 1] + 1, curr[j] + 1, prev[j] + (c1 != c2)))
        prev = curr
    return prev[-1]


def normalize_text(text: str) -> str:
    import re
    return re.sub(r'\s+', ' ', text.lower()).strip()


def run_tesseract_ocr(image_path: Path) -> str:
    try:
        return subprocess.run(
            ["tesseract", str(image_path), "stdout", "-l", "spa", "--psm", "6"],
            capture_output=True, text=True, timeout=60
        ).stdout
    except:
        return ""


def apply_pil_filter(image_path: Path, genome: PILFilterGenome) -> Image.Image:
    image = Image.open(image_path)
    if image.mode != 'L':
        image = image.convert('L')
    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(genome.contrast_factor)
    image = image.filter(ImageFilter.MedianFilter(size=genome.median_size))
    return image


class CheckpointCallback(Callback):
    """Save checkpoint after each generation."""

    def __init__(self, base_path: Path):
        super().__init__()
        self.base_path = base_path
        self.checkpoint_file = base_path / "validation_ga_checkpoint.json"

    def notify(self, algorithm):
        gen = algorithm.n_gen
        pop = algorithm.pop

        # Save current population
        solutions = []
        for i, ind in enumerate(pop):
            x = ind.X
            f = ind.F

            median_size = int(x[1])
            if median_size % 2 == 0:
                median_size += 1
            median_size = max(3, min(7, median_size))

            solutions.append({
                "id": i,
                "raw_x": x.tolist(),
                "contrast_factor": float(x[0]),
                "median_size": median_size,
                "objectives": f.tolist() if f is not None else None,
            })

        checkpoint = {
            "generation": gen,
            "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
            "population": solutions,
        }

        with open(self.checkpoint_file, 'w') as f:
            json.dump(checkpoint, f, indent=2)

        print(f"  [Checkpoint] Gen {gen} saved")


class ValidationProblem(Problem):
    """Single-objective problem for quick validation."""

    def __init__(self, base_path: Path, ground_truth: str):
        self.base_path = base_path
        self.ground_truth = ground_truth
        self.test_image = base_path / "PRP1_Degraded" / "Q2_MediumPoor" / "555CCC-66666662025_page1.png"
        self.eval_count = 0
        self.log_file = base_path / "validation_ga_params.log"

        # Clear log file
        with open(self.log_file, 'w') as f:
            f.write("# Validation GA Parameter Log\n")
            f.write("# eval_id, contrast_factor, raw_median, processed_median, edit_distance\n")

        # Single objective for speed
        super().__init__(n_var=2, n_obj=1, xl=np.array([1.0, 3]), xu=np.array([2.5, 7]))

    def _evaluate(self, X, out, *args, **kwargs):
        objectives = []

        for x in X:
            self.eval_count += 1

            # Raw values from optimizer
            raw_contrast = float(x[0])
            raw_median = float(x[1])

            # Process median (must be odd, in range 3-7)
            median_size = int(x[1])
            if median_size % 2 == 0:
                median_size += 1
            median_size = max(3, min(7, median_size))

            genome = PILFilterGenome(raw_contrast, median_size)

            # Apply filter and OCR
            enhanced = apply_pil_filter(self.test_image, genome)
            temp_path = self.base_path / f"temp_val_{self.eval_count}.png"
            enhanced.save(temp_path)
            ocr_text = run_tesseract_ocr(temp_path)
            distance = levenshtein_distance(
                normalize_text(self.ground_truth),
                normalize_text(ocr_text)
            )
            temp_path.unlink()

            # LOG THE PARAMETERS
            with open(self.log_file, 'a') as f:
                f.write(f"{self.eval_count}, {raw_contrast:.4f}, {raw_median:.4f}, {median_size}, {distance}\n")

            objectives.append([distance])

            # Print every evaluation
            print(f"  Eval {self.eval_count}: contrast={raw_contrast:.3f}, median_raw={raw_median:.2f}→{median_size}, dist={distance}")

        out["F"] = np.array(objectives)


def main():
    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*70)
    print("VALIDATION GA: Verify median_size exploration")
    print("="*70)
    print()
    print("Config: Pop=10, Gen=5 (~50 evals, ~5 minutes)")
    print("Search space: contrast [1.0, 2.5], median [3, 7]")
    print()

    # Get ground truth
    print("Loading ground truth...")
    pristine_path = base_path / "PRP1" / "555CCC-66666662025_page1.png"
    ground_truth = run_tesseract_ocr(pristine_path)
    print(f"Ground truth length: {len(ground_truth)} chars")
    print()

    # Create problem and algorithm
    problem = ValidationProblem(base_path, ground_truth)

    algorithm = NSGA2(
        pop_size=10,
        sampling=FloatRandomSampling(),
        crossover=SBX(prob=0.9, eta=15),
        mutation=PM(eta=20),
        eliminate_duplicates=True
    )

    callback = CheckpointCallback(base_path)

    print("Starting optimization...")
    print("-"*70)

    start_time = time.time()
    res = minimize(
        problem,
        algorithm,
        termination=get_termination("n_gen", 5),
        seed=42,
        verbose=False,
        callback=callback
    )
    elapsed = time.time() - start_time

    print("-"*70)
    print(f"\nCompleted in {elapsed:.1f} seconds")
    print()

    # Analyze results
    print("="*70)
    print("ANALYSIS: Median size distribution")
    print("="*70)

    log_file = base_path / "validation_ga_params.log"
    median_counts = {3: 0, 5: 0, 7: 0}

    with open(log_file) as f:
        for line in f:
            if line.startswith("#"):
                continue
            parts = line.strip().split(",")
            if len(parts) >= 4:
                median = int(parts[3].strip())
                if median in median_counts:
                    median_counts[median] += 1

    print()
    print("Processed median_size counts:")
    for m, count in sorted(median_counts.items()):
        pct = count / sum(median_counts.values()) * 100 if sum(median_counts.values()) > 0 else 0
        bar = "█" * int(pct / 5)
        print(f"  median={m}: {count:3d} ({pct:5.1f}%) {bar}")

    # Check raw values
    print()
    print("Raw median values from optimizer (sample):")
    with open(log_file) as f:
        lines = [l for l in f if not l.startswith("#")]
        for line in lines[:10]:
            parts = line.strip().split(",")
            if len(parts) >= 4:
                raw = float(parts[2].strip())
                processed = int(parts[3].strip())
                print(f"  raw={raw:.2f} → processed={processed}")

    # Conclusion
    print()
    print("="*70)
    if median_counts[5] > 0 or median_counts[7] > 0:
        print("RESULT: Median IS varying in search space!")
        print("        The GA is correctly exploring different values.")
    else:
        print("RESULT: Median NOT varying - all converted to 3")
        print("        Need to investigate the search space bounds.")
    print("="*70)

    # Save final Pareto front
    pareto_file = base_path / "validation_ga_pareto.json"
    solutions = []
    for i, (x, f) in enumerate(zip(res.X, res.F)):
        median_size = int(x[1])
        if median_size % 2 == 0:
            median_size += 1
        median_size = max(3, min(7, median_size))
        solutions.append({
            "id": i,
            "contrast_factor": float(x[0]),
            "median_size": median_size,
            "edit_distance": int(f[0]),
        })

    with open(pareto_file, 'w') as f:
        json.dump(solutions, f, indent=2)

    print(f"\nPareto front saved to: {pareto_file}")
    print(f"Parameter log saved to: {log_file}")


if __name__ == "__main__":
    main()
