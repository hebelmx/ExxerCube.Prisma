#!/usr/bin/env python3
"""
NSGA-II Q2-ONLY PIL Pipeline Optimization.

4 Objectives (minimize Levenshtein distance):
- Q2_MediumPoor: 222AAA, 333BBB, 333ccc, 555CCC

SIMPLE PIL PIPELINE (matches baseline that achieved 1,219 edits):
- Contrast enhancement (1.0-2.5x)
- Median filter (size 3-7)

Configuration:
    Population: 20
    Generations: 30
    Total Evaluations: 600
    Estimated Runtime: ~2 hours

Only 2 parameters to optimize (vs 7 for OpenCV) = faster convergence!

Outputs:
- Pareto front catalog (nsga2_q2_pil_pareto_front.json)
- Progress log (nsga2_q2_pil_progress.log)
- Checkpoint file (nsga2_q2_pil_checkpoint.pkl)

Requirements:
    pip install pymoo pillow

Usage:
    python optimize_filters_nsga2_q2_pil.py
"""

import numpy as np
import subprocess
import json
import time
from pathlib import Path
from typing import Dict, List
from dataclasses import dataclass
import pickle
from PIL import Image, ImageEnhance, ImageFilter

# Multi-objective optimization library
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
    """
    Apply PIL filter pipeline (matches baseline approach).

    Pipeline:
    1. Grayscale conversion
    2. Contrast enhancement
    3. Median filter (denoising)
    """
    # Load image
    image = Image.open(image_path)

    # Convert to grayscale
    if image.mode != 'L':
        image = image.convert('L')

    # Contrast enhancement
    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(genome.contrast_factor)

    # Median filter (denoising)
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
        # Linux/macOS - use system tesseract
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

    docs = [
        "222AAA-44444444442025_page-0001.jpg",
        "333BBB-44444444442025_page1.png",
        "333ccc-6666666662025_page1.png",
        "555CCC-66666662025_page1.png"
    ]

    for doc in docs:
        doc_path = pristine_base / doc
        if doc_path.exists():
            text = run_tesseract_ocr(doc_path)
            ground_truth[doc] = text

    return ground_truth


class Q2PILFilterOptimizationProblem(Problem):
    """
    Q2-ONLY PIL pipeline optimization.

    4 Objectives (Q2_MediumPoor only):
    - 222AAA (minimize edit distance)
    - 333BBB (minimize edit distance) - THE HEARTBREAKER
    - 333ccc (minimize edit distance)
    - 555CCC (minimize edit distance)

    SIMPLE PIPELINE - Only 2 parameters!
    """

    def __init__(self, base_path: Path, ground_truth: Dict[str, str]):
        """
        Initialize Q2-only PIL optimization problem.

        Args:
            base_path: Path to Fixtures directory
            ground_truth: Dictionary of pristine OCR text
        """
        self.base_path = base_path
        self.ground_truth = ground_truth

        # Document sets (Q2 ONLY)
        self.doc_names = [
            "222AAA-44444444442025_page-0001.jpg",
            "333BBB-44444444442025_page1.png",
            "333ccc-6666666662025_page1.png",
            "555CCC-66666662025_page1.png"
        ]

        # Q2 only!
        self.quality_level = "Q2_MediumPoor"

        # Degraded image paths (we'll load on demand)
        self.degraded_dir = base_path / "PRP1_Degraded" / self.quality_level

        # Evaluation counter
        self.eval_count = 0
        self.log_file = base_path / "nsga2_q2_pil_progress.log"

        # Decision variables (2 parameters ONLY!)
        # [contrast_factor, median_size]
        super().__init__(
            n_var=2,
            n_obj=4,  # 4 objectives (Q2 documents ONLY)
            xl=np.array([1.0, 3]),       # Lower bounds
            xu=np.array([2.5, 7])        # Upper bounds
        )

    def _evaluate(self, X, out, *args, **kwargs):
        """
        Evaluate fitness for each solution in population.

        Args:
            X: Population array (n_solutions × 2 parameters)
            out: Output dictionary to fill with objectives
        """
        objectives = []

        for x in X:
            # Decode genome
            median_size = int(x[1])
            # Ensure median size is odd
            if median_size % 2 == 0:
                median_size += 1
            # Clamp to valid values
            median_size = max(3, min(7, median_size))

            genome = PILFilterGenome(
                contrast_factor=float(x[0]),
                median_size=median_size
            )

            # Evaluate on 4 Q2 documents
            doc_objectives = []

            for doc in self.doc_names:
                degraded_path = self.degraded_dir / doc

                if not degraded_path.exists():
                    doc_objectives.append(9999)  # Penalty for missing
                    continue

                # Apply PIL filters
                enhanced_img = apply_pil_filters(degraded_path, genome)

                # Save temporary image for OCR
                temp_path = self.base_path / f"temp_ocr_pil_{self.eval_count}.png"
                enhanced_img.save(temp_path)

                # Run OCR
                ocr_text = run_tesseract_ocr(temp_path)

                # Compute Levenshtein distance to ground truth
                gt_text = self.ground_truth.get(doc, "")
                distance = levenshtein_distance(
                    normalize_text(gt_text),
                    normalize_text(ocr_text)
                )

                doc_objectives.append(distance)

                # Cleanup
                if temp_path.exists():
                    temp_path.unlink()

                self.eval_count += 1

            objectives.append(doc_objectives)

            # Log progress every 4 evaluations (1 individual)
            if len(doc_objectives) == 4:
                with open(self.log_file, 'a') as f:
                    f.write(f"Eval {self.eval_count}: {doc_objectives}\n")

        out["F"] = np.array(objectives)


def main():
    """Run Q2-ONLY PIL pipeline NSGA-II optimization."""

    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*80)
    print("NSGA-II Q2-ONLY PIL PIPELINE OPTIMIZATION")
    print("="*80)
    print()
    print("Configuration:")
    print("  Pipeline: PIL (Contrast + Median Filter)")
    print("  Parameters: 2 (vs 7 for OpenCV)")
    print("  Population: 20")
    print("  Generations: 30")
    print("  Total evaluations: 600")
    print("  Estimated time: ~2 hours")
    print()
    print("4 Objectives (Q2_MediumPoor ONLY):")
    print("  - 222AAA (rescue degraded)")
    print("  - 333BBB (THE HEARTBREAKER - current: 431 edits)")
    print("  - 333ccc (rescue degraded)")
    print("  - 555CCC (rescue degraded)")
    print()
    print("This matches the BASELINE APPROACH that achieved 1,219 edits!")
    print("="*80)
    print()

    # Step 1: Load ground truth
    print("Loading ground truth from pristine documents...")
    ground_truth = load_ground_truth(base_path)
    print(f"  Loaded {len(ground_truth)} ground truth documents")
    print()

    # Step 2: Setup optimization problem
    print("Setting up Q2-only PIL optimization problem...")
    problem = Q2PILFilterOptimizationProblem(base_path, ground_truth)
    print()

    # Step 3: Configure NSGA-II algorithm
    print("Configuring NSGA-II algorithm (PIL - 2 parameters)...")
    algorithm = NSGA2(
        pop_size=20,
        sampling=FloatRandomSampling(),
        crossover=SBX(prob=0.9, eta=15),
        mutation=PM(eta=20),
        eliminate_duplicates=True
    )
    print()

    # Step 4: Run optimization
    print()
    print("="*80)
    print("STARTING Q2-ONLY PIL OPTIMIZATION")
    print("="*80)
    print()

    start_time = time.time()

    res = minimize(
        problem,
        algorithm,
        termination=get_termination("n_gen", 30),
        seed=1,
        verbose=True,
        save_history=True
    )

    elapsed_time = time.time() - start_time

    print()
    print("="*80)
    print(f"Q2-ONLY PIL OPTIMIZATION COMPLETE ({elapsed_time/3600:.2f} hours)")
    print("="*80)
    print()

    # Step 5: Extract results
    print("Extracting results...")

    # Get Pareto front
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
            "genome": {
                "contrast_factor": genome.contrast_factor,
                "median_size": genome.median_size
            },
            "objectives": {
                "Q2_222AAA": int(f[0]),
                "Q2_333BBB": int(f[1]),
                "Q2_333ccc": int(f[2]),
                "Q2_555CCC": int(f[3])
            },
            "total_edits_Q2": int(f.sum())
        }

        pareto_solutions.append(solution)

    print(f"  Pareto front size: {len(pareto_solutions)} solutions")
    print()

    # Save results
    output_file = base_path / "nsga2_q2_pil_pareto_front.json"
    with open(output_file, 'w') as f:
        json.dump(pareto_solutions, f, indent=2)

    print(f"PIL results saved to: {output_file}")
    print()

    # Save checkpoint
    checkpoint_file = base_path / "nsga2_q2_pil_checkpoint.pkl"
    with open(checkpoint_file, 'wb') as f:
        pickle.dump(res, f)

    print(f"Checkpoint saved to: {checkpoint_file}")
    print()

    # Best solution analysis
    print("="*80)
    print("BEST PIL SOLUTION ANALYSIS")
    print("="*80)
    print()

    # Best by total edits
    best_total = min(pareto_solutions, key=lambda x: x['total_edits_Q2'])
    print(f"Best Total Q2 Edits: {best_total['total_edits_Q2']}")
    print(f"  Baseline (fixed enhancement): 619 edits")
    if best_total['total_edits_Q2'] < 619:
        improvement = 619 - best_total['total_edits_Q2']
        print(f"  IMPROVEMENT: -{improvement} edits ({100*improvement/619:.1f}% better!)")
    else:
        degradation = best_total['total_edits_Q2'] - 619
        print(f"  Degradation: +{degradation} edits ({100*degradation/619:.1f}% worse)")
    print(f"  Parameters: {best_total['genome']}")
    print(f"  Objectives: {best_total['objectives']}")
    print()

    # Best for 333BBB (the heartbreaker)
    best_333bbb = min(pareto_solutions, key=lambda x: x['objectives']['Q2_333BBB'])
    print(f"Best for Q2 333BBB (HEARTBREAKER): {best_333bbb['objectives']['Q2_333BBB']} edits")
    print(f"  Baseline: 431 edits")
    if best_333bbb['objectives']['Q2_333BBB'] < 431:
        improvement = 431 - best_333bbb['objectives']['Q2_333BBB']
        print(f"  IMPROVEMENT: -{improvement} edits ({100*improvement/431:.1f}% better!)")
    else:
        degradation = best_333bbb['objectives']['Q2_333BBB'] - 431
        print(f"  Degradation: +{degradation} edits ({100*degradation/431:.1f}% worse)")
    print(f"  Parameters: {best_333bbb['genome']}")
    print()

    print("="*80)
    print("Q2-ONLY PIL OPTIMIZATION COMPLETE")
    print("="*80)
    print()
    print("HYBRID COMPARISON:")
    print("  Compare this PIL result with OpenCV Q2-only result")
    print("  to determine which pipeline is superior!")


if __name__ == "__main__":
    main()
