Technical Specification of the Document (As Extracted from the Uploaded PDF)
For Use in Unit Tests, Schema Tests, and Synthetic Generator Design

Source Document: 222AAA-44444444442025.pdf (4 pages)

1. DOCUMENT CLASSIFICATION
Document Type

Oficio de requerimiento de información (Fiscal / Hacendario)

Originating authority: Administración General de Auditoría Fiscal Federal, SAT.

Sub-authority: Administración Desconcentrada de Auditoría Fiscal de Sonora “2”.

Document Purpose

Request for account information, immobilization status, and credit-related financial data to CNBV.

Connected to enforcement actions, fiscal credits, and administrative embargoes.

Document Format

Multi-page, portrait.

SAT header graphics (national symbolic female figure with flag) repeated horizontally at top of each page.

Watermark present on all pages (“PRUEBA BNM” or similar).

2. GLOBAL DOCUMENT LAYOUT SPECIFICATION

Across all pages the document exhibits:

2.1 Header (Page 1)

Located top-left, includes:

Authority name line 1: Administración General de Auditoría Fiscal Federal

Authority name line 2: Administración Desconcentrada de Auditoría Fiscal de Sonora “2”

Top-right block:

Label: No. De Identificación del Requerimiento

Value: AGAFADAFSON2/2025/000084

2.2 Salutation Block

Contains:

Name of CNBV public servant

Position

Institution name (Comisión Nacional Bancaria y de Valores)

Full postal address (street, colonia, delegación/alcaldía, CP, city)

2.3 Section Structure

The document uses boxed sections with horizontal dividers, each with a title:

Datos generales del solicitante

Facultades de la Autoridad

Fundamento del Requerimiento

Motivación del Requerimiento

Origen del requerimiento

Información técnica de créditos, periodos, auditoría

Personas de quien se requiere información

Cuentas por conocer / instrucciones

Firmas y cierre

Each section has text formatted in paragraph form, often without punctuation consistency.

3. SECTION-BY-SECTION SPECIFICATION
3.1 DATO GENERALES DEL SOLICITANTE (Page 1)

Block shows two internal independent subblocks:

Left Column (Authority unit details)

Includes:

ADMINISTRACIÓN GENERAL DE AUDITORÍA FISCAL FEDERAL

ADMINISTRACIÓN DESCONCENTRADA DE AUDITORÍA FISCAL DE SONORA “2”

Details: Mesa, Turno, Unidad, Secretaría

Physical address lines

Right Column (Name & Contact of signing officer)

Example fields (page 1):

Name: MTRO. GUADALUPE PEPITA PEPITA

Role: ADMINISTRADOR DESCONCENTRADA DE AUTORÍA FISCAL DE SONORA “2”

Tel

E-mail

Unit Test Expectations

Validate presence of 2 columns

Required fields: authority name, admin name, role, contact

Validate Mexican phone format

Validate email format

3.2 FACULTADES DE LA AUTORIDAD (Page 1)

Large paragraph of legal authority citing:

IMSS delegation descriptions

Legal articles (251, XXXVI, “VYC 142”, 149, 150, 154…)

Many inconsistencies: e.g., mix of roman numerals, typos, missing commas, run-on sentences.

Unit Test Expectations

Must include at least one legally-cited article pattern:
artículo ?\d+

Must include references to Ley del Seguro Social or Código Fiscal de la Federación

Must allow garbage text and incorrect roman numerals to simulate real imperfections

3.3 FUNDAMENTO DEL REQUERIMIENTO (Page 1 bottom → Page 2 top)

Cites:

Article 16 of the Mexican Constitution

Ley Orgánica de la Administración Pública Federal (articles 5, 9, etc.)

Ley de Instituciones de Crédito

Ley del Mercado de Valores

Unit Test Expectations

Validate that this section exists and references at least 2 distinct laws

Allow formatting inconsistencies

Legal citations appear as alphanumeric + roman numeral patterns

3.4 MOTIVACIÓN DEL REQUERIMIENTO (Page 2)

Describes:

Diligence date (e.g., 21/03/2025)

Embargo actions on deposits

Amount seized (1,549,481.25)

Amount repeated in words (inconsistently spaced)

Unit Test Expectations

Validate presence of:

Date in DD/MM/YYYY format

Amount numeric and amount-in-words

Tolerance for multiple inconsistent spacing and uppercase/lowercase errors

Monetary values may include:

Commas

Periods

Inconsistent currency representation (“M.N.” etc.)

3.5 ORIGEN DEL REQUERIMIENTO (Page 2)

Fields include:

Whether it contains "aseguramiento/desbloqueo" → uses checkbox “Sí”

Monto a crédito

List of credit numbers (numeric sequences without separators)

Revision periods (month/year repeated)

Unit Test Expectations

Boolean field: Sí/No

Many credit numbers can appear; allow variable count

Revision periods must be listable (05/2023, 06/2023, etc.)

3.6 ANTECEDENTES / SUJETOS DE LA AUDITORÍA (Page 2 bottom)

Table with columns:

Nombre

Carácter

Values example:

AEROLÍNEAS PAYASO ORGULLO NACIONAL

Carácter: Patrón Determinado

Unit Test Expectations

Validate table structure

Required: at least one subject row

Columns must exist even if empty

3.7 SOLICITUDES ESPECÍFICAS & PERSONAS DE QUIEN SE REQUIERE INFORMACIÓN (Page 3)

Contains table with headers:

Nombre

RFC

Carácter

Dirección

Datos complementarios

Example values:

RFC format: APON33333444

Dirección: “Pza. de la Constitución S/N CP 066V6O, CDMX”

Unit Test Expectations

RFC must match regex: [A-Z]{4}\d{6}[A-Z0-9]{3} OR be synthetically flawed

Address must include CP

Additional data field optional

3.8 CUENTAS POR CONOCER (Page 3)

Section lists:

Sector Casas de Bolsa

Sector Instituciones de Banca de Desarrollo

Sector Instituciones de Banca Múltiple

Unit Test Expectations

Presence of the three sectors required

Free text allowed beneath

3.9 INSTRUCCIONES SOBRE CUENTAS POR CONOCER (Page 3 long paragraph)

Describes:

Instructions to financial institutions

References to article 160 CFF, article 142 LIC, article 192 LMV

Text contains heavy typos, grammar errors, line breaks

Unit Test Expectations

Paragraph must contain at least 2 legal references

Must tolerate major inconsistencies

May include mid-sentence line breaks

3.10 CLOSING & SIGNATURE BLOCK (Page 4)

Contains repeating header graphic
Signature line:

“MTRO. GUADALUPE PEPITA PEPITA”

“ADMINISTRADOR DESCONCENTRADA DE AUTORÍA FISCAL DE SONORA “2””

Unit Test Expectations

Signature block must appear bottom-centered or bottom-left

Name and role required

Graphic header repetition required

4. CROSS-CUTTING STRUCTURAL RULES
4.1 Typography & Styling

Multiple uppercase blocks (motivación, monto en letras, legal citations)

Frequently missing accents or too many accents

No consistent justification or alignment

Occasional OCR-like artifacts

4.2 Repetition

Header graphic appears on all pages

Watermark spans diagonally

4.3 Imperfection Patterns (Critical for Unit Tests)

Run-on sentences

Missing punctuation

Wrong roman numerals

Incorrect casing

Inconsistent spacing

Garbled words (e.g., “seguro social organo operativo…”)

Typos in law names and article references

Your generator must replicate these styles.

5. VALIDATION RULES FOR UNIT TESTING
5.1 Required Sections Test

Each generated document must include these sections (in order):

Header with authority

Identification number

Addressee block

Datos del solicitante

Facultades de la autoridad

Fundamento

Motivación

Origen

Antecedentes

Personas de quien se requiere información

Cuentas por conocer

Instrucciones

Cierre y firma

5.2 Data Presence Tests

At least one date

At least one monetary field

At least one RFC

At least one table with subject rows

At least one legal citation (regex: (artículo|art\.|art) ?\d+)

5.3 Layout Integrity Tests

Headers appear on every page

At least one watermark visible

Section titles must match expected labels (case-insensitive)

5.4 Imperfection Simulation Tests

Documents must intentionally contain:

Typographical inconsistencies

Legal reference noise

Strange whitespace

Broken sentence flow

These imperfections must not cause schema breakage in XML but must appear in the .md, .docx, .pdf outputs.