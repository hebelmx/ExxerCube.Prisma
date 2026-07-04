"""Main orchestrator for CNBV E2E Fixture Generation."""

import argparse
import json
import random
import re
import shutil
import subprocess
from datetime import datetime
from pathlib import Path
from typing import List, Dict, Optional

# Import core modules
from core.data_generator import MexicanDataGenerator
from core.legal_catalog import LegalArticleCatalog
from core.chaos_simulator import RealisticChaosSimulator
from core.llm_client import OllamaClient, LegalTextGenerator, LLMConfig
from core.variation_engine import VariationEngine, DocumentPersona, NarrativeStyle
from core.text_substitution import substitute_placeholders, find_placeholders, MOTIVACION_PLACEHOLDER_MAP

# Import exporters
from exporters.html_exporter import HTMLExporter
from exporters.pdf_exporter import PDFExporter
from exporters.docx_exporter import DOCXExporter
from exporters.markdown_exporter import MarkdownExporter
from exporters.xml_exporter import XMLExporter


def _normalize_whitespace(text: str) -> str:
    """Collapse every run of whitespace (incl. newlines) to a single space.

    `pdftotext` line-wraps and fragments multi-column layouts, so a phrase
    that's contiguous in the source can come out with embedded newlines
    (e.g. "Servicio de\\nAdministración Tributaria"). Normalizing both the
    gold value and the extracted text before comparing makes the match
    whitespace-insensitive without weakening it in any other way (still a
    literal, ordered substring match).
    """
    return re.sub(r'\s+', ' ', text).strip()


def _extract_pdf_text(pdf_path: Path) -> Optional[str]:
    """Extract plain text from a PDF via the `pdftotext` CLI (poppler-utils).

    Returns None (never raises) if `pdftotext` isn't installed or extraction
    fails for any reason -- callers must degrade to a Markdown-only
    containment check with a printed warning rather than crash generation.
    """
    if shutil.which('pdftotext') is None:
        return None

    try:
        result = subprocess.run(
            ['pdftotext', str(pdf_path), '-'],
            capture_output=True, text=True, timeout=30,
        )
        if result.returncode != 0:
            return None
        return result.stdout
    except Exception:
        return None


class CNBVFixtureGenerator:
    """Main orchestrator for generating CNBV requirement fixtures."""

    # --- Ground-truth / source-containment gate configuration ------------
    #
    # Gold fields that MUST be recoverable from the plain-text (Markdown)
    # body for EVERY requirement type, independent of the Motivación-template
    # substitution -- proven present today by other, pre-existing exporter
    # code paths (ID box / recipient block / Servidor Público table /
    # signature / Personas table / Origen section), PLUS the explicit
    # "Autoridad solicitante" line added to markdown_exporter/html
    # template/docx_exporter specifically so `autoridadNombre` is always
    # rendered regardless of requirement type. Maps
    # `ground_truth field name` -> `data dict key`.
    #
    # NOTE (owner ruling, 2026-07-04): `autoridadNombre` is the REQUESTING
    # authority (`AutoridadSolicitanteNombre` = `authority_data['nombre']`,
    # e.g. SAT/IMSS/FGR) -- the meaningful, discriminative extraction target
    # -- NOT the constant CNBV recipient. The CNBV name is still rendered
    # (Destinatario_Institucion / `AutoridadNombre`) but is no longer gold;
    # it is recorded separately as the non-gated `recipientInstitucion`.
    # `numeroExpediente` was moved here (2026-07-04, owner re-verification):
    # it is now ALSO rendered as its own dedicated "Número de expediente:"
    # body line (markdown_exporter/html template/docx_exporter), in ADDITION
    # to the pre-existing Motivación-prose mention that only judicial-type
    # templates embed. Real requerimientos label expediente as a field, and
    # a dedicated non-wrapping line is what an OCR-based harness needs to
    # read it cleanly (see EXPEDIENTE CONTIGUITY note on the gate below) --
    # so it's unconditionally body-intended now, for every requirement type.
    ALWAYS_BODY_INTENDED_FIELDS: Dict[str, str] = {
        'numeroOficio': 'Cnbv_NumeroOficio',
        'numeroExpediente': 'Cnbv_NumeroExpediente',
        'autoridadNombre': 'AutoridadSolicitanteNombre',
        'nombreSolicitante': 'NombreSolicitante',
        'personaNombre': 'Persona_Nombre',
        'personaRfc': 'Persona_Rfc',
        'monto': 'MontoCredito',
    }

    # Gold fields whose ONLY path into the plain-text body is the Motivación
    # template substitution (core/text_substitution.py) -- so they are
    # body-intended ONLY for the requirement type(s) whose template actually
    # embeds the matching `{{Placeholder}}` token (see
    # core/legal_catalog.py:generate_motivacion_template). Maps
    # `ground_truth field name` -> (`data dict key`, `{{Placeholder}}` name).
    #
    # `juzgadoNombre` here is the SAME underlying value as `autoridadNombre`
    # above (`AutoridadSolicitanteNombre`) -- it stays as a separate,
    # conditionally-gated entry because it also tracks whether THIS specific
    # document's Motivación template embedded the `{{JuzgadoNombre}}` token
    # (judicial-type only), which is orthogonal to the always-gated line.
    CONDITIONAL_BODY_INTENDED_FIELDS: Dict[str, tuple] = {
        'fechaDiligencia': ('FechaDiligencia', 'FechaDiligencia'),
        'juzgadoNombre': ('AutoridadSolicitanteNombre', 'JuzgadoNombre'),
        'ejercicio': ('Ejercicio', 'Ejercicio'),
    }

    # Recorded as gold but NOT gated against the Markdown body: today
    # `SolicitudPartes_*` is only ever rendered into the XML/HTML/PDF
    # companions -- markdown_exporter and docx_exporter have no "Partes"
    # section at all. This is a pre-existing, separate gap from the
    # `{{...}}` leak this task fixes (out of this task's 3-item scope);
    # flagged honestly here and in the task report rather than silently
    # gated as if it were body-contained.
    NOT_BODY_GATED_FIELDS: tuple = ('solicitudPartes',)

    def __init__(self,
                 output_base: Path,
                 logo_path: Optional[Path] = None,
                 template_dir: Optional[Path] = None,
                 chaos_level: str = 'medium',
                 seed: Optional[int] = None,
                 use_llm: bool = False,
                 llm_config: Optional[LLMConfig] = None):
        """Initialize fixture generator.

        Args:
            output_base: Base directory for output files
            logo_path: Path to logo image
            template_dir: Directory containing HTML templates
            chaos_level: Level of chaos to introduce (none, low, medium, high)
            seed: Random seed for reproducibility
            use_llm: Whether to use LLM for text generation
            llm_config: LLM configuration (uses defaults if None)
        """
        self.output_base = Path(output_base)
        self.chaos_level = chaos_level
        self.use_llm = use_llm
        self.seed = seed

        # Initialize generators
        self.data_gen = MexicanDataGenerator(seed=seed)
        self.legal_catalog = LegalArticleCatalog()
        self.chaos_sim = RealisticChaosSimulator(seed=seed)
        self.variation_engine = VariationEngine(seed=seed)

        # Initialize LLM client if enabled
        if use_llm:
            ollama_client = OllamaClient(config=llm_config)
            self.llm_generator = LegalTextGenerator(ollama_client=ollama_client)

            # Check if Ollama is available
            if not ollama_client.is_available():
                print("⚠️  Warning: Ollama not available. Falling back to template-based generation.")
                print("   Start Ollama with: ollama serve")
                self.use_llm = False
        else:
            self.llm_generator = None

        # Initialize exporters
        self.html_exporter = HTMLExporter(template_dir=template_dir, logo_path=logo_path)
        self.pdf_exporter = PDFExporter()
        self.docx_exporter = DOCXExporter(logo_path=logo_path)
        self.md_exporter = MarkdownExporter()
        self.xml_exporter = XMLExporter()

        # Requirement types
        self.requirement_types = ['fiscal', 'judicial', 'pld', 'aseguramiento', 'informacion']

    def generate_batch(self,
                      count: int,
                      requirement_types: Optional[List[str]] = None,
                      formats: List[str] = None,
                      authority: Optional[str] = None) -> List[Path]:
        """Generate batch of fixtures.

        Args:
            count: Number of fixtures to generate
            requirement_types: List of requirement types to generate (None = all)
            formats: List of formats to export (md, xml, html, pdf, docx)
            authority: Specific authority to use (IMSS, SAT, UIF, etc.) or None for random

        Returns:
            List of output directory paths
        """
        if requirement_types is None:
            requirement_types = self.requirement_types

        if formats is None:
            formats = ['md', 'xml', 'html', 'pdf', 'docx']

        output_dirs = []

        print(f"\n🚀 Generating {count} CNBV fixtures...")
        print(f"   Authority: {authority or 'Random'}")
        print(f"   Chaos level: {self.chaos_level}")
        print(f"   LLM generation: {'Enabled' if self.use_llm else 'Disabled'}")
        print(f"   Output formats: {', '.join(formats)}")
        print(f"   Requirement types: {', '.join(requirement_types)}\n")

        for i in range(count):
            try:
                # Select random requirement type
                req_type = random.choice(requirement_types)

                # Generate fixture
                output_dir = self.generate_single(
                    index=i + 1,
                    req_type=req_type,
                    formats=formats,
                    authority=authority
                )

                output_dirs.append(output_dir)

                print(f"✓ Generated fixture {i+1}/{count}: {output_dir.name}")

            except Exception as e:
                print(f"✗ Error generating fixture {i+1}/{count}: {e}")

        print(f"\n✅ Completed: {len(output_dirs)}/{count} fixtures generated")
        print(f"📁 Output directory: {self.output_base.absolute()}")

        return output_dirs

    def generate_single(self,
                       index: int,
                       req_type: str = 'fiscal',
                       formats: List[str] = None,
                       authority: Optional[str] = None) -> Path:
        """Generate single fixture with all formats.

        Args:
            index: Fixture index number
            req_type: Requirement type
            formats: List of formats to export
            authority: Specific authority to use or None for random

        Returns:
            Path to output directory
        """
        if formats is None:
            formats = ['md', 'xml', 'html', 'pdf', 'docx']

        # Step 1: Select random variations for this document
        persona = self.variation_engine.select_random_persona()
        narrative_style = self.variation_engine.select_random_narrative_style()

        # Step 2: Generate base data
        data = self._generate_requirement_data(req_type, authority=authority)

        # Step 3: Apply narrative style variations
        data = self.variation_engine.vary_section_order(data, narrative_style)

        # Step 4: Use LLM to generate legal text if enabled (with persona)
        if self.use_llm and self.llm_generator:
            try:
                # Get persona-specific prompt
                persona_info = self.variation_engine.get_persona_description(persona)
                persona_prompt = persona_info['description']

                # Generate text with persona
                llm_texts = {}

                # Generate each section with persona context
                if 'authority' in data:
                    llm_texts['FacultadesTexto'] = self.llm_generator.generate_facultades(
                        data['authority'], persona_prompt=persona_prompt
                    )

                # Apply phrase variations
                for key, value in llm_texts.items():
                    llm_texts[key] = self.variation_engine.apply_phrase_variations(value)

                # Update data with LLM-generated texts
                data.update(llm_texts)

                # Add variation metadata
                data['_variation_info'] = self.variation_engine.get_variation_summary(persona, narrative_style)

            except Exception as e:
                print(f"   ⚠️  LLM generation failed: {e}. Using template-based text.")
                # Continue with template-based text (already in data)
        else:
            # Apply variations to template-based text
            for key in ['FacultadesTexto', 'MotivacionTexto', 'FundamentoTexto']:
                if key in data:
                    data[key] = self.variation_engine.apply_phrase_variations(data[key])

        # Step 2: Apply chaos
        if self.chaos_level != 'none':
            data = self.chaos_sim.apply_chaos(data, level=self.chaos_level)
            data = self.chaos_sim.apply_realistic_errors_to_fields(data, level=self.chaos_level)

        # Step 3: Create output directory
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        folio_safe = data['Cnbv_SolicitudSiara'].replace('/', '-')
        output_dir = self.output_base / f"{folio_safe}_{timestamp}"
        output_dir.mkdir(parents=True, exist_ok=True)

        # Step 4: Export to all requested formats
        base_filename = folio_safe

        if 'md' in formats:
            self.md_exporter.export(data, output_dir / f"{base_filename}.md")

        if 'xml' in formats:
            self.xml_exporter.export(data, output_dir / f"{base_filename}.xml")

        if 'html' in formats:
            self.html_exporter.export(data, output_dir / f"{base_filename}.html")

        if 'pdf' in formats:
            # Export HTML first, then convert to PDF
            html_path = output_dir / f"{base_filename}.html"
            if not html_path.exists():
                self.html_exporter.export(data, html_path)

            self.pdf_exporter.export_from_file(html_path, output_dir / f"{base_filename}.pdf")

        if 'docx' in formats:
            self.docx_exporter.export(data, output_dir / f"{base_filename}.docx")

        # Step 5: Emit the god's-eye ground-truth manifest + containment gate.
        #
        # Dumped from `data` AFTER chaos/phrase-variation, so the recorded
        # gold values byte-match whatever was actually rendered into the
        # exported files -- the manifest is the only thing downstream eval
        # harnesses should trust; the exported PDF/DOCX/XML/MD are all just
        # noisy OBSERVATIONS of it.
        ground_truth = self._build_ground_truth(data, req_type=req_type, output_dir=output_dir)

        md_path = output_dir / f"{base_filename}.md"
        pdf_path = output_dir / f"{base_filename}.pdf"

        if md_path.exists():
            body_text = md_path.read_text(encoding='utf-8')

            # PDF-text cross-check is ADVISORY ONLY (owner re-verification
            # 2026-07-04, reversing the prior "hard-gate on pdftotext too"
            # design): ground-truth OCR of the rendered PIXELS (pdftoppm +
            # tesseract) recovered a hyphen that Chrome's `pdftotext` TEXT
            # LAYER silently drops in some digit-hyphen-digit runs -- the
            # eval harness reads the PDF via OCR of the image, not the text
            # layer, so a pdftotext-only mismatch is a false positive for
            # "not recoverable" and must NOT block generation. The .md body
            # check remains the HARD, authoritative gate: it's the
            # ground-truth proof that the value is genuinely IN the
            # document body at all (the exact defect that made client
            # fixtures untrustworthy) -- independent of which rendering path
            # (pdftotext vs OCR) a downstream harness happens to use.
            pdf_text = None
            if pdf_path.exists():
                pdf_text = _extract_pdf_text(pdf_path)
                if pdf_text is None:
                    print(f"   ⚠️  'pdftotext' unavailable/failed for {output_dir.name}; "
                          f"pdfContained will be recorded as unchecked (advisory only, "
                          f"does not affect the hard .md-based gate).")
            else:
                print(f"   ⚠️  'pdf' not in requested formats for {output_dir.name}; "
                      f"pdfContained will be recorded as unchecked (advisory only).")

            hard_failures, advisory_failures = self._check_body_containment(
                ground_truth, data, body_text, pdf_text
            )
        else:
            hard_failures, advisory_failures = [], []
            print(f"   ⚠️  'md' not in requested formats for {output_dir.name}; "
                  f"body-containment gate skipped (no plain-text source to check against).")

        gt_path = output_dir / "ground_truth.json"
        with open(gt_path, 'w', encoding='utf-8') as f:
            json.dump(ground_truth, f, ensure_ascii=False, indent=2)

        # Advisory: printed for visibility, never blocks generation --
        # pdftotext is a lossy proxy for what an OCR-based harness actually
        # sees (see rationale above). The doc IS still written.
        if advisory_failures:
            print(f"   ⚠️  ADVISORY (non-fatal) for {output_dir.name}: gold field(s) present "
                  f"in the .md body but NOT contiguously recoverable from pdftotext: "
                  f"{'; '.join(advisory_failures)}. Recorded in pdfContained; doc still written "
                  f"-- an OCR-based harness reading pixels is not expected to be affected.")

        # Hard, fail-loud gate: only enforced on the clean set (chaos ==
        # 'none'). This is the AUTHORITATIVE "is the gold genuinely in the
        # document body" guarantee -- it checks the .md text only. Chaos
        # deliberately corrupts rendered text at higher levels, so a
        # mismatch there is expected degradation, not a defect --
        # bodyContained/pdfContained are still recorded honestly for those
        # runs, they just don't raise. The manifest is already persisted
        # above (with the failing maps) before we raise, so it's available
        # for debugging.
        if hard_failures and self.chaos_level == 'none':
            raise ValueError(
                f"Source-containment gate FAILED for {output_dir.name}: gold field(s) "
                f"not found in the rendered Markdown body: {'; '.join(hard_failures)}. "
                f"See {gt_path} for the full bodyContained/pdfContained maps."
            )

        return output_dir

    def _build_ground_truth(self, data: Dict, req_type: str, output_dir: Path) -> Dict:
        """Assemble the god's-eye gold manifest for one generated document.

        Values come straight from the generator's own `data` dict (post-
        chaos) -- never by re-parsing an exported file. See the module-level
        rationale: the delivered PDF/DOCX/JSON/XML are all just noisy
        OBSERVATIONS; this manifest is what the generator itself stamped.

        Args:
            data: Fully-assembled document data (post narrative/LLM/chaos).
            req_type: Requirement type used for this document.
            output_dir: The per-document output directory (used for docId).

        Returns:
            Ground-truth dict, ready for JSON serialization. `bodyContained`
            starts empty; `_check_body_containment` fills it in.
        """
        solicitud_partes = []
        if data.get('SolicitudPartes_Nombre'):
            solicitud_partes.append({
                'nombre': data.get('SolicitudPartes_Nombre', ''),
                'caracter': data.get('SolicitudPartes_Caracter', ''),
            })

        return {
            'docId': output_dir.name,
            'seed': self.seed,
            'chaosLevel': self.chaos_level,
            'requirementType': req_type,
            'folioSiara': data.get('Cnbv_SolicitudSiara', ''),
            'numeroOficio': data.get('Cnbv_NumeroOficio', ''),
            'numeroExpediente': data.get('Cnbv_NumeroExpediente', ''),
            # Requesting authority (SAT/IMSS/FGR/etc.) -- the meaningful,
            # discriminative gold field per owner ruling 2026-07-04.
            'autoridadNombre': data.get('AutoridadSolicitanteNombre', ''),
            # Non-gold, informational only: the constant CNBV recipient
            # institution (always "Comisión Nacional Bancaria y de Valores").
            # Kept for traceability but deliberately NOT named
            # `autoridadNombre` and NOT part of any body-intended field set.
            'recipientInstitucion': data.get('AutoridadNombre', ''),
            'nombreSolicitante': data.get('NombreSolicitante', ''),
            'personaNombre': data.get('Persona_Nombre', ''),
            'personaRfc': data.get('Persona_Rfc', ''),
            'solicitudPartes': solicitud_partes,
            'monto': data.get('MontoCredito', ''),
            'fechaDiligencia': data.get('FechaDiligencia', ''),
            'juzgadoNombre': data.get('AutoridadSolicitanteNombre', ''),
            'ejercicio': data.get('Ejercicio', ''),
            'bodyContained': {},  # filled in by _check_body_containment (Markdown check)
            'pdfContained': {},   # filled in by _check_body_containment (normalized PDF-text check)
        }

    def _check_body_containment(self, ground_truth: Dict, data: Dict, body_text: str,
                                 pdf_text: Optional[str] = None) -> tuple:
        """Verify every body-intended gold field's value is recoverable from the rendered document.

        Checks TWO independent sources per field, with DIFFERENT weight
        (owner re-verification 2026-07-04, reversing the prior "pdftotext is
        also a hard gate" design after an empirical ground-truth OCR test):

          1. `body_text` (the rendered Markdown) -- exact substring match.
             This is the HARD, authoritative "is the gold genuinely IN the
             document body" guarantee -- the exact invariant this whole
             manifest/gate exists to prove (the client-fixture defect was
             values that were NEVER in the body at all, XML-only).
          2. `pdf_text` (pdftotext output of the rendered PDF), if provided --
             whitespace-NORMALIZED substring match. ADVISORY ONLY: an actual
             pixel-OCR test (pdftoppm + tesseract) on a doc where Chrome's
             `pdftotext` TEXT LAYER silently dropped a hyphen inside a
             digit-hyphen-digit run showed the OCR of the rendered IMAGE
             recovered the hyphen fine (just soft-wrapped across a line) --
             the eval harness reads the PDF via OCR of pixels, not the
             pdftotext text layer, so a pdftotext-only mismatch is a false
             positive for "not recoverable" and must NOT block generation.

        A field's `bodyContained` entry reflects ONLY the Markdown check
        (the hard gate). `pdfContained` reflects ONLY the normalized PDF
        check (advisory) and is recorded but never causes a raise. When
        `pdf_text` is None (pdf not generated, or `pdftotext` unavailable),
        `pdfContained` is `None` for every checked field (unchecked, not a
        failure).

        Args:
            ground_truth: Dict from `_build_ground_truth` (mutated in place).
            data: The document data dict (source of truth for field values
                and of `_motivacion_placeholders_used`).
            body_text: The rendered Markdown body to check containment against.
            pdf_text: Raw `pdftotext` output for the rendered PDF, or None if
                unavailable (pdf not requested, or `pdftotext` missing/failed).

        Returns:
            `(hard_failures, advisory_failures)` -- both lists of
            human-readable descriptions. `hard_failures` (body-intended
            fields absent from the Markdown body) are what the caller
            raises on at `chaos == 'none'`. `advisory_failures`
            (body-intended fields present in the Markdown body but NOT
            contiguously recoverable from normalized pdftotext) are
            printed as a warning by the caller but never raised.
        """
        placeholders_used = set(data.get('_motivacion_placeholders_used', []))
        hard_failures: List[str] = []
        advisory_failures: List[str] = []
        body_contained: Dict[str, Optional[bool]] = {}
        pdf_contained: Dict[str, Optional[bool]] = {}

        normalized_pdf_text = _normalize_whitespace(pdf_text) if pdf_text is not None else None

        def _check_one(gold_field: str, value: str) -> None:
            md_found = bool(value) and value in body_text
            body_contained[gold_field] = md_found
            if not md_found:
                hard_failures.append(f"{gold_field}='{value}' (missing from: md)")

            if normalized_pdf_text is None:
                pdf_contained[gold_field] = None
                return

            pdf_found = bool(value) and _normalize_whitespace(value) in normalized_pdf_text
            pdf_contained[gold_field] = pdf_found
            # Advisory only: a pdf-only miss is NOT added to hard_failures,
            # and is only worth flagging when the .md check itself passed
            # (otherwise it's already covered by the hard failure above).
            if md_found and not pdf_found:
                advisory_failures.append(f"{gold_field}='{value}' (not contiguous in pdftotext)")

        for gold_field, data_key in self.ALWAYS_BODY_INTENDED_FIELDS.items():
            _check_one(gold_field, str(data.get(data_key, '')))

        for gold_field, (data_key, placeholder_name) in self.CONDITIONAL_BODY_INTENDED_FIELDS.items():
            if placeholder_name not in placeholders_used:
                body_contained[gold_field] = None  # not body-intended for this document
                pdf_contained[gold_field] = None
                continue
            _check_one(gold_field, str(data.get(data_key, '')))

        for gold_field in self.NOT_BODY_GATED_FIELDS:
            body_contained[gold_field] = None
            pdf_contained[gold_field] = None

        ground_truth['bodyContained'] = body_contained
        ground_truth['pdfContained'] = pdf_contained
        return hard_failures, advisory_failures

    def _generate_requirement_data(self, req_type: str, authority: Optional[str] = None) -> Dict:
        """Generate complete requirement data.

        Args:
            req_type: Requirement type
            authority: Specific authority code (IMSS, SAT, etc.) or None for random

        Returns:
            Dictionary with all required fields
        """
        # Generate authority (specific or random)
        authority_data = self.data_gen.generate_authority(authority_siglas=authority)

        # Generate person/company being investigated
        persona = self.data_gen.generate_person()

        # Generate public servant (requestor)
        servidor = self.data_gen.generate_person(include_curp=False)

        # Generate recipient (bank official)
        destinatario = self.data_gen.generate_person(include_curp=False)

        # Generate folio and reference numbers. `authority_data['siglas']` is
        # passed through so the folio/oficio prefix is drawn from the SAME
        # authority as the gold `AutoridadSolicitanteNombre` below -- fixes
        # DEFECT A (owner re-verification 2026-07-04): previously the folio
        # prefix and the gold authority were two independent random draws,
        # so a document could self-contradict (e.g. a SAT folio prefix next
        # to an IMSS gold authority).
        folio_siara = self.data_gen.generate_folio_siara(authority_siglas=authority_data['siglas'])
        expediente = self.data_gen.generate_numero_expediente()

        # Generate amounts
        monto = self.data_gen.generate_monto()

        # Get legal framework
        fundamento_articles = self.legal_catalog.get_articles_for_requirement(req_type, count=4)
        fundamento = ", ".join(fundamento_articles)

        facultades = self.legal_catalog.get_facultades(authority_data['siglas'])

        # Get motivation template
        motivacion_template = self.legal_catalog.generate_motivacion_template(req_type)
        motivacion = f"{motivacion_template['intro']} {motivacion_template['accion']} {motivacion_template['objetivo']}"

        # Which `{{Placeholder}}` tokens this req_type's raw template actually
        # embeds (captured BEFORE substitution) -- drives which conditional
        # gold fields are body-intended for THIS document (see
        # CONDITIONAL_BODY_INTENDED_FIELDS / _check_body_containment).
        motivacion_placeholders_used = find_placeholders(motivacion)

        # Get instructions
        instrucciones = self.legal_catalog.generate_instrucciones_cuentas(req_type)

        # Get banking sectors
        sectores = self.legal_catalog.get_random_sectores(count=3)

        # Assemble complete data dictionary
        data = {
            # Identification
            'Cnbv_SolicitudSiara': folio_siara,
            'Cnbv_NumeroOficio': folio_siara,  # SIARA folio — matches what TxtFieldExtractor and DocxFieldExtractor extract from PDF/DOCX text
            'Cnbv_OficioYear': str(datetime.now().year),
            'Cnbv_AreaDescripcion': authority_data['area'],
            'Cnbv_Folio': folio_siara,
            'Cnbv_NumeroExpediente': expediente,
            'Cnbv_FechaPublicacion': datetime.now().strftime("%d/%m/%Y"),
            'Cnbv_DiasPlazo': str(random.randint(3, 10)),

            # Authority
            # Set AutoridadNombre to "Comisión Nacional Bancaria y de Valores" — the
            # CNBV is the receiving regulatory body whose name appears prominently in the
            # document text.  TxtFieldExtractor and DocxFieldExtractor both extract this
            # value via their Priority-2 rule (full-name search), so setting it here in the
            # XML ensures the XML source agrees with the PDF/DOCX OCR sources during
            # multi-source fusion.  Using the requesting authority's internal name instead
            # would create an AutoridadNombre conflict (because the extractors always return
            # "Comisión Nacional Bancaria y de Valores" from any CNBV-addressed letter) and
            # trigger ManualReviewRequired, blocking the §2 export gate.
            'AutoridadNombre': 'Comisión Nacional Bancaria y de Valores',
            'NombreSolicitante': f"{servidor['nombre_completo']}",
            'authority': authority_data,  # Include full authority data for LLM
            'tipo': req_type,  # Include requirement type for LLM

            # The REQUESTING authority's own official name (SAT/IMSS/FGR/etc,
            # from `generate_authority()` -- catalog `nombre`, VARIES per
            # doc). This is the gold `autoridadNombre` field (owner ruling
            # 2026-07-04): the meaningful, discriminative extraction target,
            # as opposed to `AutoridadNombre` above which is hardcoded to the
            # constant CNBV recipient. It also supplies the Motivación
            # template's `{{JuzgadoNombre}}` placeholder for judicial-type
            # docs (see core/text_substitution.py:MOTIVACION_PLACEHOLDER_MAP)
            # -- using "Juzgado" wording for a non-judicial requesting
            # authority (e.g. SAT) is an inherited naming imprecision from
            # the original template text, flagged as a caveat, not hidden.
            # It is ALSO rendered explicitly as an "Autoridad solicitante"
            # line in markdown_exporter/html template/docx_exporter for
            # EVERY requirement type, independent of that placeholder.
            'AutoridadSolicitanteNombre': authority_data['nombre'],
            # Ejercicio exists ONLY to give the Motivación template's
            # `{{Ejercicio}}` placeholder (informacion-type only) a real,
            # non-empty value; reuses the current year (no separate
            # "ejercicio fiscal" concept is modeled by this generator).
            'Ejercicio': str(datetime.now().year),

            # References
            'Referencia': f"REF-{random.randint(1000, 9999)}",
            'Referencia1': f"REF1-{random.randint(1000, 9999)}",
            'Referencia2': f"REF2-{random.randint(1000, 9999)}",

            # Destinatario (bank official)
            'Destinatario_Nombre': destinatario['nombre_completo'],
            'Destinatario_Cargo': "Vicepresidente de Supervisión de Procesos Preventivos",
            'Destinatario_Institucion': "Comisión Nacional Bancaria y de Valores",
            'Destinatario_Direccion': "Insurgentes Sur 1971, Conjunto Plaza Inn, col. Guadalupe Inn,\nDel Alvaro Obregón, C.P. 01020, Ciudad de México",

            # Solicitante
            'UnidadSolicitante': authority_data['area'],
            'DomicilioSolicitante': authority_data['direccion'],
            'ServidorPublico_Nombre': servidor['nombre_completo'],
            'ServidorPublico_Cargo': authority_data['area'],
            'ServidorPublico_Telefono': servidor['telefono'],
            'ServidorPublico_Correo': servidor['correo'],

            # Legal texts
            'FacultadesTexto': facultades,
            'FundamentoTexto': fundamento,
            'MotivacionTexto': motivacion,
            'MontoTexto': f"El monto total es de {monto['cantidad_formatted']} ({monto['letra']}) más los accesorios legales que se generen hasta la fecha de pago.",

            # Motivación details
            'FechaDiligencia': datetime.now().strftime("%d/%m/%Y"),
            'MontoEmbargado': monto['cantidad_formatted'],
            'MontoEnLetra': monto['letra'],

            # Origen
            'TieneAseguramiento': 'Sí' if req_type == 'aseguramiento' else 'No',
            'NoOficioRevision': f"OF-REV-{random.randint(100, 999)}-{datetime.now().year}",
            'MontoCredito': monto['cantidad_formatted'],
            'CreditosFiscales': ' '.join(self.data_gen.generate_creditos_fiscales(5)),
            'Periodos': ' '.join(self.data_gen.generate_periodos(6)),

            # Partes
            'SolicitudPartes_Nombre': persona['nombre_completo'],
            'SolicitudPartes_Caracter': 'Contribuyente' if req_type == 'fiscal' else 'Investigado',

            # Solicitud Especifica
            'InstruccionesCuentasPorConocer': instrucciones,

            # Sectores
            'SectoresBancarios': sectores,

            # Persona
            'Persona_Nombre': persona['nombre_completo'],
            'Persona_Rfc': persona['rfc'],
            'Persona_Caracter': 'Contribuyente' if req_type == 'fiscal' else 'Investigado',
            'Persona_Domicilio': persona['direccion'],
            'Persona_Complementarios': f"Tel: {persona['telefono']}, Email: {persona['correo']}",
        }

        # Substitute the leaking `{{Placeholder}}` tokens in MotivacionTexto
        # with their real values from `data` -- this is what makes fields
        # like Cnbv_NumeroExpediente recoverable from the rendered document
        # body (md/html/docx) instead of being XML-only. Raises loudly
        # (KeyError) if a placeholder can't be mapped/resolved, rather than
        # ever leaving a literal `{{...}}` token in the generated document.
        data['MotivacionTexto'] = substitute_placeholders(
            data['MotivacionTexto'], data, MOTIVACION_PLACEHOLDER_MAP
        )

        # Record which placeholders THIS document's template actually used
        # (pre-substitution) so the ground-truth/containment gate knows which
        # conditional gold fields are body-intended for this specific doc.
        data['_motivacion_placeholders_used'] = motivacion_placeholders_used

        return data


def main():
    """CLI entry point."""
    parser = argparse.ArgumentParser(
        description='Generate CNBV E2E test fixtures',
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument(
        '-c', '--count',
        type=int,
        default=1,
        help='Number of fixtures to generate (default: 1)'
    )

    parser.add_argument(
        '-o', '--output',
        type=str,
        default='output',
        help='Output directory (default: output)'
    )

    parser.add_argument(
        '--chaos',
        choices=['none', 'low', 'medium', 'high'],
        default='medium',
        help='Chaos level for realistic errors (default: medium)'
    )

    parser.add_argument(
        '--types',
        nargs='+',
        choices=['fiscal', 'judicial', 'pld', 'aseguramiento', 'informacion'],
        help='Requirement types to generate (default: all)'
    )

    parser.add_argument(
        '--formats',
        nargs='+',
        choices=['md', 'xml', 'html', 'pdf', 'docx'],
        default=['md', 'xml', 'html', 'pdf', 'docx'],
        help='Output formats (default: all)'
    )

    parser.add_argument(
        '--logo',
        type=str,
        help='Path to logo image file'
    )

    parser.add_argument(
        '--seed',
        type=int,
        help='Random seed for reproducibility'
    )

    parser.add_argument(
        '--authority',
        type=str,
        choices=['IMSS', 'SAT', 'UIF', 'FGR', 'SEIDO', 'PJF', 'INFONAVIT', 'SHCP', 'CONDUSEF'],
        help='Specific authority to generate documents for (default: random)'
    )

    parser.add_argument(
        '--llm',
        action='store_true',
        help='Use LLM (Ollama) for generating legal text variations'
    )

    parser.add_argument(
        '--llm-model',
        type=str,
        default='llama2',
        help='Ollama model to use (default: llama2)'
    )

    parser.add_argument(
        '--llm-url',
        type=str,
        default='http://localhost:11434',
        help='Ollama API URL (default: http://localhost:11434)'
    )

    args = parser.parse_args()

    # Resolve logo path
    logo_path = None
    if args.logo:
        logo_path = Path(args.logo)
    else:
        # Try to find logo in common locations
        possible_logos = [
            Path(__file__).parent / 'LogoMexico.jpg',
            Path(__file__).parent.parent / 'LogoMexico.jpg',
            Path('LogoMexico.jpg'),
        ]
        for path in possible_logos:
            if path.exists():
                logo_path = path
                break

    # Setup LLM config if enabled
    llm_config = None
    if args.llm:
        llm_config = LLMConfig(
            base_url=args.llm_url,
            model=args.llm_model,
            temperature=0.7,
            max_tokens=500
        )

    # Initialize generator
    generator = CNBVFixtureGenerator(
        output_base=Path(args.output),
        logo_path=logo_path,
        chaos_level=args.chaos,
        seed=args.seed,
        use_llm=args.llm,
        llm_config=llm_config
    )

    # Generate fixtures
    generator.generate_batch(
        count=args.count,
        requirement_types=args.types,
        formats=args.formats,
        authority=args.authority
    )


if __name__ == '__main__':
    main()
