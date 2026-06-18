# Document Variations Guide

## 🎭 How We Generate Credible Variations

The CNBV Fixture Generator uses **multiple layers of variation** to create documents that look different while remaining realistic and credible.

---

## 🔄 Variation Strategies

### 1. **Persona-Based Variations** (5 Types)

Each document is written from the perspective of a different bureaucratic persona:

#### **Formal Meticulous**
- **Style:** Very detailed, formal, precise
- **Sentence Length:** Long (25-35 words)
- **Vocabulary:** Technical-legal advanced
- **Example:**
  > "Con fundamento en lo dispuesto por los artículos 16 constitucional, 1, 2 y 42 del Código Fiscal de la Federación, así como en ejercicio de las amplias facultades conferidas a esta autoridad fiscal en materia de fiscalización y determinación de créditos fiscales, se procede a solicitar..."

#### **Rushed Practical**
- **Style:** Direct, concise, to-the-point
- **Sentence Length:** Short (10-15 words)
- **Vocabulary:** Simple but correct
- **Example:**
  > "Con base en el artículo 42 del CFF se solicita información bancaria. El plazo es de 5 días hábiles."

#### **Verbose Elaborate**
- **Style:** Long-winded, uses many synonyms
- **Sentence Length:** Very long (35-50 words)
- **Vocabulary:** Rich in synonyms and circumlocutions
- **Example:**
  > "Derivado de las amplias y extensas facultades que la legislación aplicable en materia fiscal otorga, confiere y establece a favor de esta autoridad hacendaria, y en virtud de las necesidades derivadas del procedimiento de revisión, fiscalización y auditoría en curso..."

#### **Technical Precise**
- **Style:** Technical, exact legal citations
- **Sentence Length:** Medium (15-25 words)
- **Vocabulary:** Specific legal terminology
- **Example:**
  > "Conforme al artículo 145, fracciones I, II y III del Código Fiscal de la Federación en relación con el diverso 142 de la Ley de Instituciones de Crédito, se requiere..."

#### **Casual Informal**
- **Style:** Less formal, more accessible
- **Sentence Length:** Medium (12-20 words)
- **Vocabulary:** Standard, avoiding excessive technicalities
- **Example:**
  > "De acuerdo con la ley, necesitamos información de las cuentas bancarias para completar nuestra revisión."

---

### 2. **Narrative Style Variations** (4 Types)

#### **Chronological**
- Order: Background → Motivation → Legal Framework → Instructions
- Focus: Timeline of events

#### **Legal-First**
- Order: Legal Framework → Faculties → Motivation → Instructions
- Focus: Legal authority and regulations

#### **Fact-Based**
- Order: Motivation → Origin → Legal Framework → Instructions
- Focus: Facts and circumstances

#### **Formal Academic**
- Order: Legal Framework → Faculties → Motivation → Instructions
- Focus: Structured, academic approach

---

### 3. **Phrase Variations**

Common legal phrases are replaced with synonyms:

| Original | Variations |
|----------|------------|
| solicitar | requerir, pedir, demandar |
| información | datos, documentación, antecedentes |
| proporcionar | entregar, suministrar, facilitar |
| con fundamento en | con base en, de conformidad con |
| derivado de | como resultado de, en consecuencia de |

**Example Transformation:**
- **Before:** "Se solicita información con fundamento en la ley."
- **After:** "Se requiere documentación de conformidad con la normativa."

---

### 4. **Data Variations**

#### **Names & RFC**
- Each document has unique random Mexican names
- RFC calculated from name and birth date
- CURP with regional variations

#### **Amounts**
- Random amounts between configurable ranges
- Varied number formats:
  - `$1,549,481.25 MN`
  - `$1,549,481.25 M.N.`
  - `la cantidad de $1,549,481.25`

#### **Dates**
- Random dates within realistic ranges
- Format variations:
  - `21/11/2025`
  - `21-11-2025`

#### **Legal References**
- Random selection from authority-specific article catalogs
- Different combinations each time

---

### 5. **Structural Variations**

#### **Section Emphasis**
Different personas emphasize different sections:
- Formal: Heavy on legal citations
- Practical: Focus on requirements
- Verbose: Extensive background

#### **Paragraph Length**
- Short paragraphs (Rushed)
- Medium paragraphs (Technical)
- Long paragraphs (Verbose)

---

## 🧪 Testing Variations

### Generate 5 Documents - See the Difference

```bash
python main_generator.py --count 5 --authority IMSS --seed 42
```

Each document will have:
- **Different persona** (writing style)
- **Different narrative structure** (section order/emphasis)
- **Different phrasing** (synonym variations)
- **Different data** (names, amounts, dates, references)
- **Different errors** (chaos variations)

### With LLM (Maximum Variation)

```bash
ollama serve
python main_generator.py --count 5 --authority IMSS --llm --llm-model llama3
```

LLM adds:
- **Completely unique legal narratives** per document
- **Natural language variations**
- **Contextual phrasing** based on persona
- **Spanish linguistic diversity**

---

## 📊 Variation Matrix

| Layer | Without LLM | With LLM |
|-------|-------------|----------|
| **Persona** | Template-based style hints | Full persona-driven generation |
| **Narrative** | Section order variations | Content structure variations |
| **Phrasing** | Synonym substitution | Natural language paraphrasing |
| **Data** | Random names/amounts/dates | Same + contextual references |
| **Chaos** | Typos, formatting errors | Same |

---

## 🎯 Credibility Factors

### What Makes Variations Credible?

1. **Realistic Personas**: Based on actual bureaucratic writing styles
2. **Legal Coherence**: Phrases are legal synonyms, not random words
3. **Consistent Authority**: Each authority has its specific articles/style
4. **Mexican Context**: Names, RFC, addresses are authentically Mexican
5. **Natural Errors**: Chaos introduces realistic mistakes (missing accents, spacing)

### What We Avoid

❌ **Not Credible:**
- Random technical jargon
- Inconsistent legal references
- Mixing authority types
- Unrealistic data (non-Mexican names, wrong RFC format)

✅ **Credible:**
- Authority-appropriate language
- Valid legal article references
- Mexican RFC/CURP formats
- Realistic bureaucratic imperfections

---

## 💡 Usage Tips

### For Maximum Uniqueness

```bash
# Use LLM + high variation count
python main_generator.py --count 100 --authority IMSS --llm --llm-model llama3
```

Each document will be **substantively different**:
- Different writing style
- Different legal phrasing
- Different document structure
- Different data points

### For Controlled Testing

```bash
# Use seed for reproducibility
python main_generator.py --count 10 --authority SAT --seed 12345
```

Same documents every time, but still with all variation layers applied.

### For Performance

```bash
# Skip LLM for faster generation
python main_generator.py --count 1000 --authority IMSS
```

Still get persona, narrative, phrasing, and data variations - just faster.

---

## 🔬 Variation Examples

### Same Requirement, 3 Different Personas

**Formal Meticulous:**
> "En ejercicio de las facultades conferidas a esta autoridad fiscal mediante los artículos 16 constitucional, 1, 2 y 42 del Código Fiscal de la Federación, se procede a requerir la información bancaria detallada a continuación, misma que deberá ser proporcionada en el plazo legal establecido de cinco días hábiles contados a partir de la notificación del presente oficio."

**Rushed Practical:**
> "Se requiere información bancaria. Fundamento: Art. 42 CFF. Plazo: 5 días hábiles."

**Verbose Elaborate:**
> "Derivado de las amplias, extensas y robustas facultades que la normativa aplicable en materia de fiscalización y revisión otorga, confiere, establece y determina a favor de esta autoridad hacendaria en el ámbito de sus atribuciones y competencias, y considerando las necesidades derivadas, originadas y surgidas del procedimiento administrativo de auditoría fiscal..."

---

## 🚀 Advanced: Custom Personas

You can extend the system by editing `core/variation_engine.py`:

```python
DocumentPersona.CUSTOM_STYLE = "custom_style"
```

Add your persona description and the LLM will generate text accordingly.

---

## 📈 Impact on E2E Testing

### Why Variations Matter

1. **Robust Parsing**: Your parser must handle different phrasings
2. **Resilient Extraction**: Same data in different formats
3. **Real-World Simulation**: Actual documents vary widely
4. **Edge Case Coverage**: Personas create natural edge cases

### Testing Strategy

```bash
# Generate diverse test set
python batch_generate.py --all --count 50 --llm

# Your E2E tests should handle:
# - Different section orders
# - Synonym variations
# - Different legal references
# - Various formatting styles
# - Realistic typos/errors
```

---

**Result:** Your system will be tested against realistic bureaucratic variation, making it production-ready for actual Mexican banking authority requirements! 🎯
