Task Execution Plan for Automation Agent
Project: Automated Preprocessing Pipeline for Scanned Documents with Unknown Visual Characteristics
Overview:

This plan describes a sequential and fully automated pipeline to process scanned legal/financial documents with poor or variable quality, aiming to prepare them for OCR. Each task builds on the previous, generating artifacts for the next phase.

Step-by-Step Task Workflow
✅ Task 1: Extract Legal and Financial Terms

Objective: Extract domain-specific terms to guide further data synthesis.

Input Files:

Articulo142.pdf

SIARA_Manual_Civiles_v01.pdf

LUC.pdf

LACP.pdf

LRASCAP.pdf

LeyInstTecnFinanciera.pdf

LeyInstCredito.pdf

Automation Action:

Perform NLP-based term extraction targeting legal and financial language (in Spanish).

Normalize and categorize terms under headers (e.g., Laws, Entities, Operations).

Output: A JSON file of categorized terms and entities.
Save file to disk under the name entities.json

✅ Task 2: Generate Fictitious Requirements Corpus

Objective: Simulate authentic, diverse legal requirement texts.

Input: Terms from Task 1 and the legal documents.

Automation Action:

Generate a text corpus simulating court-requested requirements.

Style variation: emulate multiple writing voices, add common legal drafting errors and polished samples.

Output: fictitious_requerimientos_raw.md with multiple distinct examples.

✅ Task 3: Classify Requirements and Define Schema

Objective: Model the structure of requirement data.

Input: Requirements from Task 2.

Automation Action:

Analyze samples to derive hierarchical requirement types.

Generate a tree-like classification structure.

Design a JSON schema representing standardized output structure.

Output:

requerimientos_tree_structure.md

requerimientos_schema.json

✅ Task 4: Generate Large Corpus with Hashes

Objective: Produce an extensive synthetic dataset with integrity markers.

Input: Final format of requirements (validated from Task 3).

Automation Action:

Generate 999 unique legal requirement texts.

Compute and embed a SHA256 hash for each one.

Output: corpus_requerimientos.md with format:

<--Start Requirment--->
**requirment**
(Text)
**Hash**
(SHA256)
<--End Requirment--->

✅ Task 5: Simulate Document Degradation

Objective: Create degraded synthetic scanned documents for testing preprocessing.

Input: corpus_requerimientos.md

Automation Action (Python script):

Render each requirement into a base image (e.g., A4 format).

Overlay its hash diagonally in red (watermark).

Apply random image degradations:

Gaussian blur

Salt-and-pepper noise

Rotation/skew

Contrast/brightness changes

Simulated coffee stains or RGB artifacts

Save each as Fixtures001 to Fixtures999 in PNG and PDF.

Output: 999 degraded document pairs in both formats.

If you want this turned into an executable script or pipeline, or managed via a task runner like Airflow, Prefect, or a plain orchestrator in Python, I can design that next. Shall I proceed?

test and run the script, save the files to a folder called Fixtures