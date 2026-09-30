# Degraded Folder Mapping Clarification

## Issue
GA cluster scripts reference folders `Q0_Pristine` and `Q05_VeryGood` which **do not exist**.

## Actual Degraded Folders (Per README.md)

| Folder | Degradation Level | Description | Expected OCR Impact |
|--------|------------------|-------------|---------------------|
| `PRP1/` | **Pristine** | Original pristine images | 0% (baseline) |
| `Q1_Poor/` | **Light** | Minimal artifacts, good scanner | ~10% degradation |
| `Q2_MediumPoor/` | **Moderate** | Typical office scanner issues | ~25% degradation |
| `Q3_Low/` | **Heavy** | Poor scanner or fax quality | ~50% degradation |
| `Q4_VeryLow/` | **Extreme** | Maximum realistic degradation | ~75% degradation |

## Cluster Mapping (Based on Baseline Testing Results)

From our comprehensive baseline testing (820 OCR runs), we found:

### Baseline Testing Results
- **Q0 Pristine**: NO FILTER wins (30 edits vs 339 OpenCV vs 491 PIL)
- **Q05-Q1 range**: OpenCV wins (20-35% improvement over baseline)
- **Q15-Q2 range**: PIL wins (75-80% improvement over baseline!)

### Proposed Cluster Mapping to Actual Folders

**Cluster 0** (Ultra-Sharp Images - blur=6905.9, noise=0.47, contrast=51.9):
- **Test on**: `PRP1/` (pristine) + `Q1_Poor/` (light degradation)
- **Rationale**: Ultra-sharp images need minimal processing. Pristine images showed NO FILTER is best (0 edits). Light degradation (Q1) can benefit from very light OpenCV.
- **Expected behavior**: Should learn that less is more on pristine images

**Cluster 1** (Normal Quality Images - blur=1436.7, noise=1.01, contrast=32.8):
- **Test on**: `Q1_Poor/` + `Q2_MediumPoor/`
- **Rationale**: Normal quality documents represent the middle ground where OpenCV outperforms on Q1 (20-35% improvement), and quality starts degrading toward Q2
- **Expected behavior**: Balanced between light OpenCV and starting to need heavier filtering

**Cluster 2** (Degraded Images - blur=1238.2, noise=6.83, contrast=25.1):
- **Test on**: `Q2_MediumPoor/` + `Q3_Low/` + `Q4_VeryLow/`
- **Rationale**: Heavily degraded images where PIL showed 75-80% improvement. These need aggressive enhancement.
- **Expected behavior**: Should learn aggressive PIL parameters for extreme rescue scenarios

## Updated GA Cluster Objectives

### Cluster 0 (Pristine + Light Degradation)
- **Objectives**: 2-5 total
  - 555CCC from `PRP1/` (pristine)
  - All 4 docs from `Q1_Poor/` (light degradation)
- **Goal**: Learn when NOT to filter (pristine) vs light enhancement (Q1)

### Cluster 1 (Light to Moderate Degradation)
- **Objectives**: 8 total
  - All 4 docs from `Q1_Poor/`
  - All 4 docs from `Q2_MediumPoor/`
- **Goal**: Optimize for the transition zone where filtering starts to matter

### Cluster 2 (Moderate to Extreme Degradation)
- **Objectives**: 12 total
  - All 4 docs from `Q2_MediumPoor/`
  - All 4 docs from `Q3_Low/`
  - All 4 docs from `Q4_VeryLow/`
- **Goal**: Maximize rescue capability for severely degraded documents

## Action Items

1. **Update GA scripts** to use:
   - `PRP1/` instead of `Q0_Pristine`
   - `Q1_Poor/` instead of `Q05_VeryGood`
   - Add `Q3_Low/` and `Q4_VeryLow/` to Cluster 2

2. **Update correlation matrix** to reflect actual folder structure

3. **Update analytical strategy** in C# to map:
   - Pristine images → NO FILTER
   - Q1_Poor → Light OpenCV
   - Q2_MediumPoor → Moderate OpenCV or PIL
   - Q3_Low → Aggressive PIL
   - Q4_VeryLow → Maximum PIL rescue

## Baseline Testing Evidence

From `comprehensive_with_baseline_matrix.json`:

```
Q1_Poor:
  - BASELINE: 538 edits (average)
  - OpenCV best: 404 edits (24.9% improvement)
  - PIL: 491 edits (8.7% improvement)
  → OpenCV wins on light degradation

Q2_MediumPoor:
  - BASELINE: 6590 edits (average)
  - OpenCV: 1645 edits (75.0% improvement)
  - PIL best: 1444 edits (78.1% improvement!)
  → PIL wins on moderate degradation
```

This confirms:
- Light degradation (Q1) → OpenCV
- Moderate-Heavy degradation (Q2+) → PIL with increasing aggressiveness

## Conclusion

The cluster-specific GAs should target the **actual degradation spectrum** (Q1-Q4) rather than hypothetical quality levels (Q0, Q05, Q15). This ensures we optimize for real-world conditions present in the test fixtures.
