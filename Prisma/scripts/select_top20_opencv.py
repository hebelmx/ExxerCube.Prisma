"""
Select Top 20 OpenCV Pareto Solutions from 100

Strategy:
- 7 best Q1 performers (light enhancement for high-quality docs)
- 7 best Q2 performers (heavy enhancement for degraded docs)
- 6 best overall compromises (balanced)
"""

import json
from pathlib import Path

INPUT_JSON = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures/nsga2_pareto_front.json")
OUTPUT_JSON = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures/nsga2_opencv_top20.json")

print("="*80)
print("SELECTING TOP 20 OPENCV PARETO SOLUTIONS FROM 100")
print("="*80)
print()

# Load all 100 solutions
with open(INPUT_JSON, 'r') as f:
    all_solutions = json.load(f)

print(f"Loaded {len(all_solutions)} OpenCV Pareto solutions")
print()

# Sort by different criteria
sorted_by_q1 = sorted(all_solutions, key=lambda x: x['total_edits_Q1'])
sorted_by_q2 = sorted(all_solutions, key=lambda x: x['total_edits_Q2'])
sorted_by_total = sorted(all_solutions, key=lambda x: x['total_edits_all'])
sorted_by_333bbb = sorted(all_solutions, key=lambda x: x['objectives']['Q2_333BBB'])

# Select top 20 (avoid duplicates)
selected_ids = set()
selected = []

# 1. Best 7 Q1 performers
print("Top 7 Q1 Performers (light enhancement):")
print("-"*80)
for sol in sorted_by_q1:
    if sol['id'] not in selected_ids and len([s for s in selected if s.get('category') == 'best_q1']) < 7:
        sol['category'] = 'best_q1'
        selected.append(sol)
        selected_ids.add(sol['id'])
        print(f"  ID {sol['id']:3d}: Q1={sol['total_edits_Q1']:4d}, "
              f"h={sol['genome']['denoise_h']:2.0f}, "
              f"CLAHE={sol['genome']['clahe_clip']:.2f}")

print()

# 2. Best 7 Q2 performers
print("Top 7 Q2 Performers (heavy enhancement):")
print("-"*80)
for sol in sorted_by_q2:
    if sol['id'] not in selected_ids and len([s for s in selected if s.get('category') == 'best_q2']) < 7:
        sol['category'] = 'best_q2'
        selected.append(sol)
        selected_ids.add(sol['id'])
        print(f"  ID {sol['id']:3d}: Q2={sol['total_edits_Q2']:4d}, "
              f"333BBB={sol['objectives']['Q2_333BBB']:4d}, "
              f"h={sol['genome']['denoise_h']:2.0f}, "
              f"CLAHE={sol['genome']['clahe_clip']:.2f}")

print()

# 3. Best 333BBB rescuers
print("Best 333BBB Rescuers (showing top 5 from selections):")
print("-"*80)
count = 0
for sol in sorted_by_333bbb:
    if sol['id'] in selected_ids:
        print(f"  ID {sol['id']:3d}: 333BBB={sol['objectives']['Q2_333BBB']:4d} "
              f"({sol.get('category', 'unknown')} category)")
        count += 1
        if count >= 5:
            break

print()

# 4. Best 6 overall compromises
print("Top 6 Overall Compromises (balanced Q1+Q2):")
print("-"*80)
for sol in sorted_by_total:
    if sol['id'] not in selected_ids and len(selected) < 20:
        sol['category'] = 'best_compromise'
        selected.append(sol)
        selected_ids.add(sol['id'])
        print(f"  ID {sol['id']:3d}: Total={sol['total_edits_all']:4d}, "
              f"Q1={sol['total_edits_Q1']:4d}, "
              f"Q2={sol['total_edits_Q2']:4d}")

print()
print("="*80)
print(f"Selected {len(selected)} solutions:")
print(f"  - Best Q1: {len([s for s in selected if s.get('category') == 'best_q1'])} solutions")
print(f"  - Best Q2: {len([s for s in selected if s.get('category') == 'best_q2'])} solutions")
print(f"  - Best compromise: {len([s for s in selected if s.get('category') == 'best_compromise'])} solutions")
print("="*80)
print()

# Save selected top 20
with open(OUTPUT_JSON, 'w') as f:
    json.dump(selected, f, indent=2)

print(f"✓ Saved top 20 OpenCV solutions to: {OUTPUT_JSON}")
print()

# Print summary statistics
print("SUMMARY STATISTICS:")
print("-"*80)
print(f"Best Q1 total:     {min(s['total_edits_Q1'] for s in selected):4d} edits")
print(f"Best Q2 total:     {min(s['total_edits_Q2'] for s in selected):4d} edits")
print(f"Best Q2_333BBB:    {min(s['objectives']['Q2_333BBB'] for s in selected):4d} edits (baseline: 431)")
print(f"Best overall:      {min(s['total_edits_all'] for s in selected):4d} edits")
print()

# Compare to baseline and PIL
print("COMPARISON:")
print("-"*80)
best_333bbb = min(s['objectives']['Q2_333BBB'] for s in selected)
pil_best = 371  # From PIL Q2 optimizer
baseline = 431

print(f"Baseline:          {baseline} edits")
print(f"PIL Q2-only best:  {pil_best} edits ({(baseline-pil_best)/baseline*100:.1f}% better)")
print(f"OpenCV 100-sol best: {best_333bbb} edits ({(best_333bbb-baseline)/baseline*100:.1f}% vs baseline)")
print()

if best_333bbb < baseline:
    print(f"✓ OpenCV BEATS baseline by {baseline-best_333bbb} edits!")
else:
    print(f"✗ OpenCV still {best_333bbb-baseline} edits worse than baseline")

if pil_best < best_333bbb:
    print(f"✓ PIL still wins overall ({best_333bbb-pil_best} edits better than OpenCV)")
else:
    print(f"✓ OpenCV wins! ({pil_best-best_333bbb} edits better than PIL)")
