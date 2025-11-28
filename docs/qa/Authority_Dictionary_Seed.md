# Authority Dictionary Seed (MX) for Fuzzy Matching

Goal: seed a canonical list of common Mexican authorities for fuzzy matching (federal focus) with combinatorial patterns for courts. This is a starting point to load into a dictionary table (CanonicalName, Acronym, Aliases, Scope, Jurisdiction, Category).

## Core federal regulators/authorities (explicit)
- SAT — Servicio de Administración Tributaria; aliases: “SAT”, “Servicio Administracion Tributaria”
- IMSS — Instituto Mexicano del Seguro Social; aliases: “IMSS”, “Inst Mexicano del Seguro Social”
- INFONAVIT — Instituto del Fondo Nacional de la Vivienda para los Trabajadores; aliases: “INFONAVIT”
- CNBV — Comisión Nacional Bancaria y de Valores; aliases: “CNBV”, “Comision Nac Bancaria y de Valores”
- UIF — Unidad de Inteligencia Financiera; aliases: “UIF”, “Unidad Inteligencia Financiera”
- SHCP — Secretaría de Hacienda y Crédito Público; aliases: “SHCP”, “Secretaria Hacienda y Credito Publico”
- FGR — Fiscalía General de la República; aliases: “FGR”, “Fiscalia Gral de la Republica”, “PGR”
- PROFECO — Procuraduría Federal del Consumidor; aliases: “PROFECO”
- CONDUSEF — Comisión Nacional para la Protección y Defensa de los Usuarios de Servicios Financieros; aliases: “CONDUSEF”

## Combinatorial court name patterns (federal/state)
Use these tokens to generate candidate aliases:
- Ordinals: “Primero”, “Segundo”, “Tercero”, “Cuarto”, “Quinto”
- Organ type: “Juzgado”, “Tribunal”, “Sala”
- Specialty: “Familiar”, “Laboral”, “Penal”, “Civil”, “Mercantil”, “Administrativo”, “Fiscal”
- Instance: “Primera Instancia”, “Segunda Instancia”, “Colegiado”, “Unitario”
- Jurisdiction placeholders: e.g., “CDMX”, “Jalisco”, “Nuevo León”, “Estado de México”
- Examples to generate:
  - “Juzgado Primero de lo Familiar”, “Juzgado Segundo de lo Civil”, “Juzgado Tercero Penal”
  - “Tribunal Colegiado en Materia Administrativa”, “Sala Penal”, “Sala Civil”
  - Add jurisdiction suffix: “del Distrito Federal”, “de la Ciudad de México”, “de Jalisco”, etc.

Suggested combinatorial expansion (pseudo):
```
Ordinals × Organ × Specialty → base name
Append instance (optional): “de Primera Instancia”, “Colegiado”, “Unitario”
Append jurisdiction: “del Distrito Federal”, “de la Ciudad de México”, “del Estado de Jalisco”, etc.
```

## Data model fields (for the dictionary table)
- CanonicalName (string)
- Acronym (string)
- Aliases (array)
- Scope (Federal/State)
- Jurisdiction (e.g., “CDMX”, “Jalisco”, “Nacional”)
- Category (Authority/Judicial/Regulator/Fiscal)
- Source (e.g., “seed:core”, “generated:combinatorial”)
- LastUpdated (date)

## Next steps
1) Seed the explicit federal authorities above.
2) Generate combinatorial court aliases using the patterns and add top jurisdictions you handle most.
3) Store in DB/CSV and wire the matcher to map incoming text → canonical record (keep Unknown/Other for outliers).
