#!/usr/bin/env python3
"""
NSGA-II Cluster 0 PIL Pipeline Optimization

Cluster 0: Ultra-Sharp Images (555CCC Q0 + Q05)
- 2 objectives (2 images)
- Characteristics: blur=6905.9, noise=0.47, contrast=51.9

SIMPLE PIL PIPELINE:
- Contrast enhancement (1.0-2.5x)
- Median filter (size 3-7)

Configuration:
    Population: 20
    Generations: 20
    Total Evaluations: 400
    Estimated Runtime: ~40 minutes

Outputs:
- Pareto front catalog (cluster0_pil_pareto_front.json)
- Progress log (cluster0_pil_progress.log)

Usage:
    python optimize_cluster0_pil.py
"""

import numpy as np
import subprocess
import json
import time
from pathlib import Path
from typing import Dict, List
from dataclasses import dataclass
from PIL import Image, ImageEnhance, ImageFilter

from pymoo.core.problem import Problem
from pymoo.algorithms.moo.nsga2 import NSGA2
from pymoo.operators.crossover.sbx import SBX
from pymoo.operators.mutation.pm import PM
from pymoo.operators.sampling.rnd import FloatRandomSampling
from pymoo.optimize import minimize
from pymoo.termination import get_termination


@dataclass
class PILFilterGenome:
    """Simple PIL filter parameters."""
    contrast_factor: float   # 1.0-2.5
    median_size: int         # 3, 5, 7


def levenshtein_distance(s1: str, s2: str) -> int:
    """Compute Levenshtein distance (minimum edit operations)."""
    if len(s1) < len(s2):
        return levenshtein_distance(s2, s1)
    if len(s2) == 0:
        return len(s1)

    previous_row = range(len(s2) + 1)
    for i, c1 in enumerate(s1):
        current_row = [i + 1]
        for j, c2 in enumerate(s2):
            insertions = previous_row[j + 1] + 1
            deletions = current_row[j] + 1
            substitutions = previous_row[j] + (c1 != c2)
            current_row.append(min(insertions, deletions, substitutions))
        previous_row = current_row

    return previous_row[-1]


def normalize_text(text: str) -> str:
    """Normalize OCR text for comparison."""
    import re
    text = text.lower()
    text = re.sub(r'\s+', ' ', text)
    return text.strip()


def apply_pil_filters(image_path: Path, genome: PILFilterGenome) -> Image.Image:
    """Apply PIL filter pipeline."""
    image = Image.open(image_path)

    if image.mode != 'L':
        image = image.convert('L')

    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(genome.contrast_factor)

    image = image.filter(ImageFilter.MedianFilter(size=genome.median_size))

    return image


def run_tesseract_ocr(image_path: Path, lang: str = "spa", psm: int = 6) -> str:
    """Run Tesseract OCR on an image."""
    import platform

    if platform.system() == "Windows":
        cmd = [
            "C:/Program Files/Tesseract-OCR/tesseract.exe",
            "--tessdata-dir", "C:/Program Files/Tesseract-OCR/tessdata",
            str(image_path),
            "stdout",
            "-l", lang,
            "--psm", str(psm)
        ]
    else:
        cmd = [
            "tesseract",
            str(image_path),
            "stdout",
            "-l", lang,
            "--psm", str(psm)
        ]

    try:
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
        return result.stdout
    except Exception as e:
        return ""


def load_ground_truth(base_path: Path) -> Dict[str, str]:
    """Load ground truth text from pristine documents."""
    ground_truth = {}
    pristine_base = base_path / "PRP1"

    doc = "555CCC-66666662025_page1.png"
    doc_path = pristine_base / doc
    if doc_path.exists():
        text = run_tesseract_ocr(doc_path)
        ground_truth[doc] = text

    return ground_truth


class Cluster0PILOptimizationProblem(Problem):
    """
    Cluster 0 PIL pipeline optimization.

    2 Objectives (Ultra-Sharp 555CCC):
    - Q0_Pristine (minimize edit distance)
    - Q05_VeryGood (minimize edit distance)

    Cluster Characteristics:
    - Ultra-sharp (blur=6905.9)
    - Pristine quality (noise=0.47)
    - High contrast (51.9)
    """

    def __init__(self, base_path: Path, ground_truth: Dict[str, str]):
        self.base_path = base_path
        self.ground_truth = ground_truth

        # Cluster 0: 555CCC at Q0 and Q05
        self.test_cases = [
            {
                'doc': "555CCC-66666662025_page1.png",
                'level': "Q0_Pristine",
                'path': base_path / "PRP1_Degraded" / "Q0_Pristine" / "555CCC-66666662025_page1.png"
            },
            {
                'doc': "555CCC-66666662025_page1.png",
                'level': "Q05_VeryGood",
                'path': base_path / "PRP1_Degraded" / "Q05_VeryGood" / "555CCC-66666662025_page1.png"
            }
        ]

        self.eval_count = 0
        self.log_file = base_path / "cluster0_pil_progress.log"

        # 2 parameters: contrast_factor, median_size
        super().__init__(
            n_var=2,
            n_obj=2,  # 2 objectives (Q0 + Q05 for 555CCC)
            xl=np.array([1.0, 3]),
            xu=np.array([2.5, 7])
        )

    def _evaluate(self, X, out, *args, **kwargs):
        objectives = []

        for x in X:
            median_size = int(x[1])
            if median_size % 2 == 0:
                median_size += 1
            median_size = max(3, min(7, median_size))

            genome = PILFilterGenome(
                contrast_factor=float(x[0]),
                median_size=median_size
            )

            doc_objectives = []

            for test_case in self.test_cases:
                if not test_case['path'].exists():
                    doc_objectives.append(9999)
                    continue

                enhanced_img = apply_pil_filters(test_case['path'], genome)

                temp_path = self.base_path / f"temp_ocr_c0pil_{self.eval_count}.png"
                enhanced_img.save(temp_path)

                ocr_text = run_tesseract_ocr(temp_path)

                gt_text = self.ground_truth.get(test_case['doc'], "")
                distance = levenshtein_distance(
                    normalize_text(gt_text),
                    normalize_text(ocr_text)
                )

                doc_objectives.append(distance)

                if temp_path.exists():
                    temp_path.unlink()

                self.eval_count += 1

            objectives.append(doc_objectives)

            if len(doc_objectives) == 2:
                with open(self.log_file, 'a') as f:
                    f.write(f"Eval {self.eval_count}: {doc_objectives}\n")

        out["F"] = np.array(objectives)


def main():
    """Run Cluster 0 PIL pipeline NSGA-II optimization."""

    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*80)
    print("NSGA-II CLUSTER 0 PIL PIPELINE OPTIMIZATION")
    print("="*80)
    print()
    print("Cluster 0 Characteristics:")
    print("  Ultra-sharp images (blur=6905.9)")
    print("  Pristine quality (noise=0.47)")
    print("  High contrast (51.9)")
    print()
    print("Configuration:")
    print("  Pipeline: PIL (Contrast + Median Filter)")
    print("  Parameters: 2")
    print("  Population: 20")
    print("  Generations: 20")
    print("  Total evaluations: 400")
    print("  Estimated time: ~40 minutes")
    print()
    print("2 Objectives:")
    print("  - 555CCC Q0_Pristine")
    print("  - 555CCC Q05_VeryGood")
    print("="*80)
    print()

    # Load ground truth
    print("Loading ground truth...")
    ground_truth = load_ground_truth(base_path)
    print(f"  Loaded {len(ground_truth)} ground truth documents")
    print()

    # Setup optimization problem
    print("Setting up Cluster 0 PIL optimization problem...")
    problem = Cluster0PILOptimizationProblem(base_path, ground_truth)
    print()

    # Configure NSGA-II algorithm
    print("Configuring NSGA-II algorithm...")
    algorithm = NSGA2(
        pop_size=20,
        sampling=FloatRandomSampling(),
        crossover=SBX(prob=0.9, eta=15),
        mutation=PM(eta=20),
        eliminate_duplicates=True
    )
    print()

    # Run optimization
    print("="*80)
    print("STARTING CLUSTER 0 PIL OPTIMIZATION")
    print("="*80)
    print()

    start_time = time.time()

    res = minimize(
        problem,
        algorithm,
        termination=get_termination("n_gen", 20),
        seed=1,
        verbose=True,
        save_history=True
    )

    elapsed_time = time.time() - start_time

    print()
    print("="*80)
    print(f"CLUSTER 0 PIL OPTIMIZATION COMPLETE ({elapsed_time/60:.1f} minutes)")
    print("="*80)
    print()

    # Extract results
    print("Extracting results...")

    pareto_solutions = []

    for i, (x, f) in enumerate(zip(res.X, res.F)):
        median_size = int(x[1])
        if median_size % 2 == 0:
            median_size += 1
        median_size = max(3, min(7, median_size))

        genome = PILFilterGenome(
            contrast_factor=float(x[0]),
            median_size=median_size
        )

        solution = {
            "id": i,
            "cluster": 0,
            "genome": {
                "contrast_factor": genome.contrast_factor,
                "median_size": genome.median_size
            },
            "objectives": {
                "Q0_555CCC": int(f[0]),
                "Q05_555CCC": int(f[1])
            },
            "total_edits": int(f.sum())
        }

        pareto_solutions.append(solution)

    print(f"  Pareto front size: {len(pareto_solutions)} solutions")
    print()

    # Save results
    output_file = base_path / "cluster0_pil_pareto_front.json"
    with open(output_file, 'w') as f:
        json.dump(pareto_solutions, f, indent=2)

    print(f"Results saved to: {output_file}")
    print()

    # Best solution analysis
    print("="*80)
    print("BEST CLUSTER 0 PIL SOLUTION")
    print("="*80)
    print()

    best_total = min(pareto_solutions, key=lambda x: x['total_edits'])
    print(f"Best Total Edits: {best_total['total_edits']}")
    print(f"  Parameters: {best_total['genome']}")
    print(f"  Objectives: {best_total['objectives']}")
    print()

    print("="*80)
    print("✓ CLUSTER 0 PIL OPTIMIZATION COMPLETE")
    print("="*80)


if __name__ == "__main__":
    main()
