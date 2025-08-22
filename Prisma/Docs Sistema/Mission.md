##Project Title:
**Automated Preprocessing Pipeline for Scanned Documents with Unknown Visual Characteristics**

##1. Project Overview
This project addresses the challenge of preprocessing highly variable scanned documents originating from diverse sources across Mexico. These documents exhibit a wide range of visual degradation—from medium to very poor quality—and possess unpredictable content structures and noise patterns. In some cases, even human interpretation requires magnification tools.
The objective is to design and validate a preprocessing pipeline that can dynamically adapt to the unique characteristics of each scanned document, improving their readiness for downstream Optical Character Recognition (OCR) without altering the underlying textual content.

##2. Problem Statement
Manual preprocessing is infeasible due to:
The scale and heterogeneity of the documents


The lack of prior metadata describing quality or format


The presence of visual obstructions like RGB watermarks, overlays, and stains


An automated approach is needed to intelligently assess and enhance each image before OCR is applied.

##3. Methodology
3.1. Synthetic Document Generation
Generate a diverse corpus of synthetic (dummy) document images with controlled properties (e.g., contrast, noise, watermark types).


These will serve as the testbed for pipeline development and evaluation.

##4. Tasks

4.1 Task 1
**Task Asigment**

#Provided a curated list of legal terms for court ordered finantial requirments
Read the reference documents to extract a curated list of spanish legal terms who can be presents on the reference documents to use
on the pipeline to solve the present project, Articulo142.pdf, SIARA_Manual_Civiles_v01.pdf, LUC.pdf, LACP.pdf, LRASCAP.pdf,LeyInstTecnFinanciera.pdf,LeyInstCredito.pdf 
an example of a requirment is provided, DumyPrisma1.md please not this is a fictional requirment, the pdf documents are the source of thrut as they are the law
this sample is just provided as an example or how the document are exhibited on two paragraphs and with a water mark hash on red
crossing the document beenig this a text document is not visible on this way so a image is also provided. DumyPrisma1.png Please noticed this is also
a fiticious document.

4.2 Task 2
**Task Asigment**

Generate a corpus of text of fictitios requirments acording to the Articulo142.pdf, SIARA_Manual_Civiles_v01.pdf, with data ficticious but reasonable sounded
in order to make concept test, the requirments must be diverse worded as junior lawers providing from distinc mexican cosmologies, with some errors
of each type typograical, legalese (small errors) and redaction, and also very good redacted documents must be provided.

4.3 Task 3
**Task Asigment**

Generate a clasification of all posible requirments acording to the Articulo142.pdf, SIARA_Manual_Civiles_v01.pdf.
This clasification must be generated on a tree format structure and must be acompanaided of a proposed json structured output of the information or requirment 
needed.

4.4 Task 4
**Task Asigment**

Generate a corpus of text of fictitios requirments acording to the Articulo142.pdf, SIARA_Manual_Civiles_v01.pdf, with data ficticious but reasonable sounded
in order to make concept test, the requirments must be diverse worded as junior lawers providing from distinc mexican cosmologies, with some errors
of each type typograical, legalese (small errors) and redaction, and also very good redacted documents must be provided.
Each document must be acompanaided of a fictiuous hash to generate a watermarks overlays. At least 999 hundres requirments must be generated. on a simple txt or md file
separated by a clear makr

<--Start Requirment--->
**requirment**
(Text of the requeriment )
**Hash**
(Generated hash )
<--End Requirment--->

4.2 Task 5
**Task Asigment**
Write a script to simulate the printing, scaning and deterioring of the  document with the watermark, the input will be the output from the task 4
and the ouput will be a series of Fixutures001 to Fixtures999, both pdf and png files format are required 