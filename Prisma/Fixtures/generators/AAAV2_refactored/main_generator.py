"""Main orchestrator for CNBV E2E Fixture Generation."""

import argparse
import random
from datetime import datetime
from pathlib import Path
from typing import List, Dict, Optional

# Import core modules
from core.data_generator import MexicanDataGenerator
from core.legal_catalog import LegalArticleCatalog
from core.chaos_simulator import RealisticChaosSimulator
from core.llm_client import OllamaClient, LegalTextGenerator, LLMConfig
from core.variation_engine import VariationEngine, DocumentPersona, NarrativeStyle

# Import exporters
from exporters.html_exporter import HTMLExporter
from exporters.pdf_exporter import PDFExporter
from exporters.docx_exporter import DOCXExporter
from exporters.markdown_exporter import MarkdownExporter
from exporters.xml_exporter import XMLExporter


class CNBVFixtureGenerator:
    """Main orchestrator for generating CNBV requirement fixtures."""

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

        return output_dir

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

        # Generate folio and reference numbers
        folio_siara = self.data_gen.generate_folio_siara()
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

        # Get instructions
        instrucciones = self.legal_catalog.generate_instrucciones_cuentas(req_type)

        # Get banking sectors
        sectores = self.legal_catalog.get_random_sectores(count=3)

        # Assemble complete data dictionary
        data = {
            # Identification
            'Cnbv_SolicitudSiara': folio_siara,
            'Cnbv_NumeroOficio': f"OF-{random.randint(1000, 9999)}-{datetime.now().year}",
            'Cnbv_OficioYear': str(datetime.now().year),
            'Cnbv_AreaDescripcion': authority_data['area'],
            'Cnbv_Folio': folio_siara,
            'Cnbv_NumeroExpediente': expediente,
            'Cnbv_FechaPublicacion': datetime.now().strftime("%d/%m/%Y"),
            'Cnbv_DiasPlazo': str(random.randint(3, 10)),

            # Authority
            'AutoridadNombre': authority_data['nombre'],
            'NombreSolicitante': f"{servidor['nombre_completo']}",
            'authority': authority_data,  # Include full authority data for LLM
            'tipo': req_type,  # Include requirement type for LLM

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
