# Mandatory Fields for CNBV Compliance (Anexo 3 Requirements)

## Document Source
Based on: "R29 A-2911 Aseguramientos, Transferencias y Desbloqueos de Cuentas_03032016.pdf" (Instructivo)

---

## Field Categories per R29 Report Structure

### SECTION I: REPORT IDENTIFIER

| Column | Field Name | Spanish Name | Data Type | Length | Format | Required | Validation |
|--------|------------|--------------|-----------|--------|--------|----------|------------|
| 1 | Period | PERIODO | Numeric | 6 | AAAAMM | YES | Must be valid year-month |
| 2 | Institution Code | CLAVE DE LA INSTITUCIÓN | Alphanumeric | 6 | XXXXXX | YES | From CNBV institution catalog |
| 3 | Report Code | REPORTE | Numeric | 4 | #### | YES | Fixed value: 2911 |

**Page Reference:** R29 Instructivo, page 21

**XML Documentation:**
```csharp
/// <summary>
/// Periodo de reporte en formato año-mes (AAAAMM)
/// </summary>
/// <example>202312</example>
public string Periodo { get; set; }

/// <summary>
/// Clave de la institución financiera asignada por CNBV
/// </summary>
public string ClaveInstitucion { get; set; }

/// <summary>
/// Código del reporte R29 (valor fijo: 2911)
/// </summary>
public int CodigoReporte { get; set; } = 2911;
```

---

### SECTION II: REQUIREMENT DATA (DATOS DEL REQUERIMIENTO)

| Column | Field Name | Spanish Name | Data Type | Length | Format | Required | Validation |
|--------|------------|--------------|-----------|--------|--------|----------|------------|
| 4 | Request Medium | MEDIO DE SOLICITUD DEL REQUERIMIENTO | Numeric | 3 | ### | YES | 100=Directo, 200=Vía CNBV |
| 5 | Authority Code | CLAVE DE AUTORIDAD ESPECÍFICA | Alphanumeric | 15 | XXXXXXXXXX | YES | From Authority Catalog |
| 6 | Authority Description | DESCRIPCIÓN DE LA AUTORIDAD | Alphanumeric | 250 | XXXXXXXXXX | YES | Full authority name |
| 7 | Request Number | NÚMERO DE OFICIO EMITIDO POR LA AUTORIDAD | Alphanumeric | 30 | XXXXXXXXXX | YES | With -XXX suffix for multiple holders |
| 8 | Request Date | FECHA DE SOLICITUD DEL ASEGURAMIENTO | Numeric | 8 | AAAAMMDD | YES | ISO date format |
| 9 | SIARA Folio | FOLIO SIARA O REFERENCIA | Alphanumeric | 30 | XXXXXXXXXX | YES | 18digits/AAAA/6digits OR authority ref |
| 10 | Requested Amount | MONTO SOLICITADO A ASEGURAR | Numeric | 25 | ######### | YES | Pesos rounded, 0 if not specified |

**Page Reference:** R29 Instructivo, pages 4-6

**XML Documentation:**
```csharp
/// <summary>
/// Medio por el cual fue notificado el requerimiento a la institución
/// </summary>
/// <remarks>
/// 100 = Directo (notificado directamente por la autoridad)
/// 200 = Vía CNBV (notificado a través del Sistema SIARA)
/// </remarks>
public int MedioSolicitud { get; set; }

/// <summary>
/// Número de oficio con el que la autoridad identifica su solicitud
/// </summary>
/// <remarks>
/// Debe agregarse sufijo -XXX (001-999) cuando existen más de 2 titulares o cotitulares
/// para asociar cada oficio con cada persona (Artículo 7, columna 7)
/// </remarks>
/// <example>FGR/123/2023-001</example>
public string NumeroOficio { get; set; }

/// <summary>
/// Fecha en que el requerimiento fue notificado a la institución
/// </summary>
/// <remarks>
/// Puede ser notificación física o a través del SITIAA (Art. 8)
/// </remarks>
public DateTime FechaSolicitud { get; set; }

/// <summary>
/// Monto en pesos que la autoridad ordenó asegurar
/// </summary>
/// <remarks>
/// Para divisas extranjeras, valorizar a pesos usando criterio A-2 del Anexo 33 CUB.
/// Si la autoridad no especifica monto, reportar 0 (cero)
/// </remarks>
public decimal MontoSolicitado { get; set; }
```

---

### SECTION III: ACCOUNT HOLDER DATA (DATOS DEL TITULAR)

| Column | Field Name | Spanish Name | Data Type | Length | Format | Required | Validation |
|--------|------------|--------------|-----------|--------|--------|----------|------------|
| 11 | Legal Personality | PERSONALIDAD JURÍDICA DEL TITULAR | Numeric | 1 | # | YES | 1=Física Nacional, 2=Moral Nacional |
| 12 | Character/Role | CARÁCTER DEL TITULAR | Alphanumeric | 13 | XXXXXXX | YES | From Character Catalog (CON, IND, etc.) |
| 13 | RFC | CLAVE DEL RFC CON HOMOCLAVE | Alphanumeric | 13 | XXXXXXX | YES | 13 chars (física) or _+12 chars (moral) |
| 14 | Company Name | RAZÓN SOCIAL DEL TITULAR | Alphanumeric | 250 | XXXXXXX | CONDITIONAL | Required if moral, empty if física |
| 15 | First Name(s) | NOMBRE(S) DEL TITULAR | Alphanumeric | 100 | XXXXXXX | CONDITIONAL | Required if física, empty if moral |
| 16 | Paternal Surname | APELLIDO PATERNO DEL TITULAR | Alphanumeric | 100 | XXXXXXX | CONDITIONAL | Required if física, empty if moral |
| 17 | Maternal Surname | APELLIDO MATERNO DEL TITULAR | Alphanumeric | 100 | XXXXXXX | CONDITIONAL | Required if física, empty if moral |

**Page Reference:** R29 Instructivo, pages 7-10

**Validation Rules:**
- **RFC Format:** Persona física = XXXXAAMMDDXXX (4 letters + 6 date digits + 3 homoclave). If homoclave unknown, use XXX
- **RFC Format:** Persona moral = _XXXAAMMDDXXX (underscore + 3 letters + 6 date digits + 3 homoclave)
- **Mutual Exclusion:** If personalidad=1 (física), columns 15-17 required, column 14 empty
- **Mutual Exclusion:** If personalidad=2 (moral), column 14 required, columns 15-17 empty
- **No Special Characters:** Names without accents, abbreviations, titles (Lic., Don, etc.)

**XML Documentation:**
```csharp
/// <summary>
/// Personalidad jurídica del titular de la cuenta asegurada
/// </summary>
/// <remarks>
/// 1 = Persona Física Nacional
/// 2 = Persona Moral Nacional
/// El titular no necesariamente es la persona investigada (puede ser titular o cotitular)
/// </remarks>
public PersonalidadJuridica TitularPersonalidad { get; set; }

/// <summary>
/// Carácter que tiene el titular dentro del procedimiento legal
/// </summary>
/// <remarks>
/// Ejemplos: CON=Contribuyente, IND=Indiciado, IMP=Imputado, INV=Investigado
/// Ver catálogo completo en páginas 7-8 del instructivo R29
/// </remarks>
public string TitularCaracter { get; set; }

/// <summary>
/// Registro Federal de Contribuyentes del titular con homoclave
/// </summary>
/// <remarks>
/// Formato persona física: XXXXAAMMDDXXX (13 caracteres)
/// Formato persona moral: _XXXAAMMDDXXX (13 caracteres con guión bajo inicial)
/// Sin guiones, espacios o caracteres especiales. Usar XXX si no se conoce homoclave
/// </remarks>
/// <example>MAVT790914L20</example>
/// <example>_DCL750621K60</example>
public string TitularRFC { get; set; }

/// <summary>
/// Denominación de la persona moral titular (SIN tipo de sociedad)
/// </summary>
/// <remarks>
/// Ejemplo: "LA FINANCIERA SA DE CV" se reporta como "LA FINANCIERA"
/// Campo vacío si personalidad = 1 (física)
/// </remarks>
public string? TitularRazonSocial { get; set; }

/// <summary>
/// Nombre(s) de la persona física titular
/// </summary>
/// <remarks>
/// Sin abreviaciones, acentos o guiones. Un espacio entre nombres.
/// No incluir títulos (Lic., Don, Señor, etc.)
/// Campo vacío si personalidad = 2 (moral)
/// </remarks>
public string? TitularNombre { get; set; }
```

---

### SECTION IV: CO-HOLDER DATA (DATOS DEL COTITULAR)

| Column | Field Name | Spanish Name | Data Type | Length | Format | Required | Validation |
|--------|------------|--------------|-----------|--------|--------|----------|------------|
| 18 | Legal Personality | PERSONALIDAD JURÍDICA DEL COTITULAR | Numeric | 1 | # | YES | 0=Sin Cotitular, 1=Física, 2=Moral |
| 19 | Character/Role | CARÁCTER DEL COTITULAR | Alphanumeric | 13 | XXXXXXX | YES | From Character Catalog (0 if no co-holder) |
| 20 | RFC | CLAVE DEL RFC DEL COTITULAR | Alphanumeric | 13 | XXXXXXX | CONDITIONAL | Required if personality ≠ 0 |
| 21 | Company Name | RAZÓN SOCIAL DEL COTITULAR | Alphanumeric | 250 | XXXXXXX | CONDITIONAL | Required if moral, empty otherwise |
| 22 | First Name(s) | NOMBRE(S) DEL COTITULAR | Alphanumeric | 100 | XXXXXXX | CONDITIONAL | Required if física, empty otherwise |
| 23 | Paternal Surname | APELLIDO PATERNO DEL COTITULAR | Alphanumeric | 100 | XXXXXXX | CONDITIONAL | Required if física, empty otherwise |
| 24 | Maternal Surname | APELLIDO MATERNO DEL COTITULAR | Alphanumeric | 100 | XXXXXXX | CONDITIONAL | Required if física, empty otherwise |

**Page Reference:** R29 Instructivo, pages 10-13

**Special Note:** "Cotitular" includes beneficiaries, authorized signers, and legal representatives (page 10)

**XML Documentation:**
```csharp
/// <summary>
/// Personalidad jurídica del cotitular, beneficiario, autorizado o representante
/// </summary>
/// <remarks>
/// 0 = Sin Cotitular (cuenta sin cotitular)
/// 1 = Persona Física Nacional
/// 2 = Persona Moral Nacional
/// </remarks>
public PersonalidadJuridica CotitularPersonalidad { get; set; }

/// <summary>
/// Carácter del cotitular, beneficiario, autorizado, firmante o representante
/// </summary>
/// <remarks>
/// Usar "0" si no aplica (sin cotitular)
/// Ver catálogo completo en páginas 11-12
/// </remarks>
public string CotitularCaracter { get; set; }
```

---

### SECTION V: ACCOUNT INFORMATION (INFORMACIÓN DE LA CUENTA)

| Column | Field Name | Spanish Name | Data Type | Length | Format | Required | Validation |
|--------|------------|--------------|-----------|--------|--------|----------|------------|
| 25 | Branch Code | CLAVE DE LA SUCURSAL | Alphanumeric | 30 | XXXXXXX | YES | Institution's internal code |
| 26 | State INEGI | ESTADO INEGI | Numeric | 5 | ##### | YES | From Localidades 2015 catalog |
| 27 | Locality INEGI | LOCALIDAD INEGI | Numeric | 14 | ############ | YES | From Localidades 2015 catalog |
| 28 | Postal Code | CÓDIGO POSTAL DE LA SUCURSAL | Numeric | 5 | ##### | YES | From Estado/Municipio/Colonia catalog |
| 29 | Modality | MODALIDAD | Numeric | 2 | ## | YES | 21=Nómina, 22=Mercado Abierto |
| 30 | Account Level | TIPO O NIVEL CUENTA | Numeric | 3 | ### | YES | 401-406 (Nivel 1-4, Tarjeta, Inversión) |
| 31 | Account Number | NÚMERO DE CUENTA | Alphanumeric | 30 | XXXXXXX | YES | Institution's internal identifier |
| 32 | Product Type | DESCRIPCIÓN DEL PRODUCTO | Numeric | 3 | ### | YES | From Financial Product catalog |
| 33 | Currency | MONEDA DE LA CUENTA | Numeric | 1 | # | YES | 0=Pesos, 1=Dólares, 2=Otra divisa |
| 34 | Initial Blocked Amount | MONTO INICIAL ASEGURADO | Numeric | 25 | ######### | YES | Pesos rounded at blocking moment |

**Page Reference:** R29 Instructivo, pages 13-16

**Key Validation Rules:**
- **Modalidad:** Must match account type (Nómina per Art. 48 bis 2 LIC, or Mercado Abierto)
- **Account Level:** Based on Circular 3/2012 Banxico - transaction limits in UDIS
- **Currency:** All foreign currency amounts MUST be converted to pesos using Criterion A-2 (Anexo 33 CUB)
- **Initial Amount:** Actual amount blocked (may differ from requested if insufficient funds)

**XML Documentation:**
```csharp
/// <summary>
/// Clave de la sucursal donde fue aperturada la cuenta objeto del requerimiento
/// </summary>
public string ClaveSucursal { get; set; }

/// <summary>
/// Código del estado (entidad federativa) donde se ubica físicamente la sucursal
/// </summary>
/// <remarks>
/// Usar campo "Clave Estado" del Catálogo de Localidades 2015 CNBV
/// </remarks>
public int EstadoINEGI { get; set; }

/// <summary>
/// Código de localidad donde se ubica la sucursal
/// </summary>
/// <remarks>
/// Usar campo "Clave de Localidad CNBV" del Catálogo de Localidades 2015
/// </remarks>
public long LocalidadINEGI { get; set; }

/// <summary>
/// Modalidad del producto de captación
/// </summary>
/// <remarks>
/// 21 = Cuenta de Nómina (Art. 48 bis 2 LIC - transferencia electrónica)
/// 22 = Mercado Abierto (cualquier modalidad sin restricción operativa)
/// </remarks>
public int Modalidad { get; set; }

/// <summary>
/// Tipo o nivel de cuenta según Circular 3/2012 Banxico
/// </summary>
/// <remarks>
/// 401 = Nivel 1 (hasta 750 UDIS abonos/mes, 1000 UDIS saldo)
/// 402 = Nivel 2 (hasta 3,000 UDIS abonos/mes)
/// 403 = Nivel 3 (hasta 10,000 UDIS abonos/mes)
/// 404 = Nivel 4 (sin límite de abonos)
/// 405 = Sin nivel (tarjeta de crédito)
/// 406 = Valores e instrumentos de inversión
/// </remarks>
public int NivelCuenta { get; set; }

/// <summary>
/// Número de cuenta asignado por la sucursal
/// </summary>
public string NumeroCuenta { get; set; }

/// <summary>
/// Tipo de producto financiero ofrecido al cliente
/// </summary>
/// <remarks>
/// Ejemplos: 1=Depósito General, 13=Cuenta de Ahorro, 101=Depósito a la Vista, 106=Nómina
/// Ver catálogo completo página 16 instructivo R29
/// </remarks>
public int TipoProducto { get; set; }

/// <summary>
/// Moneda en que está denominada la cuenta
/// </summary>
/// <remarks>
/// 0 = Pesos mexicanos (MXN)
/// 1 = Dólares estadounidenses (USD) - valorizar según Criterio A-2
/// 2 = Otra moneda extranjera - valorizar según Criterio A-2
/// </remarks>
public int MonedaCuenta { get; set; }

/// <summary>
/// Monto en pesos que fue asegurado al momento de la notificación
/// </summary>
/// <remarks>
/// Puede ser menor al solicitado si saldo insuficiente.
/// Para divisas extranjeras, valor ya convertido a pesos (Criterio A-2, Anexo 33 CUB).
/// Sin decimales, sin comas, sin puntos. Redondeo: ≥0.5 hacia arriba, <0.5 hacia abajo.
/// </remarks>
public decimal MontoInicialAsegurado { get; set; }
```

---

### SECTION VI: OPERATION DATA (DATOS DE LA OPERACIÓN)

| Column | Field Name | Spanish Name | Data Type | Length | Format | Required | Validation |
|--------|------------|--------------|-----------|--------|--------|----------|------------|
| 35 | Operation Type | TIPO DE OPERACIÓN | Numeric | 3 | ### | YES | 101-104 (Block/Unblock/Transfer/Cashier) |
| 36 | Operation Request Number | NÚMERO DE OFICIO OPERACIÓN | Alphanumeric | 35 | ######### | YES | Same format as column 7 with -XXX |
| 37 | Operation Request Date | FECHA REQUERIMIENTO OPERACIÓN | Numeric | 8 | AAAAMMDD | YES | ISO date format |
| 38 | SIARA Folio | FOLIO SIARA (SI NO DIRECTO) | Alphanumeric | 30 | XXXXXXX | YES | Format or authority reference |
| 39 | Execution Date | FECHA DE APLICACIÓN MOVIMIENTO | Numeric | 8 | AAAAMMDD | YES | Actual date institution executed |
| 40 | Requested Operation Amount | MONTO DE LA OPERACIÓN REQUERIDO | Numeric | 25 | ######### | YES | Pesos rounded |
| 41 | Operation Currency | MONEDA DE LA OPERACIÓN | Numeric | 1 | # | YES | 0=Pesos, 1=Dólares, 2=Otra |
| 42 | Final Balance | SALDO DESPUÉS DE LA OPERACIÓN | Numeric | 25 | ######### | YES | Account balance after operation |

**Page Reference:** R29 Instructivo, pages 17-20

**Operation Type Codes:**
- **101:** Bloqueo (Freezing/Seizure)
- **102:** Desbloqueo (Unblocking)
- **103:** Transferencia electrónica (Electronic Transfer)
- **104:** Situación de fondos - cheque de caja (Cashier's Check)

**XML Documentation:**
```csharp
/// <summary>
/// Tipo de operación que realiza la institución derivado del requerimiento
/// </summary>
/// <remarks>
/// 101 = Bloqueo (aseguramiento, inmovilización, embargo)
/// 102 = Desbloqueo (liberación de cuenta bloqueada)
/// 103 = Transferencia electrónica de fondos
/// 104 = Situación de fondos (cheque de caja o billete de depósito)
/// </remarks>
public TipoOperacion OperacionTipo { get; set; }

/// <summary>
/// Número de oficio con el que la autoridad requiere la operación específica
/// </summary>
/// <remarks>
/// Debe coincidir con formato de columna 7 (incluyendo sufijo -XXX para múltiples titulares)
/// </remarks>
public string NumeroOficioOperacion { get; set; }

/// <summary>
/// Fecha en que la autoridad notificó el requerimiento de la operación
/// </summary>
/// <remarks>
/// Puede ser física o a través del SITIAA. Formato AAAAMMDD sin separadores
/// </remarks>
public DateTime FechaRequerimientoOperacion { get; set; }

/// <summary>
/// Fecha efectiva en que la institución ejecutó la operación
/// </summary>
/// <remarks>
/// Fecha real de bloqueo, desbloqueo, transferencia o emisión de cheque
/// </remarks>
public DateTime FechaAplicacion { get; set; }

/// <summary>
/// Monto en pesos que la autoridad ordenó desbloquear, transferir o poner a disposición
/// </summary>
/// <remarks>
/// Diferente al aseguramiento inicial (columna 34).
/// Puede ser parcial en caso de liberación o transferencia gradual.
/// Divisas extranjeras valorizadas a pesos (Criterio A-2)
/// </remarks>
public decimal MontoOperacionRequerido { get; set; }

/// <summary>
/// Saldo de la cuenta actualizado después de ejecutar la operación
/// </summary>
/// <remarks>
/// Actualizado a la fecha del reporte.
/// Divisas extranjeras valorizadas a pesos.
/// Sin decimales, redondeado.
/// </remarks>
public decimal SaldoDespuesOperacion { get; set; }
```

---

## Data Type Validation Summary

### Numeric Fields
- **No Decimals:** All monetary amounts rounded (≥0.5 up, <0.5 down)
- **No Separators:** No commas, periods, or special characters
- **Positive Only:** All amounts must be positive or zero
- **Example:** $236,569.68 → 236570

### Date Fields
- **Format:** AAAAMMDD (8 digits, no separators)
- **Example:** August 15, 2023 → 20230815
- **Validation:** Must be valid calendar date

### Alphanumeric Fields
- **No Accents:** Remove all diacritical marks (á→a, ñ→n)
- **Uppercase:** Institutional preference for consistency
- **Single Spaces:** One space between words, no leading/trailing spaces
- **No Special Titles:** Remove Lic., Dr., Don, Sra., etc.

### Currency Conversion
- **Criterion A-2 (Anexo 33 CUB):** Foreign exchange rate methodology
- **Apply to:** All amounts in foreign currency before reporting
- **Result:** All monetary values in pesos

---

## Anexo 3 Cross-Reference Map

**Note:** The "Anexo 3" mentioned in legal documents refers to the R29-2911 report structure itself. The mandatory fields ARE the Anexo 3 specifications.

**Compliance Checklist:**
- [ ] All 42 columns populated (no empty fields permitted per Article 207 CUB)
- [ ] RFC format validated against SAT standards
- [ ] INEGI codes from official CNBV catalogs
- [ ] Currency amounts converted to pesos (Criterion A-2)
- [ ] Numeric values without decimal points, commas, or special characters
- [ ] Dates in AAAAMMDD format
- [ ] Multiple holders handled with -XXX suffix on request numbers
- [ ] Operation types correctly classified (101-104)
- [ ] Co-holder fields conditional on personality type (0/1/2)

---

## XML Schema Generation Notes

**Nullable Fields:**
- Columns 14-17: Conditional based on TitularPersonalidad
- Columns 21-24: Conditional based on CotitularPersonalidad

**Required Attributes:**
- All 42 fields must have `[Required]` attribute
- Conditional fields need `[RequiredIf]` custom validation

**Data Annotations:**
```csharp
[StringLength(13, MinimumLength = 13)]
[RegularExpression(@"^[A-Z]{4}\d{6}[A-Z0-9]{3}$|^_[A-Z]{3}\d{6}[A-Z0-9]{3}$")]
public string RFC { get; set; }

[RegularExpression(@"^\d{8}$")]
public string Fecha { get; set; }

[Range(0, double.MaxValue)]
public decimal Monto { get; set; }
```

---

## Authority for Field Requirements

- **Article 207 CUB:** Institutions must provide operational information established in Anexo 36
- **Article 208 CUB:** Monthly reporting within 10 days of following month
- **Article 213 CUB:** Electronic transmission via SITI system
- **R29 Instructivo (March 3, 2016):** Complete field specifications and validation rules
