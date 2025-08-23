# Project Title:
**Automated Preprocessing Pipeline for Scanned Documents with Unknown Visual Characteristics**

## 1. Project Overview
This project addresses the challenge of preprocessing highly variable scanned documents originating from diverse sources across Mexico. These documents exhibit a wide range of visual degradation—from medium to very poor quality—and possess unpredictable content structures and noise patterns. In some cases, even human interpretation requires magnification tools.
The objective is to design and validate a preprocessing pipeline that can dynamically adapt to the unique characteristics of each scanned document, improving their readiness for downstream Optical Character Recognition (OCR) without altering the underlying textual content.

## 2. Problem Statement
Manual preprocessing is infeasible due to:
*   The scale and heterogeneity of the documents.
*   The lack of prior metadata describing quality or format.
*   The presence of visual obstructions like RGB watermarks, overlays, and stains.

An automated approach is needed to intelligently assess and enhance each image before OCR is applied.

## 3. Methodology
3.1. **Synthetic Document Generation:** Generate a diverse corpus of synthetic (dummy) document images with controlled properties (e.g., contrast, noise, watermark types). These will serve as the testbed for pipeline development and evaluation.

## 4. Tasks

### Task 1: Extract Legal and Financial Terms
**Goal:** Extract a curated list of Spanish legal and financial terms from the provided reference documents.
**Input:**
*   `Articulo142.pdf`
*   `SIARA_Manual_Civiles_v01.pdf`
*   `LUC.pdf`
*   `LACP.pdf`
*   `LRASCAP.pdf`
*   `LeyInstTecnFinanciera.pdf`
*   `LeyInstCredito.pdf`
**Output:** A list of Spanish legal and financial terms relevant to court-ordered financial requirements.

### Task 2: Generate Fictitious Requirements Corpus
**Goal:** Generate a corpus of text of fictitious requirements for concept testing.
**Input:** The legal documents from Task 1.
**Output:** A collection of text-based requirements with the following characteristics:
*   Diverse wording, simulating different writing styles ("junior lawyers from distinct Mexican cosmologies").
*   Inclusion of errors: typographical, minor legalese errors, and redaction/wording issues.
*   Inclusion of well-redacted examples.

### Task 3: Classify Requirements and Propose JSON Structure
**Goal:** Generate a classification of all possible requirements and a corresponding JSON schema.
**Input:** The legal documents from Task 1.
**Output:**
1.  A classification of requirements in a tree format structure.
2.  A proposed JSON structure for the output of the information.

### Task 4: Generate Large Corpus with Hashes
**Goal:** Generate a large corpus of 999 fictitious requirements, each with a fictitious hash.
**Input:** The legal documents from Task 1.
**Output:** A single `.md` file named `corpus_requerimientos.md` containing 999 requirements in the following format:
```
<--Start Requirment--->
**requirment**
(Text of the requeriment)
**Hash**
(Generated hash)
<--End Requirment--->
```

### Task 5: Simulate Document Degradation
**Goal:** Write a Python script to simulate the printing, scanning, and deterioration of the documents from Task 4.
**Input:** The `corpus_requerimientos.md` file generated in Task 4.
**Output:** A Python script that generates a series of PNG and PDF files named `Fixtures001` to `Fixtures999`. The script should perform the following transformations:
*   Render the requirement text onto an image.
*   Overlay a watermark using the hash from the input file. The watermark should be red and cross the document.
*   Apply a variety of deterioration effects, such as noise, blur, contrast/brightness changes, rotation/skew, and stains.
 



  Extracted Legal and Financial Terms


  Leyes y Disposiciones (Laws and Provisions)
   * Ley de Instituciones de Crédito
   * Ley de Ahorro y Crédito Popular
   * Ley de Uniones de Crédito
   * Ley para Regular las Actividades de las Sociedades Cooperativas de Ahorro y Préstamo
   * Ley de Fondos de Inversión
   * Ley para Regular las Instituciones de Tecnología Financiera (Ley Fintech)
   * Disposiciones de Carácter General


  Autoridades y Entidades (Authorities and Entities)
   * Comisión Nacional Bancaria y de Valores (CNBV)
   * Secretaría de Hacienda y Crédito Público (SHCP)
   * Autoridades Judiciales, Hacendarias Federales y Administrativas
   * Fiscal General de la República
   * Procurador General de Justicia Militar
   * Tesorero de la Federación
   * Auditoría Superior de la Federación
   * Banco de México


  Instituciones Financieras (Financial Institutions)
   * Instituciones de Crédito
   * Uniones de Crédito
   * Sociedades Financieras Populares y Comunitarias
   * Sociedades Cooperativas de Ahorro y Préstamo
   * Fondos de Inversión
   * Instituciones de Tecnología Financiera (ITF)


  Términos Judiciales y Legales (Judicial and Legal Terms)
   * Requerimiento de información
   * Orden de aseguramiento
   * Desbloqueo de cuentas
   * Transferencia de fondos
   * Secreto financiero
   * Averiguación previa / Carpeta de investigación
   * Cuerpo del delito / Probable responsabilidad
   * Indiciado / Acusado
   * Providencia dictada en juicio
   * Litigio / Concurso mercantil / Quiebra
   * Firma autógrafa / Firma electrónica
   * Fundado y motivado
   * Precepto legal / Hipótesis normativa
   * RFC / CURP / Domicilio


  Actores (Actors)
   * Cliente / Usuario / Socio / Accionista
   * Depositante / Deudor / Titular / Beneficiario
   * Representante legal
   * Servidor público
   * Consejo de Administración / Director General / Comisario / Auditor externo


  Operaciones y Documentos (Operations and Documents)
   * Operaciones o servicios
   * Saldos / Contratos / Estados de cuenta
   * Cheques / Fichas de depósito
   * Operaciones electrónicas / Transferencia / CLABE
   * Órdenes de pago
   * Factoraje financiero / Arrendamiento financiero
   * Fideicomisos de garantía
   * Obligaciones subordinadas


  Sistemas y Plataformas (Systems and Platforms)
   * SIARA (Sistema de Atención de Requerimientos de Autoridad)
   * Portal de Gestión Documental
   * Correo electrónico