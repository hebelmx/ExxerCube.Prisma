# Project: Prisma Dummy Generator

This project is a development and testing platform for a solution that automates a bank's response to official information requests (*requerimientos*) from Mexican authorities (e.g., SAT, FGR). These requests are centralized and distributed to financial institutions by the CNBV (Comisión Nacional Bancaria y de Valores) through its SIARA (Sistema de Atención de Requerimientos de Autoridad) platform.

## Core Workflow Being Automated

1.  **Request Reception (Authority -> CNBV -> Bank):** An authority sends a request to the CNBV, which then formally notifies the bank electronically via SIARA.
2.  **Bank's Internal Management & Response (Automated Solution):**
    *   **Reception:** The system automatically ingests the electronic request from the CNBV.
    *   **Analysis & Classification:** An OCR and NLP pipeline analyzes the request document (PDF/image) to extract key metadata: requesting authority, type of request (information, freeze funds, etc.), and client identifiers (name, CURP, etc.).
    *   **Search & Compilation:** The system integrates with the bank's internal databases (core banking, digital records) to find the requested information (account statements, contracts, etc.).
    *   **Response Generation:** It automatically creates the response files in the specific formats mandated by the CNBV (e.g., XML, formatted PDFs).
    *   **Delivery:** The generated response is securely transmitted back to the CNBV.
3.  **Final Delivery (CNBV -> Authority):** The CNBV channels the bank's response to the original requesting authority.

## Role of this Repository

This repository does not handle live production data. Instead, it serves as a comprehensive testbed to develop and validate the automation pipeline.

-   **Dummy Generation (`prp1_generator`, `simulate_documents.py`):** These scripts create realistic but synthetic *requerimientos* and associated client data. This allows for end-to-end testing without using sensitive, real-world information.
-   **OCR & Extraction (`ocr_pipeline`, `ocr_modules`):** This is the core engine that "reads" the dummy requests and extracts the necessary parameters for the subsequent steps.
-   **Validation (`*_validator.py` scripts):** These tools are used to verify that the data extracted from the dummy requests is accurate and that the generated response files are correctly formatted according to regulatory specifications.
