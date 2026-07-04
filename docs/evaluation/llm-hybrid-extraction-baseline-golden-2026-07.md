# LLM/Hybrid Extraction Baseline — PRP1 Gold Fixtures

Generated: 2026-07-04 13:01:25Z

> **SMALL-N / DIRECTIONAL — this is a baseline, not a statistical claim. Per-field evaluable N varies by track (see the 'Evaluable' column below; a track is skipped when its gate rejects or its provider is unreachable). Enough to catch gross regressions and rank tracks, not to certify accuracy.**

## Run configuration

- Ollama base URL: `http://localhost:11434`
- Ollama reachable: **True**
- Text model: `llama3.1:8b`
- Vision model: `gemma3:12b`
- Fixtures evaluated: 20 (AGAFADAFSON2-2023-900824_20260704_050223, AGAFADAFSON2-2025-763109_20260704_050226, AGAFF-2023-027961_20260704_050222, CNBV-2025-605483_20260704_050221, CONDUSEF-2023-386930_20260704_050230, CONDUSEF-2024-419892_20260704_050224, FGRMX-2023-571625_20260704_050220, FGRMX-2023-956122_20260704_050221, IMSS-2023-555020_20260704_050223, IMSS-2025-691903_20260704_050229, INFONAVIT-2024-234964_20260704_050225, INFONAVIT-2024-994568_20260704_050226, PJFMX-2023-234626_20260704_050224, PJFMX-2024-415897_20260704_050219, PJFMX-2025-242889_20260704_050229, PJFMX-2025-698406_20260704_050220, PJFMX-2025-738666_20260704_050218, SEIDO-2025-110624_20260704_050227, SEIDO-2025-128577_20260704_050228, SHCP-2023-631352_20260704_050228)

## Per-fixture, per-track, per-field results

| Fixture | Track | Field | Gold | Candidate | Status | Note |
|---|---|---|---|---|---|---|
| AGAFADAFSON2-2023-900824_20260704_050223 | Deterministic | NumeroExpediente | B/FI1-5684-679243-BBB |  | Missing |  |
| AGAFADAFSON2-2023-900824_20260704_050223 | Deterministic | NumeroOficio | AGAFADAFSON2/2023/900824 | AGAFADAFSON2/2023/900824 | Matched |  |
| AGAFADAFSON2-2023-900824_20260704_050223 | Deterministic | AutoridadNombre | Servicio de Administración Tributaria | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| AGAFADAFSON2-2023-900824_20260704_050223 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| AGAFADAFSON2-2023-900824_20260704_050223 | LlmText | NumeroExpediente | B/FI1-5684-679243-BBB |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-5684-679243-BBB' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| AGAFADAFSON2-2023-900824_20260704_050223 | LlmText | NumeroOficio | AGAFADAFSON2/2023/900824 |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-5684-679243-BBB' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| AGAFADAFSON2-2023-900824_20260704_050223 | LlmText | AutoridadNombre | Servicio de Administración Tributaria |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-5684-679243-BBB' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| AGAFADAFSON2-2023-900824_20260704_050223 | LlmText | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-5684-679243-BBB' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| AGAFADAFSON2-2023-900824_20260704_050223 | LlmVision | NumeroExpediente | B/FI1-5684-679243-BBB |  | TrackSkipped | Gate rejected LLM output: Monto '58665.271.89' is not a valid decimal number. |
| AGAFADAFSON2-2023-900824_20260704_050223 | LlmVision | NumeroOficio | AGAFADAFSON2/2023/900824 |  | TrackSkipped | Gate rejected LLM output: Monto '58665.271.89' is not a valid decimal number. |
| AGAFADAFSON2-2023-900824_20260704_050223 | LlmVision | AutoridadNombre | Servicio de Administración Tributaria |  | TrackSkipped | Gate rejected LLM output: Monto '58665.271.89' is not a valid decimal number. |
| AGAFADAFSON2-2023-900824_20260704_050223 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '58665.271.89' is not a valid decimal number. |
| AGAFADAFSON2-2025-763109_20260704_050226 | Deterministic | NumeroExpediente | H/IN2-4034-376793-AAA | H/IN2-4034-376793-AAA | Matched |  |
| AGAFADAFSON2-2025-763109_20260704_050226 | Deterministic | NumeroOficio | AGAFADAFSON2/2025/763109 | AGAFADAFSON2/2025/763109 | Matched |  |
| AGAFADAFSON2-2025-763109_20260704_050226 | Deterministic | AutoridadNombre | Servicio de Administración Tributaria | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| AGAFADAFSON2-2025-763109_20260704_050226 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| AGAFADAFSON2-2025-763109_20260704_050226 | LlmText | NumeroExpediente | H/IN2-4034-376793-AAA | H/IN2-4034-376793-AAA | Matched |  |
| AGAFADAFSON2-2025-763109_20260704_050226 | LlmText | NumeroOficio | AGAFADAFSON2/2025/763109 |  | Missing |  |
| AGAFADAFSON2-2025-763109_20260704_050226 | LlmText | AutoridadNombre | Servicio de Administración Tributaria | Servicio de Administración Tributaria | Matched |  |
| AGAFADAFSON2-2025-763109_20260704_050226 | LlmText | ParteCount | 1 | 2 | Mismatch |  |
| AGAFADAFSON2-2025-763109_20260704_050226 | LlmVision | NumeroExpediente | H/IN2-4034-376793-AAA |  | TrackSkipped | Gate rejected LLM output: Monto '57462.119.26' is not a valid decimal number. |
| AGAFADAFSON2-2025-763109_20260704_050226 | LlmVision | NumeroOficio | AGAFADAFSON2/2025/763109 |  | TrackSkipped | Gate rejected LLM output: Monto '57462.119.26' is not a valid decimal number. |
| AGAFADAFSON2-2025-763109_20260704_050226 | LlmVision | AutoridadNombre | Servicio de Administración Tributaria |  | TrackSkipped | Gate rejected LLM output: Monto '57462.119.26' is not a valid decimal number. |
| AGAFADAFSON2-2025-763109_20260704_050226 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '57462.119.26' is not a valid decimal number. |
| AGAFF-2023-027961_20260704_050222 | Deterministic | NumeroExpediente | B/JU1-1075-509447-SAT | B/JU1-1075-509447-SAT | Matched |  |
| AGAFF-2023-027961_20260704_050222 | Deterministic | NumeroOficio | AGAFF/2023/027961 | AGAFF/2023/027961 | Matched |  |
| AGAFF-2023-027961_20260704_050222 | Deterministic | AutoridadNombre | Servicio de Administración Tributaria | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| AGAFF-2023-027961_20260704_050222 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| AGAFF-2023-027961_20260704_050222 | LlmText | NumeroExpediente | B/JU1-1075-509447-SAT | B/JU1-1075-509447-SAT | Matched |  |
| AGAFF-2023-027961_20260704_050222 | LlmText | NumeroOficio | AGAFF/2023/027961 |  | Missing |  |
| AGAFF-2023-027961_20260704_050222 | LlmText | AutoridadNombre | Servicio de Administración Tributaria | Administración General de Auditoría Fiscal Federal | Mismatch |  |
| AGAFF-2023-027961_20260704_050222 | LlmText | ParteCount | 1 | 2 | Mismatch |  |
| AGAFF-2023-027961_20260704_050222 | LlmVision | NumeroExpediente | B/JU1-1075-509447-SAT |  | TrackSkipped | Gate rejected LLM output: Expediente 'BJU1-1075-509447-SAT' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| AGAFF-2023-027961_20260704_050222 | LlmVision | NumeroOficio | AGAFF/2023/027961 |  | TrackSkipped | Gate rejected LLM output: Expediente 'BJU1-1075-509447-SAT' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| AGAFF-2023-027961_20260704_050222 | LlmVision | AutoridadNombre | Servicio de Administración Tributaria |  | TrackSkipped | Gate rejected LLM output: Expediente 'BJU1-1075-509447-SAT' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| AGAFF-2023-027961_20260704_050222 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'BJU1-1075-509447-SAT' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CNBV-2025-605483_20260704_050221 | Deterministic | NumeroExpediente | B/PL1-9497-933469-BBB | B/PL1-9497-933469-BBB | Matched |  |
| CNBV-2025-605483_20260704_050221 | Deterministic | NumeroOficio | CNBV/2025/605483 | CNBV/2025/605483 | Matched |  |
| CNBV-2025-605483_20260704_050221 | Deterministic | AutoridadNombre | Procuraduría General de la República - Visitaduría General | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| CNBV-2025-605483_20260704_050221 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| CNBV-2025-605483_20260704_050221 | LlmText | NumeroExpediente | B/PL1-9497-933469-BBB |  | TrackSkipped | Gate rejected LLM output: Expediente 'CNBV/2025/605483' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CNBV-2025-605483_20260704_050221 | LlmText | NumeroOficio | CNBV/2025/605483 |  | TrackSkipped | Gate rejected LLM output: Expediente 'CNBV/2025/605483' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CNBV-2025-605483_20260704_050221 | LlmText | AutoridadNombre | Procuraduría General de la República - Visitaduría General |  | TrackSkipped | Gate rejected LLM output: Expediente 'CNBV/2025/605483' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CNBV-2025-605483_20260704_050221 | LlmText | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'CNBV/2025/605483' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CNBV-2025-605483_20260704_050221 | LlmVision | NumeroExpediente | B/PL1-9497-933469-BBB |  | TrackSkipped | Gate rejected LLM output: Expediente 'CNBV/2025/605483' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CNBV-2025-605483_20260704_050221 | LlmVision | NumeroOficio | CNBV/2025/605483 |  | TrackSkipped | Gate rejected LLM output: Expediente 'CNBV/2025/605483' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CNBV-2025-605483_20260704_050221 | LlmVision | AutoridadNombre | Procuraduría General de la República - Visitaduría General |  | TrackSkipped | Gate rejected LLM output: Expediente 'CNBV/2025/605483' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CNBV-2025-605483_20260704_050221 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'CNBV/2025/605483' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| CONDUSEF-2023-386930_20260704_050230 | Deterministic | NumeroExpediente | H/JU1-2541-741274-BBB | H/JU1-2541-741274-BBB | Matched |  |
| CONDUSEF-2023-386930_20260704_050230 | Deterministic | NumeroOficio | CONDUSEF/2023/386930 | CONDUSEF/2023/386930 | Matched |  |
| CONDUSEF-2023-386930_20260704_050230 | Deterministic | AutoridadNombre | Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| CONDUSEF-2023-386930_20260704_050230 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| CONDUSEF-2023-386930_20260704_050230 | LlmText | NumeroExpediente | H/JU1-2541-741274-BBB | H/JU1-2541-741274-BBB | Matched |  |
| CONDUSEF-2023-386930_20260704_050230 | LlmText | NumeroOficio | CONDUSEF/2023/386930 |  | Missing |  |
| CONDUSEF-2023-386930_20260704_050230 | LlmText | AutoridadNombre | Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros | Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros | Matched |  |
| CONDUSEF-2023-386930_20260704_050230 | LlmText | ParteCount | 1 | 2 | Mismatch |  |
| CONDUSEF-2023-386930_20260704_050230 | LlmVision | NumeroExpediente | H/JU1-2541-741274-BBB | H/JU1-2541-741274-BBB | Matched |  |
| CONDUSEF-2023-386930_20260704_050230 | LlmVision | NumeroOficio | CONDUSEF/2023/386930 |  | Missing |  |
| CONDUSEF-2023-386930_20260704_050230 | LlmVision | AutoridadNombre | Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros | Administración General de Auditoría Fiscal Federal Dirección de Orientación Legal | Mismatch |  |
| CONDUSEF-2023-386930_20260704_050230 | LlmVision | ParteCount | 1 | 1 | Matched |  |
| CONDUSEF-2024-419892_20260704_050224 | Deterministic | NumeroExpediente | A/IN2-2882-986327-BBB | A/IN2-2882-986327-BBB | Matched |  |
| CONDUSEF-2024-419892_20260704_050224 | Deterministic | NumeroOficio | CONDUSEF/2024/419892 | CONDUSEF/2024/419892 | Matched |  |
| CONDUSEF-2024-419892_20260704_050224 | Deterministic | AutoridadNombre | Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| CONDUSEF-2024-419892_20260704_050224 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| CONDUSEF-2024-419892_20260704_050224 | LlmText | NumeroExpediente | A/IN2-2882-986327-BBB | A/IN2-2882-986327-BBB | Matched |  |
| CONDUSEF-2024-419892_20260704_050224 | LlmText | NumeroOficio | CONDUSEF/2024/419892 |  | Missing |  |
| CONDUSEF-2024-419892_20260704_050224 | LlmText | AutoridadNombre | Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros | Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros | Matched |  |
| CONDUSEF-2024-419892_20260704_050224 | LlmText | ParteCount | 1 | 3 | Mismatch |  |
| CONDUSEF-2024-419892_20260704_050224 | LlmVision | NumeroExpediente | A/IN2-2882-986327-BBB |  | TrackSkipped | Gate rejected LLM output: Monto '4.526.265.05' is not a valid decimal number. |
| CONDUSEF-2024-419892_20260704_050224 | LlmVision | NumeroOficio | CONDUSEF/2024/419892 |  | TrackSkipped | Gate rejected LLM output: Monto '4.526.265.05' is not a valid decimal number. |
| CONDUSEF-2024-419892_20260704_050224 | LlmVision | AutoridadNombre | Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros |  | TrackSkipped | Gate rejected LLM output: Monto '4.526.265.05' is not a valid decimal number. |
| CONDUSEF-2024-419892_20260704_050224 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '4.526.265.05' is not a valid decimal number. |
| FGRMX-2023-571625_20260704_050220 | Deterministic | NumeroExpediente | B/FI1-3465-780088-FGR |  | Missing |  |
| FGRMX-2023-571625_20260704_050220 | Deterministic | NumeroOficio | FGRMX/2023/571625 | FGRMX/2023/571625 | Matched |  |
| FGRMX-2023-571625_20260704_050220 | Deterministic | AutoridadNombre | Fiscalía General de la República | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| FGRMX-2023-571625_20260704_050220 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| FGRMX-2023-571625_20260704_050220 | LlmText | NumeroExpediente | B/FI1-3465-780088-FGR |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-3465-780088-FGR' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| FGRMX-2023-571625_20260704_050220 | LlmText | NumeroOficio | FGRMX/2023/571625 |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-3465-780088-FGR' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| FGRMX-2023-571625_20260704_050220 | LlmText | AutoridadNombre | Fiscalía General de la República |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-3465-780088-FGR' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| FGRMX-2023-571625_20260704_050220 | LlmText | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-3465-780088-FGR' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| FGRMX-2023-571625_20260704_050220 | LlmVision | NumeroExpediente | B/FI1-3465-780088-FGR | B/F11-3465-780088-FGR | Mismatch |  |
| FGRMX-2023-571625_20260704_050220 | LlmVision | NumeroOficio | FGRMX/2023/571625 |  | Missing |  |
| FGRMX-2023-571625_20260704_050220 | LlmVision | AutoridadNombre | Fiscalía General de la República | Fiscalía General de la República | Matched |  |
| FGRMX-2023-571625_20260704_050220 | LlmVision | ParteCount | 1 | 1 | Matched |  |
| FGRMX-2023-956122_20260704_050221 | Deterministic | NumeroExpediente | A/AS2-7278-537605-BBB | A/AS2-7278-537605-BBB | Matched |  |
| FGRMX-2023-956122_20260704_050221 | Deterministic | NumeroOficio | FGRMX/2023/956122 | FGRMX/2023/956122 | Matched |  |
| FGRMX-2023-956122_20260704_050221 | Deterministic | AutoridadNombre | Fiscalía General de la República | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| FGRMX-2023-956122_20260704_050221 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| FGRMX-2023-956122_20260704_050221 | LlmText | NumeroExpediente | A/AS2-7278-537605-BBB | A/AS2-7278-537605-BBB | Matched |  |
| FGRMX-2023-956122_20260704_050221 | LlmText | NumeroOficio | FGRMX/2023/956122 |  | Missing |  |
| FGRMX-2023-956122_20260704_050221 | LlmText | AutoridadNombre | Fiscalía General de la República | Fiscalía General de la República | Matched |  |
| FGRMX-2023-956122_20260704_050221 | LlmText | ParteCount | 1 | 2 | Mismatch |  |
| FGRMX-2023-956122_20260704_050221 | LlmVision | NumeroExpediente | A/AS2-7278-537605-BBB |  | TrackSkipped | Gate rejected LLM output: Expediente 'AJ/S2-7278-537605-BBB' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| FGRMX-2023-956122_20260704_050221 | LlmVision | NumeroOficio | FGRMX/2023/956122 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AJ/S2-7278-537605-BBB' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| FGRMX-2023-956122_20260704_050221 | LlmVision | AutoridadNombre | Fiscalía General de la República |  | TrackSkipped | Gate rejected LLM output: Expediente 'AJ/S2-7278-537605-BBB' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| FGRMX-2023-956122_20260704_050221 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'AJ/S2-7278-537605-BBB' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| IMSS-2023-555020_20260704_050223 | Deterministic | NumeroExpediente | H/AS2-9295-655574-FGR | H/AS2-9295-655574-FGR | Matched |  |
| IMSS-2023-555020_20260704_050223 | Deterministic | NumeroOficio | IMSS/2023/555020 | IMSS/2023/555020 | Matched |  |
| IMSS-2023-555020_20260704_050223 | Deterministic | AutoridadNombre | Instituto Mexicano del Seguro Social | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| IMSS-2023-555020_20260704_050223 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| IMSS-2023-555020_20260704_050223 | LlmText | NumeroExpediente | H/AS2-9295-655574-FGR | H/AS2-9295-655574-FGR | Matched |  |
| IMSS-2023-555020_20260704_050223 | LlmText | NumeroOficio | IMSS/2023/555020 |  | Missing |  |
| IMSS-2023-555020_20260704_050223 | LlmText | AutoridadNombre | Instituto Mexicano del Seguro Social | Instituto Mexicano del Seguro Social | Matched |  |
| IMSS-2023-555020_20260704_050223 | LlmText | ParteCount | 1 | 1 | Matched |  |
| IMSS-2023-555020_20260704_050223 | LlmVision | NumeroExpediente | H/AS2-9295-655574-FGR | H/AS2-9295-655574-FGR | Matched |  |
| IMSS-2023-555020_20260704_050223 | LlmVision | NumeroOficio | IMSS/2023/555020 |  | Missing |  |
| IMSS-2023-555020_20260704_050223 | LlmVision | AutoridadNombre | Instituto Mexicano del Seguro Social | Administración General de Auditoría Fiscal Federal | Mismatch |  |
| IMSS-2023-555020_20260704_050223 | LlmVision | ParteCount | 1 | 1 | Matched |  |
| IMSS-2025-691903_20260704_050229 | Deterministic | NumeroExpediente | B/IN2-2741-142971-AAA | B/IN2-2741-142971-AAA | Matched |  |
| IMSS-2025-691903_20260704_050229 | Deterministic | NumeroOficio | IMSS/2025/691903 | IMSS/2025/691903 | Matched |  |
| IMSS-2025-691903_20260704_050229 | Deterministic | AutoridadNombre | Procuraduría General de la República - Visitaduría General | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| IMSS-2025-691903_20260704_050229 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| IMSS-2025-691903_20260704_050229 | LlmText | NumeroExpediente | B/IN2-2741-142971-AAA | B/IN2-2741-142971-AAA | Matched |  |
| IMSS-2025-691903_20260704_050229 | LlmText | NumeroOficio | IMSS/2025/691903 |  | Missing |  |
| IMSS-2025-691903_20260704_050229 | LlmText | AutoridadNombre | Procuraduría General de la República - Visitaduría General | Administración General de Auditoría Fiscal Federal | Mismatch |  |
| IMSS-2025-691903_20260704_050229 | LlmText | ParteCount | 1 | 2 | Mismatch |  |
| IMSS-2025-691903_20260704_050229 | LlmVision | NumeroExpediente | B/IN2-2741-142971-AAA |  | TrackSkipped | Gate rejected LLM output: Monto '601.394.37' is not a valid decimal number. |
| IMSS-2025-691903_20260704_050229 | LlmVision | NumeroOficio | IMSS/2025/691903 |  | TrackSkipped | Gate rejected LLM output: Monto '601.394.37' is not a valid decimal number. |
| IMSS-2025-691903_20260704_050229 | LlmVision | AutoridadNombre | Procuraduría General de la República - Visitaduría General |  | TrackSkipped | Gate rejected LLM output: Monto '601.394.37' is not a valid decimal number. |
| IMSS-2025-691903_20260704_050229 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '601.394.37' is not a valid decimal number. |
| INFONAVIT-2024-234964_20260704_050225 | Deterministic | NumeroExpediente | H/JU1-6622-466959-AAA | H/JU1-6622-466959-AAA | Matched |  |
| INFONAVIT-2024-234964_20260704_050225 | Deterministic | NumeroOficio | INFONAVIT/2024/234964 | INFONAVIT/2024/234964 | Matched |  |
| INFONAVIT-2024-234964_20260704_050225 | Deterministic | AutoridadNombre | Instituto del Fondo Nacional de la Vivienda para los Trabajadores | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| INFONAVIT-2024-234964_20260704_050225 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| INFONAVIT-2024-234964_20260704_050225 | LlmText | NumeroExpediente | H/JU1-6622-466959-AAA | H/IJU1-6622-466959-AAA | Mismatch |  |
| INFONAVIT-2024-234964_20260704_050225 | LlmText | NumeroOficio | INFONAVIT/2024/234964 |  | Missing |  |
| INFONAVIT-2024-234964_20260704_050225 | LlmText | AutoridadNombre | Instituto del Fondo Nacional de la Vivienda para los Trabajadores | Instituto del Fondo Nacional de la Vivienda para los Trabajadores | Matched |  |
| INFONAVIT-2024-234964_20260704_050225 | LlmText | ParteCount | 1 | 2 | Mismatch |  |
| INFONAVIT-2024-234964_20260704_050225 | LlmVision | NumeroExpediente | H/JU1-6622-466959-AAA |  | TrackSkipped | Gate rejected LLM output: Monto '2.153.875.07' is not a valid decimal number. |
| INFONAVIT-2024-234964_20260704_050225 | LlmVision | NumeroOficio | INFONAVIT/2024/234964 |  | TrackSkipped | Gate rejected LLM output: Monto '2.153.875.07' is not a valid decimal number. |
| INFONAVIT-2024-234964_20260704_050225 | LlmVision | AutoridadNombre | Instituto del Fondo Nacional de la Vivienda para los Trabajadores |  | TrackSkipped | Gate rejected LLM output: Monto '2.153.875.07' is not a valid decimal number. |
| INFONAVIT-2024-234964_20260704_050225 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '2.153.875.07' is not a valid decimal number. |
| INFONAVIT-2024-994568_20260704_050226 | Deterministic | NumeroExpediente | B/PL1-8038-704672-UIF | B/PL1-8038-704672-UIF | Matched |  |
| INFONAVIT-2024-994568_20260704_050226 | Deterministic | NumeroOficio | INFONAVIT/2024/994568 | INFONAVIT/2024/994568 | Matched |  |
| INFONAVIT-2024-994568_20260704_050226 | Deterministic | AutoridadNombre | Instituto del Fondo Nacional de la Vivienda para los Trabajadores | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| INFONAVIT-2024-994568_20260704_050226 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| INFONAVIT-2024-994568_20260704_050226 | LlmText | NumeroExpediente | B/PL1-8038-704672-UIF | B/PL1-8038-704672-UIF | Matched |  |
| INFONAVIT-2024-994568_20260704_050226 | LlmText | NumeroOficio | INFONAVIT/2024/994568 |  | Missing |  |
| INFONAVIT-2024-994568_20260704_050226 | LlmText | AutoridadNombre | Instituto del Fondo Nacional de la Vivienda para los Trabajadores | Instituto del Fondo Nacional de la Vivienda para los Trabajadores | Matched |  |
| INFONAVIT-2024-994568_20260704_050226 | LlmText | ParteCount | 1 | 1 | Matched |  |
| INFONAVIT-2024-994568_20260704_050226 | LlmVision | NumeroExpediente | B/PL1-8038-704672-UIF |  | TrackSkipped | Gate rejected LLM output: Monto '7488.097.16' is not a valid decimal number. |
| INFONAVIT-2024-994568_20260704_050226 | LlmVision | NumeroOficio | INFONAVIT/2024/994568 |  | TrackSkipped | Gate rejected LLM output: Monto '7488.097.16' is not a valid decimal number. |
| INFONAVIT-2024-994568_20260704_050226 | LlmVision | AutoridadNombre | Instituto del Fondo Nacional de la Vivienda para los Trabajadores |  | TrackSkipped | Gate rejected LLM output: Monto '7488.097.16' is not a valid decimal number. |
| INFONAVIT-2024-994568_20260704_050226 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '7488.097.16' is not a valid decimal number. |
| PJFMX-2023-234626_20260704_050224 | Deterministic | NumeroExpediente | B/IN1-6602-894350-BBB | B/IN1-6602-894350-BBB | Matched |  |
| PJFMX-2023-234626_20260704_050224 | Deterministic | NumeroOficio | PJFMX/2023/234626 | PJFMX/2023/234626 | Matched |  |
| PJFMX-2023-234626_20260704_050224 | Deterministic | AutoridadNombre | Poder Judicial de la Federación | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| PJFMX-2023-234626_20260704_050224 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| PJFMX-2023-234626_20260704_050224 | LlmText | NumeroExpediente | B/IN1-6602-894350-BBB | B/IN1-6602-894350-BBB | Matched |  |
| PJFMX-2023-234626_20260704_050224 | LlmText | NumeroOficio | PJFMX/2023/234626 |  | Missing |  |
| PJFMX-2023-234626_20260704_050224 | LlmText | AutoridadNombre | Poder Judicial de la Federación | Poder Judicial de la Federación | Matched |  |
| PJFMX-2023-234626_20260704_050224 | LlmText | ParteCount | 1 | 2 | Mismatch |  |
| PJFMX-2023-234626_20260704_050224 | LlmVision | NumeroExpediente | B/IN1-6602-894350-BBB | B/I1-6602-894350-BBB | Mismatch |  |
| PJFMX-2023-234626_20260704_050224 | LlmVision | NumeroOficio | PJFMX/2023/234626 |  | Missing |  |
| PJFMX-2023-234626_20260704_050224 | LlmVision | AutoridadNombre | Poder Judicial de la Federación | Administración General de Auditoría Fiscal Federal Tribunal Colegiado en Materia Penal del Primer Circuito | Mismatch |  |
| PJFMX-2023-234626_20260704_050224 | LlmVision | ParteCount | 1 | 1 | Matched |  |
| PJFMX-2024-415897_20260704_050219 | Deterministic | NumeroExpediente | H/AS1-5886-149949-AAA | H/AS1-5886-149949-AAA | Matched |  |
| PJFMX-2024-415897_20260704_050219 | Deterministic | NumeroOficio | PJFMX/2024/415897 | PJFMX/2024/415897 | Matched |  |
| PJFMX-2024-415897_20260704_050219 | Deterministic | AutoridadNombre | Poder Judicial de la Federación | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| PJFMX-2024-415897_20260704_050219 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| PJFMX-2024-415897_20260704_050219 | LlmText | NumeroExpediente | H/AS1-5886-149949-AAA | H/AS1-5886-149949-AAA | Matched |  |
| PJFMX-2024-415897_20260704_050219 | LlmText | NumeroOficio | PJFMX/2024/415897 |  | Missing |  |
| PJFMX-2024-415897_20260704_050219 | LlmText | AutoridadNombre | Poder Judicial de la Federación | Poder Judicial de la Federación | Matched |  |
| PJFMX-2024-415897_20260704_050219 | LlmText | ParteCount | 1 | 1 | Matched |  |
| PJFMX-2024-415897_20260704_050219 | LlmVision | NumeroExpediente | H/AS1-5886-149949-AAA | H/AS1-5886-149949-AAA | Matched |  |
| PJFMX-2024-415897_20260704_050219 | LlmVision | NumeroOficio | PJFMX/2024/415897 |  | Missing |  |
| PJFMX-2024-415897_20260704_050219 | LlmVision | AutoridadNombre | Poder Judicial de la Federación | Poder Judicial de la Federación | Matched |  |
| PJFMX-2024-415897_20260704_050219 | LlmVision | ParteCount | 1 | 1 | Matched |  |
| PJFMX-2025-242889_20260704_050229 | Deterministic | NumeroExpediente | B/AS2-8342-272672-BBB | B/AS2-8342-272672-BBB | Matched |  |
| PJFMX-2025-242889_20260704_050229 | Deterministic | NumeroOficio | PJFMX/2025/242889 | PJFMX/2025/242889 | Matched |  |
| PJFMX-2025-242889_20260704_050229 | Deterministic | AutoridadNombre | Poder Judicial de la Federación | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| PJFMX-2025-242889_20260704_050229 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| PJFMX-2025-242889_20260704_050229 | LlmText | NumeroExpediente | B/AS2-8342-272672-BBB | B/AS2-8342-272672-BBB | Matched |  |
| PJFMX-2025-242889_20260704_050229 | LlmText | NumeroOficio | PJFMX/2025/242889 |  | Missing |  |
| PJFMX-2025-242889_20260704_050229 | LlmText | AutoridadNombre | Poder Judicial de la Federación | Administración General de Auditoría Fiscal Federal | Mismatch |  |
| PJFMX-2025-242889_20260704_050229 | LlmText | ParteCount | 1 | 1 | Matched |  |
| PJFMX-2025-242889_20260704_050229 | LlmVision | NumeroExpediente | B/AS2-8342-272672-BBB | B/AS2-8342-272672-BBB | Matched |  |
| PJFMX-2025-242889_20260704_050229 | LlmVision | NumeroOficio | PJFMX/2025/242889 | PJFMX/2025/242889 | Matched |  |
| PJFMX-2025-242889_20260704_050229 | LlmVision | AutoridadNombre | Poder Judicial de la Federación | Administración General de Auditoría Fiscal Federal | Mismatch |  |
| PJFMX-2025-242889_20260704_050229 | LlmVision | ParteCount | 1 | 1 | Matched |  |
| PJFMX-2025-698406_20260704_050220 | Deterministic | NumeroExpediente | H/FI1-8555-830095-AAA | H/FI1-8555-830095-AAA | Matched |  |
| PJFMX-2025-698406_20260704_050220 | Deterministic | NumeroOficio | PJFMX/2025/698406 | PJFMX/2025/698406 | Matched |  |
| PJFMX-2025-698406_20260704_050220 | Deterministic | AutoridadNombre | Poder Judicial de la Federación | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| PJFMX-2025-698406_20260704_050220 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| PJFMX-2025-698406_20260704_050220 | LlmText | NumeroExpediente | H/FI1-8555-830095-AAA | H/FI1-8555-830095-AAA | Matched |  |
| PJFMX-2025-698406_20260704_050220 | LlmText | NumeroOficio | PJFMX/2025/698406 |  | Missing |  |
| PJFMX-2025-698406_20260704_050220 | LlmText | AutoridadNombre | Poder Judicial de la Federación | Poder Judicial de la Federación | Matched |  |
| PJFMX-2025-698406_20260704_050220 | LlmText | ParteCount | 1 | 2 | Mismatch |  |
| PJFMX-2025-698406_20260704_050220 | LlmVision | NumeroExpediente | H/FI1-8555-830095-AAA |  | TrackSkipped | Gate rejected LLM output: Monto '7437.492.51' is not a valid decimal number. |
| PJFMX-2025-698406_20260704_050220 | LlmVision | NumeroOficio | PJFMX/2025/698406 |  | TrackSkipped | Gate rejected LLM output: Monto '7437.492.51' is not a valid decimal number. |
| PJFMX-2025-698406_20260704_050220 | LlmVision | AutoridadNombre | Poder Judicial de la Federación |  | TrackSkipped | Gate rejected LLM output: Monto '7437.492.51' is not a valid decimal number. |
| PJFMX-2025-698406_20260704_050220 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '7437.492.51' is not a valid decimal number. |
| PJFMX-2025-738666_20260704_050218 | Deterministic | NumeroExpediente | A/PL1-3625-399842-AAA | A/PL1-3625-399842-AAA | Matched |  |
| PJFMX-2025-738666_20260704_050218 | Deterministic | NumeroOficio | PJFMX/2025/738666 | PJFMX/2025/738666 | Matched |  |
| PJFMX-2025-738666_20260704_050218 | Deterministic | AutoridadNombre | Poder Judicial de la Federación | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| PJFMX-2025-738666_20260704_050218 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| PJFMX-2025-738666_20260704_050218 | LlmText | NumeroExpediente | A/PL1-3625-399842-AAA | A/PL1-3625-399842-AAA | Matched |  |
| PJFMX-2025-738666_20260704_050218 | LlmText | NumeroOficio | PJFMX/2025/738666 |  | Missing |  |
| PJFMX-2025-738666_20260704_050218 | LlmText | AutoridadNombre | Poder Judicial de la Federación | Poder Judicial de la Federación | Matched |  |
| PJFMX-2025-738666_20260704_050218 | LlmText | ParteCount | 1 | 1 | Matched |  |
| PJFMX-2025-738666_20260704_050218 | LlmVision | NumeroExpediente | A/PL1-3625-399842-AAA | A/PL1-3625-399842-AAA | Matched |  |
| PJFMX-2025-738666_20260704_050218 | LlmVision | NumeroOficio | PJFMX/2025/738666 | PJFMX/2025/738666 | Matched |  |
| PJFMX-2025-738666_20260704_050218 | LlmVision | AutoridadNombre | Poder Judicial de la Federación | Poder Judicial de la Federación | Matched |  |
| PJFMX-2025-738666_20260704_050218 | LlmVision | ParteCount | 1 | 1 | Matched |  |
| SEIDO-2025-110624_20260704_050227 | Deterministic | NumeroExpediente | B/AS1-5134-143257-IMX | B/AS1-5134-143257-IMX | Matched |  |
| SEIDO-2025-110624_20260704_050227 | Deterministic | NumeroOficio | SEIDO/2025/110624 | SEIDO/2025/110624 | Matched |  |
| SEIDO-2025-110624_20260704_050227 | Deterministic | AutoridadNombre | Subprocuraduría Especializada en Investigación de Delincuencia Organizada | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| SEIDO-2025-110624_20260704_050227 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| SEIDO-2025-110624_20260704_050227 | LlmText | NumeroExpediente | B/AS1-5134-143257-IMX | B/AS1-5134-143257-IMX | Matched |  |
| SEIDO-2025-110624_20260704_050227 | LlmText | NumeroOficio | SEIDO/2025/110624 |  | Missing |  |
| SEIDO-2025-110624_20260704_050227 | LlmText | AutoridadNombre | Subprocuraduría Especializada en Investigación de Delincuencia Organizada | Subprocuraduría Especializada en Investigación de Delincuencia Organizada | Matched |  |
| SEIDO-2025-110624_20260704_050227 | LlmText | ParteCount | 1 | 3 | Mismatch |  |
| SEIDO-2025-110624_20260704_050227 | LlmVision | NumeroExpediente | B/AS1-5134-143257-IMX |  | TrackSkipped | Gate rejected LLM output: Monto '52505.608.91' is not a valid decimal number. |
| SEIDO-2025-110624_20260704_050227 | LlmVision | NumeroOficio | SEIDO/2025/110624 |  | TrackSkipped | Gate rejected LLM output: Monto '52505.608.91' is not a valid decimal number. |
| SEIDO-2025-110624_20260704_050227 | LlmVision | AutoridadNombre | Subprocuraduría Especializada en Investigación de Delincuencia Organizada |  | TrackSkipped | Gate rejected LLM output: Monto '52505.608.91' is not a valid decimal number. |
| SEIDO-2025-110624_20260704_050227 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '52505.608.91' is not a valid decimal number. |
| SEIDO-2025-128577_20260704_050228 | Deterministic | NumeroExpediente | B/FI1-7854-155349-SAT |  | Missing |  |
| SEIDO-2025-128577_20260704_050228 | Deterministic | NumeroOficio | SEIDO/2025/128577 | SEIDO/2025/128577 | Matched |  |
| SEIDO-2025-128577_20260704_050228 | Deterministic | AutoridadNombre | Subprocuraduría Especializada en Investigación de Delincuencia Organizada | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| SEIDO-2025-128577_20260704_050228 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| SEIDO-2025-128577_20260704_050228 | LlmText | NumeroExpediente | B/FI1-7854-155349-SAT |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-7854-155349-SAT' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| SEIDO-2025-128577_20260704_050228 | LlmText | NumeroOficio | SEIDO/2025/128577 |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-7854-155349-SAT' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| SEIDO-2025-128577_20260704_050228 | LlmText | AutoridadNombre | Subprocuraduría Especializada en Investigación de Delincuencia Organizada |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-7854-155349-SAT' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| SEIDO-2025-128577_20260704_050228 | LlmText | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Expediente 'B/Fl1-7854-155349-SAT' does not match required CNBV format (e.g. A/AS1-1111-222222-AAA). |
| SEIDO-2025-128577_20260704_050228 | LlmVision | NumeroExpediente | B/FI1-7854-155349-SAT |  | TrackSkipped | Gate rejected LLM output: Monto '5.170.621.41' is not a valid decimal number. |
| SEIDO-2025-128577_20260704_050228 | LlmVision | NumeroOficio | SEIDO/2025/128577 |  | TrackSkipped | Gate rejected LLM output: Monto '5.170.621.41' is not a valid decimal number. |
| SEIDO-2025-128577_20260704_050228 | LlmVision | AutoridadNombre | Subprocuraduría Especializada en Investigación de Delincuencia Organizada |  | TrackSkipped | Gate rejected LLM output: Monto '5.170.621.41' is not a valid decimal number. |
| SEIDO-2025-128577_20260704_050228 | LlmVision | ParteCount | 1 |  | TrackSkipped | Gate rejected LLM output: Monto '5.170.621.41' is not a valid decimal number. |
| SHCP-2023-631352_20260704_050228 | Deterministic | NumeroExpediente | A/JU1-6129-778206-FGR | A/JU1-6129-778206-FGR | Matched |  |
| SHCP-2023-631352_20260704_050228 | Deterministic | NumeroOficio | SHCP/2023/631352 | SHCP/2023/631352 | Matched |  |
| SHCP-2023-631352_20260704_050228 | Deterministic | AutoridadNombre | Secretaría de Hacienda y Crédito Público | Comisión Nacional Bancaria y de Valores | Mismatch |  |
| SHCP-2023-631352_20260704_050228 | Deterministic | ParteCount | 1 |  | TrackSkipped | deterministic track does not extract SolicitudPartes (architecturally out of scope) |
| SHCP-2023-631352_20260704_050228 | LlmText | NumeroExpediente | A/JU1-6129-778206-FGR | A/JU1-6129-778206-FGR | Matched |  |
| SHCP-2023-631352_20260704_050228 | LlmText | NumeroOficio | SHCP/2023/631352 |  | Missing |  |
| SHCP-2023-631352_20260704_050228 | LlmText | AutoridadNombre | Secretaría de Hacienda y Crédito Público | Secretaría de Hacienda y Crédito Público | Matched |  |
| SHCP-2023-631352_20260704_050228 | LlmText | ParteCount | 1 | 1 | Matched |  |
| SHCP-2023-631352_20260704_050228 | LlmVision | NumeroExpediente | A/JU1-6129-778206-FGR | A/JU1-6129-778206-FGR | Matched |  |
| SHCP-2023-631352_20260704_050228 | LlmVision | NumeroOficio | SHCP/2023/631352 |  | Missing |  |
| SHCP-2023-631352_20260704_050228 | LlmVision | AutoridadNombre | Secretaría de Hacienda y Crédito Público | Administración General de Auditoría Fiscal Federal Dirección General de Análisis Económico | Mismatch |  |
| SHCP-2023-631352_20260704_050228 | LlmVision | ParteCount | 1 | 1 | Matched |  |

## Per-field, per-track accuracy (matches / fixtures-with-gold)

| Field | Track | Matches | Evaluable | Accuracy |
|---|---|---|---|---|
| NumeroExpediente | Deterministic | 17 | 20 | 85% |
| NumeroExpediente | LlmText | 15 | 16 | 94% |
| NumeroExpediente | LlmVision | 6 | 8 | 75% |
| NumeroOficio | Deterministic | 20 | 20 | 100% |
| NumeroOficio | LlmText | 0 | 16 | 0% |
| NumeroOficio | LlmVision | 2 | 8 | 25% |
| AutoridadNombre | Deterministic | 0 | 20 | 0% |
| AutoridadNombre | LlmText | 13 | 16 | 81% |
| AutoridadNombre | LlmVision | 3 | 8 | 38% |
| ParteCount | LlmText | 6 | 16 | 38% |
| ParteCount | LlmVision | 8 | 8 | 100% |

## Per-track coverage (non-null candidates / total evaluations)

| Track | Non-null | Total | Coverage |
|---|---|---|---|
| Deterministic | 57 | 80 | 71% |
| LlmText | 48 | 80 | 60% |
| LlmVision | 26 | 80 | 33% |

_Generated by `LlmExtractionEvalHarness` (S4-A, manual-only — not a CI gate). See `docs/implementation-artifacts/spec-llm-hybrid-extractor-S4A.md`._
