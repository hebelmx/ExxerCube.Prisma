# Legal Requirements Summary - CNBV Authority Requirements System

## Document Sources
- Disposiciones SIARA 4 de septiembre de 2018.pdf
- 4a. Resolución modificatoria Req. Inf. LIC, LACP (Dec 24, 2021).pdf
- R29 A-2911 Aseguramientos, Transferencias y Desbloqueos de Cuentas_03032016.pdf
- Especificaciones FORMATO PDF.pdf

---

## I. Response Time Requirements

### Primary Response Window
**Legal Deadline:** 20-day window from notification
**Typical Response Time:** 6 days (industry practice)
**Legal Basis:** Articles 142 LIC, 34 LACP, 44 LUC, 69 LRASCAP, 55 LFI, 73 LITF

**Page Reference:** Disposiciones SIARA, CONSIDERANDO section, page 2

**Timeline Details:**
```
Day 0: Authority notifies requirement to CNBV
Day 0: CNBV forwards to financial institution (same day if via SIARA)
Day 1-6: Institution processes and responds (typical)
Day 1-20: Maximum legal window (20 business days)
Day 20+: Potential legal non-compliance
```

**Critical Timing Notes:**
- Clock starts on notification date, NOT receipt by institution
- Business days only (exclude weekends and Mexican federal holidays)
- SIARA notifications received after 15:00 hrs count as next business day (Article 11)
- Emergency/precautionary measures may have shorter timeframes

---

### Response Time Variations by Request Type

| Request Type | Standard Response | Urgent Cases | Legal Authority |
|--------------|-------------------|--------------|-----------------|
| **Information Request** | 6-10 days | 3-5 days (criminal investigations) | Art. 142 LIC |
| **Aseguramiento (Freezing)** | Immediate (same business day) | Within hours (precautionary) | Art. 142 LIC, CNPP |
| **Desbloqueo (Unblocking)** | 1-2 days | Same day (amparo orders) | Constitutional mandate |
| **Transferencia (Transfer)** | 2-5 days | N/A | Art. 155 CFF (fiscal) |
| **Situación de Fondos** | 3-7 days | N/A | Various |
| **Oficio de Seguimiento** | 3 days (acknowledgment) | N/A | Article 2(IV) 2021 |

**Page Reference:** R29 Instructivo, implicit in operation types; Disposiciones Article 9

---

### Institutional Response Timeline (Internal SLA)

**For Financial Institutions:**
```
Hour 0: Requirement received via SIARA or direct notification
Hour 0-2: Document validation and classification
Hour 2-4: Route to appropriate department (compliance, legal, operations)
Day 1: Subject identification and account lookup
Day 1-3: Information gathering (statements, contracts, etc.)
Day 3-5: Legal review and approval
Day 5-6: Dual format generation (XML + PDF)
Day 6: Transmission to CNBV via SITI or designated channel
```

**Blocking Operations (Aseguramiento):**
- Must execute SAME BUSINESS DAY as notification
- Generate blocking confirmation within 4 hours
- Report in next monthly R29 submission (within 10 days of month-end)

---

## II. Dual Format Requirements (XML + PDF)

### Format Mandate Overview
**Requirement:** All responses must be provided in BOTH electronic structured format AND human-readable format
**Legal Basis:** Article 9, Disposiciones SIARA (2018 and 2021 amendments)

**Page Reference:** Disposiciones SIARA, Article 9, pages 8-9

---

### XML Format Requirements

**Purpose:** Machine-readable structured data for automated processing

**Schema Requirements:**
- Must conform to CNBV-published XSD schema
- UTF-8 encoding
- Well-formed and validated XML
- Digital signature (optional but recommended)

**For R29 Report:**
- Monthly submission via SITI system
- 42 mandatory fields (no nulls permitted)
- CSV format accepted as alternative to pure XML
- File naming: `[INSTITUCION]_R29_2911_[AAAAMM].csv`

**Page Reference:** Disposiciones Article 213, page 2 (CUB reference); R29 Instructivo, page 21

**XML Structure Example:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<ReporteR29>
  <Cabecera>
    <Periodo>202312</Periodo>
    <ClaveInstitucion>040002</ClaveInstitucion>
    <Reporte>2911</Reporte>
  </Cabecera>
  <Requerimiento>
    <MedioSolicitud>200</MedioSolicitud>
    <AutoridadClave>FGR001</AutoridadClave>
    <!-- ... 39 more mandatory fields ... -->
  </Requerimiento>
</ReporteR29>
```

---

### PDF Format Requirements

**Purpose:** Human-readable documentation for legal archives and court presentation

**Technical Specifications (Anexo 1):**

| Characteristic | Value | Validation |
|----------------|-------|------------|
| **Resolution** | 150x150 / 200x200 / 300x300 dpi | Legibility test |
| **Page Size** | Letter (preferred) or Legal | Letter = 8.5" x 11" |
| **File Size** | Maximum 8 MB | Per document |
| **PDF Version** | 1.4 or higher (Acrobat 5.x+) | Check properties |
| **Orientation** | Vertical (no rotation) | 0 degrees |
| **Color** | B&W or Grayscale (recommended) | Avoid color for stamps |
| **Password** | NOT PERMITTED | Must be unprotected |
| **Compression** | Standard PDF compression | Automatic |

**Page Reference:** Anexo 1 (4a. Resolución modificatoria, Dec 24, 2021), pages 5-6; Especificaciones FORMATO PDF, pages 1-3

**Format Validation Script:**
```bash
# Check PDF version
pdfinfo document.pdf | grep "PDF version"
# Expected: PDF version: 1.4 or higher

# Check page size
pdfinfo document.pdf | grep "Page size"
# Expected: Page size: 612 x 792 pts (letter)

# Check encryption
pdfinfo document.pdf | grep "Encrypted"
# Expected: Encrypted: no
```

---

### TIFF Format (SIARA Submissions Only)

**When Required:** Authorities submitting requirements through SIARA system

**Technical Specifications:**

| Characteristic | Value |
|----------------|-------|
| **Resolution** | 150x150 or 200x200 dpi |
| **Color** | Black & White (Binary text mode) |
| **Format** | TIFF Multipage |
| **Compression** | CCITT Group 3 and 4 (1d Huffman modified) |
| **Units** | Pixels |
| **Target Size** | ≤150 KB per page |

**Page Reference:** Anexo 1, page 5

**Conversion Best Practices:**
- Scan original at 200 dpi for optimal balance
- Use "text" or "document" scanner mode
- Enable automatic deskew
- Remove blank pages
- Verify legibility before submission

---

### Dual Format Submission Process

```
1. Generate XML/CSV from database
   ↓
2. Validate against CNBV schema
   ↓
3. Generate PDF from same data source
   ↓
4. Validate PDF technical specs (Anexo 1)
   ↓
5. Cross-validate: XML data matches PDF content
   ↓
6. Package both formats:
   - If via SITI: Upload XML + attach PDF
   - If via email: Send both as attachments
   - If via SIARA response: Embed both in system
   ↓
7. Retain copies for audit (minimum 5 years)
```

**Critical Rule:** XML and PDF must contain IDENTICAL information. Discrepancies will trigger rejection.

---

## III. Audit and Security Requirements (CIS Control 6)

### CIS Control 6: Access Control Management
**Relevance:** Protection of financial secrecy (secreto financiero) data during requirement processing

**Page Reference:** Implicit in Articles 142 LIC, 46 LIC (financial secrecy provisions); Article 9 notification security

---

### Access Control Requirements

**1. Role-Based Access Control (RBAC)**
```
Role: Compliance Officer
  - Permissions: Receive requirements, classify, route
  - Restrictions: Cannot view customer account details

Role: Operations Manager
  - Permissions: Retrieve account data, generate reports
  - Restrictions: Cannot communicate with authorities

Role: Legal Counsel
  - Permissions: Review, approve responses, communicate
  - Restrictions: Cannot directly access core banking system

Role: Authorized Signer
  - Permissions: Digital signature, final transmission
  - Restrictions: Cannot modify content
```

**Implementation:**
- Minimum 2-person rule (dual control)
- Segregation of duties between receipt, processing, and transmission
- No single person has end-to-end access

---

### Audit Trail Requirements

**Mandatory Logging:**
- [ ] Requirement receipt timestamp (Article 11 - after 15:00 = next day)
- [ ] User who classified document
- [ ] User who retrieved account information
- [ ] User who generated XML/PDF
- [ ] User who reviewed and approved
- [ ] User who transmitted response
- [ ] Timestamp of transmission
- [ ] Acknowledgment receipt from CNBV/Authority (within 3 days - Article 9)

**Log Retention:** Minimum 5 years (financial institution standard)

**Page Reference:** Article 9 (receipt acknowledgment), pages 8-9; Article 11 (timestamp rules), page 9

---

### Data Protection During Processing

**Encryption Requirements:**
- **In Transit:** TLS 1.2 or higher for SIARA/email communications
- **At Rest:** AES-256 encryption for stored requirements/responses
- **Email:** Encrypted email required for transmission per Article 3(VIII)

**Access Restrictions:**
- Customer data visible only on need-to-know basis
- Masking of sensitive data in logs (account numbers, balances)
- Secure deletion after legal retention period expires

**Page Reference:** Article 3(VIII) - email security requirements, page 3 (4a. Resolución modificatoria)

---

### Portal de Gestión Documental Security

**New as of Dec 2021:** CNBV provides secure portal for large file transfers

**Security Features (Article 9, Section IV):**
- Individual authority credentials
- Download link expires after 30 days
- Single-use download link (optional)
- Automatic deletion after authority confirms receipt
- Encrypted storage (AES-256)
- Audit log of all downloads

**Authority Obligations:**
- Must acknowledge receipt within 3 business days
- Failure to acknowledge = deemed received (legal fiction)
- Must use institutional email address (no personal email)

**Page Reference:** Article 9, Section IV, page 9 (4a. Resolución modificatoria)

---

### Digital Signature Requirements

**For Authority Requirements (Article 3(II)):**
- Autograph (physical) signature acceptable
- Electronic signature (FIEL) acceptable with prior CNBV agreement
- Must be from authorized official or delegated authority

**For Institution Responses:**
- Digital signature recommended (not mandatory)
- If used, must comply with Mexican Electronic Signature Law
- Provides non-repudiation evidence

**Page Reference:** Article 3(II), page 5

---

## IV. Compliance Checklists

### Checklist 1: Requirement Receipt Validation

**Upon receiving authority requirement, verify:**

- [ ] **Authenticity (Article 3)**
  - [ ] Official letterhead present
  - [ ] Autograph or electronic signature present
  - [ ] Document properly founded (legal articles cited)
  - [ ] Document properly motivated (specific facts stated)
  - [ ] Issue date present
  - [ ] Official number present
  - [ ] Return address for responses present

- [ ] **Channel Validation (Article 9)**
  - [ ] Received via authorized channel (SIARA, email, or physical)
  - [ ] If email: from institutional address matching Article 3(VIII) format
  - [ ] If SIARA: TIFF format meets Anexo 1 specs
  - [ ] If direct email: PDF format meets Anexo 1 specs
  - [ ] Received during business hours (9:00-15:00) or timestamped next day

- [ ] **Format Compliance (Anexo 1)**
  - [ ] PDF/TIFF is legible (all text readable)
  - [ ] Technical specifications met (resolution, size, version)
  - [ ] No password protection
  - [ ] Content matches metadata (if SIARA submission)

- [ ] **Required Information (Article 4)**
  - [ ] Subject name or company denomination present
  - [ ] RFC with homoclave (or CURP/address/DOB if no RFC)
  - [ ] Request type specified (info, seizure, transfer, unblock)
  - [ ] Subject's legal character stated (contribuyente, indiciado, etc.)
  - [ ] Fiscal nexus explained (if third-party request)
  - [ ] Specific information/documents listed
  - [ ] Target financial institution identified
  - [ ] Period of information specified

**If ANY item fails:** Reject per Article 17 with specific deficiency cited

---

### Checklist 2: Response Generation Compliance

**Before transmitting response to authority, verify:**

- [ ] **Timeliness**
  - [ ] Within 20-day legal window
  - [ ] Preferably within 6-day standard
  - [ ] Blocking operations executed same day

- [ ] **Dual Format**
  - [ ] XML/CSV generated and validated
  - [ ] PDF generated with identical content
  - [ ] Both formats cross-verified for consistency
  - [ ] PDF meets Anexo 1 technical specs

- [ ] **Content Accuracy**
  - [ ] All requested information included
  - [ ] Correct customer identification
  - [ ] Correct date range
  - [ ] Amounts properly converted to pesos (Criterion A-2)
  - [ ] No extraneous information (only what was requested)

- [ ] **Legal Review**
  - [ ] Legal counsel approved content
  - [ ] Confidentiality markings applied
  - [ ] Proper scope (only financial operations per Article 6)
  - [ ] Digital signature applied (if required)

- [ ] **Transmission**
  - [ ] Sent via same channel as received (Article 9)
  - [ ] Confirmation email sent if using Portal de Gestión Documental
  - [ ] Receipt acknowledgment requested
  - [ ] Transmission logged in audit trail

---

### Checklist 3: Monthly R29 Report Submission

**Before submitting R29-2911 report to CNBV via SITI:**

- [ ] **Reporting Window**
  - [ ] Within 10 days of month-end (Article 208 CUB)
  - [ ] If no data: Submit empty report per Article 213

- [ ] **Data Completeness**
  - [ ] All 42 columns populated (no nulls)
  - [ ] All blocking/unblocking operations included
  - [ ] All transfers and fund dispositions included
  - [ ] Multiple co-holders handled with -XXX suffix

- [ ] **Data Quality**
  - [ ] RFC formats validated (13 chars, proper structure)
  - [ ] INEGI codes from official catalogs
  - [ ] Amounts rounded to pesos (no decimals)
  - [ ] Dates in AAAAMMDD format
  - [ ] No special characters in numeric fields

- [ ] **File Format**
  - [ ] CSV or XML per SITI specifications
  - [ ] UTF-8 encoding
  - [ ] Proper delimiter (comma for CSV)
  - [ ] File naming convention: [INST]_R29_2911_[AAAAMM]
  - [ ] File size within SITI limits

- [ ] **Transmission**
  - [ ] Uploaded via SITI system (Article 213)
  - [ ] Validation errors resolved
  - [ ] Confirmation receipt obtained
  - [ ] Copy retained for internal audit

---

### Checklist 4: Audit Trail Verification (Monthly)

**Security and compliance audit, perform monthly:**

- [ ] **Access Control**
  - [ ] Review user access logs for authority requirement files
  - [ ] Verify no unauthorized access
  - [ ] Confirm segregation of duties maintained
  - [ ] Validate dual control for transmissions

- [ ] **Data Protection**
  - [ ] Encryption verified for stored requirements
  - [ ] TLS certificate valid for SIARA/email connections
  - [ ] No unencrypted transmission of customer data
  - [ ] Secure deletion procedures followed for expired data

- [ ] **Timeliness Compliance**
  - [ ] All requirements responded within 20 days
  - [ ] Average response time ≤ 6 days
  - [ ] Blocking orders executed same day
  - [ ] No overdue items

- [ ] **Format Compliance**
  - [ ] All responses in dual format (XML + PDF)
  - [ ] PDF technical specs verified (random sample)
  - [ ] No rejection notices from CNBV for format errors

- [ ] **Legal Compliance**
  - [ ] All rejections properly documented (Article 17)
  - [ ] Financial secrecy maintained (Article 142 LIC)
  - [ ] Only authorized authorities served
  - [ ] No information outside Article 6 scope provided

- [ ] **Reconciliation**
  - [ ] R29 report matches internal records
  - [ ] All operations accounted for
  - [ ] No discrepancies between systems

---

## V. Summary of Legal Articles and Their Impact

### Core Legal Framework

| Article | Law | Requirement | Impact on System |
|---------|-----|-------------|------------------|
| **142** | LIC | Authorities may request financial info via CNBV | Primary legal basis; defines authority competence |
| **34** | LACP | Same for savings and loan cooperatives | Extends to popular savings sector |
| **44** | LUC | Same for credit unions | Extends to credit unions |
| **69** | LRASCAP | Same for savings cooperatives | Extends to cooperative sector |
| **55** | LFI | Same for investment funds | Extends to fund managers |
| **73** | LITF | Same for fintech institutions | Extends to fintech (2018 law) |

---

### CNBV Disposiciones (Sept 4, 2018, amended Dec 24, 2021)

| Article | Topic | Key Requirements |
|---------|-------|------------------|
| **2** | Definitions | Defines Requerimiento, SIARA, Oficio de Seguimiento, Portal de Gestión |
| **3** | Formalities | Letterhead, signature, foundation, motivation, date, number, address, email |
| **4** | Content | Name, RFC, request type, character, fiscal nexus, details, entity, period |
| **5** | Request Nature | Must state if new, reminder (recordatorio), precision, scope (alcance) |
| **6** | Scope Limit | Only info related to Art. 46 LIC operations (financial activities) |
| **7** | Detailed Requirements | Specific data for known accounts (account #, dates, operations) |
| **8** | Supporting Docs | Authorities may attach contracts, statements, checks, deposits |
| **9** | Notification & Delivery | SIARA, email, or physical; dual format response; 3-day acknowledgment |
| **10** | Forms | Must use CNBV-published formats (if not using SIARA) |
| **11** | Delivery Procedures | Business hours 9:00-15:00; after-hours = next day |
| **12-15** | SIARA Procedures | Adhesion, testing, administration, digitalization requirements |
| **16** | Contingency | Use email if SIARA unavailable; CNBV publishes notice |
| **17** | Rejection | Six grounds for rejection; rejection notice required |

**Page References:** Throughout Disposiciones SIARA document

---

### R29 Report Requirements (CUB Annexo 36)

| CUB Article | Requirement | Deadline |
|-------------|-------------|----------|
| **207** | Submit operational information per Anexo 36 | Defines reporting obligation |
| **208** | Monthly frequency | Within 10 days after month-end |
| **213** | Electronic transmission via SITI | Mandatory digital submission |

**Anexo 36** specifies R29-2911 report structure (42 mandatory fields)

---

## VI. Areas Requiring Legal Consultation

### Ambiguities Identified

1. **Timing Ambiguity:**
   - Documents state "20-day window" but don't specify if calendar or business days
   - **Recommendation:** Treat as business days (conservative interpretation)
   - **Consultation Needed:** Confirm with CNBV legal department

2. **"Recordatorio" vs "Alcance":**
   - Article 5 mentions both but doesn't define when reminder becomes scope expansion
   - **Recommendation:** Treat as reminder if no new subjects/accounts; alcance if new data
   - **Consultation Needed:** Request CNBV clarification or examples

3. **Multiple Authority Types:**
   - Same document from joint authority (e.g., SAT + FGR co-signed)
   - **Recommendation:** Classify by primary requesting authority
   - **Consultation Needed:** Establish precedence hierarchy

4. **Foreign Currency Conversion Timing:**
   - Criterion A-2 doesn't specify exchange rate date (transaction date, request date, or report date)
   - **Recommendation:** Use exchange rate as of last day of reporting month
   - **Consultation Needed:** Confirm with CNBV via written query

5. **Co-holder Definition:**
   - R29 uses "cotitular" but also mentions "beneficiario, autorizado, firmante, representante"
   - Are these all reported as co-holders or separately?
   - **Recommendation:** Report all as co-holders with appropriate "character" code
   - **Consultation Needed:** Review Circular 3/2012 Banxico definitions

6. **Oficio de Seguimiento Impact:**
   - Dec 2021 amendment adds follow-up offices but doesn't clarify response obligations
   - Must institution provide full detail again or just summary status?
   - **Recommendation:** Provide summary status + reference to original response
   - **Consultation Needed:** CNBV FAQ or sample responses

---

### Contradictions Between Documents

1. **PDF Page Size:**
   - Anexo 1 says "Carta, Oficio" acceptable
   - Especificaciones PDF document emphasizes "Carta" strongly
   - **Resolution:** Use Letter as primary, Legal only if unavoidable
   - **Impact:** Low - both are accepted

2. **Response Delivery Method:**
   - Article 9 (2018) says "physical, electronic device, or SIARA"
   - Article 9 (2021 amendment) adds "email and Portal de Gestión"
   - Are physical and device still permitted?
   - **Resolution:** 2021 amendment supersedes; prefer electronic methods
   - **Impact:** Low - electronic is easier anyway

3. **SIARA Mandatory vs Optional:**
   - Article 12 implies SIARA is optional ("authorities that opt to")
   - Article 16 implies mandatory once adhered ("cannot present by other means")
   - **Resolution:** Optional to join, mandatory once joined (except contingency)
   - **Impact:** Medium - clarifies institution behavior

---

### Edge Cases Needing Precedent

1. **Cryptocurrency Accounts:**
   - Not explicitly covered in 2018/2021 regulations
   - LITF (Art. 73) may cover if fintech offers crypto services
   - **Consultation Needed:** CNBV position on virtual asset reporting

2. **Frozen Accounts Earning Interest:**
   - If account blocked for 6 months and earns interest, is interest also blocked?
   - Does new interest require amended R29 report?
   - **Consultation Needed:** Legal memo on interest treatment during seizure

3. **Deceased Account Holder:**
   - Requirement received for deceased person's account
   - Estate in probate, multiple heirs with claims
   - **Consultation Needed:** Procedure for blocked estate accounts

4. **Amparo Suspension vs Definitive:**
   - Provisional amparo suspends seizure temporarily
   - Definitive amparo orders permanent unblocking
   - How to report each in R29 (both are "desbloqueo")?
   - **Consultation Needed:** Differentiate in "character" or "notes" field

5. **Authority Succession:**
   - Original requirement from State Prosecutor
   - Case federalized, now Federal Prosecutor
   - Old requirement still valid? Need new requirement?
   - **Consultation Needed:** Process for authority transfer of jurisdiction

---

## VII. Document Retention Requirements

### Legal Retention Periods

| Document Type | Retention Period | Legal Basis |
|---------------|------------------|-------------|
| **Original Authority Requirements** | 5 years from closure | Financial institution standard |
| **Institution Responses (XML + PDF)** | 5 years from transmission | Audit requirement |
| **R29 Monthly Reports** | 5 years from submission | CNBV regulation |
| **Rejection Notices** | 5 years from issuance | Article 17 compliance |
| **Audit Logs (access, transmission)** | 5 years from generation | CIS Control 6 |
| **Supporting Documents** | 5 years from case closure | General financial records law |

**Storage Requirements:**
- Secure encrypted storage (AES-256)
- Access restricted to compliance/legal/audit personnel
- Regular backup (daily minimum)
- Disaster recovery plan (offsite backup)
- Secure deletion after retention period (DOD 5220.22-M standard)

---

## VIII. Implementation Priorities for RegTech System

### High Priority (Legal Mandate)

1. **Dual Format Engine:**
   - Generate XML + PDF from single data source
   - Validate consistency between formats
   - Ensure PDF meets Anexo 1 specs programmatically

2. **Response Deadline Tracker:**
   - Calculate 20-day window from notification
   - Alert at 5-day, 10-day, 15-day marks
   - Escalate if approaching deadline

3. **R29 Auto-Generator:**
   - Monthly extraction of all freezing/transfer operations
   - Auto-populate 42 mandatory fields
   - Validate RFC, INEGI codes, amounts
   - Submit to SITI automatically

4. **Authority Catalog Validator:**
   - Maintain up-to-date authority catalog
   - Flag unknown authorities
   - Validate authority competence per cited articles

### Medium Priority (Operational Efficiency)

5. **Requirement Classifier:**
   - NLP-based keyword extraction
   - Auto-classify type (100-104)
   - Flag ambiguous cases for human review

6. **Multi-Holder Manager:**
   - Detect accounts with >2 holders
   - Auto-generate -XXX suffix variants
   - Create separate R29 records for each

7. **Portal de Gestión Integration:**
   - Automated upload of large responses
   - Notification to authority's institutional email
   - Track authority download confirmation

8. **Audit Trail Dashboard:**
   - Real-time compliance metrics
   - User access reports
   - SLA performance (average response time)

### Low Priority (Enhancement)

9. **Precedent Database:**
   - Store past classifications and outcomes
   - Machine learning for similar case suggestions
   - Legal consultation history

10. **Authority Self-Service Portal:**
    - Status check for submitted requirements
    - Automatic recordatorio generation
    - Historical request lookup

---

## Regulatory Contact Information

**Comisión Nacional Bancaria y de Valores (CNBV)**
- **Address:** Insurgentes Sur No. 1971, Plaza Inn, Col. Guadalupe Inn C.P. 01020, CDMX
- **Phone:** +52 55 1454-6000
- **Website:** www.gob.mx/cnbv
- **Email (Requirements):** comunicacionAA@cnbv.gob.mx
- **SIARA Support:** Check CNBV website for current contact

**Dirección General de Atención a Autoridades (DGAA)**
- **Department:** Vicepresidencia de Supervisión de Procesos Preventivos
- **Email:** comunicacionAA@cnbv.gob.mx

---

## Change Log

| Date | Document | Change | Impact |
|------|----------|--------|--------|
| **Feb 12, 2013** | Disposiciones SIARA | Original publication | Established requirement framework |
| **Aug 26, 2014** | Resolution | Investment funds added (Art. 55 LFI) | Extended to fund managers |
| **Mar 13, 2017** | Resolution | Expedited processing mandate | Reduced response expectations |
| **Sep 4, 2018** | Resolution | Fintech added (Art. 73 LITF) | Extended to fintech institutions |
| **Dec 24, 2021** | 4a. Resolución modificatoria | Oficio de Seguimiento, Portal de Gestión, Anexo 1 specs | Electronic workflow enhancements |

**Next Review Date:** Monitor CNBV website and Diario Oficial de la Federación for amendments

---

## Glossary of Legal Terms

| Spanish Term | English Translation | Definition |
|--------------|---------------------|------------|
| **Aseguramiento** | Seizure/Freezing | Court/authority order to block account access |
| **Autoridad Requirente** | Requesting Authority | Government entity with legal power to request info |
| **Carpeta de Investigación** | Investigation File | Criminal case file (replaces "averiguación previa" post-2016) |
| **CLABE** | Standardized Bank Code | 18-digit interbank account number |
| **Desbloqueo** | Unblocking | Order to release previously frozen account |
| **Oficio** | Official Document | Numbered government communication |
| **Recordatorio** | Reminder | Follow-up on unanswered prior request |
| **Alcance** | Scope Expansion | Amendment adding subjects/accounts to prior request |
| **Precisión** | Clarification | Correction of ambiguous prior request |
| **Secreto Financiero** | Financial Secrecy | Legal protection of customer financial data |
| **SIARA** | Authority Request System | CNBV electronic platform for authority communications |
| **SITI** | Interinstitutional Transfer System | CNBV platform for institution submissions |
| **UIF** | Financial Intelligence Unit | Anti-money laundering authority |
| **FGR** | Attorney General's Office | Federal prosecutor's office |
| **SAT** | Tax Administration Service | Federal tax collection authority |

---

**Document prepared for:** ExxerCube.Prisma RegTech Compliance System
**Prepared by:** Legal Research Analysis (based on CNBV published regulations)
**Date:** 2025-01-25
**Status:** For development team implementation planning
**Next Action:** Legal review and validation by qualified Mexican banking attorney
