"""
Chaos Simulator - Real-World SIARA Scenarios

Simulates the messy reality of SIARA:
- 5% of requests come without XML
- High percentage have XML with null/empty data
- PDF and XML data don't always match
- Random fields missing or corrupted
"""

from __future__ import annotations

import random
from dataclasses import dataclass
from typing import Optional

from .cnbv_schema import CNBVExpediente, PersonasSolicitud, SolicitudEspecifica, SolicitudPartes


@dataclass
class ChaosProfile:
    """Defines how much chaos to inject."""
    no_xml_probability: float = 0.05  # 5% no XML at all
    null_data_probability: float = 0.30  # 30% have significant null data
    mismatch_probability: float = 0.15  # 15% PDF-XML mismatch
    missing_fields_probability: float = 0.20  # 20% missing non-critical fields
    corrupted_data_probability: float = 0.10  # 10% corrupted/malformed data


class ChaosSimulator:
    """
    Simulates real-world chaos in SIARA documents.

    Based on actual observations:
    - Not all requests include XML
    - XML schema exists but many fields are null/empty
    - PDF content doesn't always match XML
    - Fields are missing or have unexpected values
    """

    def __init__(self, profile: Optional[ChaosProfile] = None, seed: Optional[int] = None):
        """
        Initialize chaos simulator.

        Args:
            profile: Chaos profile defining probabilities
            seed: Random seed for reproducibility
        """
        self.profile = profile or ChaosProfile()
        self.random = random.Random(seed)

    def should_have_xml(self) -> bool:
        """Determine if this request should have XML."""
        return self.random.random() > self.profile.no_xml_probability

    def apply_null_data_chaos(self, expediente: CNBVExpediente) -> CNBVExpediente:
        """
        Apply null/empty data chaos to expediente.

        Randomly nullifies non-critical fields to simulate real-world data quality.
        """
        if self.random.random() > self.profile.null_data_probability:
            return expediente  # No chaos

        # Fields that can be safely nullified
        nullable_fields = [
            "Referencia",
            "Referencia1",
            "NombreSolicitante",
            "AutoridadEspecificaNombre",
        ]

        # Nullify 1-2 random fields
        num_to_null = self.random.randint(1, 2)
        fields_to_null = self.random.sample(nullable_fields, min(num_to_null, len(nullable_fields)))

        for field in fields_to_null:
            if field == "Referencia":
                expediente.Referencia = "                         "  # Keep spaces
            elif field == "Referencia1":
                expediente.Referencia1 = "                         "
            elif field == "NombreSolicitante":
                expediente.NombreSolicitante = None
            elif field == "AutoridadEspecificaNombre":
                expediente.AutoridadEspecificaNombre = None

        # Sometimes nullify RFC (13 spaces)
        if self.random.random() < 0.3:
            expediente.SolicitudPartes.Rfc = "             "

        # Sometimes nullify complementarios
        if self.random.random() < 0.4:
            expediente.SolicitudEspecifica.PersonasSolicitud.Complementarios = ""

        return expediente

    def apply_missing_fields_chaos(self, expediente: CNBVExpediente) -> CNBVExpediente:
        """
        Randomly remove/empty non-critical fields.
        """
        if self.random.random() > self.profile.missing_fields_probability:
            return expediente

        # Randomly empty some fields
        if self.random.random() < 0.5:
            expediente.SolicitudPartes.Paterno = ""
            expediente.SolicitudPartes.Materno = ""

        if self.random.random() < 0.5:
            expediente.SolicitudEspecifica.PersonasSolicitud.Relacion = ""

        return expediente

    def apply_corrupted_data_chaos(self, expediente: CNBVExpediente) -> CNBVExpediente:
        """
        Apply data corruption (realistic errors).
        """
        if self.random.random() > self.profile.corrupted_data_probability:
            return expediente

        # Trailing spaces (common in real data)
        if self.random.random() < 0.5:
            expediente.Cnbv_NumeroOficio += "  "  # Extra spaces

        if self.random.random() < 0.5:
            expediente.Cnbv_NumeroExpediente += "    "  # Extra spaces

        # Excessive padding in Referencia2
        if self.random.random() < 0.3:
            expediente.Referencia2 += " " * self.random.randint(10, 150)

        # Mixed case in names (should be upper but sometimes isn't)
        if self.random.random() < 0.4:
            name = expediente.SolicitudPartes.Nombre
            if name:
                expediente.SolicitudPartes.Nombre = name.title()  # Mixed case

        return expediente

    def create_pdf_xml_mismatch(
        self,
        pdf_expediente: CNBVExpediente,
        xml_expediente: CNBVExpediente
    ) -> tuple[CNBVExpediente, CNBVExpediente]:
        """
        Create intentional mismatches between PDF and XML.

        This simulates the real-world scenario where the PDF content
        doesn't exactly match the XML data.
        """
        if self.random.random() > self.profile.mismatch_probability:
            return pdf_expediente, xml_expediente  # No mismatch

        # Types of mismatches observed in real data:

        # 1. Name variations
        if self.random.random() < 0.5:
            # PDF has full company name, XML has abbreviated
            pdf_name = pdf_expediente.SolicitudEspecifica.PersonasSolicitud.Nombre
            if pdf_name and ", S.A. DE C.V." in pdf_name:
                xml_expediente.SolicitudPartes.Nombre = pdf_name.replace(", S.A. DE C.V.", "")

        # 2. Typo in one but not the other
        if self.random.random() < 0.4:
            # Add typo to PDF version
            pdf_name = pdf_expediente.SolicitudEspecifica.PersonasSolicitud.Nombre
            if pdf_name and len(pdf_name) > 5:
                # Insert random character
                pos = self.random.randint(0, len(pdf_name) - 1)
                pdf_expediente.SolicitudEspecifica.PersonasSolicitud.Nombre = (
                    pdf_name[:pos] + self.random.choice("EAIOU") + pdf_name[pos:]
                )

        # 3. Spacing differences
        if self.random.random() < 0.6:
            # PDF has extra spaces, XML doesn't
            pdf_expediente.AutoridadNombre = pdf_expediente.AutoridadNombre.replace(" ", "  ", 1)

        # 4. Case differences
        if self.random.random() < 0.3:
            xml_expediente.AutoridadNombre = xml_expediente.AutoridadNombre.upper()
            pdf_expediente.AutoridadNombre = pdf_expediente.AutoridadNombre.title()

        # 5. RFC mismatch (common in real data)
        if self.random.random() < 0.5:
            xml_rfc = xml_expediente.SolicitudEspecifica.PersonasSolicitud.Rfc
            if xml_rfc and xml_rfc.strip():
                # PDF might have spaces, XML might not
                pdf_expediente.SolicitudEspecifica.PersonasSolicitud.Rfc = xml_rfc.replace(" ", "")

        return pdf_expediente, xml_expediente

    def apply_all_chaos(self, expediente: CNBVExpediente) -> CNBVExpediente:
        """
        Apply all chaos transformations to an expediente.

        This simulates the cumulative effect of real-world data quality issues.
        """
        # Apply transformations in order
        expediente = self.apply_null_data_chaos(expediente)
        expediente = self.apply_missing_fields_chaos(expediente)
        expediente = self.apply_corrupted_data_chaos(expediente)

        return expediente

    def generate_chaotic_scenario(self) -> dict:
        """
        Generate a realistic chaotic scenario.

        Returns:
            Dictionary describing the scenario
        """
        scenario = {
            "has_xml": self.should_have_xml(),
            "has_null_data": self.random.random() < self.profile.null_data_probability,
            "has_mismatch": self.random.random() < self.profile.mismatch_probability,
            "has_missing_fields": self.random.random() < self.profile.missing_fields_probability,
            "has_corruption": self.random.random() < self.profile.corrupted_data_probability,
        }

        # Add scenario description
        issues = []
        if not scenario["has_xml"]:
            issues.append("NO_XML")
        if scenario["has_null_data"]:
            issues.append("NULL_DATA")
        if scenario["has_mismatch"]:
            issues.append("PDF_XML_MISMATCH")
        if scenario["has_missing_fields"]:
            issues.append("MISSING_FIELDS")
        if scenario["has_corruption"]:
            issues.append("DATA_CORRUPTION")

        scenario["issues"] = issues
        scenario["severity"] = len(issues)

        return scenario


def simulate_real_world_batch(
    count: int,
    base_expedientes: list[CNBVExpediente],
    chaos_profile: Optional[ChaosProfile] = None,
    seed: Optional[int] = None
) -> list[dict]:
    """
    Simulate a batch of real-world SIARA documents with chaos.

    Args:
        count: Number of documents to generate
        base_expedientes: Base expedientes to apply chaos to
        chaos_profile: Chaos profile to use
        seed: Random seed

    Returns:
        List of scenarios with chaotic expedientes
    """
    simulator = ChaosSimulator(chaos_profile, seed)
    results = []

    for i in range(count):
        # Pick a random base expediente
        base_idx = (i % len(base_expedientes)) if base_expedientes else 0
        base_exp = base_expedientes[base_idx] if base_expedientes else CNBVExpediente()

        # Generate scenario
        scenario = simulator.generate_chaotic_scenario()

        # Apply chaos if XML exists
        if scenario["has_xml"]:
            chaotic_exp = simulator.apply_all_chaos(base_exp)
            scenario["expediente"] = chaotic_exp
            scenario["xml_available"] = True
        else:
            scenario["expediente"] = None
            scenario["xml_available"] = False

        results.append(scenario)

    return results


if __name__ == "__main__":
    # Test chaos simulator
    print("Chaos Simulator Test\n")

    # Create test expediente
    test_exp = CNBVExpediente(
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
            Nombre="AEROLINEAS PAYASO ORGULLO NACIONAL",
            Caracter="Patrón Determinado",
        ),
        SolicitudEspecifica=SolicitudEspecifica(
            InstruccionesCuentasPorConocer="Test instructions...",
            PersonasSolicitud=PersonasSolicitud(
                Nombre="AEROLINEAS PAYASO ORGULLO NACIONAL, S.A. DE C.V.",
                Rfc="APON33333444",
                Domicilio="Pza. de la Constitución S/N CP 066V60",
            )
        )
    )

    # Simulate batch
    print("Simulating 10 chaotic scenarios...")
    scenarios = simulate_real_world_batch(10, [test_exp], seed=42)

    # Print summary
    print(f"\nGenerated {len(scenarios)} scenarios:\n")

    for i, scenario in enumerate(scenarios, 1):
        print(f"{i}. Severity: {scenario['severity']}/5")
        print(f"   Issues: {', '.join(scenario['issues']) if scenario['issues'] else 'CLEAN'}")
        print(f"   XML Available: {scenario['xml_available']}")
        print()

    # Statistics
    total_with_xml = sum(1 for s in scenarios if s["xml_available"])
    total_with_issues = sum(1 for s in scenarios if s["issues"])

    print(f"Statistics:")
    print(f"  With XML: {total_with_xml}/{len(scenarios)} ({total_with_xml/len(scenarios)*100:.1f}%)")
    print(f"  With Issues: {total_with_issues}/{len(scenarios)} ({total_with_issues/len(scenarios)*100:.1f}%)")
