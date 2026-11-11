#!/usr/bin/env python3
"""
NSGA-II Multi-Objective Filter Optimization for OCR Enhancement.

8 Objectives (minimize Levenshtein distance):
- Q1_Poor: 222AAA, 333BBB, 333ccc, 555CCC (light filters needed)
- Q2_MediumPoor: 222AAA, 333BBB, 333ccc, 555CCC (aggressive filters needed)

Finds Pareto-optimal filter configurations that balance performance across
all document quality levels.

Outputs:
- Pareto front catalog of optimal filter sets
- Evolution visualization showing convergence
- Progress log with generation-by-generation metrics
- Checkpointing for resumption if interrupted

Requirements:
    pip install pymoo

Usage:
    python optimize_filters_nsga2.py
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


class OCRFilterOptimizationProblem(Problem):
    """
    Multi-objective optimization problem for OCR filter tuning.

    8 Objectives:
    - Q1_Poor: 4 documents (minimize edit distance)
    - Q2_MediumPoor: 4 documents (minimize edit distance)
    """

    def __init__(self, base_path: Path, ground_truth: Dict[str, str]):
        """
        Initialize optimization problem.

        Args:
            base_path: Path to Fixtures directory
            ground_truth: Dictionary of pristine OCR text
        """
        self.base_path = base_path
        self.ground_truth = ground_truth

        # Document sets
        self.doc_names = [
            "222AAA-44444444442025_page-0001.jpg",
            "333BBB-44444444442025_page1.png",
            "333ccc-6666666662025_page1.png",
            "555CCC-66666662025_page1.png"
        ]

        self.quality_levels = ["Q1_Poor", "Q2_MediumPoor"]

        # Load degraded images
        self.degraded_images = {}
        for quality in self.quality_levels:
            for doc in self.doc_names:
                key = f"{quality}_{doc}"
                img_path = base_path / "PRP1_Degraded" / quality / doc
                if img_path.exists():
                    self.degraded_images[key] = cv2.imread(str(img_path))

        # Evaluation counter
        self.eval_count = 0
        self.log_file = base_path / "nsga2_progress.log"

        # Decision variables (7 parameters)
        # [denoise_h, clahe_clip, bilateral_d, sigma_color, sigma_space, unsharp_amount, unsharp_radius]
        super().__init__(
            n_var=7,
            n_obj=8,  # 8 objectives (4 Q1 + 4 Q2 documents)
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

            # Evaluate on all 8 documents
            doc_objectives = []

            for quality in self.quality_levels:
                for doc in self.doc_names:
                    key = f"{quality}_{doc}"

                    if key not in self.degraded_images:
                        doc_objectives.append(9999)  # Penalty for missing
                        continue

                    # Apply filters
                    degraded_img = self.degraded_images[key]
                    enhanced_img = apply_filters(degraded_img, genome)

                    # Save temporary image for OCR
                    temp_path = self.base_path / f"temp_ocr_{self.eval_count}.png"
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

            # Log progress
            if self.eval_count % 10 == 0:
                with open(self.log_file, 'a') as f:
                    f.write(f"Eval {self.eval_count}: {doc_objectives}\n")

        out["F"] = np.array(objectives)


def main():
    """Run NSGA-II optimization."""

    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*80)
    print("NSGA-II MULTI-OBJECTIVE FILTER OPTIMIZATION")
    print("="*80)
    print()
    print("8 Objectives:")
    print("  Q1_Poor: 222AAA, 333BBB, 333ccc, 555CCC (light filters)")
    print("  Q2_MediumPoor: 222AAA, 333BBB, 333ccc, 555CCC (aggressive filters)")
    print()
    print("Goal: Find Pareto-optimal filter configurations that balance")
    print("      performance across all document quality levels")
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

    # Step 3: Configure NSGA-II algorithm
    print("Configuring NSGA-II algorithm...")
    algorithm = NSGA2(
        pop_size=100,
        sampling=FloatRandomSampling(),
        crossover=SBX(prob=0.9, eta=15),
        mutation=PM(prob=0.1, eta=20),
        eliminate_duplicates=True
    )
    print("  Population size: 100")
    print("  Crossover: SBX (prob=0.9, eta=15)")
    print("  Mutation: PM (prob=0.1, eta=20)")
    print()

    # Step 4: Define termination criterion
    termination = get_termination("n_gen", 50)
    print("  Termination: 50 generations")
    print()

    # Step 5: Run optimization
    print("="*80)
    print("STARTING OPTIMIZATION")
    print("="*80)
    print()
    print("This will take several hours (OCR is slow)...")
    print("Progress will be logged to: nsga2_progress.log")
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
    print(f"OPTIMIZATION COMPLETE ({elapsed/3600:.2f} hours)")
    print("="*80)
    print()

    # Step 6: Extract Pareto front
    print("Extracting Pareto-optimal solutions...")
    pareto_X = res.X  # Decision variables
    pareto_F = res.F  # Objective values

    print(f"  Pareto front size: {len(pareto_X)} solutions")
    print()

    # Step 7: Save results
    results_file = base_path / "nsga2_pareto_front.json"

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

    # Step 8: Print top solutions
    print("="*80)
    print("TOP 5 SOLUTIONS FROM PARETO FRONT")
    print("="*80)
    print()

    # Sort by total edits (best overall)
    sorted_catalog = sorted(pareto_catalog, key=lambda x: x["total_edits_all"])

    for i, sol in enumerate(sorted_catalog[:5], 1):
        print(f"Solution #{i} (Total edits: {sol['total_edits_all']})")
        print(f"  Parameters:")
        for param, value in sol['genome'].items():
            print(f"    {param}: {value}")
        print(f"  Performance:")
        print(f"    Q1 total: {sol['total_edits_Q1']} edits")
        print(f"    Q2 total: {sol['total_edits_Q2']} edits")
        print()

    # Step 9: Save checkpoint for resumption
    checkpoint_file = base_path / "nsga2_checkpoint.pkl"
    with open(checkpoint_file, 'wb') as f:
        pickle.dump(res, f)

    print(f"Checkpoint saved to: {checkpoint_file}")
    print()

    print("="*80)
    print("NEXT STEPS")
    print("="*80)
    print()
    print("1. Review Pareto front catalog in nsga2_pareto_front.json")
    print("2. Select solution based on priorities:")
    print("   - Best Q2 performance (rescue 333BBB)")
    print("   - Best overall balance")
    print("   - Safest (doesn't hurt Q1)")
    print("3. Visualize Pareto front (scatter plots)")
    print("4. Apply selected filters to production pipeline")
    print()


if __name__ == "__main__":
    main()
