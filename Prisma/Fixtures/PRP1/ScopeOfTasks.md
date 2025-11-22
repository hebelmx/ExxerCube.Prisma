Context:

SIARA (Sistema de Atención de Requerimientos de Autoridad) is the centralized digital platform operated by the CNBV (National Banking and Securities Commission) in Mexico. It acts as the secure bridge between government authorities (like judges or tax agencies) and financial institutions (banks, fintechs, etc.).Its primary purpose is to process legal requests efficiently, such as freezing bank accounts, providing transaction histories, or unblocking funds during legal investigations.1. How It Works (The Logistical Process)The process transforms what used to be a slow paper trail into a secure digital workflow. It generally follows this lifecycle:Step 1: The Authority Initiates a RequestA registered authority (e.g., a Civil Judge or the Tax Administration Service - SAT) logs into SIARA.They submit a formal request (known as a requerimiento) targeting a specific individual or company. This could be a request for information (e.g., "Does Person X have accounts here?") or an execution order (e.g., "Freeze funds up to $50,000 MXN").Step 2: CNBV ValidationThe CNBV acts as the "traffic controller." It validates that the request meets all legal formatting requirements.If valid, the CNBV routes the request to the relevant financial institutions (often broadcasting to multiple banks if the authority is searching for assets).Step 3: Financial Institution ComplianceBanks and financial entities receive the alert via their own SIARA interface.Compliance Officers at the bank must query their internal databases.They must respond within strict legal deadlines (usually measured in days or hours depending on the urgency).Action: They either attach the requested documents (statements, contracts) or confirm the freezing/unblocking of funds.Step 4: Response & ExecutionThe institution sends the response back through SIARA to the CNBV.The CNBV relays this final status back to the original Authority.2. Legal FrameworkFor financial institutions, using SIARA is not optional; it is a mandatory compliance requirement rooted in Mexican financial law.Key Regulation: The core legal instrument is the "Disposiciones de carácter general" (General Provisions) issued by the CNBV. These provisions establish the procedure for attending to information and documentation requests from competent authorities.Confidentiality (Secrecy): The system is designed to respect secreto bancario (banking secrecy). Only authorized officials can request data, and institutions are legally protected when they share data through this official channel.Anti-Money Laundering (AML): SIARA is a critical tool for the UIF (Financial Intelligence Unit) and prosecutors to trace illicit funds quickly without alerting the suspect.3. Key Stakeholders (Partes Interesadas)The ecosystem involves three main parties:StakeholderRoleThe Authorities (Requirientes)Requestors. These are the entities asking for data. They include Judicial Authorities (Judges), Fiscal Authorities (SAT), Administrative bodies, and the Attorney General (FGR).CNBV (The Regulator)Intermediary. They manage the SIARA platform, ensuring that requests are legal and correctly routed. They do not hold the money; they simply pass the orders.Financial Institutions (Entidades)Executors. Banks, SOFOMES, Fintechs, and Brokerage firms. They hold the actual data and funds. They are legally obligated to have a specialized team or officer to monitor SIARA daily.Summary Table: Types of RequestsInformation: "Does John Doe have an account? Send statements from Jan-Dec 2023."Blocking (Aseguramiento): "Freeze account #1234 immediately."Unblocking (Desbloqueo): "Release the funds in account #1234." (Note: CNBV processes unblocking immediately, but the bank may take operational time to reflect it).


The ultimate purpose of this prompt is to develop a fully autonomous synthetic-document generator, capable of producing high-fidelity, realistic test cases that mimic regulatory requests exchanged between government authorities, the CNBV, and financial institutions.

In simple terms:

Build an automated system that fabricates entire multi-format case files that look exactly like the real ones authorities send to banks—but with synthetic, legally safe, artificially generated data.

These instructions define what must be generated, how it should look, what imperfections it should contain, what technologies to use, what structure each document must follow, and how the system should behave end-to-end.

🎯 The End Goal for the Developer Agent

The unified prompt instructs the agent to construct a complete testing asset generator with these capabilities:

1. Analyze real samples to learn their structure

The agent must inspect real files in PRP1/ (especially XML schemas).

Then replicate the structure perfectly, including quirks, errors, and formatting flaws.

This ensures the output is compatible with your C# production system.

2. Generate ultra-realistic synthetic regulatory documents

Each test case must include:

Document 1 — “Authority Originating Request”

Looks like an actual document from:

SAT, FGR, judges, UIF, etc.

Bureaucratic tone

Typos, formatting defects, and inconsistencies

Document 2 — “CNBV Vetted Request”

Standardized

Clean(er)

With CNBV-specific metadata

Both documents must be created in Markdown first, then exported as:

.md

.xml (matching PRP1 schema)

.docx

.pdf

The finished program is effectively becoming a synthetic-regulatory-document factory.

3. Generate Mexican-flavored realistic data

Using:

Faker (es_MX) for names, addresses, companies

curated lists if Faker lacks fidelity

An LLM persona (“rushed junior lawyer”) to generate believable legal narrative

The goal is not perfection—it’s authentic imperfection.

4. Introduce controlled errors

This is essential.

The developer agent must create:

Typographical mistakes

Slight inconsistencies

Formatting flaws

Real-life document quality issues

Because your downstream C# system must be tested against messy, real-world inputs.

This is crucial for system robustness.

5. Generate many cases automatically

The developer agent must produce:

Batches of unique test cases

Distinct names, case numbers, dates, narratives

Everything self-contained

This forms a reusable dataset for automated QA, training, and validation.

6. Build a deterministic, repeatable pipeline

The instructions also tell the agent to design:

A Python pipeline

With interchangeable components (Faker, LLM, templating, exporters)

This makes the generator:

Maintainable

Extensible

Auditable

Deterministic when needed

🧩 In Summary

Learns real regulatory document structure from PRP1

Produces two-part regulatory request files

Generates realistic Mexican data

Adds controlled imperfections

Exports files in multiple formats (.md, .xml, .docx, .pdf)

Generates large batches of test cases

Simulates the exact messiness of real-world CNBV workflows

In other words:

You are developing an industrial-grade simulator of regulatory request documents for testing our internal compliance automation system.


Unified Prompt Instructions for the Test-Document Generation System
1. Data Generation Layer
1.1 Realistic Structured Data

Use the Faker library (Python) as the primary engine for generating realistic Mexican data.

Ensure Faker uses the es_MX locale for:

Full names (authentic to Mexico)

CURP/RFC-appropriate name patterns (where needed)

Mexican phone numbers and email patterns

Cities, municipalities, states, and postal codes

Mexican-style addresses and company names

If Faker’s locale does not support a required field with adequate realism, fall back to custom curated datasets.

1.2 Semi-Structured and Narrative Data

For sections requiring human-written institutional narrative—such as:

“Fundamento jurídico”

“Motivación y antecedentes”

“Descripción de hechos”

“Contexto del requerimiento”

Generate text using an LLM configured with the persona:

“A competent but rushed junior lawyer from a Mexican regulatory entity.”

The persona must intentionally introduce:

Occasional typos

Minor inconsistencies

Slight formatting imperfections

Redundant phrasing

These imperfections must remain moderate and plausible.

2. Template & Document Assembly Layer
2.1 Base Template Format

Every test case is generated into a Markdown (.md) file.

Each file contains two sequential documents in the following strict order:

Document 1 – Originating Authority Request

Simulate the formal document emitted by the true originating authority, such as:

SAT

FGR

Poder Judicial

Unidad de Inteligencia Financiera

Autoridades estatales o municipales

The style must reflect:

Realistic formatting

Imperfections and bureaucratic irregularities

Government-style jargon

Optional stamps, seals, or codes as text representations

Document 2 – CNBV “Vetted” Request

This is the processed and standardized version that CNBV sends to the bank:

Must include CNBV-format identifiers, tracking numbers, legends, disclaimers

Must preserve all essential elements of Document 1

May re-organize or standardize content

Tone should be more polished but still contain realistic clerical imperfections

3. Output Formats Layer
3.1 Markdown Output

The generator must produce one .md file per test case.

Each .md file is a self-contained bundle containing the two documents described above.

3.2 Additional Required Formats

After assembling the Markdown version:

Produce equivalent outputs in:

XML

DOCX

PDF

The XML schema must reflect the schema extracted from analyzing the real samples located in PRP1/.

The PDF and DOCX must preserve:

Section structure

Tables

Line breaks

Imperfections and formatting inconsistencies

4. Ground-Truth Analysis Layer

Before generating any test set:

4.1 Analyze PRP1/ Directory

You must fully analyze:

At least one real XML file to learn:

Tag hierarchy

Attribute conventions

Null-field representation

Data typing expectations

All companion documents to understand:

Page layout conventions

How inconsistencies appear in real samples

How CNBV transforms an originating request into a vetted request

The goal:

Mimic real-world imperfections, not idealized versions.

5. Document Generation Pipeline
5.1 Step-by-Step Pipeline Requirements
Step 1 — Extract Schema

Parse one PRP1/*.xml file.

Build an internal representation of the real-world schema.

Step 2 — Generate Synthetic Data

Use Faker + curated lists.

Use LLM for narrative/legalistic paragraphs.

Step 3 — Render Document 1 (Authority)

Combine structured + narrative data into the Markdown template.

Step 4 — Render Document 2 (CNBV Vetted)

Normalize structure.

Add CNBV metadata.

Correct some—but not all—errors from Document 1.

Step 5 — Export XML

Populate your recovered schema.

Reflect missing or malformed fields as real systems do.

Step 6 — Export DOCX + PDF

Maintain layout, imperfections, headers.

Step 7 — Batch Generation

Produce hundreds of test cases.

Each case is unique, with distinct:

Names

Dates

Tracking numbers

Narrative details

Typographical defects

6. Architectural & Implementation Notes
6.1 Python as Primary Orchestration

Faker + python-docx + PyPDF or equivalent recommended for the generation stage.

LLM integration must be modular to allow persona swapping.

6.2 Imperfection Framework

Introduce controlled noise:

Randomized missing fields

Misaligned text blocks

Inconsistent spacing

Occasional OCR-like artifacts (simulated in text)

6.3 Safety & Realism

No real data.

All persons/entities strictly synthetic.

End of Unified Instruction Document

If you'd like, I can now also:
✅ Convert this to a system prompt
✅ Convert into JSON for programmatic use
✅ Convert into a technical specification document
✅ Transform it into a JIRA epic with user stories and tasks