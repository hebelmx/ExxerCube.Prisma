# Entity Source Catalog for Prisma

**Date:** November 2025
**Purpose:** Official sources for extracting entity catalogs using the established methodology

---

## Summary of Sources Found

| Entity Category | Priority | Source Type | Download Available |
|-----------------|----------|-------------|-------------------|
| SAT Structure | HIGH | PDF + DOF | ✅ Yes |
| FGR/Fiscalías | HIGH | PDF + Web | ✅ Yes |
| State Fiscalías (32) | HIGH | PDF | ✅ Yes |
| UIF Structure | HIGH | PDF | ✅ Yes |
| IMSS Delegations | MEDIUM | Web + PDF | ✅ Yes |
| CNBV Entities | MEDIUM | Web API | ✅ Yes |
| Financial Institutions | MEDIUM | Banxico API | ✅ Yes |

---

## 1. SAT (Servicio de Administración Tributaria)

### Primary Source: Manual de Organización General del SAT 2024

**Published:** February 1, 2024 in DOF (Diario Oficial de la Federación)

| Resource | URL |
|----------|-----|
| DOF Publication | https://www.dof.gob.mx/nota_detalle.php?codigo=5715769&fecha=01/02/2024 |
| Official Organigrama PDF | http://omawww.sat.gob.mx/SNTHYSAT/Paginas/documentos/Organigrama.pdf |

**Content includes:**
- Estructura orgánica completa
- Administraciones Generales (AG)
- Administraciones Centrales
- Administraciones Desconcentradas
- Subadministraciones
- Funciones de cada unidad

**Key entities to extract:**
- Jefatura del SAT
- Administración General de Recaudación
- Administración General de Auditoría Fiscal Federal
- Administración General de Aduanas
- Administración General Jurídica
- Administraciones Locales (by state)

---

## 2. FGR (Fiscalía General de la República)

### Primary Sources

| Resource | URL |
|----------|-----|
| Official Directory | https://fgr.org.mx/es/FGR/directorio |
| Alternative Directory | https://fgr.org.mx/swb/FGR/directorio |
| Fiscalías Page | https://fgr.org.mx/en/FGR/Fiscalias |
| Delegations Directory PDF | https://www.gob.mx/cms/uploads/attachment/file/578735/Delegaciones_FGR.pdf |
| Ley de la FGR (structure) | https://www.diputados.gob.mx/LeyesBiblio/pdf/LFGR.pdf |

**Key entities to extract:**

### Fiscalías Especializadas:
1. Fiscalía Especializada en Materia de Delincuencia Organizada (FEMDO)
2. Fiscalía Especializada en Materia de Delitos Electorales (FEDE)
3. Fiscalía Especializada en Materia de Combate a la Corrupción
4. Fiscalía Especializada en Materia de Derechos Humanos
5. Fiscalía Especializada en Delitos de Violencia contra las Mujeres
6. Fiscalía Especializada de Asuntos Internos

### Other Units:
- Agencia de Investigación Criminal
- Órgano Especializado de Mecanismos Alternativos
- Delegaciones Estatales (32 states)

---

## 3. State Fiscalías/Procuradurías (32 States)

### Primary Sources

| Resource | URL |
|----------|-----|
| CNPJ Directory | http://www.cnpj.gob.mx/Paginas/procuradurias_fiscalias.aspx |
| FONACOT Directory PDF | https://www.fonacot.gob.mx/creditofonacot/cliente/Documents/Prevenci%C3%B3nFraudes/Directorio_Fiscalias.pdf |
| GOB.MX 2018 Directory PDF | https://www.gob.mx/cms/uploads/attachment/file/395910/Directorio_Procuraduri_as-Fiscali_as_2018.pdf |

**States to extract (32):**
1. Aguascalientes
2. Baja California
3. Baja California Sur
4. Campeche
5. Chiapas
6. Chihuahua
7. Ciudad de México (FGJCDMX)
8. Coahuila
9. Colima
10. Durango
11. Estado de México
12. Guanajuato
13. Guerrero
14. Hidalgo
15. Jalisco
16. Michoacán
17. Morelos
18. Nayarit
19. Nuevo León
20. Oaxaca
21. Puebla
22. Querétaro
23. Quintana Roo
24. San Luis Potosí
25. Sinaloa
26. Sonora
27. Tabasco
28. Tamaulipas
29. Tlaxcala
30. Veracruz
31. Yucatán
32. Zacatecas

---

## 4. UIF (Unidad de Inteligencia Financiera)

### Primary Sources

| Resource | URL |
|----------|-----|
| Official Portal | https://www.gob.mx/uif |
| Alternative Portal | https://www.uif.gob.mx/ |
| Presentation PDF | https://www.gob.mx/cms/uploads/attachment/file/425024/PRESENTACION_UIF_GOBMX.pdf |
| Atribuciones PDF | https://www.pld.hacienda.gob.mx/work/models/PLD/documentos/atribucionesuif_art15_rishcp.pdf |

**Note:** UIF is a unit within SHCP, not a separate organization. Key personnel:
- Titular de la UIF
- Direcciones internas

---

## 5. IMSS (Instituto Mexicano del Seguro Social)

### Primary Sources

| Resource | URL |
|----------|-----|
| Official Structure | https://www.imss.gob.mx/conoce-al-imss/estructura |
| Installation Directory | https://www.imss.gob.mx/directorio |
| Subdelegations Guide | https://subdelegacionesimss.com/ |
| Organic Regulation PDF | http://www.diputados.gob.mx/LeyesBiblio/regla/n224.pdf |

**Key entities to extract:**
- Dirección General
- Direcciones Normativas (DPM, DJ, DIR, etc.)
- OOAD (Órganos de Operación Administrativa Desconcentrada) - 35 units
- Delegaciones Estatales
- Subdelegaciones (130+ nationwide)

---

## 6. CNBV (Comisión Nacional Bancaria y de Valores)

### Primary Sources

| Resource | URL |
|----------|-----|
| Padrón de Entidades Supervisadas | https://www.gob.mx/cnbv/articulos/padron-de-entidades-supervisadas-de-la-cnbv |
| Consulta de Entidades | https://www.gob.mx/cnbv/articulos/consulta-de-entidades-autorizadas-y-supervisadas-por-la-cnbv |
| Official Site | https://www.cnbv.gob.mx |

**Entity types supervised:**
- Instituciones de Banca Múltiple (Commercial Banks)
- Banca de Desarrollo (Development Banks)
- Casas de Bolsa (Brokerage Firms)
- Sociedades de Inversión (Investment Funds)
- SOFIPOs / SOCAPs (Popular Finance)
- ITFs (FinTech Institutions)

---

## 7. Financial Institutions (Banxico)

### Primary Source

| Resource | URL |
|----------|-----|
| Banxico Institution List | https://www.banxico.org.mx/cep-scl/listaInstituciones.do |

**Provides:**
- Complete list of SPEI-connected institutions
- Bank codes (claves)
- Institution types

---

## 8. Additional Sources

### SHCP (Secretaría de Hacienda y Crédito Público)

| Resource | URL |
|----------|-----|
| Structure | https://www.gob.mx/shcp |
| Procuraduría Fiscal | Part of SHCP structure |

### CONDUSEF

| Resource | URL |
|----------|-----|
| Registry Portal | https://pur.condusef.gob.mx/ |

### INEGI Geographic Codes

| Resource | URL |
|----------|-----|
| State/Municipality Catalog | https://www.inegi.org.mx/app/ageeml/ |

---

## Extraction Priority Plan

### Phase 1 (Immediate - This Week)
1. **SAT Organigrama PDF** - Direct download, well-structured
2. **FGR Delegations PDF** - Direct download
3. **State Fiscalías PDF** - FONACOT/GOB.MX versions

### Phase 2 (Next Week)
4. **IMSS Structure** - Web scraping or PDF
5. **CNBV Padrón** - May need web scraping

### Phase 3 (Following Week)
6. **Banxico Institutions** - API or web scraping
7. **UIF Structure** - Limited public info
8. **INEGI Codes** - Download from INEGI

---

## Download Commands

```bash
# Create directory for source documents
mkdir -p "Prisma/Entidades Legales/Sources"
cd "Prisma/Entidades Legales/Sources"

# SAT Organigrama
wget -O SAT_Organigrama.pdf "http://omawww.sat.gob.mx/SNTHYSAT/Paginas/documentos/Organigrama.pdf"

# FGR Delegations
wget -O FGR_Delegaciones.pdf "https://www.gob.mx/cms/uploads/attachment/file/578735/Delegaciones_FGR.pdf"

# State Fiscalías (2018 version)
wget -O Fiscalias_Estatales_2018.pdf "https://www.gob.mx/cms/uploads/attachment/file/395910/Directorio_Procuraduri_as-Fiscali_as_2018.pdf"

# FONACOT Fiscalías Directory
wget -O FONACOT_Directorio_Fiscalias.pdf "https://www.fonacot.gob.mx/creditofonacot/cliente/Documents/Prevenci%C3%B3nFraudes/Directorio_Fiscalias.pdf"

# UIF Presentation
wget -O UIF_Presentacion.pdf "https://www.gob.mx/cms/uploads/attachment/file/425024/PRESENTACION_UIF_GOBMX.pdf"

# UIF Atribuciones
wget -O UIF_Atribuciones.pdf "https://www.pld.hacienda.gob.mx/work/models/PLD/documentos/atribucionesuif_art15_rishcp.pdf"

# IMSS Reglamento Orgánico
wget -O IMSS_Reglamento_Organico.pdf "http://www.diputados.gob.mx/LeyesBiblio/regla/n224.pdf"

# Ley de la FGR
wget -O Ley_FGR.pdf "https://www.diputados.gob.mx/LeyesBiblio/pdf/LFGR.pdf"
```

---

## Notes

1. Some PDFs may be outdated (2018). Always check for newer versions.
2. Web directories may require scraping for structured data.
3. CNBV Padrón is dynamic - consider periodic refresh.
4. State fiscalías change names frequently (Procuraduría → Fiscalía transition).

---

**Document Status:** Complete
**Next Action:** Download source PDFs and run extraction pipeline
