# Legal Data Alignment & Canonical XML Plan

## Scope and Sources
This memo aligns the SIARA/SIRO regulatory data requirements with our current XML samples and domain model. Sources reviewed: R29 A-2911 instructivo (03/03/2016, 22 pp.), Disposiciones SIARA (04/09/2018, 15 pp.), mesicic6_mx_anex18 (federal dispositions, 11 pp.), SIARA user manuals (Civiles v01, Sistemas de Gestión, manual_SIARA), Instructivo requerimiento físico DGAAAC (2019), and CNBV XML samples in `Prisma/Fixtures/PRP1/*.xml`. Requirements catalog: `docs/qa/Requirements.md`.

## Legal Field Requirements (high level)
- Identification: expediente, número de oficio, folio SIARA, año, autoridad solicitante, área/subdivisión, fundamento legal, plazo en días, fecha de publicación/recepción, referencias (oficios previos, acuerdos).
- Sujetos requeridos: carácter (actor/demandado/investigado/patrón), tipo de persona, nombre completo y RFC, domicilio, complementarios (CURP, variantes RFC).
- Medidas y motivación: aseguramiento/desbloqueo/transferencia/documentación/información; instrucciones textuales; cuentas/productos, montos, fechas límite; bloqueos previos y oficio inicial.
- Evidencia de envío y medio: canal SIARA, firma/folio, formato PDF/TIFF según guía de gestión.
- SLA: fecha estimada de conclusión = fecha recepción + días hábiles (Disposiciones SIARA, 2018).

## Canonical XML 
```xml
<Expediente xmlns="http://www.cnbv.gob.mx">
  <Encabezado>
    <NumeroExpediente>H/IN1-0000-000000-AAA</NumeroExpediente>
    <NumeroOficio>214-1-18714972/2025</NumeroOficio>
    <FolioSiara>UIFB/2025/000104</FolioSiara>
    <Area clave="A/AS">ASEGURAMIENTO</Area>
    <Autoridad nombre="CNBV" unidad="DGAAAC" especifica="UIF" />
    <FechaPublicacion>2025-06-05</FechaPublicacion>
    <FechaRecepcion>2025-06-06</FechaRecepcion>
    <DiasPlazo>10</DiasPlazo>
    <FundamentoLegal>Art. 142 LIC; Disposiciones 04/09/2018</FundamentoLegal>
    <Referencias>
      <OficioInicial>110/F/B/4469/2021</OficioInicial>
      <Acuerdo>105/2021</Acuerdo>
    </Referencias>
  </Encabezado>
  <Sujetos>
    <Parte id="1" rol="Investigado" tipoPersona="Fisica">
      <Nombre>
        <Paterno>LUIS</Paterno><Materno>MCDONALD</Materno><Nombre>HUGO PACO</Nombre>
      </Nombre>
      <RFC variante="v1">LUMH111111111</RFC>
      <RFC variante="v2">LUMH222222222</RFC>
      <CURP>…</CURP>
      <Domicilio>…</Domicilio>
      <Complementarios>FechaNacimiento=1991-01-01</Complementarios>
    </Parte>
  </Sujetos>
  <Medidas>
    <Tipo>Desbloqueo</Tipo>
    <Motivacion texto="Acuerdo 144/2025…" />
    <Cuentas>
      <Cuenta numero="00466773850" banco="BBVA" sucursal="Uruapan" producto="CuentaDebito" />
      <Cuenta numero="00195019117" banco="BBVA" sucursal="Nueva Italia" />
    </Cuentas>
    <Montos>
      <Monto moneda="MXN" tipo="Garantia">76813.31</Monto>
    </Montos>
    <Documentacion solicitada="true">
      <Item tipo="EstadoCuenta" periodoInicio="2024-01-01" periodoFin="2024-03-31" certificada="true" />
      <Item tipo="Contrato" certificada="true" />
      <Item tipo="IDCliente" />
    </Documentacion>
  </Medidas>
  <Entrega>
    <Medio>SIARA</Medio>
    <Formato>XML</Formato>
    <EvidenciaFirma tipo="FELAVA" />
  </Entrega>
</Expediente>
```

## Gap Analysis (samples vs. required)
- Missing completeness markers: XML samples lack fecha de recepción, fundamento legal, medio de envío/firma, and explicit medida tipo (bloqueo/desbloqueo/documentación). Only `TieneAseguramiento` hints at intent.
- Identity fidelity: Multiple RFC variants appear only in text (`Complementarios`) or duplicated `<PersonasSolicitud>` (e.g., `555CCC-66666662025.xml`); canonical should normalize RFCs with variant metadata.
- Traceability: No SLA-derived fecha estimada de conclusión and no references to oficio inicial or bloqueo origin, which Disposiciones and Instructivo require.
- Product-level detail: Accounts/products are embedded in free text (`InstruccionesCuentasPorConocer`); schema should expose structured `<Cuentas>` and `<Productos>` nodes.

## Domain Model Alignment (C#)
- Existing enums (`Domain/Enums`) cover workflow (Classification*, ComplianceActionType, ReviewStatus) but not legal areas/subdivisions (A/AS, A/DE, A/TF, A/IN, J/AS, J/DE, J/IN, H/IN, E/AS, E/DE, E/IN) or measure types (Bloqueo, Desbloqueo, Transferencia, Documentacion, Informacion).
- Entities not yet verified for: oficio metadata (fundamento legal, referencias), SLA dates (recepción, estimada conclusión), multi-RFC handling, structured cuentas/productos, and evidence of envío/firma. These need inspection and augmentation in Application + Infrastructure adapters.

## Proposed Enum Additions (Domain)
- `LegalSubdivision`: values per CNBV layout (A/AS, A/DE, A/TF, A/IN, J/AS, J/DE, J/IN, H/IN, E/AS, E/DE, E/IN).
- `MeasureType`: Bloqueo, Desbloqueo, TransferenciaFondos, Documentacion, Informacion.
- `DocumentItemType`: EstadoCuenta, Contrato, Identificacion, ComprobanteDomicilio, MuestraFirma, ImagenCheque, ExpedienteApertura, Otros.
- `AuthorityType`: CNBV, UIF, Juzgado, SAT/Hacienda, Otra; include `UnidadEspecifica` string for UIF directions.

## Refactor & Testing Plan
- Model: Add canonical `Expediente` aggregate capturing encabezado, sujetos, medidas, cuentas/productos, documentación, SLA fields, and reference links. Introduce value objects for RFC (with variants) and Cuenta (banco, sucursal, producto, numero).
- Parsing: Extend XML mapper to populate structured medidas/cuentas/documentación; add text parsers for PDFs/Word to extract missing fields when XML absent (per Requirements.md note).
- Validation: Enforce required set per Disposiciones (plazo, oficio, autoridad, área, sujeto, medida) with completeness checks and reasoned warnings when derived from OCR.
- Tests: 
  - Fixture-driven unit tests using `Prisma/Fixtures/PRP1/*.xml` to assert canonical mapping and missing-field detection.
  - Golden canonical XML regeneration snapshot tests to ensure schema stability.
  - Integration tests for SLA calculation (fecha recepción + días hábiles) and multi-RFC resolution.

## Extensibility, enums, and null-handling
- Include `Unknown`/`Other` members in enums (`MeasureType`, `LegalSubdivision`, `DocumentItemType`, `AuthorityType`) to absorb legal or schema drift without pipeline breaks.
- Allow optional extension elements under `<Medidas>` and `<Sujetos>`; consumers should ignore unknown nodes gracefully while flagging them for review.
- Prefer derived values (SLA dates, subdivision mapping, measure inference, RFC variants) to minimize empty fields; surface unresolved required fields via validation instead of silent nulls.

## References (APA style)
- Comisión Nacional Bancaria y de Valores. (2018). *Disposiciones de carácter general aplicables a los requerimientos de información...* (4 de septiembre). PDF in `Prisma/Fixtures/PRP1/Disposiciones SIARA 4 de septiembre de 2018.pdf`.
- Comisión Nacional Bancaria y de Valores. (2016). *Instructivo de la Serie R29 A-2911 Aseguramientos, Transferencias y Desbloqueo de Cuentas*. PDF in `Prisma/Fixtures/PRP1/R29 A-2911 Aseguramientos, Transferencias y Desbloqueos de Cuentas_03032016.pdf`.
- Comisión Nacional Bancaria y de Valores. (2019). *Instructivo para el llenado de los formatos para requerir información y documentación por parte de las autoridades hacendarias federales*. PDF in `Prisma/Fixtures/PRP1/Instructivo_requerimiento_fisico_DGAAAC_v2019.pdf`.
- Dirección General Adjunta Atención a Autoridades. (2019). *Manual de usuario del SIARA (Civiles v01)*. PDF in `Prisma/Fixtures/PRP1/SIARA_Manual_Civiles_v01.pdf`.
