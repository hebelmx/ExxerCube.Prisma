# SmartEnum - Requirement Types (Tipos de Requerimiento)

## Document Source
Based on: "Disposiciones SIARA 4 de septiembre de 2018.pdf" and "R29 A-2911 Aseguramientos, Transferencias y Desbloqueos de Cuentas_03032016.pdf"

---

## Requirement Types Found in Legal Documents

### 1. Requerimientos de Información y Documentación
**Spanish Term:** Solicitud de información y documentación
**English Translation:** Information and Documentation Request
**Legal Authority:** Articles 142 LIC, 34 LACP, 44 LUC, 69 LRASCAP, 55 LFI, 73 LITF
**Page Reference:** Disposiciones SIARA, Article 2, Fraction V, page 5

**Classification Identifiers:**
- Explicitly requests bank statements, contracts, account balances
- Cites Article 142 of Ley de Instituciones de Crédito or equivalent
- Seeks information protected by financial secrecy (secreto financiero)
- Does NOT order freezing or transfer of funds

**Authorities:**
- Autoridades Judiciales (Judicial Authorities)
- Autoridades Hacendarias Federales (Federal Tax Authorities - SAT, FGR fiscal)
- Autoridades Administrativas (Administrative Authorities)

---

### 2. Aseguramiento (Bloqueo/Embargo)
**Spanish Term:** Orden de aseguramiento / Inmovilización / Embargo / Bloqueo
**English Translation:** Seizure / Freezing / Attachment Order
**Legal Authority:** Article 2, Fraction V, inciso b) - Disposiciones SIARA
**Page Reference:** Disposiciones SIARA page 5; R29 report page 17, Type 101

**Classification Identifiers:**
- Contains explicit order to "asegurar", "bloquear", "embargar", or "inmovilizar" funds
- Specifies an amount to be frozen (may be total account balance)
- Issued by judicial or fiscal authority with competence
- Prevents account holder from accessing funds

**Authorities:**
- Autoridades Judiciales (judges in criminal/civil proceedings)
- Autoridades Fiscales (SAT - tax authorities)
- Ministerio Público (Prosecutor's Office)

**SmartEnum Value Suggestion:** `Aseguramiento = 101`

---

### 3. Desbloqueo
**Spanish Term:** Orden de desbloqueo / Liberación
**English Translation:** Unblocking / Release Order
**Legal Authority:** R29 report, Type 102
**Page Reference:** R29 report page 18

**Classification Identifiers:**
- Explicitly orders the release ("desbloquear", "liberar") of previously frozen funds
- References a prior aseguramiento order
- May be issued by ordering authority or amparo court

**Authorities:**
- Same authority that ordered the original aseguramiento
- Juzgados de Amparo (constitutional protection courts)
- Autoridad de Amparo

**SmartEnum Value Suggestion:** `Desbloqueo = 102`

---

### 4. Transferencia de Fondos (Electrónica)
**Spanish Term:** Transferencia de fondos / Disposición de recursos
**English Translation:** Electronic Fund Transfer
**Legal Authority:** R29 report, Type 103
**Page Reference:** R29 report page 18

**Classification Identifiers:**
- Orders electronic transfer of funds to authority's account
- Specifies destination bank account (CLABE)
- Typically follows an aseguramiento
- Uses phrase "transferir" or "disponer de recursos"

**Authorities:**
- Autoridades Fiscales (SAT for tax debts)
- Autoridades Judiciales (court-ordered payments)

**SmartEnum Value Suggestion:** `TransferenciaElectronica = 103`

---

### 5. Situación de Fondos (Cheque de Caja / Billete de Depósito)
**Spanish Term:** Situación de fondos / Cheque de caja
**English Translation:** Fund Disposition via Cashier's Check
**Legal Authority:** R29 report, Type 104
**Page Reference:** R29 report page 18

**Classification Identifiers:**
- Requests funds be made available via cashier's check or deposit slip
- Alternative to electronic transfer
- Specifies physical pickup or delivery

**Authorities:**
- Primarily Autoridades Judiciales
- Autoridades Fiscales (less common)

**SmartEnum Value Suggestion:** `SituacionFondos = 104`

---

## Summary Table for SmartEnum Implementation

| Spanish Term | English Term | Code | Legal Basis | Identifying Keywords |
|--------------|--------------|------|-------------|---------------------|
| Solicitud de Información | Information Request | 100 | Art. 142 LIC | "solicita información", "estados de cuenta", "contratos" |
| Aseguramiento/Bloqueo | Seizure/Freezing | 101 | Art. 2(V)(b) | "asegurar", "bloquear", "embargar", "inmovilizar" |
| Desbloqueo | Unblocking | 102 | R29 Type 102 | "desbloquear", "liberar", "levantar embargo" |
| Transferencia Electrónica | Electronic Transfer | 103 | R29 Type 103 | "transferir", "CLABE", "disponer recursos" |
| Situación de Fondos | Fund Disposition | 104 | R29 Type 104 | "cheque de caja", "billete de depósito", "poner a disposición" |

---

## Decision Tree for Classification

```
1. Does document request account information WITHOUT freezing?
   → YES: InformationRequest (100)
   → NO: Continue

2. Does document order freezing/blocking of funds?
   → YES: Aseguramiento (101)
   → NO: Continue

3. Does document reference prior blocking and order release?
   → YES: Desbloqueo (102)
   → NO: Continue

4. Does document order electronic transfer with CLABE?
   → YES: TransferenciaElectronica (103)
   → NO: Continue

5. Does document order cashier's check or physical delivery?
   → YES: SituacionFondos (104)
   → NO: Flag as UNKNOWN - requires legal review
```

---

## Unknown Type Handling

**Legal Consultation Required When:**
- Document uses terminology not in above categories
- Authority issuing request has unclear jurisdiction
- Request combines multiple types (e.g., block AND transfer)
- Document lacks clear legal foundation citation

**Process:** Flag as `TipoDesconocido = 999` and route to legal compliance officer for manual classification.

---

## Notes for Implementation

1. **Multiple Operations:** A single requirement may request information BEFORE ordering seizure - treat as separate operations
2. **Sequential Dependencies:** Transfers/unblocking typically require prior aseguramiento order
3. **Authority Validation:** Cross-reference authority against "Catálogo de Autoridades Requirentes" (SIARA catalog)
4. **SIARA vs Direct:** Classification applies regardless of notification method (Article 9, Disposiciones SIARA)
