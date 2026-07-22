# Veriqan Corpus Calibration Report

**Generated:** 2026-07-22 21:41 UTC  
**Corpus size:** 3 specimen(s)  
**KnownGood:** 0 · **KnownSynthetic:** 3 (non-compliant placeholders — NOT compliance evidence) · **KnownBroken:** 0

---

## ⚠️ UNCALIBRATED — Detection Power Unmeasured

**This corpus contains ZERO KnownBroken specimens.  All thresholds remain UNCALIBRATED — detection power (true-positive rate) is UNMEASURED.  The distributions below are one-sided (good-only).  Adding deliberately-broken specimens is the single highest-value next step before drawing any conclusion about rule sensitivity.**

---

## Specimen Run Summary

| Specimen | Label | Skipped | Signal | Findings | NewFails | AllowedFails (ref-data gaps) | KnownFixtureDefects (PDF non-compliance) |
|----------|-------|---------|--------|----------|----------|------------------------------|------------------------------------------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | no | Red | 59 | none | CL-42 | CL-31, CL-34, CL-35, CL-48, LAW-TYPO-MINSIZE, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-§13-TRANSFERENCIA, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§20-WATERFALL, LAW-§26-NOTAS, LAW-§27-GLOSARIO |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | no | Red | 59 | none | CL-18, CL-42, CL-52, CL-53 | CL-28, CL-31, CL-34, CL-35, LAW-TYPO-MINSIZE, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-§13-TRANSFERENCIA, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§20-WATERFALL, LAW-§23-STATUS, CL-48, LAW-§26-NOTAS, LAW-§27-GLOSARIO |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | no | Red | 59 | none | CL-42 | CL-31, CL-34, CL-35, LAW-TYPO-MINSIZE, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-§13-TRANSFERENCIA, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§20-WATERFALL, LAW-§26-NOTAS, LAW-§27-GLOSARIO, CL-48 |

## Fail CheckIds per Specimen

### 01+Dummie+VEC+jul_ago+20252.pdf (KnownSynthetic)

| CheckId | AllowedFails (ref-data gap) | KnownFixtureDefects (PDF non-compliance) | IntendedDefects | NewFail? |
|---------|----------------------------|------------------------------------------|-----------------|----------|
| CL-31 | no | yes | no | no |
| CL-34 | no | yes | no | no |
| CL-35 | no | yes | no | no |
| LAW-§13-TRANSFERENCIA | no | yes | no | no |
| LAW-§18-COMPLETE | no | yes | no | no |
| LAW-§20-WATERFALL | no | yes | no | no |
| LAW-§27-GLOSARIO | no | yes | no | no |
| LAW-SEC-ORDER-GAP | no | yes | no | no |
| LAW-SEC-PRESENCE | no | yes | no | no |
| LAW-TYPO-MINSIZE | no | yes | no | no |

### 02+Dummie+VEC+ago_sep+2025.pdf (KnownSynthetic)

| CheckId | AllowedFails (ref-data gap) | KnownFixtureDefects (PDF non-compliance) | IntendedDefects | NewFail? |
|---------|----------------------------|------------------------------------------|-----------------|----------|
| CL-18 | yes | no | no | no |
| CL-28 | no | yes | no | no |
| CL-31 | no | yes | no | no |
| CL-34 | no | yes | no | no |
| CL-35 | no | yes | no | no |
| CL-48 | no | yes | no | no |
| CL-52 | yes | no | no | no |
| CL-53 | yes | no | no | no |
| LAW-§13-TRANSFERENCIA | no | yes | no | no |
| LAW-§18-COMPLETE | no | yes | no | no |
| LAW-§20-WATERFALL | no | yes | no | no |
| LAW-§23-STATUS | no | yes | no | no |
| LAW-§27-GLOSARIO | no | yes | no | no |
| LAW-SEC-ORDER-GAP | no | yes | no | no |
| LAW-SEC-PRESENCE | no | yes | no | no |
| LAW-TYPO-MINSIZE | no | yes | no | no |

### 03+Dummie+VEC+sep_oct+2025.pdf (KnownSynthetic)

| CheckId | AllowedFails (ref-data gap) | KnownFixtureDefects (PDF non-compliance) | IntendedDefects | NewFail? |
|---------|----------------------------|------------------------------------------|-----------------|----------|
| CL-34 | no | yes | no | no |
| CL-35 | no | yes | no | no |
| CL-48 | no | yes | no | no |
| LAW-§13-TRANSFERENCIA | no | yes | no | no |
| LAW-§18-COMPLETE | no | yes | no | no |
| LAW-§20-WATERFALL | no | yes | no | no |
| LAW-§27-GLOSARIO | no | yes | no | no |
| LAW-SEC-ORDER-GAP | no | yes | no | no |
| LAW-SEC-PRESENCE | no | yes | no | no |
| LAW-TYPO-MINSIZE | no | yes | no | no |

## Per-CheckId Verdict Matrix

| CheckId | FailCount | PassCount | InsufficientData | DetectionRate | FalsePositiveRate |
|---------|-----------|-----------|-----------------|---------------|-------------------|
| CL-18 | 1 | 0 | 0 | n/a | 0% |
| CL-28 | 1 | 0 | 0 | n/a | 0% |
| CL-31 | 2 | 0 | 0 | n/a | 0% |
| CL-34 | 3 | 0 | 0 | n/a | 0% |
| CL-35 | 3 | 0 | 0 | n/a | 0% |
| CL-48 | 2 | 0 | 0 | n/a | 0% |
| CL-52 | 1 | 0 | 0 | n/a | 0% |
| CL-53 | 1 | 0 | 0 | n/a | 0% |
| LAW-§13-TRANSFERENCIA | 3 | 0 | 0 | n/a | 0% |
| LAW-§18-COMPLETE | 3 | 0 | 0 | n/a | 0% |
| LAW-§20-WATERFALL | 3 | 0 | 0 | n/a | 0% |
| LAW-§23-STATUS | 1 | 0 | 0 | n/a | 0% |
| LAW-§27-GLOSARIO | 3 | 0 | 0 | n/a | 0% |
| LAW-SEC-ORDER-GAP | 3 | 0 | 0 | n/a | 0% |
| LAW-SEC-PRESENCE | 3 | 0 | 0 | n/a | 0% |
| LAW-TYPO-MINSIZE | 3 | 0 | 0 | n/a | 0% |

## Threshold-Evidence Tables

### CL-19

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=985.39 | (none) | tolerance applied: 0.50 | Verdict: Pass |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=964.73 | (none) | tolerance applied: 0.50 | Verdict: Pass |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=964.73 | (none) | tolerance applied: 0.50 | Verdict: Pass |

### LAW-§11-URLS

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=Both §11 CONDUSEF URLs found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=Both §11 CONDUSEF URLs found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=Both §11 CONDUSEF URLs found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |

### LAW-§13-TRANSFERENCIA

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=missing: 'TRANSFERENCIA DE SALDO' not found in §13 Nivel de uso section text | TRANSFERENCIA DE SALDO | n/a | Verdict: Fail |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=missing: 'TRANSFERENCIA DE SALDO' not found in §13 Nivel de uso section text | TRANSFERENCIA DE SALDO | n/a | Verdict: Fail |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=missing: 'TRANSFERENCIA DE SALDO' not found in §13 Nivel de uso section text | TRANSFERENCIA DE SALDO | n/a | Verdict: Fail |

### LAW-§17-LEGENDS

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=All 4 §17 art-6-IV mandatory legends found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=All 4 §17 art-6-IV mandatory legends found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=All 4 §17 art-6-IV mandatory legends found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |

### LAW-§18-COMPLETE

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=missing: SALDO INICIAL, GENERADOS, REDIMIDOS, VENCIDOS, POR VENCER, SALDO FINAL, UNIDAD, EQUIVALENCIA EN PESOS, CONTACTO | SALDO INICIAL, GENERADOS, REDIMIDOS, VENCIDOS, POR VENCER, SALDO FINAL, UNIDAD, EQUIVALENCIA EN PESOS, CONTACTO | n/a | Verdict: Fail |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=missing: SALDO INICIAL, GENERADOS, REDIMIDOS, VENCIDOS, POR VENCER, SALDO FINAL, UNIDAD, EQUIVALENCIA EN PESOS, CONTACTO | SALDO INICIAL, GENERADOS, REDIMIDOS, VENCIDOS, POR VENCER, SALDO FINAL, UNIDAD, EQUIVALENCIA EN PESOS, CONTACTO | n/a | Verdict: Fail |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=missing: SALDO INICIAL, GENERADOS, REDIMIDOS, VENCIDOS, POR VENCER, SALDO FINAL, UNIDAD, EQUIVALENCIA EN PESOS, CONTACTO | SALDO INICIAL, GENERADOS, REDIMIDOS, VENCIDOS, POR VENCER, SALDO FINAL, UNIDAD, EQUIVALENCIA EN PESOS, CONTACTO | n/a | Verdict: Fail |

### LAW-§20-WATERFALL

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=67796.35 | 61033.35 | tolerance applied: 0.50 | Verdict: Fail |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=34944.69 | 34206.69 | tolerance applied: 0.50 | Verdict: Fail |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=41000.00 | 40760.00 | tolerance applied: 0.50 | Verdict: Fail |

### LAW-§23-STATUS

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=§23 is present and applicable but none of the mandated charge-row status tokens were found in the statement text. | At least one of the mandated status tokens: PENDIENTE EN REVISION, CONCLUIDA PROCEDENTE, CONCLUIDA IMPROCEDENTE | n/a | Verdict: Fail |

### LAW-§24-QUEJAS

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=§24 CONDUSEF contact legend found in document (similarity=1.000). | (none) | tolerance applied: 0.82 | Verdict: Pass |

### LAW-§26-NOTAS

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=All 13 §26 notas aclaratorias (a–m) found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=All 13 §26 notas aclaratorias (a–m) found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=All 13 §26 notas aclaratorias (a–m) found in document (threshold=0.820). | (none) | tolerance applied: 0.82 | Verdict: Pass |

### LAW-§27-GLOSARIO

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=Missing/low-similarity §27 term(s): §27-a (similarity=0.317); §27-b (similarity=0.309); §27-c (similarity=0.407); §27-d (similarity=0.382); §27-e (similarity=0.340); §27-f (similarity=0.485); §27-g (similarity=0.338); §27-h (similarity=0.368); §27-i (similarity=0.316); §27-j (similarity=0.390); §27-k (similarity=0.400); §27-l (similarity=0.343); §27-m (similarity=0.351); §27-n (similarity=0.336); §27-o (similarity=0.370) | All 15 §27 verbatim glosario terms present. | tolerance applied: 0.82 | Verdict: Fail |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=Missing/low-similarity §27 term(s): §27-a (similarity=0.321); §27-b (similarity=0.312); §27-c (similarity=0.421); §27-d (similarity=0.407); §27-e (similarity=0.347); §27-f (similarity=0.471); §27-g (similarity=0.348); §27-h (similarity=0.400); §27-i (similarity=0.314); §27-j (similarity=0.388); §27-k (similarity=0.405); §27-l (similarity=0.359); §27-m (similarity=0.358); §27-n (similarity=0.350); §27-o (similarity=0.375) | All 15 §27 verbatim glosario terms present. | tolerance applied: 0.82 | Verdict: Fail |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=Missing/low-similarity §27 term(s): §27-a (similarity=0.318); §27-b (similarity=0.314); §27-c (similarity=0.410); §27-d (similarity=0.382); §27-e (similarity=0.333); §27-f (similarity=0.471); §27-g (similarity=0.344); §27-h (similarity=0.400); §27-i (similarity=0.312); §27-j (similarity=0.386); §27-k (similarity=0.402); §27-l (similarity=0.359); §27-m (similarity=0.347); §27-n (similarity=0.333); §27-o (similarity=0.347) | All 15 §27 verbatim glosario terms present. | tolerance applied: 0.82 | Verdict: Fail |

### LAW-ADS-PLACEMENT

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | §12 length OK (47 chars ≤ 804 Fail threshold / 700 legal cap); no advertising markers in non-permitted sections. | ≤ 805 chars | OK | From RuleFinding.Observed (verdict: Pass) |
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | 47 chars (§12 section text) | ≤ 805 chars | +758 chars | Measured from StatementModel.Sections[12].SectionText |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | §12 length OK (47 chars ≤ 804 Fail threshold / 700 legal cap); no advertising markers in non-permitted sections. | ≤ 805 chars | OK | From RuleFinding.Observed (verdict: Pass) |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | 47 chars (§12 section text) | ≤ 805 chars | +758 chars | Measured from StatementModel.Sections[12].SectionText |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | §12 length OK (47 chars ≤ 804 Fail threshold / 700 legal cap); no advertising markers in non-permitted sections. | ≤ 805 chars | OK | From RuleFinding.Observed (verdict: Pass) |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | 47 chars (§12 section text) | ≤ 805 chars | +758 chars | Measured from StatementModel.Sections[12].SectionText |

### LAW-SEC-ORDER-GAP

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=Out-of-order sections: §17 (Mensajes adicionales) appears after §26 at reading position 8 (page 2); §18 (Programas de beneficios) appears after §17 at reading position 9 (page 2); §19 (Saldo sobre el que se calcularon los intereses) appears after §18 at reading position 10 (page 2); §16 (Información de otras líneas de crédito) appears after §19 at reading position 11 (page 2); §20 (Distribución de tu último pago) appears after §16 at reading position 12 (page 2); §22 (Desglose de movimientos) appears after §20 at reading position 13 (page 3) | Present sections in ascending §-order with no inter-section same-page gap > 2 cm (56.69 pt). Expected order: §6 → §7 → §8 → §11 → §12 → §13 → §16 → §17 → §18 → §19 → §20 → §22 → §26 → §27 | n/a | Verdict: Fail |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=Out-of-order sections: §17 (Mensajes adicionales) appears after §26 at reading position 8 (page 2); §18 (Programas de beneficios) appears after §17 at reading position 9 (page 2); §19 (Saldo sobre el que se calcularon los intereses) appears after §18 at reading position 10 (page 2); §16 (Información de otras líneas de crédito) appears after §19 at reading position 11 (page 2); §20 (Distribución de tu último pago) appears after §16 at reading position 12 (page 2); §22 (Desglose de movimientos) appears after §20 at reading position 13 (page 3); §23 (Cargos no reconocidos) appears after §22 at reading position 14 (page 5) | Present sections in ascending §-order with no inter-section same-page gap > 2 cm (56.69 pt). Expected order: §6 → §7 → §8 → §11 → §12 → §13 → §16 → §17 → §18 → §19 → §20 → §22 → §23 → §26 → §27 | n/a | Verdict: Fail |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=Out-of-order sections: §17 (Mensajes adicionales) appears after §26 at reading position 8 (page 2); §18 (Programas de beneficios) appears after §17 at reading position 9 (page 2); §19 (Saldo sobre el que se calcularon los intereses) appears after §18 at reading position 10 (page 2); §16 (Información de otras líneas de crédito) appears after §19 at reading position 11 (page 2); §20 (Distribución de tu último pago) appears after §16 at reading position 12 (page 2); §22 (Desglose de movimientos) appears after §20 at reading position 13 (page 3); §24 (Atención de quejas) appears after §22 at reading position 14 (page 7) | Present sections in ascending §-order with no inter-section same-page gap > 2 cm (56.69 pt). Expected order: §6 → §7 → §8 → §11 → §12 → §13 → §16 → §17 → §18 → §19 → §20 → §22 → §24 → §26 → §27 | n/a | Verdict: Fail |

### LAW-SEC-PRESENCE

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=missing: §2, §3, §4, §5, §9, §10, §14, §15, §24 | §2, §3, §4, §5, §6, §7, §8, §9, §10, §11, §12, §13, §14, §15, §16, §17, §18, §19, §20, §22, §24, §26, §27 | n/a | Verdict: Fail |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=missing: §2, §3, §4, §5, §9, §10, §14, §15, §24 | §2, §3, §4, §5, §6, §7, §8, §9, §10, §11, §12, §13, §14, §15, §16, §17, §18, §19, §20, §22, §23, §24, §26, §27 | n/a | Verdict: Fail |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=missing: §2, §3, §4, §5, §9, §10, §14, §15 | §2, §3, §4, §5, §6, §7, §8, §9, §10, §11, §12, §13, §14, §15, §16, §17, §18, §19, §20, §22, §24, §26, §27 | n/a | Verdict: Fail |

### LAW-SEC-SIZECAP

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | §17 text chars: 606 | ≤ 25% of page area | n/a (text-length proxy; page-area requires rendered coords) | §17 'Mensajes adicionales' — direct page-fraction from Observed if rule fires |
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | All measured sections within cap. Measured: §17 10.1 % ≤ 25 %. §21 abstained (§21 is not present); §28 abstained (§28 is not present). | ≤ 25% | OK | From RuleFinding.Observed (verdict: Pass, §Acuerdo §17 (¼ página) / §21·§28 (⅓ página)) |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | §17 text chars: 606 | ≤ 25% of page area | n/a (text-length proxy; page-area requires rendered coords) | §17 'Mensajes adicionales' — direct page-fraction from Observed if rule fires |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | All measured sections within cap. Measured: §17 10.1 % ≤ 25 %. §21 abstained (§21 is not present); §28 abstained (§28 is not present). | ≤ 25% | OK | From RuleFinding.Observed (verdict: Pass, §Acuerdo §17 (¼ página) / §21·§28 (⅓ página)) |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | §17 text chars: 606 | ≤ 25% of page area | n/a (text-length proxy; page-area requires rendered coords) | §17 'Mensajes adicionales' — direct page-fraction from Observed if rule fires |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | All measured sections within cap. Measured: §17 10.1 % ≤ 25 %. §21 abstained (§21 is not present); §28 abstained (§28 is not present). | ≤ 25% | OK | From RuleFinding.Observed (verdict: Pass, §Acuerdo §17 (¼ página) / §21·§28 (⅓ página)) |

### LAW-TYPO-MINSIZE

_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._

| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |
|----------|-------|---------------|-----------------|--------|------|
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | 5.04 pt (min body word) | 8.0 pt | -2.96 pt | Body word count: 2552 |
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | 8.52 pt (fecha-límite words) | 10.0 pt | -1.48 pt | Measured near 'fecha' / 'límite' text tokens |
| 01+Dummie+VEC+jul_ago+20252.pdf | KnownSynthetic | Observed=Body text floor breach: 32 real-word sample(s) below 8 pt. Lowest offender: 5.04 pt ('11', page 1). | ≥ 8 pt (body text floor) | n/a | Verdict: Fail |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | 5.04 pt (min body word) | 8.0 pt | -2.96 pt | Body word count: 2340 |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | 8.52 pt (fecha-límite words) | 10.0 pt | -1.48 pt | Measured near 'fecha' / 'límite' text tokens |
| 02+Dummie+VEC+ago_sep+2025.pdf | KnownSynthetic | Observed=Body text floor breach: 2 real-word sample(s) below 8 pt. Lowest offender: 5.04 pt ('11', page 1). | ≥ 8 pt (body text floor) | n/a | Verdict: Fail |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | 5.04 pt (min body word) | 8.0 pt | -2.96 pt | Body word count: 2652 |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | 8.52 pt (fecha-límite words) | 10.0 pt | -1.48 pt | Measured near 'fecha' / 'límite' text tokens |
| 03+Dummie+VEC+sep_oct+2025.pdf | KnownSynthetic | Observed=Body text floor breach: 4 real-word sample(s) below 8 pt. Lowest offender: 5.04 pt ('11', page 1). | ≥ 8 pt (body text floor) | n/a | Verdict: Fail |

## Recommendations

> **All items below are Proposed, NOT applied.**  Threshold changes are legal/compliance decisions and must never be auto-applied.

- [ ] **Acquire KnownBroken specimens** — at least one per calibratable rule (LAW-TYPO-MINSIZE, LAW-SEC-SIZECAP, LAW-ADS-PLACEMENT, LAW-§19-INTERES, currency residuals). Without broken specimens no threshold recommendation can be made.
- [ ] **Promote KnownSynthetic to KnownGood** only after regenerating the fixture PDFs with correct Aptos fonts, proper section structure, and full verbatim legends. Current KnownSynthetic specimens are NOT compliant statements.
- [ ] **Confirm AllowedFails are reference-data gaps only** — review the AllowedFails table above; every entry must be explained by omitted optional reference data.  Genuine PDF non-compliance must live in KnownFixtureDefects, not AllowedFails.

## AllowedFails Legend (reference-data gaps only)

> These Fails are suppressed ONLY because optional reference data was omitted from
> the synthetic bundle (e.g. faked RFC/rate/period).  They say nothing about the
> PDF's compliance and are NOT fixture defects.

| Specimen | CheckId | Reason |
|----------|---------|--------|
| 01+Dummie+VEC+jul_ago+20252.pdf | CL-42 | Omitted reference-data: movement dates vs. period — synthetic fixture may have transactions outside the bundle period range; not a PDF defect. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-18 | Omitted reference-data: regular charges sum vs. desglose — synthetic Ago-Sep bundle has a period/rate mismatch with the faked reference data; not a PDF defect. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-42 | Omitted reference-data: movement dates vs. period — synthetic fixture may have transactions outside the bundle period range; not a PDF defect. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-52 | Omitted reference-data: issuer RFC not in the synthetic bundle — RFC check cannot pass without a matching RFC entry in the reference bundle. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-53 | Omitted reference-data: receiver RFC not in the synthetic bundle — RFC check cannot pass without a matching RFC entry in the reference bundle. |
| 03+Dummie+VEC+sep_oct+2025.pdf | CL-42 | Omitted reference-data: movement dates vs. period — synthetic fixture may have transactions outside the bundle period range; not a PDF defect. |

## KnownFixtureDefects Legend (genuine PDF non-compliance)

> These Fails reflect GENUINE non-compliance properties of the fixture PDF itself:
> sub-floor typography, non-Aptos fonts, missing mandatory sections, absent verbatim
> legends, pagination issues.  A green driver test with these suppressed MUST NOT be
> read as proof of compliance — these fixtures are NOT compliant statements.

| Specimen | CheckId | Reason |
|----------|---------|--------|
| 01+Dummie+VEC+jul_ago+20252.pdf | CL-31 | Fixture PDF pagination labels do not match document page count — inherent synthetic generator limitation. |
| 01+Dummie+VEC+jul_ago+20252.pdf | CL-34 | Fixture PDF card number absent from some pages — inherent synthetic generator limitation. |
| 01+Dummie+VEC+jul_ago+20252.pdf | CL-35 | Fixture PDF uses non-Aptos embedded fonts — synthetic generator does not embed Aptos; this is a genuine typography non-compliance. |
| 01+Dummie+VEC+jul_ago+20252.pdf | CL-48 | Fixture PDF has intra-page vertical gaps > 2 cm — a genuine consequence of the synthetic generator's missing mandatory sections (see LAW-SEC-PRESENCE) and non-standard section spacing (see LAW-SEC-ORDER-GAP), which leave large blank vertical bands. Detected by the CL-48 2 cm-gap check (VERIQAN-E2-S11); a true positive on this known-malformed synthetic, not a false fail. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-TYPO-MINSIZE | Fixture PDF body text measured at 5.04 pt (word '11', page 1) — genuinely below the 8 pt floor. Synthetic generator emits small-size page-header numbers. Not a false rule. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-SEC-ORDER-GAP | Fixture PDF section gap geometry is non-standard — mandatory sections appear out of order or with non-standard spacing in the synthetic layout. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-SEC-PRESENCE | Fixture PDF is missing mandatory CONDUSEF sections (e.g. §2–§5, §9, §10, §14, §15) — genuine structural non-compliance of the synthetic generator. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-§13-TRANSFERENCIA | Fixture PDF §13 Transferencia content is absent — genuine missing section content in the synthetic generator output. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-§17-LEGENDS | Fixture PDF §17 mandatory verbatim legends are absent — genuine missing legends; not the same as catalog text mismatch. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-§18-COMPLETE | Fixture PDF §18 benefit program completeness check fails — section content incomplete in the synthetic generator output. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-§20-WATERFALL | Fixture PDF §20 payment distribution waterfall is absent — genuine missing content in synthetic generator output. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-§26-NOTAS | Fixture PDF §26 Notas Aclaratorias content is absent — genuine missing section content in the synthetic generator output. |
| 01+Dummie+VEC+jul_ago+20252.pdf | LAW-§27-GLOSARIO | Fixture PDF §27 Glosario content is absent — genuine missing section content in the synthetic generator output. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-28 | Fixture PDF has overlapping text detected in the Ago-Sep layout — genuine visual/structural defect in the synthetic generator output. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-31 | Fixture PDF pagination labels do not match document page count — inherent synthetic generator limitation. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-34 | Fixture PDF card number absent from some pages — inherent synthetic generator limitation. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-35 | Fixture PDF uses non-Aptos embedded fonts — synthetic generator does not embed Aptos; genuine typography non-compliance. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-TYPO-MINSIZE | Fixture PDF body text below 8 pt floor — same root cause as specimen 01 (small-size page-header numbers from synthetic generator). |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-SEC-ORDER-GAP | Fixture PDF section gap geometry is non-standard — same as specimen 01. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-SEC-PRESENCE | Fixture PDF missing mandatory CONDUSEF sections — same structural non-compliance as specimen 01. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-§13-TRANSFERENCIA | Fixture PDF §13 Transferencia content is absent — same as specimen 01. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-§17-LEGENDS | Fixture PDF §17 mandatory verbatim legends are absent — same as specimen 01. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-§18-COMPLETE | Fixture PDF §18 benefit program completeness fails — same as specimen 01. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-§20-WATERFALL | Fixture PDF §20 payment distribution waterfall is absent — same as specimen 01. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-§23-STATUS | Fixture PDF §23 account status content is absent — genuine missing section content in the synthetic generator for the Ago-Sep period. |
| 02+Dummie+VEC+ago_sep+2025.pdf | CL-48 | Fixture PDF has intra-page vertical gaps > 2 cm — a genuine consequence of the synthetic generator's missing mandatory sections (see LAW-SEC-PRESENCE) and non-standard section spacing (see LAW-SEC-ORDER-GAP), which leave large blank vertical bands. Detected by the CL-48 2 cm-gap check (VERIQAN-E2-S11); a true positive on this known-malformed synthetic, not a false fail. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-§26-NOTAS | Fixture PDF §26 Notas Aclaratorias content is absent — same as specimen 01. |
| 02+Dummie+VEC+ago_sep+2025.pdf | LAW-§27-GLOSARIO | Fixture PDF §27 Glosario content is absent — same as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | CL-31 | Fixture PDF pagination labels do not match document page count — inherent synthetic generator limitation. |
| 03+Dummie+VEC+sep_oct+2025.pdf | CL-34 | Fixture PDF card number absent from some pages — inherent synthetic generator limitation. |
| 03+Dummie+VEC+sep_oct+2025.pdf | CL-35 | Fixture PDF uses non-Aptos embedded fonts — synthetic generator does not embed Aptos; genuine typography non-compliance. |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-TYPO-MINSIZE | Fixture PDF body text below 8 pt floor — same root cause as specimen 01 (small-size page-header numbers from synthetic generator). |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-SEC-ORDER-GAP | Fixture PDF section gap geometry is non-standard — same as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-SEC-PRESENCE | Fixture PDF missing mandatory CONDUSEF sections — same structural non-compliance as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-§13-TRANSFERENCIA | Fixture PDF §13 Transferencia content is absent — same as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-§17-LEGENDS | Fixture PDF §17 mandatory verbatim legends are absent — same as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-§18-COMPLETE | Fixture PDF §18 benefit program completeness fails — same as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-§20-WATERFALL | Fixture PDF §20 payment distribution waterfall is absent — same as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-§26-NOTAS | Fixture PDF §26 Notas Aclaratorias content is absent — same as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | LAW-§27-GLOSARIO | Fixture PDF §27 Glosario content is absent — same as specimen 01. |
| 03+Dummie+VEC+sep_oct+2025.pdf | CL-48 | Fixture PDF has intra-page vertical gaps > 2 cm — a genuine consequence of the synthetic generator's missing mandatory sections (see LAW-SEC-PRESENCE) and non-standard section spacing (see LAW-SEC-ORDER-GAP), which leave large blank vertical bands. Detected by the CL-48 2 cm-gap check (VERIQAN-E2-S11); a true positive on this known-malformed synthetic, not a false fail. |

