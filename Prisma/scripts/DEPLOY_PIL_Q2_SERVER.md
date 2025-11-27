# Deploy PIL Q2-Only Optimizer to Server

Quick 2-hour validation run to prove PIL beats OpenCV.

## Configuration

- **Population:** 20
- **Generations:** 30
- **Total evaluations:** 600
- **Runtime:** ~2 hours
- **Parameters:** 2 (contrast_factor, median_size)
- **Objectives:** 4 (Q2 documents only: 222AAA, 333BBB, 333ccc, 555CCC)

## Server Deployment Steps

### 1. Pull Latest Code

```bash
cd ~/ExxerCube.Prisma
git pull origin Kt2
```

### 2. Launch Optimizer in Background

```bash
cd ~/ExxerCube.Prisma/Prisma/scripts
nohup python3 optimize_filters_nsga2_q2_pil.py > nsga2_pil_q2_run.log 2>&1 &
echo $! > nsga2_pil_q2.pid
```

### 3. Monitor Progress

**Live tail:**
```bash
tail -f ~/ExxerCube.Prisma/Prisma/Fixtures/nsga2_q2_pil_progress.log
```

**Check process:**
```bash
ps -p $(cat ~/ExxerCube.Prisma/Prisma/scripts/nsga2_pil_q2.pid)
```

**Count evaluations:**
```bash
wc -l ~/ExxerCube.Prisma/Prisma/Fixtures/nsga2_q2_pil_progress.log
```

### 4. Expected Output Files

After ~2 hours:
- `Prisma/Fixtures/nsga2_q2_pil_pareto_front.json` - Pareto solutions
- `Prisma/Fixtures/nsga2_q2_pil_progress.log` - Progress log
- `Prisma/Fixtures/nsga2_q2_pil_checkpoint.pkl` - Checkpoint (ignored by git)

## Success Criteria

**PIL should BEAT the baseline:**
- PIL Baseline: 431 edits for Q2_333BBB
- OpenCV Server: 576 edits (34% worse)
- OpenCV Windows: 595 edits (38% worse)

**Target:** PIL optimizer should find solutions with Q2_333BBB < 431 edits

## Progress Updates

Create progress snapshot:
```bash
cd ~/ExxerCube.Prisma/Prisma/Fixtures
lines=$(wc -l nsga2_q2_pil_progress.log | awk '{print $1}')
gen=$((lines / 20))  # 20 evals per generation
pct=$((gen * 100 / 30))  # 30 total generations

echo "# PIL Q2 Run Progress ($pct% Complete)" > nsga2_pil_q2_progress_${pct}pct.txt
echo "" >> nsga2_pil_q2_progress_${pct}pct.txt
echo "Gen ~${gen}/30 ($pct%)" >> nsga2_pil_q2_progress_${pct}pct.txt

# Find best Q2_333BBB (2nd position in objectives)
best=$(grep -E "Eval [0-9]+:" nsga2_q2_pil_progress.log | \
       awk -F'[][]' '{split($2, a, ", "); print a[2]}' | \
       sort -n | head -1)

echo "Best Q2_333BBB: ${best} edits" >> nsga2_pil_q2_progress_${pct}pct.txt
echo "Baseline: 431 edits" >> nsga2_pil_q2_progress_${pct}pct.txt

if [ "$best" -lt 431 ]; then
    improvement=$((431 - best))
    pct_improve=$((improvement * 100 / 431))
    echo "Gap: -${improvement} edits (${pct_improve}% BETTER!)" >> nsga2_pil_q2_progress_${pct}pct.txt
    echo "Status: PIL BEATING baseline!" >> nsga2_pil_q2_progress_${pct}pct.txt
else
    gap=$((best - 431))
    pct_worse=$((gap * 100 / 431))
    echo "Gap: +${gap} edits (${pct_worse}% worse)" >> nsga2_pil_q2_progress_${pct}pct.txt
    echo "Status: Still searching..." >> nsga2_pil_q2_progress_${pct}pct.txt
fi

# Force add and commit
git add nsga2_pil_q2_progress_${pct}pct.txt
git commit -m "feat(ocr): PIL Q2 optimizer progress update - ${pct}% complete"
git push origin Kt2
```

## When Complete

Force commit results (progress log is in gitignore):
```bash
cd ~/ExxerCube.Prisma/Prisma/Fixtures
git add -f nsga2_q2_pil_progress.log nsga2_q2_pil_pareto_front.json
git commit -m "feat(ocr): PIL Q2-only optimizer complete - validation results"
git push origin Kt2
```
