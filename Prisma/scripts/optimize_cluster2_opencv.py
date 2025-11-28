#!/usr/bin/env python3
"""
NSGA-II Cluster 2 OpenCV Pipeline Optimization

Cluster 2: Degraded Images (Q2_MediumPoor + Q3_Low + Q4_VeryLow)
- 12 objectives (4 docs × 3 levels)
- Characteristics: blur=1238.2, noise=6.83, contrast=25.1

OpenCV PIPELINE (7 parameters):
- Denoising, CLAHE, Bilateral filter, Unsharp mask

Configuration: Pop=50, Gen=50, ~10 hours
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
            current_row.append(min(previous_row[j + 1] + 1, current_row[j] + 1, previous_row[j] + (c1 != c2)))
        previous_row = current_row
    return previous_row[-1]


def normalize_text(text: str) -> str:
    import re
    return re.sub(r'\s+', ' ', text.lower()).strip()


def apply_filters(image: np.ndarray, genome: FilterGenome) -> np.ndarray:
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image
    denoised = cv2.fastNlMeansDenoising(gray, h=genome.denoise_h)
    clahe = cv2.createCLAHE(clipLimit=genome.clahe_clip, tileGridSize=(8, 8))
    enhanced = clahe.apply(denoised)
    bilateral = cv2.bilateralFilter(enhanced, d=genome.bilateral_d, sigmaColor=genome.bilateral_sigma_color, sigmaSpace=genome.bilateral_sigma_space)
    gaussian = cv2.GaussianBlur(bilateral, (0, 0), genome.unsharp_radius)
    return cv2.addWeighted(bilateral, 1.0 + genome.unsharp_amount, gaussian, -genome.unsharp_amount, 0)


def run_tesseract_ocr(image_path: Path, lang: str = "spa", psm: int = 6) -> str:
    import platform
    if platform.system() == "Windows":
        cmd = ["C:/Program Files/Tesseract-OCR/tesseract.exe", "--tessdata-dir", "C:/Program Files/Tesseract-OCR/tessdata", str(image_path), "stdout", "-l", lang, "--psm", str(psm)]
    else:
        cmd = ["tesseract", str(image_path), "stdout", "-l", lang, "--psm", str(psm)]
    try:
        return subprocess.run(cmd, capture_output=True, text=True, timeout=60).stdout
    except:
        return ""


def load_ground_truth(base_path: Path) -> Dict[str, str]:
    ground_truth = {}
    pristine_base = base_path / "PRP1"
    for doc in ["222AAA-44444444442025_page-0001.jpg", "333BBB-44444444442025_page1.png", "333ccc-6666666662025_page1.png", "555CCC-66666662025_page1.png"]:
        doc_path = pristine_base / doc
        if doc_path.exists():
            ground_truth[doc] = run_tesseract_ocr(doc_path)
    return ground_truth


class Cluster2OpenCVOptimizationProblem(Problem):
    def __init__(self, base_path: Path, ground_truth: Dict[str, str]):
        self.base_path = base_path
        self.ground_truth = ground_truth

        # Cluster 2: Q2_MediumPoor + Q3_Low + Q4_VeryLow for all 4 docs
        docs = ["222AAA-44444444442025_page-0001.jpg", "333BBB-44444444442025_page1.png", "333ccc-6666666662025_page1.png", "555CCC-66666662025_page1.png"]
        levels = ["Q2_MediumPoor", "Q3_Low", "Q4_VeryLow"]

        self.test_cases = []
        self.degraded_images = {}
        for doc in docs:
            for level in levels:
                path = base_path / "PRP1_Degraded" / level / doc
                if path.exists():
                    img = cv2.imread(str(path))
                    key = f"{doc}_{level}"
                    self.test_cases.append({'doc': doc, 'level': level, 'key': key})
                    self.degraded_images[key] = img

        self.eval_count = 0
        self.log_file = base_path / "cluster2_opencv_progress.log"

        super().__init__(n_var=7, n_obj=len(self.test_cases), xl=np.array([5, 1.0, 5, 50, 50, 0.3, 0.5]), xu=np.array([30, 4.0, 15, 100, 100, 2.0, 5.0]))

    def _evaluate(self, X, out, *args, **kwargs):
        objectives = []
        for x in X:
            bilateral_d = int(x[2])
            if bilateral_d % 2 == 0:
                bilateral_d += 1
            bilateral_d = max(5, min(15, bilateral_d))
            genome = FilterGenome(int(x[0]), float(x[1]), bilateral_d, int(x[3]), int(x[4]), float(x[5]), float(x[6]))

            doc_objectives = []
            for tc in self.test_cases:
                enhanced = apply_filters(self.degraded_images[tc['key']], genome)
                temp_path = self.base_path / f"temp_ocr_c2opencv_{self.eval_count}.png"
                cv2.imwrite(str(temp_path), enhanced)
                ocr_text = run_tesseract_ocr(temp_path)
                distance = levenshtein_distance(normalize_text(self.ground_truth.get(tc['doc'], "")), normalize_text(ocr_text))
                doc_objectives.append(distance)
                if temp_path.exists():
                    temp_path.unlink()
                self.eval_count += 1

            objectives.append(doc_objectives)
            with open(self.log_file, 'a') as f:
                f.write(f"Eval {self.eval_count}: {doc_objectives}\n")

        out["F"] = np.array(objectives)


def main():
    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*80)
    print("NSGA-II CLUSTER 2 OPENCV PIPELINE OPTIMIZATION")
    print("="*80)
    print()
    print("Cluster 2: Degraded (blur=1238.2, noise=6.83, contrast=25.1)")
    print("Configuration: Pop=50, Gen=50, ~10 hours")
    print("12 Objectives: All 4 docs × Q2_MediumPoor + Q3_Low + Q4_VeryLow")
    print("="*80)
    print()

    ground_truth = load_ground_truth(base_path)
    problem = Cluster2OpenCVOptimizationProblem(base_path, ground_truth)
    algorithm = NSGA2(pop_size=50, sampling=FloatRandomSampling(), crossover=SBX(prob=0.9, eta=15), mutation=PM(eta=20), eliminate_duplicates=True)

    start_time = time.time()
    res = minimize(problem, algorithm, termination=get_termination("n_gen", 50), seed=1, verbose=True, save_history=True)
    elapsed_time = time.time() - start_time

    print(f"\n✓ COMPLETE ({elapsed_time/3600:.1f} hours)\n")

    pareto_solutions = []
    for i, (x, f) in enumerate(zip(res.X, res.F)):
        bilateral_d = int(x[2])
        if bilateral_d % 2 == 0:
            bilateral_d += 1
        genome = FilterGenome(int(x[0]), float(x[1]), bilateral_d, int(x[3]), int(x[4]), float(x[5]), float(x[6]))
        solution = {
            "id": i,
            "cluster": 2,
            "genome": {"denoise_h": genome.denoise_h, "clahe_clip": genome.clahe_clip, "bilateral_d": genome.bilateral_d, "bilateral_sigma_color": genome.bilateral_sigma_color, "bilateral_sigma_space": genome.bilateral_sigma_space, "unsharp_amount": genome.unsharp_amount, "unsharp_radius": genome.unsharp_radius},
            "objectives": {f"{problem.test_cases[j]['level']}_{problem.test_cases[j]['doc'].split('-')[0]}": int(f[j]) for j in range(len(f))},
            "total_edits": int(f.sum())
        }
        pareto_solutions.append(solution)

    output_file = base_path / "cluster2_opencv_pareto_front.json"
    with open(output_file, 'w') as f:
        json.dump(pareto_solutions, f, indent=2)

    print(f"Pareto front: {len(pareto_solutions)} solutions")
    print(f"Saved to: {output_file}")
    best = min(pareto_solutions, key=lambda x: x['total_edits'])
    print(f"Best: {best['total_edits']} edits")


if __name__ == "__main__":
    main()
