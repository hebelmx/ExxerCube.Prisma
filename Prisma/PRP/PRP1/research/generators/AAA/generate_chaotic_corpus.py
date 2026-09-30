#!/usr/bin/env python3
"""
Generate Chaotic CNBV Corpus

Generates realistic SIARA document corpus with real-world chaos:
- 5% without XML
- 30% with significant null data
- 15% PDF-XML mismatches
- 20% missing non-critical fields
- 10% data corruption

This simulates the actual chaos observed in production SIARA requests.
"""

import sys
from pathlib import Path
from typing import List, Optional

# Add parent to path
sys.path.insert(0, str(Path(__file__).parent))

from prp1_generator import (
    CNBVExpediente,
    CNBVPDFGenerator,
    ChaosSimulator,
    ChaosProfile,
    create_cnbv_xml,
    xml_to_pdf,
)


def generate_chaotic_corpus(
    count: int,
    output_dir: Path,
    base_expedientes: Optional[List[CNBVExpediente]] = None,
    chaos_profile: Optional[ChaosProfile] = None,
    seed: Optional[int] = None,
):
    """
    Generate corpus of chaotic CNBV documents.

    Args:
        count: Number of documents to generate
        output_dir: Where to save generated files
        base_expedientes: Base expedientes to apply chaos to
        chaos_profile: Chaos profile to use (default: realistic)
        seed: Random seed for reproducibility
    """
    output_dir.mkdir(parents=True, exist_ok=True)

    # Default chaos profile (realistic SIARA scenarios)
    if chaos_profile is None:
        chaos_profile = ChaosProfile(
            no_xml_probability=0.05,  # 5% no XML
            null_data_probability=0.30,  # 30% null data
            mismatch_probability=0.15,  # 15% PDF-XML mismatch
            missing_fields_probability=0.20,  # 20% missing fields
            corrupted_data_probability=0.10,  # 10% corruption
        )

    # Default base expedientes if not provided
    if not base_expedientes:
        base_expedientes = _create_default_expedientes()

    # Initialize chaos simulator
    simulator = ChaosSimulator(chaos_profile, seed)

    # Initialize PDF generator
    pdf_gen = CNBVPDFGenerator()

    # Statistics
    stats = {
        "total": count,
        "with_xml": 0,
        "without_xml": 0,
        "with_chaos": 0,
        "severity_counts": {0: 0, 1: 0, 2: 0, 3: 0, 4: 0, 5: 0},
    }

    print(f"Generating {count} chaotic CNBV documents...")
    print(f"Output directory: {output_dir}")
    print(f"\nChaos Profile:")
    print(f"  No XML:         {chaos_profile.no_xml_probability*100:.1f}%")
    print(f"  Null Data:      {chaos_profile.null_data_probability*100:.1f}%")
    print(f"  PDF-XML Mismatch: {chaos_profile.mismatch_probability*100:.1f}%")
    print(f"  Missing Fields: {chaos_profile.missing_fields_probability*100:.1f}%")
    print(f"  Corruption:     {chaos_profile.corrupted_data_probability*100:.1f}%")
    print()

    for i in range(count):
        # Pick a random base expediente
        base_idx = i % len(base_expedientes)
        base_exp = base_expedientes[base_idx]

        # Generate scenario
        scenario = simulator.generate_chaotic_scenario()

        # File naming
        doc_id = f"chaotic_{i+1:04d}"
        xml_path = output_dir / f"{doc_id}.xml"
        pdf_path = output_dir / f"{doc_id}.pdf"
        meta_path = output_dir / f"{doc_id}.scenario.txt"

        # Handle XML generation
        if scenario["has_xml"]:
            # Apply chaos to expediente
            chaotic_exp = simulator.apply_all_chaos(base_exp)

            # Generate XML
            create_cnbv_xml(chaotic_exp, xml_path)

            # Generate PDF
            try:
                xml_to_pdf(xml_path, pdf_path)
                stats["with_xml"] += 1
            except Exception as e:
                print(f"  ⚠ Error generating PDF for {doc_id}: {e}")
                continue

        else:
            # No XML - PDF only scenario
            pdf_gen.generate_pdf(base_exp, pdf_path)
            stats["without_xml"] += 1

        # Save scenario metadata
        with open(meta_path, "w", encoding="utf-8") as f:
            f.write(f"Document ID: {doc_id}\n")
            f.write(f"XML Available: {scenario['has_xml']}\n")
            f.write(f"Severity: {scenario['severity']}/5\n")
            f.write(f"Issues: {', '.join(scenario['issues']) if scenario['issues'] else 'CLEAN'}\n")
            f.write(f"\nDetails:\n")
            f.write(f"  Null Data:        {'YES' if scenario['has_null_data'] else 'NO'}\n")
            f.write(f"  PDF-XML Mismatch: {'YES' if scenario['has_mismatch'] else 'NO'}\n")
            f.write(f"  Missing Fields:   {'YES' if scenario['has_missing_fields'] else 'NO'}\n")
            f.write(f"  Data Corruption:  {'YES' if scenario['has_corruption'] else 'NO'}\n")

        # Update stats
        if scenario["issues"]:
            stats["with_chaos"] += 1
        stats["severity_counts"][scenario["severity"]] += 1

        # Progress
        if (i + 1) % 10 == 0:
            print(f"  Generated {i+1}/{count} documents...")

    # Final statistics
    print(f"\n{'='*60}")
    print("Generation Complete")
    print(f"{'='*60}\n")

    print(f"Total Documents:     {stats['total']}")
    print(f"  With XML:          {stats['with_xml']} ({stats['with_xml']/stats['total']*100:.1f}%)")
    print(f"  Without XML:       {stats['without_xml']} ({stats['without_xml']/stats['total']*100:.1f}%)")
    print(f"  With Chaos:        {stats['with_chaos']} ({stats['with_chaos']/stats['total']*100:.1f}%)")
    print()

    print("Severity Distribution:")
    for severity, count_sev in sorted(stats["severity_counts"].items()):
        if count_sev > 0:
            bar = "█" * int(count_sev / stats["total"] * 50)
            print(f"  {severity}/5: {count_sev:3d} {bar} ({count_sev/stats['total']*100:.1f}%)")

    print(f"\nAll files saved to: {output_dir}")


def _create_default_expedientes() -> List[CNBVExpediente]:
    """Create default base expedientes for different authority types."""
    from prp1_generator.cnbv_schema import (
        SolicitudPartes,
        SolicitudEspecifica,
        PersonasSolicitud,
    )

    expedientes = []

    # 1. ASEGURAMIENTO (IMSS)
    expedientes.append(
        CNBVExpediente(
            Cnbv_NumeroOficio="222/AAA/-4444444444/2025",
            Cnbv_NumeroExpediente="A/AS1-1111-222222-AAA",
            Cnbv_SolicitudSiara="AGAFADAFSON2/2025/000084",
            Cnbv_Folio="6789",
            Cnbv_OficioYear="2025",
            Cnbv_AreaClave="3",
            Cnbv_AreaDescripcion="ASEGURAMIENTO",
            Cnbv_FechaPublicacion="2025-06-05",
            Cnbv_DiasPlazo="7",
            AutoridadNombre="SUBDELEGACION 8 SAN ANGEL",
            Referencia2="IMSSCOB/40/01/001283/2025",
            TieneAseguramiento=True,
            SolicitudPartes=SolicitudPartes(
                Nombre="CORPORACION EJEMPLO SA DE CV",
                Caracter="Patrón Determinado",
            ),
            SolicitudEspecifica=SolicitudEspecifica(
                InstruccionesCuentasPorConocer="Test instructions...",
                PersonasSolicitud=PersonasSolicitud(
                    Nombre="CORPORACION EJEMPLO, S.A. DE C.V.",
                    Rfc="CEJ123456ABC",
                    Domicilio="Av. Ejemplo 123 Col. Centro CP 06000",
                ),
            ),
        )
    )

    # 2. HACENDARIO (SAT)
    expedientes.append(
        CNBVExpediente(
            Cnbv_NumeroOficio="333/BBB/-4444444444/2025",
            Cnbv_NumeroExpediente="A/HD1-2222-333333-BBB",
            Cnbv_SolicitudSiara="500-05-07-02-02-2025-00012345",
            Cnbv_Folio="8901",
            Cnbv_OficioYear="2025",
            Cnbv_AreaClave="1",
            Cnbv_AreaDescripcion="HACENDARIO",
            Cnbv_FechaPublicacion="2025-06-10",
            Cnbv_DiasPlazo="10",
            AutoridadNombre="ADMINISTRACION GENERAL DE AUDITORIA FISCAL FEDERAL",
            Referencia2="500-05-07-02-02-2025-12345",
            TieneAseguramiento=False,
            SolicitudPartes=SolicitudPartes(
                Nombre="EMPRESA MUESTRA SA DE CV",
                Caracter="Contribuyente Auditado",
            ),
            SolicitudEspecifica=SolicitudEspecifica(
                InstruccionesCuentasPorConocer="Favor de proporcionar la información...",
                PersonasSolicitud=PersonasSolicitud(
                    Nombre="EMPRESA MUESTRA, S.A. DE C.V.",
                    Rfc="EMS987654XYZ",
                    Domicilio="Calle Muestra 456 Col. Industrial CP 03000",
                ),
            ),
        )
    )

    # 3. JUDICIAL
    expedientes.append(
        CNBVExpediente(
            Cnbv_NumeroOficio="333/CCC/-6666666666/2025",
            Cnbv_NumeroExpediente="A/J1-3333-444444-CCC",
            Cnbv_SolicitudSiara="JUD/2025/000123",
            Cnbv_Folio="1234",
            Cnbv_OficioYear="2025",
            Cnbv_AreaClave="6",
            Cnbv_AreaDescripcion="JUDICIAL",
            Cnbv_FechaPublicacion="2025-06-15",
            Cnbv_DiasPlazo="5",
            AutoridadNombre="JUZGADO PRIMERO DE DISTRITO",
            Referencia2="EXH/123/2025",
            TieneAseguramiento=False,
            SolicitudPartes=SolicitudPartes(
                Nombre="PERSONA FISICA EJEMPLO",
                Caracter="Demandado",
                Persona="Fisica",
            ),
            SolicitudEspecifica=SolicitudEspecifica(
                InstruccionesCuentasPorConocer="Se solicita información bancaria...",
                PersonasSolicitud=PersonasSolicitud(
                    Nombre="PERSONA FISICA EJEMPLO",
                    Rfc="PEJE850123ABC",
                    Domicilio="Domicilio Conocido CP 01000",
                    Persona="Fisica",
                ),
            ),
        )
    )

    # 4. INFORMACION (UIF)
    expedientes.append(
        CNBVExpediente(
            Cnbv_NumeroOficio="555/DDD/-6666666/2025",
            Cnbv_NumeroExpediente="A/I1-4444-555555-DDD",
            Cnbv_SolicitudSiara="UIF/DGACTI/2025/00456",
            Cnbv_Folio="5678",
            Cnbv_OficioYear="2025",
            Cnbv_AreaClave="5",
            Cnbv_AreaDescripcion="INFORMACION",
            Cnbv_FechaPublicacion="2025-06-20",
            Cnbv_DiasPlazo="3",
            AutoridadNombre="UNIDAD DE INTELIGENCIA FINANCIERA",
            Referencia2="UIF-DGACTI-2025-456",
            TieneAseguramiento=False,
            SolicitudPartes=SolicitudPartes(
                Nombre="SOSPECHOSO SA DE CV",
                Caracter="Investigado",
            ),
            SolicitudEspecifica=SolicitudEspecifica(
                InstruccionesCuentasPorConocer="Investigación lavado de dinero...",
                PersonasSolicitud=PersonasSolicitud(
                    Nombre="SOSPECHOSO, S.A. DE C.V.",
                    Rfc="SOS111222ZZZ",
                    Domicilio="Direccion Desconocida",
                ),
            ),
        )
    )

    return expedientes


def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Generate chaotic CNBV document corpus"
    )
    parser.add_argument(
        "--count",
        type=int,
        default=100,
        help="Number of documents to generate (default: 100)",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("generators/AAA/test_output/chaotic_corpus"),
        help="Output directory",
    )
    parser.add_argument(
        "--seed",
        type=int,
        help="Random seed for reproducibility",
    )
    parser.add_argument(
        "--no-xml-prob",
        type=float,
        default=0.05,
        help="Probability of no XML (default: 0.05)",
    )
    parser.add_argument(
        "--null-data-prob",
        type=float,
        default=0.30,
        help="Probability of null data (default: 0.30)",
    )
    parser.add_argument(
        "--mismatch-prob",
        type=float,
        default=0.15,
        help="Probability of PDF-XML mismatch (default: 0.15)",
    )

    args = parser.parse_args()

    # Create custom chaos profile
    chaos_profile = ChaosProfile(
        no_xml_probability=args.no_xml_prob,
        null_data_probability=args.null_data_prob,
        mismatch_probability=args.mismatch_prob,
    )

    # Generate corpus
    generate_chaotic_corpus(
        count=args.count,
        output_dir=args.output,
        chaos_profile=chaos_profile,
        seed=args.seed,
    )

    print("\n✅ Corpus generation complete!")


if __name__ == "__main__":
    main()
