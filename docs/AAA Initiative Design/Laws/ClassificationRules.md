# Classification Rules for Authority Requirements

## Document Source
Based on: "Disposiciones SIARA 4 de septiembre de 2018.pdf", "4a. Resolución modificatoria Req. Inf. LIC, LACP.pdf" (Dec 24, 2021), and "R29 A-2911 Aseguramientos_03032016.pdf"

---

## Decision Tree for Document Classification

### Level 1: Validate Document Authenticity

```
START: Document received for classification

1. Is document on official letterhead?
   → NO: REJECT - Article 3(I) violation
   → YES: Continue

2. Does document contain autograph or electronic signature?
   → NO: REJECT - Article 3(II) violation
   → YES: Continue

3. Is document properly founded and motivated?
   → NO: REJECT - Article 3(III) violation
   → YES: Continue to Level 2
```

**Page Reference:** Disposiciones SIARA, Article 3, pages 5-6

**Rejection Reasons (Article 17):**
- Missing formalities (Article 17(I))
- Outside CNBV competence (Article 17(III))
- Information not related to financial entity operations (Article 17(II))

---

### Level 2: Determine Notification Channel

```
Document passes Level 1 authentication

1. Was document received through SIARA system?
   → YES: Channel = "Vía CNBV" (Code 200)
   → NO: Continue

2. Was document received by physical delivery or direct email?
   → YES: Channel = "Directo" (Code 100)
   → NO: REJECT - Invalid notification method

RECORD: MedioSolicitudRequerimiento
```

**Page Reference:** R29 Instructivo Column 4, page 4; Disposiciones Article 9, pages 8-9

**Channel Validation Rules:**
- **SIARA (200):** Must be digitized TIFF format per Anexo 1 specifications
- **Direct (100):** Must use official formats from CNBV website or be digitized PDF per Anexo 1
- **Email Reception:** Only at comunicacionAA@cnbv.gob.mx (Article 9, Section III)
- **Business Hours:** 9:00-15:00 hrs on business days only (Article 11)

---

### Level 3: Identify Requesting Authority

```
Document authenticated and channel determined

1. Extract authority name from letterhead
   → Match against "Catálogo de Autoridades Requirentes"

2. Authority found in catalog?
   → NO: FLAG for legal review (potential new authority)
   → YES: Continue

3. Determine authority type:
   → Judicial: Courts, judges, magistrates
   → Fiscal/Hacendaria: SAT, FGR (fiscal unit), SHCP
   → Administrative: UIF, CONDUSEF, other regulators

4. Validate authority competence per cited articles:
   → Article 142 LIC / 34 LACP / 44 LUC / 69 LRASCAP / 55 LFI / 73 LITF

RECORD: ClaveAutoridad, DescripcionAutoridad
```

**Page Reference:** R29 Instructivo Columns 5-6, page 5; Disposiciones Article 2(I), page 4

**Key Authorities by Type:**

| Type | Examples | Typical Requests |
|------|----------|------------------|
| **Judicial** | Juzgados Penales, Juzgados Civiles, Tribunales | Information, Aseguramiento, Desbloqueo |
| **Fiscal** | SAT, FGR (Subprocuraduría Fiscal), Administraciones Locales | Information, Aseguramiento, Transferencias |
| **Ministerial** | Fiscalías Estatales, Ministerio Público Federal | Information, Aseguramiento |
| **Administrative** | UIF, CONDUSEF, Autoridades Laborales | Primarily Information |
| **Amparo Courts** | Juzgados de Distrito en Materia de Amparo | Desbloqueo, Suspension orders |

---

### Level 4: Classify Request Type

```
Authority identified and validated

EXTRACT KEY PHRASES from document body:

┌─────────────────────────────────────────────────────┐
│ PATTERN MATCHING FOR REQUEST TYPE                  │
└─────────────────────────────────────────────────────┘

IF document contains:
   - "solicito información"
   - "requiero estados de cuenta"
   - "se solicita documentación"
   - "remita copia de contratos"
   - "información protegida por secreto financiero"
   AND does NOT contain blocking/transfer language
   → TYPE = Information Request (100)

ELSE IF document contains:
   - "asegurar"
   - "bloquear"
   - "embargar"
   - "inmovilizar"
   - "aseguramiento precautorio"
   - "orden de inmovilización"
   AND specifies account or amount
   → TYPE = Aseguramiento (101)

ELSE IF document contains:
   - "desbloquear"
   - "liberar"
   - "levantar el aseguramiento"
   - "levantar el embargo"
   - "dejar sin efectos el bloqueo"
   AND references prior blocking order
   → TYPE = Desbloqueo (102)

ELSE IF document contains:
   - "transferir"
   - "disponer de recursos"
   - "transferencia electrónica"
   - CLABE or account number for authority
   AND specifies destination account
   → TYPE = Transferencia Electrónica (103)

ELSE IF document contains:
   - "cheque de caja"
   - "billete de depósito"
   - "poner a disposición"
   - "situar fondos"
   AND specifies physical delivery
   → TYPE = Situación de Fondos (104)

ELSE:
   → TYPE = UNKNOWN (999)
   → FLAG for manual legal classification
```

**Page Reference:** R29 Instructivo Column 35, pages 17-18; SmartEnum_RequirementTypes.md

**Multi-Operation Documents:**
- Some requirements request information BEFORE ordering seizure
- Treat as MULTIPLE sequential operations
- Process information request first, then await supplemental order for seizure

---

### Level 5: Extract Target Subject Details

```
Request type classified

EXTRACT from document body:

1. Legal character of subject:
   KEYWORDS: "contribuyente", "indiciado", "imputado", "demandado",
             "investigado", "relacionado", "titular", "cotitular"
   → Map to Catálogo Tipo de Carácter (CON, IND, IMP, etc.)

2. Subject identification:
   - Full legal name (personas físicas)
   - Business name (personas morales)
   - RFC (13 characters with homoclave)
   - Additional identifiers: CURP, address, birth date (if no RFC)

3. Account details (if known):
   - Account number
   - Branch/plaza
   - Account type
   - Contract number

4. Period of information requested:
   - Start date
   - End date
   - Or "desde apertura" (from opening)

VALIDATE RFC format:
   - Persona física: XXXXAAMMDDXXX (4 letters + 6 date + 3 homoclave)
   - Persona moral: XXXAAMMDDXXX (3 letters + 6 date + 3 homoclave)
   - If missing homoclave: substitute XXX

RECORD: All subject data to respective Titular/Cotitular fields
```

**Page Reference:** R29 Instructivo Sections III-IV, pages 7-13; Disposiciones Article 4, page 6

**Name Handling Rules (Article 4(I)):**
- Remove all titles: Lic., Dr., Don, Dña., Sra., Sr., C.
- Remove accents: María → Maria, José → Jose
- No abbreviations: expand all names fully
- Single space between compound names/surnames
- Validate against RFC if available

**Homonym Prevention (Article 4(I)):**
When RFC not available, authorities must provide:
- Domicilio (address), OR
- CURP, OR
- Fecha de nacimiento (birth date)

---

### Level 6: Validate Required Fields

```
Subject details extracted

CHECKLIST per Disposiciones Article 4:

□ Person name or company denomination (Article 4(I))
□ RFC with homoclave (Article 4(I))
□ Request type specified (Article 4(II)):
   □ a) Information and documentation
   □ b) Seizure/unblocking order
   □ c) Fund transfer
□ Subject's character in proceeding (Article 4(III))
□ Fiscal nexus if third-party request (Article 4(IV))
□ Information/documentation details (Article 4(V)):
   □ Balances, contracts, statements, signature cards specified
□ Target financial entity identified (Article 4(VI))
□ Period of requested information (Article 4(VII))

IF any required field missing:
   → REJECT per Article 17(I)
   → Generate rejection notice citing specific missing field

ELSE:
   → ACCEPT for processing
```

**Page Reference:** Disposiciones Articles 4, 17, pages 6-7, 10-11

---

### Level 7: Detailed Operation Classification (If Applicable)

```
For Aseguramiento (101), Desbloqueo (102), Transfer (103-104)

ASEGURAMIENTO VALIDATION:
   1. Does document specify amount to freeze?
      → YES: Extract MontoSolicitadoAsegurar
      → NO: Set MontoSolicitadoAsegurar = 0 (freeze entire account)

   2. Is this a precautionary measure?
      KEYWORDS: "aseguramiento precautorio", "medida cautelar"

   3. Does it cite legal basis?
      → Criminal: Arts. 40, 178 Código Nacional de Procedimientos Penales
      → Fiscal: Art. 155 Código Fiscal de la Federación
      → Civil: Arts. various in state civil codes

DESBLOQUEO VALIDATION:
   1. Does document reference prior seizure order?
      → REQUIRED: Number of original aseguramiento order

   2. Is it partial or total release?
      → Partial: Extract specific amount to unblock
      → Total: Unblock entire frozen balance

   3. Is it from ordering authority or amparo court?
      → Ordering authority: normal desbloqueo
      → Amparo court: suspension of prior order (may be temporary)

TRANSFER VALIDATION:
   1. Electronic transfer (103)?
      → REQUIRED: CLABE (18 digits) of destination account
      → REQUIRED: Beneficiary name (authority or third party)
      → REQUIRED: Amount to transfer

   2. Cashier's check (104)?
      → REQUIRED: Payable to (authority name)
      → REQUIRED: Amount
      → OPTIONAL: Pickup location or delivery address

RECORD: All operational details to Section VI fields
```

**Page Reference:** R29 Instructivo Section VI, pages 17-20

---

## Special Classification Scenarios

### Scenario 1: Sequential Operations
**Example:** Authority requests information, then issues seizure order after reviewing accounts

**Classification:**
1. First document → Information Request (100)
2. Wait for response delivery
3. Second document → Aseguramiento (101)
4. Link via NumeroOficio or subject RFC

**Flag:** Use `OficoRelacionado` field if provided (Article 5)

---

### Scenario 2: Multiple Account Holders

**Problem:** One requirement covers account with 3 co-holders

**Solution per R29 Instructivo (Column 7, page 5):**
```
Original authority number: FGR/123/2023

Generated report records:
- FGR/123/2023-001 (Titular 1)
- FGR/123/2023-002 (Cotitular 1)
- FGR/123/2023-003 (Cotitular 2)

Each record = separate row in R29 report
Same NumeroOficio base, different -XXX suffix
```

**Validation:**
- Suffix range: -001 to -999
- Each holder gets individual record
- Same account number, different RFC/names

---

### Scenario 3: Recordatorio (Reminder/Follow-up)

**Identification:** Document states "recordatorio del oficio número..."

**Classification per Article 5:**
- Still same request TYPE (100-104)
- Flag as `Recordatorio` in tracking system
- Does NOT create new R29 record
- Updates FechaSolicitud to reminder date
- May adjust priority/urgency

---

### Scenario 4: Alcance (Scope Expansion)

**Identification:** Document states "alcance al oficio número..." or "amplía información solicitada"

**Classification per Article 5:**
- Creates NEW R29 record
- References original NumeroOficio
- New NumeroOficio for the alcance
- May add accounts, extend date range, or add subjects

---

### Scenario 5: Precisión (Clarification)

**Identification:** Authority clarifies ambiguous prior request

**Classification per Article 5:**
- Updates EXISTING R29 record
- Corrects subject names, account numbers, dates
- Keeps original NumeroOficio
- Documents correction in notes

---

### Scenario 6: Unknown Authority

**Problem:** Authority not in CNBV catalog

**Process:**
1. Flag as `AutoridadDesconocida`
2. Legal review to validate:
   - Legitimate government entity?
   - Legal competence per Articles 142 LIC et al.?
   - Proper legal foundation cited?
3. If validated:
   - Add to internal catalog
   - Notify CNBV for catalog update
4. If rejected:
   - Return with Article 17(III) rejection (not CNBV competence)

---

## Edge Cases and Unknown Type Handling

### Edge Case 1: Hybrid Request
**Example:** "Solicito información Y asegurar los recursos encontrados"

**Solution:**
- Classify as Aseguramiento (101) - most restrictive operation takes precedence
- Information will be provided as part of seizure process
- If authority explicitly says "primero información, después aseguramiento", treat as sequential

---

### Edge Case 2: Conditional Order
**Example:** "Asegurar SI el saldo excede $100,000"

**Solution:**
- Classify as Aseguramiento (101)
- Flag condition in processing notes
- Execute IF condition met, otherwise return "saldo insuficiente no alcanza umbral"

---

### Edge Case 3: Protective Measures (Medidas de Protección)
**Example:** UIF orders "medidas de protección" under PLD/FT law

**Solution:**
- Classify as Aseguramiento (101)
- Note special authority: UIF under Art. 115 LFPIORPI
- Automatic 30-day block, renewable

---

### Edge Case 4: Oficio de Seguimiento (Follow-up Office)
**New as of Dec 24, 2021 Resolution**

**Identification per Article 2(IV):**
- Authority requests STATUS of prior requirement
- Keywords: "estado que guarda", "seguimiento", "estatus del requerimiento"

**Classification:**
- NOT a new requirement type
- NOT recorded in R29 report
- Internal tracking only
- Respond via email/SIARA with current status

**Page Reference:** 4a. Resolución modificatoria, Article 2(IV), published DOF Dec 24, 2021, page 1

---

## Format Validation Rules

### PDF/TIFF Requirements (Anexo 1)

**For SIARA submissions (TIFF):**
```
✓ Resolution: 150x150 or 200x200 dpi
✓ Color: Black & white (binary text)
✓ Format: TIFF Multipage
✓ Compression: CCITT Group 3 and 4
✓ Max size: 150 KB per page
```

**For email submissions (PDF):**
```
✓ Resolution: 150x150 / 200x200 / 300x300 dpi
✓ Page size: Letter or Legal
✓ Format: PDF
✓ Max size: 8 MB total
✓ PDF version: 1.4 or higher
✓ Orientation: Vertical (no rotation)
✓ Color: B&W or grayscale recommended
✗ Password protected: NOT ACCEPTED
```

**Rejection if:**
- File not legible (Article 17(V))
- Doesn't meet Anexo 1 specs (Article 17(VI))
- Content doesn't match SIARA data entry (Article 17(V))

**Page Reference:** 4a. Resolución modificatoria, Anexo 1, pages 5-6; Especificaciones FORMATO PDF.pdf, pages 1-3

---

## Classification Algorithm Pseudocode

```python
def classify_requirement(document):
    # Level 1: Authenticate
    if not validate_authenticity(document):
        return reject(reason="Article 17(I) - Missing formalities")

    # Level 2: Determine channel
    channel = extract_notification_channel(document)
    if channel not in [SIARA, DIRECTO]:
        return reject(reason="Invalid notification method")

    # Level 3: Identify authority
    authority = match_authority_catalog(document.letterhead)
    if authority is None:
        return flag_for_legal_review(reason="Unknown authority")

    if not validate_authority_competence(authority, document.legal_foundation):
        return reject(reason="Article 17(III) - Outside CNBV competence")

    # Level 4: Classify type
    req_type = classify_by_keywords(document.body)

    if req_type == INFORMATION_REQUEST:
        return process_information_request(document, authority, channel)

    elif req_type == ASEGURAMIENTO:
        return process_aseguramiento(document, authority, channel)

    elif req_type == DESBLOQUEO:
        prior_order = extract_prior_order_reference(document)
        if prior_order is None:
            return reject(reason="Desbloqueo requires prior order reference")
        return process_desbloqueo(document, authority, channel, prior_order)

    elif req_type == TRANSFERENCIA:
        clabe = extract_clabe(document)
        if clabe is None:
            return reject(reason="Transfer requires CLABE destination")
        return process_transferencia(document, authority, channel, clabe)

    elif req_type == SITUACION_FONDOS:
        return process_situacion_fondos(document, authority, channel)

    elif req_type == OFICIO_SEGUIMIENTO:
        return process_status_request(document, authority, channel)

    else:  # UNKNOWN
        return flag_for_manual_classification(document)

    # Level 5-6: Extract details and validate
    subject_details = extract_subject_details(document)
    if not validate_required_fields(subject_details):
        return reject(reason="Article 17(I) - Missing required fields")

    # Level 7: Detailed operation validation
    if req_type in [ASEGURAMIENTO, DESBLOQUEO, TRANSFERENCIA, SITUACION_FONDOS]:
        operation_details = extract_operation_details(document, req_type)
        if not validate_operation_details(operation_details, req_type):
            return reject(reason="Invalid operation details")

    # Success: Create R29 record(s)
    return create_r29_records(
        document, authority, channel, req_type,
        subject_details, operation_details
    )

def classify_by_keywords(body_text):
    """Pattern matching for request type classification"""

    # Normalize text
    text = body_text.lower()

    # Check for seguimiento first (new as of Dec 2021)
    if any(kw in text for kw in ["estado que guarda", "seguimiento del requerimiento", "estatus de"]):
        if any(kw in text for kw in ["número de oficio", "folio", "anterior"]):
            return OFICIO_SEGUIMIENTO

    # Check blocking language
    if any(kw in text for kw in ["asegurar", "bloquear", "embargar", "inmovilizar"]):
        if any(kw in text for kw in ["desbloquear", "liberar", "levantar"]):
            return DESBLOQUEO  # Unblocking takes precedence
        else:
            return ASEGURAMIENTO

    # Check unblocking (without seizure words)
    if any(kw in text for kw in ["desbloquear", "liberar", "levantar el aseguramiento"]):
        return DESBLOQUEO

    # Check transfer language
    if any(kw in text for kw in ["transferir", "disponer de recursos", "clabe"]):
        return TRANSFERENCIA

    # Check cashier's check language
    if any(kw in text for kw in ["cheque de caja", "billete de depósito", "situar fondos"]):
        return SITUACION_FONDOS

    # Default to information request if only info keywords
    if any(kw in text for kw in ["solicito información", "requiero", "estados de cuenta", "documentación"]):
        return INFORMATION_REQUEST

    # Unknown if no patterns match
    return UNKNOWN
```

---

## Quality Control Checklist

Before final classification, verify:

- [ ] Authority in official catalog (or flagged for addition)
- [ ] Notification channel correctly identified (SIARA vs Directo)
- [ ] Request type maps to one of 100-104 codes (or 999 unknown)
- [ ] All subject identifiers present (name, RFC or alternatives)
- [ ] Required fields populated per Article 4
- [ ] Operation details complete for seizures/transfers
- [ ] Multiple holders handled with -XXX suffix
- [ ] Prior orders referenced for desbloqueos
- [ ] CLABE provided for electronic transfers
- [ ] File format meets Anexo 1 specs
- [ ] Legal foundation articles cited
- [ ] Subject's legal character identified

**Escalation Triggers:**
- Unknown authority
- Missing legal foundation
- Ambiguous request language
- Contradictory instructions (e.g., "block and unblock")
- Authority appears to lack competence
- Request outside financial operations scope

---

## Regulatory Authority for Classification

- **Disposiciones SIARA (Sept 4, 2018):** Articles 2-4, 9, 17 - Request requirements and rejection criteria
- **4a. Resolución modificatoria (Dec 24, 2021):** Anexo 1 technical specs, Oficio de Seguimiento definition
- **R29 Instructivo (March 3, 2016):** Operation type codes (101-104), field specifications
- **Articles 142 LIC, 34 LACP, 44 LUC, 69 LRASCAP, 55 LFI, 73 LITF:** Authority competence and information scope
