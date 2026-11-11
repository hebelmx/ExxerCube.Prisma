#!/usr/bin/env python3
"""
Validation GA with DISCRETE median_size

FIX: median_size is a discrete Choice variable (3, 5, 7)
     not a continuous variable that gets rounded.

This ensures equal probability of exploring each median value.
"""

import numpy as np
import subprocess
import json
import time
from pathlib import Path
from dataclasses import dataclass
from PIL import Image, ImageEnhance, ImageFilter

from pymoo.core.problem import ElementwiseProblem
from pymoo.core.variable import Real, Choice
from pymoo.core.mixed import MixedVariableGA
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


class DiscreteValidationProblem(ElementwiseProblem):
    """Problem with DISCRETE median_size using pymoo's Choice variable."""

    def __init__(self, base_path: Path, ground_truth: str):
        self.base_path = base_path
        self.ground_truth = ground_truth
        self.test_image = base_path / "PRP1_Degraded" / "Q2_MediumPoor" / "555CCC-66666662025_page1.png"
        self.eval_count = 0
        self.log_file = base_path / "validation_ga_discrete_params.log"

        # Clear log file
        with open(self.log_file, 'w') as f:
            f.write("# Validation GA (Discrete) Parameter Log\n")
            f.write("# eval_id, contrast_factor, median_size, edit_distance\n")

        # Define variables: Real for contrast, Choice for median
        variables = {
            "contrast_factor": Real(bounds=(1.0, 2.5)),
            "median_size": Choice(options=[3, 5, 7]),  # DISCRETE!
        }

        super().__init__(vars=variables, n_obj=1)

    def _evaluate(self, X, out, *args, **kwargs):
        self.eval_count += 1

        # X is a dict with variable names
        contrast_factor = float(X["contrast_factor"])
        median_size = int(X["median_size"])  # Already discrete!

        genome = PILFilterGenome(contrast_factor, median_size)

        # Apply filter and OCR
        enhanced = apply_pil_filter(self.test_image, genome)
        temp_path = self.base_path / f"temp_val_disc_{self.eval_count}.png"
        enhanced.save(temp_path)
        ocr_text = run_tesseract_ocr(temp_path)
        distance = levenshtein_distance(
            normalize_text(self.ground_truth),
            normalize_text(ocr_text)
        )
        temp_path.unlink()

        # LOG THE PARAMETERS
        with open(self.log_file, 'a') as f:
            f.write(f"{self.eval_count}, {contrast_factor:.4f}, {median_size}, {distance}\n")

        print(f"  Eval {self.eval_count}: contrast={contrast_factor:.3f}, median={median_size}, dist={distance}")

        out["F"] = [distance]


def main():
    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*70)
    print("VALIDATION GA: DISCRETE median_size (Choice variable)")
    print("="*70)
    print()
    print("Config: Pop=12, Gen=5 (~60 evals)")
    print("Variables:")
    print("  - contrast_factor: Real [1.0, 2.5]")
    print("  - median_size: Choice [3, 5, 7]  <-- DISCRETE!")
    print()

    # Get ground truth
    print("Loading ground truth...")
    pristine_path = base_path / "PRP1" / "555CCC-66666662025_page1.png"
    ground_truth = run_tesseract_ocr(pristine_path)
    print(f"Ground truth length: {len(ground_truth)} chars")
    print()

    # Create problem
    problem = DiscreteValidationProblem(base_path, ground_truth)

    # Algorithm for mixed variables (handles Real + Choice automatically)
    algorithm = MixedVariableGA(pop_size=12)

    print("Starting optimization...")
    print("-"*70)

    start_time = time.time()
    res = minimize(
        problem,
        algorithm,
        termination=get_termination("n_gen", 5),
        seed=42,
        verbose=False,
    )
    elapsed = time.time() - start_time

    print("-"*70)
    print(f"\nCompleted in {elapsed:.1f} seconds")
    print()

    # Analyze results
    print("="*70)
    print("ANALYSIS: Median size distribution")
    print("="*70)

    log_file = base_path / "validation_ga_discrete_params.log"
    median_counts = {3: 0, 5: 0, 7: 0}
    median_best = {3: 9999, 5: 9999, 7: 9999}

    with open(log_file) as f:
        for line in f:
            if line.startswith("#"):
                continue
            parts = line.strip().split(",")
            if len(parts) >= 4:
                median = int(parts[2].strip())
                dist = int(parts[3].strip())
                if median in median_counts:
                    median_counts[median] += 1
                    median_best[median] = min(median_best[median], dist)

    print()
    total = sum(median_counts.values())
    print("Median_size exploration counts:")
    for m in [3, 5, 7]:
        count = median_counts[m]
        pct = count / total * 100 if total > 0 else 0
        best = median_best[m] if median_best[m] < 9999 else "N/A"
        bar = "█" * int(pct / 5)
        print(f"  median={m}: {count:3d} ({pct:5.1f}%) best_dist={best} {bar}")

    # Verify equal exploration
    print()
    min_count = min(median_counts.values())
    max_count = max(median_counts.values())

    if max_count > 0 and min_count / max_count > 0.5:
        print(">>> SUCCESS: Median values are being explored roughly equally!")
    else:
        print(">>> WARNING: Unequal exploration of median values")

    # Save Pareto front
    print()
    print("="*70)
    print("PARETO FRONT SOLUTIONS")
    print("="*70)

    solutions = []
    for i, (x, f) in enumerate(zip(res.X, res.F)):
        contrast = float(x["contrast_factor"])
        median = int(x["median_size"])
        dist = int(f[0])
        solutions.append({
            "id": i,
            "contrast_factor": contrast,
            "median_size": median,
            "edit_distance": dist,
        })
        print(f"  Solution {i}: contrast={contrast:.4f}, median={median}, dist={dist}")

    pareto_file = base_path / "validation_ga_discrete_pareto.json"
    with open(pareto_file, 'w') as f:
        json.dump(solutions, f, indent=2)

    print()
    print(f"Pareto front saved to: {pareto_file}")
    print(f"Parameter log saved to: {log_file}")


if __name__ == "__main__":
    main()
