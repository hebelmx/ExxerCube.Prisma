#!/usr/bin/env python3
"""
NSGA-II Q2-ONLY Filter Optimization for OCR Enhancement.

4 Objectives (minimize Levenshtein distance):
- Q2_MediumPoor: 222AAA, 333BBB, 333ccc, 555CCC (RESCUE degraded documents)

NO Q1 COMPROMISE - Pure Q2 optimization for maximum rescue potential.

Configuration:
    Population: 50
    Generations: 50
    Total Evaluations: 2,500
    Estimated Runtime: ~7 hours

Finds Pareto-optimal filter configurations specialized for degraded Q2 documents.
Faster than full 8-objective run (~7 hours vs 14 hours).

Outputs:
- Pareto front catalog of Q2-specialist filter sets (nsga2_q2_only_pareto_front.json)
- Progress log (nsga2_q2_only_progress.log)
- Checkpoint file (nsga2_q2_only_checkpoint.pkl)

Requirements:
    pip install pymoo

Usage:
    python optimize_filters_nsga2_q2_only.py
"""

import cv2
import numpy as np
import subprocess
import json
import time
from pathlib import Path
from typing import Dict, List, Tuple
from dataclasses import dataclass
import pickle

# Multi-objective optimization library
from pymoo.core.problem import Problem
from pymoo.algorithms.moo.nsga2 import NSGA2
from pymoo.operators.crossover.sbx import SBX
from pymoo.operators.mutation.pm import PM
from pymoo.operators.sampling.rnd import FloatRandomSampling
from pymoo.optimize import minimize
from pymoo.termination import get_termination


@dataclass
class FilterGenome:
    """Filter parameter genome for optimization."""
    denoise_h: int              # 5-30
    clahe_clip: float           # 1.0-4.0
    bilateral_d: int            # 5-15 (odd)
    bilateral_sigma_color: int  # 50-100
    bilateral_sigma_space: int  # 50-100
    unsharp_amount: float       # 0.3-2.0
    unsharp_radius: float       # 0.5-5.0


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


def apply_filters(image: np.ndarray, genome: FilterGenome) -> np.ndarray:
    """Apply filter pipeline with given parameters."""
    # 1. Grayscale
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image

    # 2. Denoising
    denoised = cv2.fastNlMeansDenoising(gray, h=genome.denoise_h)

    # 3. CLAHE
    clahe = cv2.createCLAHE(clipLimit=genome.clahe_clip, tileGridSize=(8, 8))
    enhanced = clahe.apply(denoised)

    # 4. Bilateral filter
    bilateral = cv2.bilateralFilter(
        enhanced,
        d=genome.bilateral_d,
        sigmaColor=genome.bilateral_sigma_color,
        sigmaSpace=genome.bilateral_sigma_space
    )

    # 5. Unsharp mask
    gaussian = cv2.GaussianBlur(bilateral, (0, 0), genome.unsharp_radius)
    unsharp = cv2.addWeighted(
        bilateral, 1.0 + genome.unsharp_amount,
        gaussian, -genome.unsharp_amount,
        0
    )

    return unsharp


def run_tesseract_ocr(image_path: Path, lang: str = "spa", psm: int = 6) -> str:
    """Run Tesseract OCR on an image."""
    cmd = [
        "C:/Program Files/Tesseract-OCR/tesseract.exe",
        "--tessdata-dir", "C:/Program Files/Tesseract-OCR/tessdata",
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


class Q2OnlyFilterOptimizationProblem(Problem):
    """
    Q2-ONLY Multi-objective optimization problem for OCR filter tuning.

    4 Objectives (Q2_MediumPoor only):
    - 222AAA (minimize edit distance)
    - 333BBB (minimize edit distance) - THE HEARTBREAKER
    - 333ccc (minimize edit distance)
    - 555CCC (minimize edit distance)

    NO Q1 compromise - pure Q2 rescue optimization.
    """

    def __init__(self, base_path: Path, ground_truth: Dict[str, str]):
        """
        Initialize Q2-only optimization problem.

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

        # Load degraded images (Q2 only)
        self.degraded_images = {}
        for doc in self.doc_names:
            img_path = base_path / "PRP1_Degraded" / self.quality_level / doc
            if img_path.exists():
                self.degraded_images[doc] = cv2.imread(str(img_path))

        # Evaluation counter
        self.eval_count = 0
        self.log_file = base_path / "nsga2_q2_only_progress.log"

        # Decision variables (7 parameters)
        # [denoise_h, clahe_clip, bilateral_d, sigma_color, sigma_space, unsharp_amount, unsharp_radius]
        super().__init__(
            n_var=7,
            n_obj=4,  # 4 objectives (Q2 documents ONLY)
            xl=np.array([5, 1.0, 5, 50, 50, 0.3, 0.5]),    # Lower bounds
            xu=np.array([30, 4.0, 15, 100, 100, 2.0, 5.0])  # Upper bounds
        )

    def _evaluate(self, X, out, *args, **kwargs):
        """
        Evaluate fitness for each solution in population.

        Args:
            X: Population array (n_solutions × 7 parameters)
            out: Output dictionary to fill with objectives
        """
        objectives = []

        for x in X:
            # Decode genome
            genome = FilterGenome(
                denoise_h=int(x[0]),
                clahe_clip=float(x[1]),
                bilateral_d=int(x[2]) | 1,  # Ensure odd
                bilateral_sigma_color=int(x[3]),
                bilateral_sigma_space=int(x[4]),
                unsharp_amount=float(x[5]),
                unsharp_radius=float(x[6])
            )

            # Evaluate on 4 Q2 documents
            doc_objectives = []

            for doc in self.doc_names:
                if doc not in self.degraded_images:
                    doc_objectives.append(9999)  # Penalty for missing
                    continue

                # Apply filters
                degraded_img = self.degraded_images[doc]
                enhanced_img = apply_filters(degraded_img, genome)

                # Save temporary image for OCR
                temp_path = self.base_path / f"temp_ocr_q2_{self.eval_count}.png"
                cv2.imwrite(str(temp_path), enhanced_img)

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
    """Run Q2-ONLY NSGA-II optimization."""

    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*80)
    print("NSGA-II Q2-ONLY FILTER OPTIMIZATION")
    print("="*80)
    print()
    print("Configuration:")
    print("  Population: 50")
    print("  Generations: 50")
    print("  Total evaluations: 2,500")
    print("  Estimated time: ~7 hours")
    print()
    print("4 Objectives (Q2_MediumPoor ONLY - NO Q1 COMPROMISE):")
    print("  - 222AAA (rescue degraded)")
    print("  - 333BBB (THE HEARTBREAKER - current: 431 edits)")
    print("  - 333ccc (rescue degraded)")
    print("  - 555CCC (rescue degraded)")
    print()
    print("Goal: Find Pareto-optimal filters SPECIALIZED for Q2 rescue")
    print("="*80)
    print()

    # Step 1: Load ground truth
    print("Loading ground truth from pristine documents...")
    ground_truth = load_ground_truth(base_path)
    print(f"  Loaded {len(ground_truth)} ground truth documents")
    print()

    # Step 2: Setup optimization problem
    print("Setting up Q2-only optimization problem...")
    problem = Q2OnlyFilterOptimizationProblem(base_path, ground_truth)
    print()

    # Step 3: Configure NSGA-II algorithm
    print("Configuring NSGA-II algorithm (Q2-ONLY)...")
    algorithm = NSGA2(
        pop_size=50,
        sampling=FloatRandomSampling(),
        crossover=SBX(prob=0.9, eta=15),
        mutation=PM(eta=20),
        eliminate_duplicates=True
    )
    print()

    # Step 4: Run optimization
    print()
    print("="*80)
    print("STARTING Q2-ONLY OPTIMIZATION")
    print("="*80)
    print()

    start_time = time.time()

    res = minimize(
        problem,
        algorithm,
        termination=get_termination("n_gen", 50),
        seed=1,
        verbose=True,
        save_history=True
    )

    elapsed_time = time.time() - start_time

    print()
    print("="*80)
    print(f"Q2-ONLY OPTIMIZATION COMPLETE ({elapsed_time/3600:.2f} hours)")
    print("="*80)
    print()

    # Step 5: Extract results
    print("Extracting results...")

    # Get Pareto front
    pareto_solutions = []

    for i, (x, f) in enumerate(zip(res.X, res.F)):
        genome = FilterGenome(
            denoise_h=int(x[0]),
            clahe_clip=float(x[1]),
            bilateral_d=int(x[2]) | 1,
            bilateral_sigma_color=int(x[3]),
            bilateral_sigma_space=int(x[4]),
            unsharp_amount=float(x[5]),
            unsharp_radius=float(x[6])
        )

        solution = {
            "id": i,
            "genome": {
                "denoise_h": genome.denoise_h,
                "clahe_clip": genome.clahe_clip,
                "bilateral_d": genome.bilateral_d,
                "bilateral_sigma_color": genome.bilateral_sigma_color,
                "bilateral_sigma_space": genome.bilateral_sigma_space,
                "unsharp_amount": genome.unsharp_amount,
                "unsharp_radius": genome.unsharp_radius
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
    output_file = base_path / "nsga2_q2_only_pareto_front.json"
    with open(output_file, 'w') as f:
        json.dump(pareto_solutions, f, indent=2)

    print(f"Q2-only results saved to: {output_file}")
    print()

    # Save checkpoint
    checkpoint_file = base_path / "nsga2_q2_only_checkpoint.pkl"
    with open(checkpoint_file, 'wb') as f:
        pickle.dump(res, f)

    print(f"Checkpoint saved to: {checkpoint_file}")
    print()

    # Best solution analysis
    print("="*80)
    print("BEST Q2 SOLUTION ANALYSIS")
    print("="*80)
    print()

    # Best by total edits
    best_total = min(pareto_solutions, key=lambda x: x['total_edits_Q2'])
    print(f"Best Total Q2 Edits: {best_total['total_edits_Q2']}")
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
    print("Q2-ONLY OPTIMIZATION COMPLETE")
    print("="*80)


if __name__ == "__main__":
    main()
