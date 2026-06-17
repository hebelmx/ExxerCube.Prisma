const pptxgen = require("pptxgenjs");
const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.3 x 7.5
const W = 13.3, H = 7.5;

pres.author = "Veriqan";
pres.title = "Veriqan — CONDUSEF Statement-Format Compliance";

// ---- palette ----
const NAVY = "13294B", NAVY2 = "1E2761", INK = "1B2430", MUTED = "5B6675";
const ICE = "CADCFC", LIGHT = "F4F6FB", WHITE = "FFFFFF", GOLD = "E0A458";
const GREEN = "1F9D55", AMBER = "D97706", SLATE = "94A3B8", CARD = "FFFFFF";
const HF = "Georgia", BF = "Calibri";

const shadow = () => ({ type: "outer", color: "000000", blur: 7, offset: 3, angle: 135, opacity: 0.12 });

// ---- helpers ----
function titleBlock(slide, kicker, title, dark) {
  const tc = dark ? WHITE : NAVY, kc = dark ? GOLD : GOLD;
  slide.addText(kicker.toUpperCase(), { x: 0.7, y: 0.45, w: 11.9, h: 0.35, fontFace: BF, fontSize: 12, bold: true, color: kc, charSpacing: 3, margin: 0 });
  slide.addText(title, { x: 0.7, y: 0.78, w: 11.9, h: 0.9, fontFace: HF, fontSize: 30, bold: true, color: tc, margin: 0 });
}
function chip(slide, x, y, label, color) {
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w: 1.05, h: 0.32, fill: { color }, rectRadius: 0.16 });
  slide.addText(label, { x, y, w: 1.05, h: 0.32, align: "center", valign: "middle", fontFace: BF, fontSize: 10, bold: true, color: WHITE, margin: 0 });
}

// ===================== SLIDE 1 — TITLE =====================
let s = pres.addSlide();
s.background = { color: NAVY };
s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 0.28, h: H, fill: { color: GOLD } });
s.addText("VERIQAN", { x: 0.9, y: 1.9, w: 11, h: 1.0, fontFace: HF, fontSize: 54, bold: true, color: WHITE, charSpacing: 2, margin: 0 });
s.addText("CONDUSEF Credit-Card Statement-Format Compliance Verifier", { x: 0.92, y: 3.0, w: 11.5, h: 0.6, fontFace: BF, fontSize: 21, color: ICE, margin: 0 });
s.addShape(pres.shapes.RECTANGLE, { x: 0.92, y: 3.85, w: 4.2, h: 0.04, fill: { color: GOLD } });
s.addText("Every numeral verified. Every statement defensible.", { x: 0.92, y: 4.05, w: 11, h: 0.5, fontFace: HF, fontSize: 16, italic: true, color: GOLD, margin: 0 });
s.addText([
  { text: "Acuerdo CONDUSEF · DOF 29-Dic-2022 · ", options: {} },
  { text: "obligatorio desde 17-Oct-2024", options: { bold: true, color: WHITE } },
], { x: 0.92, y: 6.5, w: 11.5, h: 0.4, fontFace: BF, fontSize: 12, color: ICE, margin: 0 });

// ===================== SLIDE 2 — THE MANDATE =====================
s = pres.addSlide();
s.background = { color: LIGHT };
titleBlock(s, "The legal mandate", "A standardized statement format — required of every issuer", false);
// left bullets
s.addText([
  { text: "CONDUSEF Acuerdo (DOF 29-Dic-2022): the standardized estado de cuenta for credit-card personas físicas.", options: { bullet: true, breakLine: true, paraSpaceAfter: 10 } },
  { text: "Mandatory for every Mexican issuer since 17-Oct-2024 — scoped to RECA-registered card contracts.", options: { bullet: true, breakLine: true, paraSpaceAfter: 10 } },
  { text: "28 mandatory sections + a guía de llenado of strict form rules (order, typography, bold fields, no advertising).", options: { bullet: true, breakLine: true, paraSpaceAfter: 10 } },
  { text: "Enforced reactively: CONDUSEF pulls client files, grades each bank publicly (El Calificador), fines, issues Programas de Cumplimiento Forzoso.", options: { bullet: true } },
], { x: 0.7, y: 1.95, w: 7.4, h: 4.4, fontFace: BF, fontSize: 16, color: INK, valign: "top", lineSpacingMultiple: 1.05 });
// right stat cards
const cards = [
  ["28", "mandatory sections", NAVY],
  ["17-Oct-2024", "compliance deadline", GOLD],
  ["Public", "grading + fines", "B23A48"],
];
let cy = 1.95;
cards.forEach(([big, lab, col]) => {
  s.addShape(pres.shapes.RECTANGLE, { x: 8.5, y: cy, w: 4.1, h: 1.32, fill: { color: CARD }, line: { color: "E2E8F0", width: 1 }, shadow: shadow() });
  s.addShape(pres.shapes.RECTANGLE, { x: 8.5, y: cy, w: 0.12, h: 1.32, fill: { color: col } });
  s.addText(big, { x: 8.75, y: cy + 0.12, w: 3.8, h: 0.7, fontFace: HF, fontSize: 30, bold: true, color: col, margin: 0 });
  s.addText(lab, { x: 8.78, y: cy + 0.85, w: 3.8, h: 0.4, fontFace: BF, fontSize: 13, color: MUTED, margin: 0 });
  cy += 1.5;
});

// ===================== SLIDE 3 — WHAT THE LAW REQUIRES =====================
s = pres.addSlide();
s.background = { color: LIGHT };
titleBlock(s, "What the law requires", "The regulation, in bullet points", false);
const groups = [
  ["Identity & product", "§1–4, §15 — logo (SIPRES), paginación, datos de envío, denominación/categoría, tarjeta/CLABE/RFC, núm. cuenta"],
  ["Payment required", "§5 — periodo, fecha de corte, días, fecha límite (negrillas ≥10pt), pago para no generar intereses, pago mínimo"],
  ["Payment simulation", "§6 — “Cuánto pagarías”: pago mínimo / 2× / 5× → meses y total de intereses (recursión de saldo revolvente)"],
  ["Charges & credits", "§7 — adeudo anterior, cargos regulares y a meses, intereses, comisiones, IVA, pagos y abonos"],
  ["Cost indicators & rates", "§8–10 — indicadores costo anual (12m), CAT (sin IVA), tasa ordinaria [fija/variable]"],
  ["Card usage & lines", "§13, §16 — nivel de uso, saldo deudor total, crédito disponible; otras líneas de crédito"],
  ["Interest basis & last payment", "§19–20 — saldo base · días · tasa · monto (6 tipos); distribución de tu último pago"],
  ["Benefit programs", "§18 — puntos/recompensas: saldo inicial, generados, redimidos, vencidos, por vencer, saldo final"],
  ["Movements", "§22–23 — desglose cronológico, Total cargos/abonos; cargos no reconocidos (estatus)"],
  ["Mandatory texts & form", "§24–27 — quejas, reestructura, 13 notas aclaratorias, glosario (15 términos); tipografía ≥8pt, orden fijo, sin publicidad"],
];
let gx = 0.7, gy = 1.82, gw = 6.0, gh = 0.9, col2 = 6.85;
groups.forEach((g, i) => {
  const x = i < 5 ? gx : col2;
  const y = gy + (i % 5) * (gh + 0.1);
  s.addShape(pres.shapes.RECTANGLE, { x, y, w: gw, h: gh, fill: { color: CARD }, line: { color: "E2E8F0", width: 1 }, shadow: shadow() });
  s.addShape(pres.shapes.RECTANGLE, { x, y, w: 0.1, h: gh, fill: { color: NAVY2 } });
  s.addText(g[0], { x: x + 0.22, y: y + 0.08, w: gw - 0.4, h: 0.34, fontFace: BF, fontSize: 14, bold: true, color: NAVY, margin: 0 });
  s.addText(g[1], { x: x + 0.22, y: y + 0.41, w: gw - 0.4, h: 0.52, fontFace: BF, fontSize: 10.5, color: MUTED, margin: 0, valign: "top" });
});

// ===================== SLIDE 4 — COVERAGE MAP =====================
s = pres.addSlide();
s.background = { color: LIGHT };
titleBlock(s, "Product coverage", "How Veriqan covers the mandate", false);
// legend chips
chip(s, 8.0, 0.92, "BUILT", GREEN);
chip(s, 9.2, 0.92, "IN PROGRESS", AMBER);
chip(s, 10.7, 0.92, "PLANNED", SLATE);
const rows = [
  ["Identity & period extraction", "§4, §5, §15", "BUILT"],
  ["Charges/credits arithmetic", "§7", "BUILT"],
  ["CAT, rates, card usage", "§9, §10, §13", "BUILT"],
  ["Movements desglose & match", "§22", "BUILT"],
  ["Benefit-program values", "§18", "BUILT"],
  ["Text overlap / header / font", "guía (forma)", "BUILT"],
  ["Mandatory legends, Compara tu tarjeta", "§11, §17", "BUILT"],
  ["Pagination, logo, card no., blank pages", "§1, §2, forma", "IN PROGRESS"],
  ["28-section presence & fixed order", "§1–28", "PLANNED"],
  ["Payment simulation (recompute)", "§6", "PLANNED"],
  ["Interest basis & last-payment waterfall", "§19, §20", "PLANNED"],
  ["Annual indicators, other credit lines", "§8, §16", "PLANNED"],
  ["Verbatim notas / glosario / quejas", "§24, §26, §27", "PLANNED"],
  ["Typography floor, bold fields, ads", "guía (forma)", "PLANNED"],
];
const statColor = { "BUILT": GREEN, "IN PROGRESS": AMBER, "PLANNED": SLATE };
const header = [
  { text: "Capability", options: { fill: { color: NAVY }, color: WHITE, bold: true, fontSize: 12, align: "left" } },
  { text: "CONDUSEF §", options: { fill: { color: NAVY }, color: WHITE, bold: true, fontSize: 12, align: "center" } },
  { text: "Status", options: { fill: { color: NAVY }, color: WHITE, bold: true, fontSize: 12, align: "center" } },
];
const body = rows.map((r, i) => {
  const bg = i % 2 ? "EEF2FA" : WHITE;
  return [
    { text: r[0], options: { fill: { color: bg }, color: INK, fontSize: 11.5, align: "left" } },
    { text: r[1], options: { fill: { color: bg }, color: MUTED, fontSize: 11, align: "center" } },
    { text: r[2], options: { fill: { color: statColor[r[2]] }, color: WHITE, bold: true, fontSize: 10, align: "center" } },
  ];
});
s.addTable([header, ...body], {
  x: 0.7, y: 1.5, w: 11.9, colW: [7.3, 2.3, 2.3], rowH: 0.345,
  border: { type: "solid", pt: 0.5, color: "D7DEEA" }, valign: "middle", fontFace: BF, margin: 2,
});

// ===================== SLIDE 5 — THE MOAT =====================
s = pres.addSlide();
s.background = { color: NAVY };
titleBlock(s, "Why it defends", "The moat is not the rules — they are public", true);
const moat = [
  ["No certification exists", "Neither CONDUSEF, CNBV nor Banxico certify the format or approve vendors — first mover, no badge to chase."],
  ["No competitor found", "No product verifies the CONDUSEF statement format today; adjacent RegTech is only PLD/AML."],
  ["Deterministic verdict", "Same statement → same result. Re-runnable by the bank, its auditor, or CONDUSEF — determinism substitutes for a signature."],
  ["Rule → DOF-numeral traceability", "Every check cites its exact Acuerdo numeral: an auditable evidence chain a weekend competitor can’t replicate."],
];
let mx = 0.7, my = 2.05, mw = 5.85, mh = 1.6;
moat.forEach((m, i) => {
  const x = i % 2 ? mx + mw + 0.3 : mx;
  const y = i < 2 ? my : my + mh + 0.25;
  s.addShape(pres.shapes.RECTANGLE, { x, y, w: mw, h: mh, fill: { color: "1B335C" }, line: { color: "2C4A7A", width: 1 } });
  s.addShape(pres.shapes.RECTANGLE, { x, y, w: 0.12, h: mh, fill: { color: GOLD } });
  s.addText(m[0], { x: x + 0.3, y: y + 0.22, w: mw - 0.6, h: 0.5, fontFace: HF, fontSize: 18, bold: true, color: WHITE, margin: 0 });
  s.addText(m[1], { x: x + 0.3, y: y + 0.8, w: mw - 0.6, h: 1.0, fontFace: BF, fontSize: 13, color: ICE, margin: 0, valign: "top" });
});

// ===================== SLIDE 6 — POSITIONING =====================
s = pres.addSlide();
s.background = { color: LIGHT };
titleBlock(s, "How it sells", "From detective report to load-bearing control", false);
const pos = [
  ["Preventive pipeline-gate", "Embed at the bank’s digital PDF generation: a non-compliant statement cannot be emitted. Critical-path infrastructure, not a cuttable report.", NAVY2],
  ["Pre-emptive supervision", "Pass El Calificador before CONDUSEF grades you. Quantified ROI: avoided fines + reputational score on the Buró.", GOLD],
  ["Credible by design", "No format certification exists — so earn the ones that matter: ISO 27001 / SOC 2, and design to the CNBV CUB outsourcing regime.", "2C7A6B"],
];
let px = 0.7, pw = 3.83, pgap = 0.2;
pos.forEach((p, i) => {
  const x = px + i * (pw + pgap);
  s.addShape(pres.shapes.RECTANGLE, { x, y: 2.0, w: pw, h: 3.85, fill: { color: CARD }, line: { color: "E2E8F0", width: 1 }, shadow: shadow() });
  s.addShape(pres.shapes.RECTANGLE, { x, y: 2.0, w: pw, h: 0.16, fill: { color: p[2] } });
  s.addText(String(i + 1), { x: x + 0.3, y: 2.32, w: 0.9, h: 0.8, fontFace: HF, fontSize: 38, bold: true, color: p[2], margin: 0 });
  s.addText(p[0], { x: x + 0.3, y: 3.12, w: pw - 0.6, h: 0.6, fontFace: HF, fontSize: 18, bold: true, color: NAVY, margin: 0, valign: "top" });
  s.addText(p[1], { x: x + 0.3, y: 3.78, w: pw - 0.6, h: 1.9, fontFace: BF, fontSize: 13.5, color: MUTED, margin: 0, valign: "top" });
});

// ===================== SLIDE 7 — CLOSING =====================
s = pres.addSlide();
s.background = { color: NAVY };
s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: W, h: 0.28, fill: { color: GOLD } });
s.addText("The standard that doesn’t officially exist — yet.", { x: 0.9, y: 2.5, w: 11.5, h: 1.2, fontFace: HF, fontSize: 34, bold: true, color: WHITE, margin: 0 });
s.addText([
  { text: "Full CONDUSEF coverage · deterministic · numeral-by-numeral evidence for ", options: {} },
  { text: "Programa de Cumplimiento Forzoso", options: { italic: true, color: GOLD } },
  { text: ".", options: {} },
], { x: 0.92, y: 3.9, w: 11, h: 0.6, fontFace: BF, fontSize: 17, color: ICE, margin: 0 });
s.addText("Veriqan · ExxerCube.Prisma", { x: 0.92, y: 6.6, w: 11, h: 0.4, fontFace: BF, fontSize: 12, color: ICE, margin: 0 });

const out = "E:/Dynamic/IndFusion/ExxerCube.Prisma/ExxerCube.Prisma/docs/planning-artifacts/Veriqan-CONDUSEF-Coverage-Deck.pptx";
pres.writeFile({ fileName: out }).then(() => console.log("WROTE " + out));
