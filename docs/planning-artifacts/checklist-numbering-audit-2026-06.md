---
name: checklist-numbering-audit
date: 2026-06-28
branch: Liv
status: complete
sources:
  - docs/legal/regulations/Acuerdo_estado_de_cuenta.pdf   # CONDUSEF, DOF 29-Dec-2022, mandatory 17-Oct-2024
  - Prisma/Fixtures/PRP2/Check+list+demo+v2+Iqubica_Check_List_VEC.csv   # original client 55-item checklist
  - Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Validation/Rules/*.cs  # rule files (grep)
  - Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/Services/ChecklistIds.cs
  - docs/planning-artifacts/LAW-VS-CHECKLIST-GAP-2026-06-17.md
---

# Veriqan VEC — CL-1…CL-55 Numbering Integrity Audit

**Story:** E9.C2  
**Follows:** E9.C1 (CL-27/30/47 false-alarm resolved; CLIENT-IMG-CATALOG re-homed)

## Purpose

Cross-check every CL-NN identifier across three sources:
1. The **emitting rule** (rule file `CheckId` + `DofNumeral`)
2. The **UI registry** (`ChecklistIds.cs` `Label()` + `DofNumeral()` + `Tier()`)
3. The **original client checklist** (`Check+list+demo+v2+Iqubica_Check_List_VEC.csv`) and the **Acuerdo law text**

Identify squatters, mis-sections, and mislabeled entries; apply only the provably unambiguous §-map fixes.

## Law section reference (Acuerdo DOF 29-Dec-2022)

| § | Title |
|---|---|
| 1 | Logo del Banco (SIPRES, todas las páginas) |
| 2 | Número de página ("Página X de Y", todas las páginas) |
| 3 | Sección de datos de envío (nombre/dirección titular) |
| 4 | Identificación del producto (denominación, núm. tarjeta, RFC, sucursal, núm. cliente, QR, CLABE) |
| 5 | "Tu pago requerido" (periodo, fecha corte, días, fecha límite bold, pago no generar int bold, pago mín) |
| 6 | "Cuánto pagarías" — simulación 3-col (pago mín / 2× / 5×) |
| 7 | Resumen de cargos y abonos del periodo |
| 8 | Indicadores del costo anual (intereses/comisiones pagados últimos 12 meses) |
| 9 | CAT (bold, "Sin IVA") |
| 10 | Tasa de interés anual ordinaria (bold, FIJA o VARIABLE) |
| 11 | "Compara tu tarjeta" — dos URLs exactas |
| 12 | Mensajes importantes (≤700 chars, sin publicidad) |
| 13 | Nivel de uso de tu tarjeta (saldo reg., saldo a meses, saldo deudor total bold, límite, crédito disp., +efectivo, +transferencia) |
| 14 | Notas al calce (footer legend all pages; last page: nombre fiscal/domicilio/tel/URL bold) |
| 15 | Número de cuenta top-left page 2+ |
| 16 | Información de otras líneas de crédito (9 columnas, condicional) |
| 17 | Mensajes adicionales (art-6-IV legends, ≤¼ página) |
| 18 | Programas de beneficios (nombre, unidad+equiv pesos, saldo inicial, usados, vencidos, generados, saldo final, por vencer, contacto) |
| 19 | Saldo sobre el que se calcularon los intereses (6 tipos, base·días·tasa·monto) |
| 20 | Distribución de tu último pago (7-column waterfall) |
| 21 | Sección opcional libre (≤⅓ página) |
| 22 | Desglose de movimientos (a: meses sin int; b: meses con int; c: cargos/abonos regulares) |
| 23 | Cargos no reconocidos (status enum: pendiente / procedente / improcedente) |
| 24 | Atención de quejas (UNE + 800-999-8080 / 55-53-40-09-99) |
| 25 | Reestructura de tu deuda (condicional) |
| 26 | Notas aclaratorias (13 verbatim mandatory notes) |
| 27 | Glosario de términos (15 verbatim terms) |
| 28 | Sección opcional libre |

---

## Master audit table (CL-1…CL-55)

VERDICT codes: **OK** · **MIS-SECTION** (label right, §-map wrong) · **MISLABELED** (label ≠ original/rule) · **SQUATTER** (rule implements different req than label) · **NO-RULE** (labeled req is real but no implementing rule) · **UNVERIFIABLE**

| CL id | ChecklistIds Label | ChecklistIds §-map | Tier | Emitting rule CheckId | Rule DofNumeral | Original checklist item | Law says | VERDICT | Corrected §-map | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| CL-1 | Nombre de la institución bancaria | §4 | Condusef | — | — | Extraer nombre del producto | §4 (denominación producto) | MISLABELED | §4 | Label says "institución" but original is product name; §4 map is acceptable. Flag label for next story. |
| CL-2 | Logotipo del banco en portada | §4 | Condusef | — | — | Extraer nombre completo del cliente | §3 (datos de envío: nombre titular) | MISLABELED | §3 | Label is logo (§1 concept); original is client name extraction (§3). §4 is wrong for both. FLAG. |
| CL-3 | Número de cuenta / contrato | §5 | Condusef | — | — | Extraer dirección | §3 (datos de envío: dirección) | MISLABELED | §3 | Label is account number (§4 concept); original is address (§3). §5 = Tu pago requerido (wrong). FLAG. |
| CL-4 | Nombre del titular en el encabezado | §5 | Condusef | — | — | Extraer Número de sucursal | §4 (sucursal) | MISLABELED | §4 | Label "nombre del titular" close to §3; original is sucursal number (§4). §5 wrong. FLAG. |
| CL-5 | CLABE interbancaria declarada | §5 | Condusef | — | — | Extraer Número de Tarjeta | §4 (núm. tarjeta) | MISLABELED | §4 | Label "CLABE" is §4 concept but original item 5 is tarjeta number. §5 = payments (wrong). FLAG. |
| CL-6 | Fecha de corte presente | §6 | Condusef | — | — | Extraer CLABE Interbancaria | §4 (CLABE) | MISLABELED | §4 | Label describes date (§5 concept); original is CLABE (§4). §6 = simulation (wrong). FLAG. |
| CL-7 | Período de facturación correcto | §6 | Condusef | — | — | Extraer Número de cliente | §4 (núm. cliente) | MISLABELED | §4 | Label describes period (§5 concept); original is núm. cliente (§4). §6 wrong. FLAG. |
| CL-8 | Fecha límite de pago | §6 | Condusef | — | — | Extraer RFC | §4 (RFC) | MISLABELED | §4 | Label describes fecha límite (§5 concept); original is RFC (§4). §6 wrong. FLAG. |
| CL-9 | Días del período declarados | §6 | Condusef | — | — | Tasa de Interés anual ordinaria fija | §10 (tasa interés anual) | MISLABELED | §10 | Label describes días del período (§5); original is tasa de interés (§10). §6 wrong. FLAG. |
| CL-10 | Año del estado de cuenta | §6 | Both | Cl10CatRule / CL-10 | §9 | CAT | §9 (CAT bold, "Sin IVA") | SQUATTER | §9 | Rule implements CAT check (§9) but label says "Año del estado de cuenta". §6=simulation is wrong. **FIXED §6→§9.** Label still wrong; story needed. |
| CL-11 | Saldo inicial declarado | §8 | Condusef | — | — | Extraer Periodo | §5 (periodo, fecha inicio/fin) | MISLABELED | §5 | Label describes saldo (§13 concept); original is period extraction (§5). §8=costo anual (wrong). FLAG. |
| CL-12 | Saldo final declarado | §8 | Condusef | — | — | Extraer Fecha de corte | §5 (fecha corte) | MISLABELED | §5 | Label describes saldo (§13); original is fecha de corte (§5). §8 wrong. FLAG. |
| CL-13 | Límite de crédito declarado | §8 | Condusef | — | — | Extraer Número de días en el periodo | §5 (días del período) | MISLABELED | §5 | Label describes límite de crédito (§13); original is días del período (§5). §8 wrong. FLAG. |
| CL-14 | Crédito disponible = Límite − Saldo | §8 | Condusef | — | — | Extraer Fecha límite de pago | §5 (fecha límite bold) | MISLABELED | §5 | Label describes crédito disponible (§13); original is fecha límite de pago (§5). §8 wrong. FLAG. |
| CL-15 | Pago mínimo declarado | §8 | Condusef | — | — | Extraer Pago para no generar intereses | §5 (pago no generar int bold) | MISLABELED | §5 | Label "pago mínimo" close to §5 content, but rule is for pago para no generar intereses. §8=costo anual wrong. FLAG. |
| CL-16 | Número de columnas de movimientos | §16 | Condusef | — | — | Extraer Pago mínimo + compras a meses | §5 (pago mínimo + compras meses) | MISLABELED | §5 | Label describes column count (not §5 or §16 concept); original is pago mínimo + compras (§5). §16=otras líneas wrong. FLAG. |
| CL-17 | Suma de cargos cuadra | §9 | Both | Cl17AdeudoPeriodoAnteriorRule / CL-17 | §7 | Adeudo del periodo anterior | §7 (Resumen de cargos y abonos) | MIS-SECTION | §7 | Rule correctly anchors to §7 (Resumen). §9=CAT is wrong. **FIXED §9→§7.** Label is approximate but acceptable (arithmetic in §7 Resumen). |
| CL-18 | Suma de abonos cuadra | §9 | Both | Cl18CargosRegularesSumaDesgloseRule / CL-18 | §7 | Cargos regulares (no a meses) | §7 | MIS-SECTION | §7 | Rule: §7. §9 wrong. **FIXED §9→§7.** |
| CL-19 | Continuidad saldo anterior | §9 | Both | Cl19CargosAMesesCapitalSumaDesgloseRule / CL-19 | §7 | Cargos compras a meses (capital) | §7 | MIS-SECTION | §7 | Rule: §7. §9 wrong. **FIXED §9→§7.** |
| CL-20 | Firma de secciones de resumen | §9 | Both | Cl20PagosYAbonosSumaDesgloseRule / CL-20 | §7 | Pagos y abonos | §7 | MIS-SECTION | §7 | Rule: §7. §9 wrong. **FIXED §9→§7.** |
| CL-21 | Aritmética saldo final (cargos vs abonos) | §9 | Both | Cl21PagoParaNoGenerarInteresesRule / CL-21 | §7 | PAGO PARA NO GENERAR INTERESES 2 | §7 | MIS-SECTION | §7 | Rule: §7. §9 wrong. **FIXED §9→§7.** |
| CL-22 | Redondeo permitido (≤ 2 centavos) | §9 | Both | Cl22SaldoCargosRegularesRule / CL-22 | §13 | Saldo cargos regulares | §13 (Nivel de uso) | SQUATTER | §13 | Rule implements saldo cargos regulares (§13). Label says "redondeo" (not in §9 or §13). §9=CAT wrong. **FIXED §9→§13.** Label flag. |
| CL-23 | Fecha de cada movimiento presente | §10 | Both | Cl23SaldoCargosAMesesSumaComprasRule / CL-23 | §13 | Saldo cargos a meses | §13 (Nivel de uso: saldo a meses) | SQUATTER | §13 | Rule implements saldo a meses (§13). Label describes movement date (§22). §10=tasa interés wrong. **FIXED §10→§13.** Label flag. |
| CL-24 | Descripción de cada movimiento | §10 | Both | Cl24SaldoDeudorTotalRule / CL-24 | §13 | Saldo deudor total | §13 (saldo deudor total bold) | SQUATTER | §13 | Rule implements saldo deudor total (§13). Label describes movement description (§22). §10 wrong. **FIXED §10→§13.** Label flag. |
| CL-25 | Importe de cada movimiento | §10 | Both | Cl25CreditoDisponibleRule / CL-25 | §13 | Crédito disponible | §13 (crédito disponible) | SQUATTER | §13 | Rule implements crédito disponible (§13). Label describes movement amount (§22). §10 wrong. **FIXED §10→§13.** Label flag. |
| CL-26 | Referencia de cada transacción | §10 | Both | Cl26CreditoDisponibleEfectivoRule / CL-26 | §13 | Crédito disponible para efectivo | §13 (crédito disp. efectivo) | SQUATTER | §13 | Rule implements crédito disponible efectivo (§13). Label describes transaction reference (§22). §10 wrong. **FIXED §10→§13.** Label flag. |
| CL-27 | Signo de cargo/abono coherente | §10 | Bank | — (E10.C3 planned) | — | Imagen de tarjeta según catálogo (original CLIENT+) | §7 (sign coherence, Resumen) | MIS-SECTION | §7 | E10.C3 will implement sign coherence (§7). §10=tasa interés wrong. **FIXED §10→§7.** Original client item was reassigned. Label describes future rule correctly. |
| CL-28 | Pagos interbancarios identificados | §11 | Both | — | — | No debe mostrar textos traslapados | guía de llenado (form rule, no specific §) | MISLABELED | unknown | Original is layout overlap check (form rule). Label "pagos interbancarios" also doesn't match original. §11=Compara tu tarjeta URLs is wrong. FLAG — no clean mapping. |
| CL-29 | MSI — cuotas y tasa declaradas | §11 | Both | — | — | Los encabezados deben ser en Negrita y Mayúscula | guía de llenado / §7 (bold headers) | MISLABELED | §7 or guía | Original is bold-headers form rule. Label "MSI cuotas y tasa" is different (closer to §13 or §22). §11 wrong. FLAG. |
| CL-30 | Reversos vinculados a cargos | §11 | Bank | — (E10.C4 planned) | — | Mensajes Importantes imagen según catálogo (CLIENT+) | §23 (Cargos no reconocidos, abono↔cargo linkage) | MIS-SECTION | §23 | E10.C4 will implement reversal linkage (§23). §11=Compara tu tarjeta wrong. **FIXED §11→§23.** Original client item was reassigned. Label describes future rule. |
| CL-31 | Paginación en pie de página | §12 | Both | — | — | La paginación debe ser correcta | §2 (Página X de Y, all pages) | MIS-SECTION | §2 | Label is approximately correct for §2. §12=Mensajes importantes is wrong. No implementing rule. FLAG — owner decision on §2 vs LAW-SEC-PRESENCE coverage. |
| CL-32 | Correlación de páginas continua | §12 | Both | Cl32ComparaTuTarjetaRule / CL-32 | §11 | Apartado "COMPARA TU TARJETA" | §11 (Compara tu tarjeta — dos URLs exactas) | SQUATTER | §11 | Rule implements §11 URL check. Label "correlación de páginas" is wrong. §12=Mensajes importantes wrong. **FIXED §12→§11.** Label flag. |
| CL-33 | Color primario de marca | §20 | Both | — | — | Todas las páginas deben tener el escudo del banco | §1 (Logo del Banco, SIPRES, all pages) | MISLABELED | §1 | Original is bank shield/logo (§1). Label "color primario" is a brand rule not in law. §20=distribución pago completely wrong. FLAG — large scope; CLIENT-IMG-CATALOG already covers §1 logo. |
| CL-34 | Imagen de la tarjeta en página 1 | §20 | Both | — | — | Todas las páginas deben tener el número de tarjeta | §15 (número de cuenta, top-left page 2+) | MISLABELED | §15 | Original is account number on all pages (§15). Label "imagen de tarjeta" is CLIENT+ concept. §20 wrong. FLAG. |
| CL-35 | Tipo de fuente (Aptos requerido) | §20 | Bank | — | — | El estándar de tipo de letra es "Aptos" | guía de llenado floor (≥8pt, font-agnostic) + CLIENT+ brand rule | MISLABELED | guía de llenado | Label matches original for Aptos brand requirement. §20=distribución pago is wrong. No single § covers font floor. FLAG. |
| CL-36 | Tamaño mínimo de fuente (10 pt) | §20 | Both | Cl36SaldoInicialRewardsPuntosRule / CL-36 | §18 | Saldo inicial Puntos y Pesos | §18 (Beneficios: saldo inicial) | SQUATTER | §18 | Rule implements saldo inicial rewards (§18). Label says "tamaño fuente" — wrong. §20 wrong. **FIXED §20→§18.** Label flag. |
| CL-37 | Contraste texto/fondo ≥ 4.5:1 | §20 | Bank | Cl37TipoCambioRewardsRule / CL-37 | §18 | Tipo de cambio puntos/pesos | §18 (Beneficios: tipo de cambio) | SQUATTER | §18 | Rule implements tipo de cambio rewards (§18). Label says "contraste" — wrong. §20 wrong. **FIXED §20→§18.** Label flag. |
| CL-38 | Formato A4 o carta declarado | §21 | Both | — | — | Espacios Generados/Redimidos/Por vencer/Vencidos muestran cantidades | §18 (Beneficios: all sub-fields shown even if 0) | MISLABELED | §18 | Original is §18 benefit field completeness. Label "formato A4" is unrelated. §21=sección opcional wrong. FLAG — no rule; label also wrong. |
| CL-39 | Márgenes dentro de tolerancia | §21 | Both | Cl39SaldoTotalPuntosRule / CL-39 | §18 | Saldo Total Puntos = inicial + generados − redimidos − vencidos | §18 (Beneficios: saldo total arithmetic) | SQUATTER | §18 | Rule implements saldo total rewards arithmetic (§18). Label "márgenes" wrong. §21 wrong. **FIXED §21→§18.** Label flag. |
| CL-40 | Marca de agua de seguridad | §21 | Both | Cl40SaldoPendienteComprasAMesesRule / CL-40 | §13 | Saldo pendiente (compras a meses sin intereses) | §22a (COMPRAS A MESES SIN INTERESES, saldo pendiente column) | SQUATTER | FLAG | Rule says §13 but original and law text suggest §22a. Label "marca de agua" is wrong for either. §21 wrong. FLAG for owner — §13 vs §22 conflict. |
| CL-41 | Leyenda CONDUSEF presente | §22 | Both | Cl41NumeroDePagoRule / CL-41 | §13 | Numero de Pago (1 de N de meses sin intereses) | §22a (MESES SIN INTERESES: "Núm. de pago" column) | SQUATTER | FLAG | Rule says §13; ChecklistIds says §22; original suggests §22a; label "Leyenda CONDUSEF" is wrong for any of them. FLAG for owner — §13 vs §22 conflict plus label wrong. |
| CL-42 | Teléfono CONDUSEF correcto | §22 | Both | Cl42MovementDatesInPeriodRule / CL-42 | §22 | Validar fechas de operaciones dentro del período | §22 (Desglose de movimientos, cronológico) | SQUATTER | §22 (§-map OK) | Rule (§22) and §-map agree. Label "teléfono CONDUSEF" is wrong (should relate to movement dates). §-map is correct; no §-map change needed. Label flag only. |
| CL-43 | URL CONDUSEF correcta | §22 | Both | Cl43DesglosePageRangeRule / CL-43 | §22 | Todas las páginas de desglose indican rango de fechas | §22 (Desglose de movimientos) | SQUATTER | §22 (§-map OK) | Rule (§22) and §-map agree. Label "URL CONDUSEF" is wrong (should relate to page range). No §-map change. Label flag. |
| CL-44 | Leyenda CNBV presente | §23 | Both | Cl44DesgloseTotalsMatchRule / CL-44 | §22 | Total cargos y Total abonos coincide con desglose | §22 (Desglose: Total cargos/abonos bold) | SQUATTER | §22 | Rule implements totals match (§22). Label "Leyenda CNBV" wrong. §23=Cargos no reconocidos wrong. **FIXED §23→§22.** Label flag. |
| CL-45 | Leyenda de protección de datos (LFPDPPP) | §24 | Bank | Cl45TransactionDescriptionMatchRule / CL-45 | §22 | La descripción del desglose coincide con Detalle de operaciones | §22 (Desglose: descripción column) | SQUATTER | §22 | Rule implements description match (§22). Label "protección datos LFPDPPP" wrong. §24=Atención quejas wrong. **FIXED §24→§22.** Label flag. |
| CL-46 | Leyenda de cuotas de intercambio | §25 | Both | Cl46MandatoryLegendsRule / CL-46 | §14/§17/§24 | Validar que las leyendas obligatorias estén incluidas | §14 (notas al calce) + §17 (mensajes adicionales) + §24 (quejas) | MIS-SECTION | §14/§17/§24 | Rule correctly maps to §14/§17/§24. §25=Reestructura is wrong. Label "cuotas de intercambio" is approximate. **FIXED §25→§14/§17/§24.** |
| CL-47 | Leyenda de CETES/GAT presente | §25 | Bank | — (E10.C5 planned) | — | Imágenes post-desglose según catálogo (CLIENT+; original reassigned) | Disposición Única de la CONDUSEF Art. 27 (GAT, operaciones pasivas) | MIS-SECTION | Disposición Única Art. 27 | Label correctly describes future E10.C5 GAT-legend rule. §25=Reestructura wrong. **FIXED §25→"Disposición Única Art. 27".** |
| CL-48 | Idioma español en todo el documento | §25 | Both | — | — | Validar que no tenga páginas en blanco | guía de llenado (idioma español) | MISLABELED | guía de llenado | Original is blank-page check; label is "idioma español" (a different form rule). Neither is §25 (Reestructura). FLAG — no clean §. |
| CL-49 | Moneda declarada (MXN) | §25 | Bank | Cl49PromotionsCurrencyRule / CL-49 | §18 | Validar que las promociones insertadas sean vigentes | §18 (Beneficios: currency/vigencia of promotions) | SQUATTER | §18 | Rule implements currency check in promotions context (§18). Label "moneda MXN" close to rule scope. §25=Reestructura wrong. **FIXED §25→§18.** |
| CL-50 | Código de barras o QR legible | §25 | Bank | Cl50FiscalQrRule / CL-50 | §4 | Leer el código QR | §4 (Identificación del producto: QR code) | SQUATTER | §4 | Rule (§4) and original both say §4. §25 wrong. **FIXED §25→§4.** |
| CL-51 | Desglose de intereses ordinarios | — (no arm) | Bank | Cl51FiscalCodeRule / CL-51 | §4 | Extraer código Fiscal | §4 (Identificación del producto) | SQUATTER | §4 | Rule implements fiscal code (§4). Label "desglose de intereses" describes §19 concept. No DofNumeral() arm exists. FLAG — add arm §4 in next label-relabeling story. |
| CL-52 | Desglose de intereses moratorios | — (no arm) | Bank | Cl52IssuerRfcRule / CL-52 | §4 | Extraer RFC emisor | §4 (RFC) | SQUATTER | §4 | Rule (§4). Label describes §19 concept. No arm. FLAG. |
| CL-53 | Número de acreditados adicionales | — (no arm) | Bank | Cl53ReceiverRfcRule / CL-53 | §4 | Extraer RFC receptor | §4 (RFC receptor) | SQUATTER | §4 | Rule (§4). Label wrong. No arm. FLAG. |
| CL-54 | Firma digital del emisor | — (no arm) | Condusef | — | — | Alerta por correo electrónico con resumen | CLIENT+ (reporting mechanism) | MISLABELED | — | Original = email alert (CLIENT+). Label "firma digital" is unrelated. No rule. FLAG. |
| CL-55 | Hash de integridad en pie de página | — (no arm) | Condusef | — | — | Marcar en PDF los hallazgos (resaltados) | CLIENT+ (reporting mechanism) | MISLABELED | — | Original = marked PDF (CLIENT+). Label "hash de integridad" is unrelated. No rule. FLAG. |

---

## A. Squatters and gaps found

**Summary counts:**
- OK: 0
- MIS-SECTION: 9 — CL-17, CL-18, CL-19, CL-20, CL-21, CL-27, CL-30, CL-31, CL-47
- MISLABELED: 18 — CL-1, CL-2, CL-3, CL-4, CL-5, CL-6, CL-7, CL-8, CL-9, CL-11, CL-12, CL-13, CL-14, CL-15, CL-16, CL-28, CL-29, CL-33, CL-34, CL-35, CL-38, CL-48, CL-54, CL-55
- SQUATTER (rule implements different requirement from label): 21 — CL-10, CL-22, CL-23, CL-24, CL-25, CL-26, CL-32, CL-36, CL-37, CL-39, CL-40, CL-41, CL-42, CL-43, CL-44, CL-45, CL-46, CL-49, CL-50, CL-51, CL-52, CL-53
- NO-RULE: 0 (all genuine law gaps are implemented under LAW-§ ids; client-checklist gaps are MISLABELED/SQUATTER)
- UNVERIFIABLE: 0

> **Systemic finding:** CL-27/30/47 are NOT isolated squatters — they are part of a systemic
> renaming. The entire ChecklistIds.cs was written independently of the original Iqubica client
> checklist, using a different conceptual grouping and invented labels. **Every CL-NN in
> ChecklistIds.cs has a label that differs from the original client checklist item.** The groups
> shifted by approximately 5 positions (CL-6..10 in ChecklistIds describe what was CL-11..16 in
> the original, etc.). Additionally, all SQUATTERs in CL-22..45 and CL-49..53 are cases where
> a real rule was assigned the CL-NN id from the original client checklist, but ChecklistIds.cs
> was written with a completely different (invented) label for that same NN.

**Squatters beyond CL-27/30/47 (previously known):**
The following are confirmed squatters — a rule exists with the given CL-NN CheckId but the ChecklistIds.cs label describes an entirely different requirement:

| CL id | ChecklistIds Label | Rule name | Rule implements |
|---|---|---|---|
| CL-10 | Año del estado de cuenta | Cl10CatRule | CAT (§9) |
| CL-22 | Redondeo permitido (≤ 2 centavos) | Cl22SaldoCargosRegularesRule | Saldo cargos regulares (§13) |
| CL-23 | Fecha de cada movimiento presente | Cl23SaldoCargosAMesesSumaComprasRule | Saldo cargos a meses (§13) |
| CL-24 | Descripción de cada movimiento | Cl24SaldoDeudorTotalRule | Saldo deudor total (§13) |
| CL-25 | Importe de cada movimiento | Cl25CreditoDisponibleRule | Crédito disponible (§13) |
| CL-26 | Referencia de cada transacción | Cl26CreditoDisponibleEfectivoRule | Crédito disponible efectivo (§13) |
| CL-32 | Correlación de páginas continua | Cl32ComparaTuTarjetaRule | Compara tu tarjeta URLs (§11) |
| CL-36 | Tamaño mínimo de fuente (10 pt) | Cl36SaldoInicialRewardsPuntosRule | Saldo inicial rewards (§18) |
| CL-37 | Contraste texto/fondo ≥ 4.5:1 | Cl37TipoCambioRewardsRule | Tipo de cambio rewards (§18) |
| CL-39 | Márgenes dentro de tolerancia | Cl39SaldoTotalPuntosRule | Saldo total puntos arithmetic (§18) |
| CL-40 | Marca de agua de seguridad | Cl40SaldoPendienteComprasAMesesRule | Saldo pendiente compras a meses (§13 per rule, §22a per original) |
| CL-41 | Leyenda CONDUSEF presente | Cl41NumeroDePagoRule | Número de pago (§13 per rule) |
| CL-42 | Teléfono CONDUSEF correcto | Cl42MovementDatesInPeriodRule | Movement dates in period (§22) |
| CL-43 | URL CONDUSEF correcta | Cl43DesglosePageRangeRule | Desglose page range (§22) |
| CL-44 | Leyenda CNBV presente | Cl44DesgloseTotalsMatchRule | Totals match (§22) |
| CL-45 | Leyenda de protección de datos (LFPDPPP) | Cl45TransactionDescriptionMatchRule | Description match (§22) |
| CL-49 | Moneda declarada (MXN) | Cl49PromotionsCurrencyRule | Promotions currency (§18) |
| CL-50 | Código de barras o QR legible | Cl50FiscalQrRule | Fiscal QR code (§4) |
| CL-51 | Desglose de intereses ordinarios | Cl51FiscalCodeRule | Fiscal code (§4) |
| CL-52 | Desglose de intereses moratorios | Cl52IssuerRfcRule | RFC emisor (§4) |
| CL-53 | Número de acreditados adicionales | Cl53ReceiverRfcRule | RFC receptor (§4) |

---

## B. Recommended ChecklistIds.cs DofNumeral() edits

**Applied in this story (E9.C2) — unambiguous, backed by rule + original checklist + law:**

| Arm (old) | Arm (new) | Evidence |
|---|---|---|
| `"CL-6" or "CL-7" or "CL-8" or "CL-9" or "CL-10" => "§6"` | Split: `"CL-6" or "CL-7" or "CL-8" or "CL-9" => "§6"` (flagged), `"CL-10" => "§9"` | Cl10CatRule DofNumeral=§9; original CL-10=CAT; §6=simulation (wrong) |
| `"CL-17" or ... or "CL-22" => "§9"` | Split: `"CL-17"..."CL-21" => "§7"`, `"CL-22" => "§13"` | Cl17..21 rules DofNumeral=§7; Cl22 rule DofNumeral=§13; §9=CAT (wrong for both) |
| `"CL-23" or ... or "CL-27" => "§10"` | Split: `"CL-23"..."CL-26" => "§13"`, `"CL-27" => "§7"` | Cl23..26 rules DofNumeral=§13; E10.C3 will implement CL-27 as §7 sign-coherence |
| `"CL-28" or "CL-29" or "CL-30" => "§11"` | Split: `"CL-28" or "CL-29" => "§11"` (flagged), `"CL-30" => "§23"` | E10.C4 will implement CL-30 as §23 reversal linkage; §11 retained for CL-28,29 pending owner |
| `"CL-31" or "CL-32" => "§12"` | Split: `"CL-31" => "§12"` (flagged), `"CL-32" => "§11"` | Cl32ComparaTuTarjetaRule DofNumeral=§11; original CL-32="COMPARA TU TARJETA" |
| `"CL-33" or ... or "CL-37" => "§20"` | Split: `"CL-33" or "CL-34" or "CL-35" => "§20"` (flagged), `"CL-36" or "CL-37" => "§18"` | Cl36,37 rules DofNumeral=§18; §20=distribución pago (wrong) |
| `"CL-38" or "CL-39" or "CL-40" => "§21"` | Split: `"CL-38" => "§21"` (flagged), `"CL-39" => "§18"`, `"CL-40" => "§21"` (flagged) | Cl39 rule DofNumeral=§18; Cl40 rule says §13 vs original §22 (owner decision) |
| `"CL-44" => "§23"` | `"CL-44" => "§22"` | Cl44DesgloseTotalsMatchRule DofNumeral=§22; §23=cargos no reconocidos (wrong) |
| `"CL-45" => "§24"` | `"CL-45" => "§22"` | Cl45TransactionDescriptionMatchRule DofNumeral=§22; §24=quejas (wrong) |
| `"CL-46" or ... or "CL-50" => "§25"` | Split into: `"CL-46" => "§14/§17/§24"`, `"CL-47" => "Disposición Única Art. 27"`, `"CL-48" => "§25"` (flagged), `"CL-49" => "§18"`, `"CL-50" => "§4"` | Cl46 rule=§14/§17/§24; E10.C5 GAT=Disp. Única; Cl49 rule=§18; Cl50 rule=§4 |

**NOT applied (flagged for owner decision):**

| CL ids | Current § | Issue | Why not auto-fixed |
|---|---|---|---|
| CL-1..9 | §4/§5/§6 | Labels AND §-maps both wrong vs original; entire Group 1-2 of ChecklistIds is a wholesale rename | Mass relabel scope — needs owner decision on whether to realign to original Iqubica numbering or keep current labels and just fix §-maps |
| CL-11..16 | §8/§16 | Should be §5 (Tu pago requerido) per law + original; labels also wrong | Same mass-rename scope |
| CL-28, CL-29 | §11 | §11 wrong for both (form rules in original); no clear law § to assign without judgment | No implementing rule; correct § debatable |
| CL-31 | §12 | Should be §2 (paginación); §12=Mensajes importantes is wrong | No implementing rule; LAW-SEC-PRESENCE may already cover §2 check — overlap judgment needed |
| CL-33..35 | §20 | §1/§15/CLIENT+ would be correct; labels also wrong | Labels wrong + §-maps wrong; mass scope |
| CL-38 | §21 | Should be §18 per original (Beneficios field completeness); label also wrong | No implementing rule; label also wrong (two-fix scope) |
| CL-40 | §21 | Cl40 rule says §13 but original/law text suggests §22a; label wrong | Conflicting evidence between rule DofNumeral and law text |
| CL-41 | §22 | Cl41 rule says §13; ChecklistIds says §22; original ambiguous; label wrong | Conflicting evidence |
| CL-48 | §25 | §25=Reestructura wrong; correct is guía de llenado (no single §); label wrong vs original | No clean § to assign |
| CL-51..53 | (empty) | Rules say §4; labels wrong; no DofNumeral() arm | Labels also need changing — two-fix scope; defer to label-relabel story |
| CL-54..55 | (empty) | CLIENT+ reporting items; labels wrong vs original | CLIENT+ scope, no law § |

---

## C. Candidate new-rule stories (NO-RULE genuine gaps)

All genuine law gaps are currently captured under `LAW-§` ids (not CL-NN ids) and already tracked in the backlog. No new NO-RULE stories are generated from this audit.

Pending stories for planned CL-NN rules:
- **E10.C3** — §7 sign-coherence rule under CL-27
- **E10.C4** — §23 abono↔cargo reversal linkage under CL-30
- **E10.C5** — Disposición Única Art. 27 GAT-legend rule under CL-47

---

## D. Reconciliation note vs LAW-VS-CHECKLIST-GAP-2026-06-17.md

The gap document uses the **original Iqubica CL numbering** from `Check+list+demo+v2+Iqubica_Check_List_VEC.csv`. It correctly maps:
- §1 → CL-33 (escudo del banco, all pages) — but ChecklistIds.cs labels CL-33 "Color primario de marca" (MISLABELED)
- §4 → CL-1,4,5,6,7,8,50 (product identification fields) — ChecklistIds.cs has CL-4..8 mapped to §5/§6 (wrong)
- §5 → CL-11..16 (Tu pago requerido) — ChecklistIds.cs maps CL-11..15 to §8 (wrong)
- §7 → CL-17..21 (Resumen cargos/abonos) — ChecklistIds.cs was §9 (fixed)
- §13 → CL-22..26 (Nivel de uso) — ChecklistIds.cs was §9/§10 (fixed)
- §18 → CL-36..39 (Beneficios) — ChecklistIds.cs was §20/§21 (partially fixed)
- §22 → CL-42..45 (Desglose) — ChecklistIds.cs was §22/§23/§24 (partially fixed)

**Key finding:** The gap document is more accurate than ChecklistIds.cs for law-section mapping. The unfixed rows (CL-1..16, CL-28..29, CL-33..35, CL-38, CL-48, CL-54..55) require a follow-on label-relabeling story to bring ChecklistIds.cs labels into alignment with the original client checklist.
