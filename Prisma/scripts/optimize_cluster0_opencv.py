#!/usr/bin/env python3
"""
NSGA-II Cluster 0 OpenCV Pipeline Optimization

Cluster 0: Ultra-Sharp Images (555CCC Q0 + Q05)
- 2 objectives (2 images)
- Characteristics: blur=6905.9, noise=0.47, contrast=51.9

OpenCV PIPELINE (7 parameters):
- Denoising (h: 5-30)
- CLAHE (clip: 1.0-4.0)
- Bilateral filter (d: 5-15, sigma_color: 50-100, sigma_space: 50-100)
- Unsharp mask (amount: 0.3-2.0, radius: 0.5-5.0)

Configuration:
    Population: 50
    Generations: 30
    Total Evaluations: 1,500
    Estimated Runtime: ~3 hours

Usage:
    python optimize_cluster0_opencv.py
"""

import cv2
import numpy as np
import subprocess
import json
import time
from pathlib import Path
from typing import Dict
from dataclasses import dataclass

from pymoo.core.problem import Problem
from pymoo.algorithms.moo.nsga2 import NSGA2
from pymoo.operators.crossover.sbx import SBX
from pymoo.operators.mutation.pm import PM
from pymoo.operators.sampling.rnd import FloatRandomSampling
from pymoo.optimize import minimize
from pymoo.termination import get_termination


@dataclass
class FilterGenome:
    """OpenCV filter parameters."""
    denoise_h: int
    clahe_clip: float
    bilateral_d: int
    bilateral_sigma_color: int
    bilateral_sigma_space: int
    unsharp_amount: float
    unsharp_radius: float


def levenshtein_distance(s1: str, s2: str) -> int:
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
    import re
    text = text.lower()
    text = re.sub(r'\s+', ' ', text)
    return text.strip()


def apply_filters(image: np.ndarray, genome: FilterGenome) -> np.ndarray:
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image
    denoised = cv2.fastNlMeansDenoising(gray, h=genome.denoise_h)
    clahe = cv2.createCLAHE(clipLimit=genome.clahe_clip, tileGridSize=(8, 8))
    enhanced = clahe.apply(denoised)
    bilateral = cv2.bilateralFilter(enhanced, d=genome.bilateral_d, sigmaColor=genome.bilateral_sigma_color, sigmaSpace=genome.bilateral_sigma_space)
    gaussian = cv2.GaussianBlur(bilateral, (0, 0), genome.unsharp_radius)
    unsharp = cv2.addWeighted(bilateral, 1.0 + genome.unsharp_amount, gaussian, -genome.unsharp_amount, 0)
    return unsharp


def run_tesseract_ocr(image_path: Path, lang: str = "spa", psm: int = 6) -> str:
    import platform
    if platform.system() == "Windows":
        cmd = ["C:/Program Files/Tesseract-OCR/tesseract.exe", "--tessdata-dir", "C:/Program Files/Tesseract-OCR/tessdata", str(image_path), "stdout", "-l", lang, "--psm", str(psm)]
    else:
        cmd = ["tesseract", str(image_path), "stdout", "-l", lang, "--psm", str(psm)]
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
        return result.stdout
    except:
        return ""


def load_ground_truth(base_path: Path) -> Dict[str, str]:
    ground_truth = {}
    pristine_base = base_path / "PRP1"
    doc = "555CCC-66666662025_page1.png"
    doc_path = pristine_base / doc
    if doc_path.exists():
        text = run_tesseract_ocr(doc_path)
        ground_truth[doc] = text
    return ground_truth


class Cluster0OpenCVOptimizationProblem(Problem):
    def __init__(self, base_path: Path, ground_truth: Dict[str, str]):
        self.base_path = base_path
        self.ground_truth = ground_truth

        self.test_cases = [
            {'doc': "555CCC-66666662025_page1.png", 'level': "Q0_Pristine", 'path': base_path / "PRP1_Degraded" / "Q0_Pristine" / "555CCC-66666662025_page1.png"},
            {'doc': "555CCC-66666662025_page1.png", 'level': "Q05_VeryGood", 'path': base_path / "PRP1_Degraded" / "Q05_VeryGood" / "555CCC-66666662025_page1.png"}
        ]

        self.degraded_images = {}
        for tc in self.test_cases:
            if tc['path'].exists():
                self.degraded_images[tc['level']] = cv2.imread(str(tc['path']))

        self.eval_count = 0
        self.log_file = base_path / "cluster0_opencv_progress.log"

        super().__init__(
            n_var=7,
            n_obj=2,
            xl=np.array([5, 1.0, 5, 50, 50, 0.3, 0.5]),
            xu=np.array([30, 4.0, 15, 100, 100, 2.0, 5.0])
        )

    def _evaluate(self, X, out, *args, **kwargs):
        objectives = []
        for x in X:
            bilateral_d = int(x[2])
            if bilateral_d % 2 == 0:
                bilateral_d += 1
            bilateral_d = max(5, min(15, bilateral_d))

            genome = FilterGenome(
                denoise_h=int(x[0]),
                clahe_clip=float(x[1]),
                bilateral_d=bilateral_d,
                bilateral_sigma_color=int(x[3]),
                bilateral_sigma_space=int(x[4]),
                unsharp_amount=float(x[5]),
                unsharp_radius=float(x[6])
            )

            doc_objectives = []
            for tc in self.test_cases:
                if tc['level'] not in self.degraded_images:
                    doc_objectives.append(9999)
                    continue

                enhanced = apply_filters(self.degraded_images[tc['level']], genome)
                temp_path = self.base_path / f"temp_ocr_c0opencv_{self.eval_count}.png"
                cv2.imwrite(str(temp_path), enhanced)
                ocr_text = run_tesseract_ocr(temp_path)
                gt_text = self.ground_truth.get(tc['doc'], "")
                distance = levenshtein_distance(normalize_text(gt_text), normalize_text(ocr_text))
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
    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*80)
    print("NSGA-II CLUSTER 0 OPENCV PIPELINE OPTIMIZATION")
    print("="*80)
    print()
    print("Cluster 0: Ultra-sharp (blur=6905.9, noise=0.47)")
    print("Configuration: Pop=50, Gen=30, ~3 hours")
    print("2 Objectives: 555CCC Q0_Pristine, Q05_VeryGood")
    print("="*80)
    print()

    ground_truth = load_ground_truth(base_path)
    problem = Cluster0OpenCVOptimizationProblem(base_path, ground_truth)
    algorithm = NSGA2(pop_size=50, sampling=FloatRandomSampling(), crossover=SBX(prob=0.9, eta=15), mutation=PM(eta=20), eliminate_duplicates=True)

    start_time = time.time()
    res = minimize(problem, algorithm, termination=get_termination("n_gen", 30), seed=1, verbose=True, save_history=True)
    elapsed_time = time.time() - start_time

    print(f"\n✓ COMPLETE ({elapsed_time/60:.1f} minutes)\n")

    pareto_solutions = []
    for i, (x, f) in enumerate(zip(res.X, res.F)):
        bilateral_d = int(x[2])
        if bilateral_d % 2 == 0:
            bilateral_d += 1
        genome = FilterGenome(int(x[0]), float(x[1]), bilateral_d, int(x[3]), int(x[4]), float(x[5]), float(x[6]))
        solution = {
            "id": i,
            "cluster": 0,
            "genome": {
                "denoise_h": genome.denoise_h,
                "clahe_clip": genome.clahe_clip,
                "bilateral_d": genome.bilateral_d,
                "bilateral_sigma_color": genome.bilateral_sigma_color,
                "bilateral_sigma_space": genome.bilateral_sigma_space,
                "unsharp_amount": genome.unsharp_amount,
                "unsharp_radius": genome.unsharp_radius
            },
            "objectives": {"Q0_555CCC": int(f[0]), "Q05_555CCC": int(f[1])},
            "total_edits": int(f.sum())
        }
        pareto_solutions.append(solution)

    output_file = base_path / "cluster0_opencv_pareto_front.json"
    with open(output_file, 'w') as f:
        json.dump(pareto_solutions, f, indent=2)

    print(f"Pareto front: {len(pareto_solutions)} solutions")
    print(f"Saved to: {output_file}")
    best = min(pareto_solutions, key=lambda x: x['total_edits'])
    print(f"Best: {best['total_edits']} edits - {best['genome']}")


if __name__ == "__main__":
    main()
