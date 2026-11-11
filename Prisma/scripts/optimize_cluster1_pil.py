#!/usr/bin/env python3
"""
NSGA-II Cluster 1 PIL Pipeline Optimization

Cluster 1: Normal Quality Images (Q1_Poor + Q2_MediumPoor)
- 8 objectives (4 docs × 2 levels)
- Characteristics: blur=1436.7, noise=1.01, contrast=32.8

PIL PIPELINE (2 parameters):
- Contrast enhancement (1.0-2.5x)
- Median filter (size 3-7)

Configuration:
    Population: 30
    Generations: 40
    Total Evaluations: 1,200
    Estimated Runtime: ~3 hours

Usage:
    python optimize_cluster1_pil.py
"""

import numpy as np
import subprocess
import json
import time
from pathlib import Path
from typing import Dict
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
    contrast_factor: float
    median_size: int


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


def apply_pil_filters(image_path: Path, genome: PILFilterGenome) -> Image.Image:
    image = Image.open(image_path)
    if image.mode != 'L':
        image = image.convert('L')
    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(genome.contrast_factor)
    image = image.filter(ImageFilter.MedianFilter(size=genome.median_size))
    return image


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


class Cluster1PILOptimizationProblem(Problem):
    def __init__(self, base_path: Path, ground_truth: Dict[str, str]):
        self.base_path = base_path
        self.ground_truth = ground_truth

        # Cluster 1: All 4 docs at Q1_Poor + Q2_MediumPoor (8 images total)
        docs = ["222AAA-44444444442025_page-0001.jpg", "333BBB-44444444442025_page1.png", "333ccc-6666666662025_page1.png", "555CCC-66666662025_page1.png"]
        levels = ["Q1_Poor", "Q2_MediumPoor"]

        self.test_cases = []
        for doc in docs:
            for level in levels:
                path = base_path / "PRP1_Degraded" / level / doc
                if path.exists():
                    self.test_cases.append({'doc': doc, 'level': level, 'path': path})

        self.eval_count = 0
        self.log_file = base_path / "cluster1_pil_progress.log"

        super().__init__(n_var=2, n_obj=len(self.test_cases), xl=np.array([1.0, 3]), xu=np.array([2.5, 7]))

    def _evaluate(self, X, out, *args, **kwargs):
        objectives = []
        for x in X:
            median_size = int(x[1])
            if median_size % 2 == 0:
                median_size += 1
            median_size = max(3, min(7, median_size))
            genome = PILFilterGenome(float(x[0]), median_size)

            doc_objectives = []
            for tc in self.test_cases:
                enhanced_img = apply_pil_filters(tc['path'], genome)
                temp_path = self.base_path / f"temp_ocr_c1pil_{self.eval_count}.png"
                enhanced_img.save(temp_path)
                ocr_text = run_tesseract_ocr(temp_path)
                gt_text = self.ground_truth.get(tc['doc'], "")
                distance = levenshtein_distance(normalize_text(gt_text), normalize_text(ocr_text))
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
    print("NSGA-II CLUSTER 1 PIL PIPELINE OPTIMIZATION")
    print("="*80)
    print()
    print("Cluster 1: Normal quality (blur=1436.7, noise=1.01)")
    print(f"Configuration: Pop=30, Gen=40, ~3 hours")
    print(f"8 Objectives: All 4 docs × Q1_Poor + Q2_MediumPoor")
    print("="*80)
    print()

    ground_truth = load_ground_truth(base_path)
    problem = Cluster1PILOptimizationProblem(base_path, ground_truth)
    algorithm = NSGA2(pop_size=30, sampling=FloatRandomSampling(), crossover=SBX(prob=0.9, eta=15), mutation=PM(eta=20), eliminate_duplicates=True)

    start_time = time.time()
    res = minimize(problem, algorithm, termination=get_termination("n_gen", 40), seed=1, verbose=True, save_history=True)
    elapsed_time = time.time() - start_time

    print(f"\n✓ COMPLETE ({elapsed_time/60:.1f} minutes)\n")

    pareto_solutions = []
    for i, (x, f) in enumerate(zip(res.X, res.F)):
        median_size = int(x[1])
        if median_size % 2 == 0:
            median_size += 1
        genome = PILFilterGenome(float(x[0]), median_size)
        solution = {
            "id": i,
            "cluster": 1,
            "genome": {"contrast_factor": genome.contrast_factor, "median_size": genome.median_size},
            "objectives": {f"{problem.test_cases[j]['level']}_{problem.test_cases[j]['doc'].split('-')[0]}": int(f[j]) for j in range(len(f))},
            "total_edits": int(f.sum())
        }
        pareto_solutions.append(solution)

    output_file = base_path / "cluster1_pil_pareto_front.json"
    with open(output_file, 'w') as f:
        json.dump(pareto_solutions, f, indent=2)

    print(f"Pareto front: {len(pareto_solutions)} solutions")
    print(f"Saved to: {output_file}")
    best = min(pareto_solutions, key=lambda x: x['total_edits'])
    print(f"Best: {best['total_edits']} edits - {best['genome']}")


if __name__ == "__main__":
    main()
