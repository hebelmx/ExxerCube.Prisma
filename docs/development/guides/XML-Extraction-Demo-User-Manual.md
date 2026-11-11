# XML Extraction Demo - User Manual

## Overview

The XML Extraction Demo is a stakeholder-ready interface for demonstrating CNBV expediente data extraction from XML files. This page provides one-click demonstrations using real PRP1 fixtures with instant visual validation.

## Purpose

This demo page is designed for non-technical stakeholders (lawyers, compliance officers, finance teams) to verify that the XML extraction system works correctly and produces accurate results.

## Accessing the Demo

**Navigation**: Click "XML Extraction Demo" in the main navigation menu under "Document Processing" section.

**Direct URL**: `/document-processing`

## Features

### 1. Quick Fixture Selection

The page displays 4 real PRP1 XML fixtures as clickable cards:
- 222AAA-44444444442025.xml
- 222AAA-55555555552025.xml
- 222AAA-66666666662025.xml
- 222AAA-77777777772025.xml

**How to use**:
1. Click any fixture card
2. Extraction happens instantly (< 1 second)
3. Success banner shows: "Successfully loaded fixture: [filename]"
4. Statistics cards animate with results

### 2. Animated Statistics Cards

After clicking a fixture, three animated cards display:
- **Fields Extracted**: Total number of individual fields extracted
- **Partes**: Number of party records (stakeholders) found
- **Específicas**: Number of specific instructions found

**Visual indicators**:
- Numbers count up with animation
- Green checkmarks pop in sequentially
- Gradient backgrounds for visual appeal

### 3. Extraction Results Tabs

#### Tab 1: Información General (General Information)
- **Side-by-side comparison table** showing:
  - Left column: XML source snippet (gray monospace)
  - Middle column: Green arrow (→)
  - Right column: Green checkmark + extracted value

**Fields shown**:
- Número de Expediente
- Número de Oficio
- Solicitud SIARA
- Folio
- Año del Oficio
- Área (Clave y Descripción)
- Fecha de Publicación
- Días de Plazo
- Autoridad
- And more...

#### Tab 2: Partes (Stakeholders)
- Table showing all extracted party records
- Columns: Carácter, Tipo, Apellido Paterno, Apellido Materno, Nombre, RFC

#### Tab 3: Específicas (Specific Instructions)
- Table showing specific regulatory instructions
- Columns: ID, Instrucciones

#### Tab 4: Complete Object
- Full JSON dump of extracted Expediente object
- Monospace font for readability
- "Copy to Clipboard" button for exporting

### 4. View Source XML

**Toggle button**: "View Source XML" / "Hide Source XML"

When activated, displays the complete raw XML content in a read-only text field showing exactly what was parsed.

## Verification Workflow

For stakeholders to verify extraction accuracy:

1. **Click a fixture** - Choose any of the 4 PRP1 XML files
2. **Check statistics** - Verify field counts match expectations
3. **Review Información General** - See side-by-side XML→Value proof for every field
4. **Inspect Partes** - Verify all stakeholders extracted correctly
5. **Check Específicas** - Verify regulatory instructions captured
6. **View Source XML** - Toggle to see raw XML for manual verification
7. **Export Complete Object** - Copy JSON for external analysis

## Trust Indicators

### Green Checkmarks
Every successfully extracted field shows a green checkmark (✓) confirming the value was found and parsed correctly.

### Side-by-Side Comparison
The comparison table shows:
```
XML: <NumeroExpediente>222AAA-44444444442025</NumeroExpediente>
  →  ✓ 222AAA-44444444442025
```

This proves the system is reading real XML and not inventing data.

### Animations
Professional CSS animations indicate successful processing:
- Statistics count up (showing progress)
- Checkmarks pop in sequentially (showing validation)

## Technical Details

### Data Source
- **Fixture location**: `Prisma/Fixtures/PRP1/`
- **Format**: CNBV XML files with UTF-8 BOM encoding
- **Source**: Real regulatory XML files from PRP1 dataset

### Extraction Engine
- **Parser**: `IXmlNullableParser<Expediente>`
- **Architecture**: Hexagonal/Clean Architecture pattern
- **Validation**: Real-time validation with AngleSharp

### Performance
- **Extraction time**: < 1 second per fixture
- **Fields extracted**: ~20 base fields + nested collections
- **No server roundtrip**: All processing happens on-demand

## Troubleshooting

### Issue: "Fixture not found" error
**Cause**: Fixture files missing from `Prisma/Fixtures/PRP1/` directory
**Solution**: Verify PRP1 XML files are in the correct location

### Issue: No results showing
**Cause**: Parser service not initialized
**Solution**: Check DI registration for `IXmlNullableParser<Expediente>`

### Issue: Statistics showing 0 values
**Cause**: XML structure doesn't match expected format
**Solution**: Verify XML file uses CNBV PRP1 schema

## Stakeholder Benefits

### For Compliance Officers
- Visual proof that system extracts all required regulatory fields
- Side-by-side comparison shows accuracy
- Export capability for audit trails

### For Legal Teams
- Verify all party information (Partes) extracted correctly
- Check specific legal instructions (Específicas) captured
- View complete data structure for legal review

### For Finance Teams
- Confirm all financial reference fields captured
- Verify dates and deadlines extracted properly
- Export data for financial system integration

### For Management
- Instant demonstration of system capabilities
- Professional presentation with animations
- Clear visual indicators of success

## Best Practices

1. **Start with one fixture** - Click one fixture, review all tabs before testing another
2. **Use side-by-side comparison** - Best proof of accuracy for stakeholders
3. **Show Complete Object last** - After building trust with visual tabs
4. **Toggle Source XML** - For technical stakeholders who want to see raw data
5. **Export for records** - Use "Copy to Clipboard" to save results for documentation

## Future Enhancements

Potential improvements (not yet implemented):
- PDF upload from stakeholder's computer
- Multiple fixture comparison side-by-side
- Export to Excel for detailed analysis
- Historical extraction comparison
- Confidence scoring visualization

## Support

For technical support or questions about the XML Extraction Demo:
- Check logs in browser developer console
- Verify fixture files are UTF-8 encoded
- Contact development team for parser updates
