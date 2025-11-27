"""
Extract Top 20 OpenCV Pareto Solutions from Checkpoint

Selects the best 20 solutions based on:
- Best Q1 performance (7 solutions)
- Best Q2 performance (7 solutions)
- Best overall compromises (6 solutions)
"""

import pickle
import json
from pathlib import Path

# Load checkpoint
CHECKPOINT_PATH = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures/nsga2_medium_checkpoint.pkl")
OUTPUT_JSON = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures/nsga2_medium_pareto_top20.json")

print("Loading checkpoint...")
with open(CHECKPOINT_PATH, 'rb') as f:
    checkpoint = pickle.load(f)

# Extract Pareto front from algorithm result
algorithm = checkpoint['algorithm']
pareto_front = algorithm.opt

print(f"Pareto front size: {len(pareto_front)}")

# Convert to readable format
solutions = []
for idx, solution in enumerate(pareto_front):
    # Extract parameters (decision variables)
    x = solution.X
    params = {
        'denoise_h': float(x[0]),
        'clahe_clip': float(x[1]),
        'bilateral_d': int(x[2]),
        'bilateral_sigma_color': float(x[3]),
        'bilateral_sigma_space': float(x[4]),
        'unsharp_amount': float(x[5]),
        'unsharp_radius': float(x[6])
    }

    # Extract objectives (edit distances)
    f = solution.F
    objectives = {
        'Q1_222AAA': int(f[0]),
        'Q1_333BBB': int(f[1]),
        'Q1_333ccc': int(f[2]),
        'Q1_555CCC': int(f[3]),
        'Q2_222AAA': int(f[4]),
        'Q2_333BBB': int(f[5]),
        'Q2_333ccc': int(f[6]),
        'Q2_555CCC': int(f[7])
    }

    # Calculate totals
    q1_total = sum([objectives['Q1_222AAA'], objectives['Q1_333BBB'], objectives['Q1_333ccc'], objectives['Q1_555CCC']])
    q2_total = sum([objectives['Q2_222AAA'], objectives['Q2_333BBB'], objectives['Q2_333ccc'], objectives['Q2_555CCC']])

    solutions.append({
        'id': idx,
        'genome': params,
        'objectives': objectives,
        'q1_total': q1_total,
        'q2_total': q2_total,
        'total_edits': q1_total + q2_total
    })

print(f"Converted {len(solutions)} solutions")
print()

# Select best 20 based on strategy
print("Selecting top 20 solutions...")
print()

# Sort by different criteria
sorted_by_q1 = sorted(solutions, key=lambda x: x['q1_total'])
sorted_by_q2 = sorted(solutions, key=lambda x: x['q2_total'])
sorted_by_total = sorted(solutions, key=lambda x: x['total_edits'])
sorted_by_333bbb = sorted(solutions, key=lambda x: x['objectives']['Q2_333BBB'])

# Select top performers (avoid duplicates using IDs)
selected_ids = set()
selected = []

# Best Q1 performers (7 solutions)
print("Best 7 Q1 performers:")
for sol in sorted_by_q1[:10]:  # Take top 10, filter to 7 unique
    if sol['id'] not in selected_ids and len([s for s in selected if 'q1' in s.get('category', '')]) < 7:
        sol['category'] = 'best_q1'
        selected.append(sol)
        selected_ids.add(sol['id'])
        print(f"  ID {sol['id']}: Q1={sol['q1_total']}, params: h={sol['genome']['denoise_h']:.0f}, CLAHE={sol['genome']['clahe_clip']:.2f}")

print()

# Best Q2 performers (7 solutions)
print("Best 7 Q2 performers:")
for sol in sorted_by_q2[:10]:
    if sol['id'] not in selected_ids and len([s for s in selected if 'q2' in s.get('category', '')]) < 7:
        sol['category'] = 'best_q2'
        selected.append(sol)
        selected_ids.add(sol['id'])
        print(f"  ID {sol['id']}: Q2={sol['q2_total']}, 333BBB={sol['objectives']['Q2_333BBB']}, params: h={sol['genome']['denoise_h']:.0f}, CLAHE={sol['genome']['clahe_clip']:.2f}")

print()

# Best 333BBB rescuers (included in Q2 performers, but show separately)
print("Best 333BBB rescuers:")
for sol in sorted_by_333bbb[:5]:
    if sol['id'] in selected_ids:
        print(f"  ID {sol['id']}: 333BBB={sol['objectives']['Q2_333BBB']} (already selected)")

print()

# Best overall compromises (6 solutions)
print("Best 6 overall compromises:")
for sol in sorted_by_total:
    if sol['id'] not in selected_ids and len(selected) < 20:
        sol['category'] = 'best_compromise'
        selected.append(sol)
        selected_ids.add(sol['id'])
        print(f"  ID {sol['id']}: Total={sol['total_edits']}, Q1={sol['q1_total']}, Q2={sol['q2_total']}")

print()

print(f"Total selected: {len(selected)} solutions")
print()

# Save selected solutions
with open(OUTPUT_JSON, 'w') as f:
    json.dump(selected, f, indent=2)

print(f"✓ Saved top 20 OpenCV solutions to: {OUTPUT_JSON}")
print()

# Print summary
print("=" * 80)
print("SUMMARY")
print("=" * 80)
print(f"Best Q1 total: {min(s['q1_total'] for s in selected)} edits")
print(f"Best Q2 total: {min(s['q2_total'] for s in selected)} edits")
print(f"Best 333BBB: {min(s['objectives']['Q2_333BBB'] for s in selected)} edits")
print(f"Best overall: {min(s['total_edits'] for s in selected)} edits")
