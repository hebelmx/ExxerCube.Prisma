# Real-Corpus Baseline Measurement (RC1.S2)

Generated: 2026-07-22. Measurement only — no calibration, no
tuning, no assertions on outcomes. Full raw detail (amounts, dates, names) lives
out-of-repo in `real-corpus-baseline.json` beside the staged corpus. This file
contains only neutral statement ids, check ids, verdict/status enum names, counts,
confidence numbers, and durations — no raw extracted values, no staging paths.

Reference bundle used for every entry: the single bundle in this repo,
`Demo_Bank_(Iqubica)` — see the class doc-comment on
`RealCorpusBaselineMeasurementTests` for why reusing it (rather than authoring an
unverified second bundle) is the correct "don't invent a new composition" choice.

## Per-statement verdict summary (12 real account statements)

| Id | Product | Signal | BankTier | CondusefTier | Fail | InsufficientData | Pass | Extraction ms | Verdict ms |
|---|---|---|---|---|---|---|---|---|---|
| A-2026-02 | checking | ExtractionGap | ExtractionGap | ExtractionGap | 0 | 0 | 0 | 908 | 678 |
| A-2026-03 | checking | ExtractionGap | ExtractionGap | ExtractionGap | 0 | 0 | 0 | 887 | 675 |
| A-2026-04 | checking | ExtractionGap | ExtractionGap | ExtractionGap | 0 | 0 | 0 | 929 | 680 |
| A-2026-05 | checking | ExtractionGap | ExtractionGap | ExtractionGap | 0 | 0 | 0 | 912 | 722 |
| B-2026-03 | credit_card | Red | Yellow | Red | 6 | 35 | 17 | 1867 | 5456 |
| B-2026-04 | credit_card | Red | Yellow | Red | 6 | 35 | 17 | 2177 | 8443 |
| B-2026-05 | credit_card | Red | Yellow | Red | 6 | 35 | 17 | 3143 | 6944 |
| B-2026-06 | credit_card | Red | Yellow | Red | 6 | 36 | 16 | 1947 | 5782 |
| C-2026-02 | credit_card | Red | Yellow | Red | 10 | 35 | 13 | 2004 | 7326 |
| C-2026-03 | credit_card | Red | Yellow | Red | 6 | 35 | 17 | 2633 | 6787 |
| C-2026-04 | credit_card | Red | Yellow | Red | 6 | 35 | 17 | 2938 | 7676 |
| C-2026-05 | credit_card | Red | Yellow | Red | 6 | 35 | 17 | 2764 | 7353 |

## Per-check aggregate — real account statements (12)

Counts of Fail / InsufficientData(Abstain) / Pass across the 12 real account
statements, by CheckId. A check absent from a statement's findings entirely is
not counted for that statement (engine didn't evaluate it).

| CheckId | Severity (max seen) | Fail | Abstain | Pass | Statements seen |
|---|---|---|---|---|---|
| CL-10 | Warning | 0 | 8 | 0 | 8 |
| CL-17 | Warning | 0 | 8 | 0 | 8 |
| CL-18 | Info | 0 | 0 | 8 | 8 |
| CL-19 | Info | 0 | 0 | 8 | 8 |
| CL-20 | Warning | 0 | 8 | 0 | 8 |
| CL-21 | Warning | 0 | 8 | 0 | 8 |
| CL-22 | Warning | 0 | 1 | 7 | 8 |
| CL-23 | Warning | 0 | 8 | 0 | 8 |
| CL-24 | Warning | 0 | 8 | 0 | 8 |
| CL-25 | Warning | 0 | 8 | 0 | 8 |
| CL-26 | Warning | 0 | 8 | 0 | 8 |
| CL-28 | Info | 0 | 0 | 8 | 8 |
| CL-29 | Warning | 0 | 8 | 0 | 8 |
| CL-31 | Warning | 0 | 8 | 0 | 8 |
| CL-32 | Critical | 8 | 0 | 0 | 8 |
| CL-33 | Info | 0 | 0 | 8 | 8 |
| CL-34 | Info | 0 | 0 | 8 | 8 |
| CL-35 | Info | 0 | 0 | 8 | 8 |
| CL-36 | Warning | 0 | 8 | 0 | 8 |
| CL-37 | Warning | 0 | 8 | 0 | 8 |
| CL-39 | Warning | 0 | 8 | 0 | 8 |
| CL-40 | Warning | 0 | 8 | 0 | 8 |
| CL-41 | Warning | 0 | 8 | 0 | 8 |
| CL-42 | Info | 0 | 0 | 8 | 8 |
| CL-43 | Warning | 0 | 8 | 0 | 8 |
| CL-44 | Warning | 0 | 8 | 0 | 8 |
| CL-45 | Warning | 0 | 8 | 0 | 8 |
| CL-46 | Critical | 1 | 0 | 7 | 8 |
| CL-48 | Critical | 8 | 0 | 0 | 8 |
| CL-49 | Warning | 0 | 8 | 0 | 8 |
| CL-50 | Critical | 1 | 0 | 7 | 8 |
| CL-51 | Critical | 1 | 0 | 7 | 8 |
| CL-52 | Critical | 1 | 0 | 7 | 8 |
| CL-53 | Warning | 0 | 8 | 0 | 8 |
| CLIENT-IMG-CATALOG | Warning | 0 | 8 | 0 | 8 |
| ITEM-58 | Warning | 0 | 8 | 0 | 8 |
| LAW-ADS-PLACEMENT | Info | 0 | 0 | 8 | 8 |
| LAW-DUC-ART27-GAT | Warning | 0 | 8 | 0 | 8 |
| LAW-SEC-ORDER-GAP | Critical | 8 | 0 | 0 | 8 |
| LAW-SEC-PRESENCE | Critical | 8 | 0 | 0 | 8 |
| LAW-SEC-SIZECAP | Warning | 0 | 8 | 0 | 8 |
| LAW-TYPO-BOLD | Warning | 0 | 8 | 0 | 8 |
| LAW-TYPO-MINSIZE | Critical | 8 | 0 | 0 | 8 |
| LAW-§11-URLS | Warning | 0 | 8 | 0 | 8 |
| LAW-§13-TRANSFERENCIA | Warning | 0 | 8 | 0 | 8 |
| LAW-§16-OTRASLINEAS | Warning | 0 | 8 | 0 | 8 |
| LAW-§17-LEGENDS | Warning | 0 | 8 | 0 | 8 |
| LAW-§18-COMPLETE | Warning | 0 | 8 | 0 | 8 |
| LAW-§19-INTERES | Warning | 0 | 8 | 0 | 8 |
| LAW-§20-WATERFALL | Warning | 0 | 8 | 0 | 8 |
| LAW-§23-ABONO-LINK | Info | 0 | 0 | 8 | 8 |
| LAW-§23-STATUS | Info | 0 | 0 | 8 | 8 |
| LAW-§24-QUEJAS | Warning | 0 | 8 | 0 | 8 |
| LAW-§25-REESTRUCTURA | Info | 0 | 0 | 8 | 8 |
| LAW-§26-NOTAS | Info | 0 | 0 | 8 | 8 |
| LAW-§27-GLOSARIO | Critical | 8 | 0 | 0 | 8 |
| LAW-§6-SIMULACION | Warning | 0 | 8 | 0 | 8 |
| LAW-§8-INDICADORES | Warning | 0 | 8 | 0 | 8 |
| NotificationFailure | Warning | 0 | 8 | 0 | 8 |

## Extraction coverage matrix — field x account series (real statements)

For each account series (A = checking, B/C = credit card), how many of the 4
monthly statements extracted the field (`Status == Extracted`), and the mean
confidence across all 4 attempts (extracted or not).

| Field | A (checking) extracted/4 | A mean conf | B (credit card) extracted/4 | B mean conf | C (credit card) extracted/4 | C mean conf |
|---|---|---|---|---|---|---|
| Address | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| BranchNumber | 4/4 | 1.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| CardNumber | 4/4 | 1.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| Clabe | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| ClientName | 4/4 | 1.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| ClientNumber | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.AdeudoPeriodoAnterior | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| PeriodSummary.CargosComprasAMesesCapital | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.CargosRegularesNoMeses | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.Cat | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| PeriodSummary.CreditoDisponible | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.DayCountPrinted | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.IvaInteresesYComisiones | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.MontoComisiones | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.MontoIntereses | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.PagoMinimo | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.PagoMinimoMasMeses | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| PeriodSummary.PagoParaNoGenerarIntereses | 0/4 | 0.00 | 3/4 | 0.75 | 4/4 | 1.00 |
| PeriodSummary.PagosYAbonos | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| PeriodSummary.PaymentDueDate | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| PeriodSummary.PeriodCutDate | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.PeriodStart | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.Product | 0/4 | 0.00 | 0/4 | 0.90 | 0/4 | 0.90 |
| PeriodSummary.SaldoCargosAMeses | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |
| PeriodSummary.SaldoCargosRegulares | 0/4 | 0.00 | 4/4 | 0.89 | 4/4 | 1.00 |
| PeriodSummary.SaldoDeudorTotal | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| PeriodSummary.Tasa | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| PeriodSummary.TotalAbonos | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| PeriodSummary.TotalCargos | 0/4 | 0.00 | 0/4 | 0.00 | 0/4 | 0.00 |
| Rfc | 0/4 | 0.00 | 4/4 | 1.00 | 4/4 | 1.00 |

## Defect-specimen sanity check (4 specimens)

These 4 files are the same fixtures used by the synthetic demo E2E suite
(`VecChecklistDemoE2ETests`) — `defect-good` / `defect-bad-math-cl21` /
`defect-bad-font-cl35` are byte-identical to that suite's `good.pdf` /
`bad-math-cl21.pdf` / `bad-font-cl35.pdf` (verified by sha256 in the corpus
index). Their known injected defects (from that suite's history) are listed for
comparison; the Actual columns are this run's measurement.

| Id | Known injected defect | Actual Signal | Actual FailCheckIds | Actual InsufficientDataCheckIds |
|---|---|---|---|---|
| defect-bad-font-cl35 | Courier font substituted for required Helvetica (historically trips CL-35) | Red | CL-32, CL-35, CL-48, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE, LAW-§27-GLOSARIO | CL-10, CL-17, CL-20, CL-21, CL-23, CL-24, CL-25, CL-26, CL-29, CL-31, CL-36, CL-37, CL-39, CL-40, CL-41, CL-43, CL-44, CL-45, CL-49, CL-53, CLIENT-IMG-CATALOG, ITEM-58, LAW-DUC-ART27-GAT, LAW-SEC-SIZECAP, LAW-TYPO-BOLD, LAW-§11-URLS, LAW-§13-TRANSFERENCIA, LAW-§16-OTRASLINEAS, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§19-INTERES, LAW-§20-WATERFALL, LAW-§24-QUEJAS, LAW-§6-SIMULACION, LAW-§8-INDICADORES |
| defect-bad-math-cl21 | +$11.00 injected on PagoParaNoGenerarIntereses (historically trips CL-21 + CL-22) | Red | CL-32, CL-48, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE, LAW-§27-GLOSARIO | CL-10, CL-17, CL-20, CL-21, CL-23, CL-24, CL-25, CL-26, CL-29, CL-31, CL-36, CL-37, CL-39, CL-40, CL-41, CL-43, CL-44, CL-45, CL-49, CL-53, CLIENT-IMG-CATALOG, ITEM-58, LAW-DUC-ART27-GAT, LAW-SEC-SIZECAP, LAW-TYPO-BOLD, LAW-§11-URLS, LAW-§13-TRANSFERENCIA, LAW-§16-OTRASLINEAS, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§19-INTERES, LAW-§20-WATERFALL, LAW-§24-QUEJAS, LAW-§6-SIMULACION, LAW-§8-INDICADORES |
| defect-good | none (reference-quality demo PDF; historically RED on structural/legend checks, not arithmetic) | Red | CL-32, CL-48, LAW-SEC-ORDER-GAP, LAW-SEC-PRESENCE, LAW-TYPO-MINSIZE, LAW-§27-GLOSARIO | CL-10, CL-17, CL-20, CL-21, CL-23, CL-24, CL-25, CL-26, CL-29, CL-31, CL-36, CL-37, CL-39, CL-40, CL-41, CL-43, CL-44, CL-45, CL-49, CL-53, CLIENT-IMG-CATALOG, ITEM-58, LAW-DUC-ART27-GAT, LAW-SEC-SIZECAP, LAW-TYPO-BOLD, LAW-§11-URLS, LAW-§13-TRANSFERENCIA, LAW-§16-OTRASLINEAS, LAW-§17-LEGENDS, LAW-§18-COMPLETE, LAW-§19-INTERES, LAW-§20-WATERFALL, LAW-§24-QUEJAS, LAW-§6-SIMULACION, LAW-§8-INDICADORES |
| defect-scanned | image-only PDF, no text layer (historically ExtractionGap) | ExtractionGap |  |  |

## Notable observations

- No harness-level Result failures or exceptions were recorded for any of the 16 statements.
- `PeriodSummary.Tasa` on account series B (credit card, 4 statements): extracted 0/4.
- `PeriodSummary.Tasa` on account series C (credit card, 4 statements): extracted 0/4.
- `PeriodSummary.Cat` on account series B (credit card, 4 statements): extracted 0/4.
- `PeriodSummary.Cat` on account series C (credit card, 4 statements): extracted 0/4.
- `PeriodSummary.TotalCargos` on account series B (credit card, 4 statements): extracted 0/4.
- `PeriodSummary.TotalCargos` on account series C (credit card, 4 statements): extracted 0/4.
- `PeriodSummary.TotalAbonos` on account series B (credit card, 4 statements): extracted 0/4.
- `PeriodSummary.TotalAbonos` on account series C (credit card, 4 statements): extracted 0/4.
- Account A (checking/savings product) shows 4/4 statements with an abstention-shaped outcome (ExtractionGap/Blocked signal or non-zero InsufficientData count) — expected per GH#19: non-credit-card products may legitimately not resolve against a credit-card-oriented reference bundle; this is an honest abstention, not a defect.
