"""Generate realistic Mexican banking data using Faker + custom catalogs."""

import json
import random
import string
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Optional
from faker import Faker


class MexicanDataGenerator:
    """Generate realistic Mexican banking data for CNBV fixtures."""

    def __init__(self, locale: str = 'es_MX', seed: Optional[int] = None,
                 catalogs_dir: Optional[Path] = None):
        """Initialize Mexican data generator.

        Args:
            locale: Faker locale (default: es_MX for Mexican Spanish)
            seed: Random seed for reproducibility
            catalogs_dir: Directory containing JSON catalogs
        """
        self.faker = Faker(locale)
        if seed:
            Faker.seed(seed)
            random.seed(seed)

        # Load authorities catalog
        if catalogs_dir is None:
            catalogs_dir = Path(__file__).parent.parent / 'catalogs'

        self.authorities_catalog = self._load_authorities_catalog(catalogs_dir)

    def _load_authorities_catalog(self, catalogs_dir: Path) -> Dict:
        """Load authorities catalog from JSON file.

        Args:
            catalogs_dir: Directory containing catalogs

        Returns:
            Authorities dictionary
        """
        try:
            catalog_path = catalogs_dir / 'authorities.json'
            with open(catalog_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
                return data.get('authorities', {})
        except Exception as e:
            print(f"Warning: Could not load authorities catalog: {e}")
            return {}

    def generate_rfc_person(self, nombre: str, apellido_paterno: str,
                           apellido_materno: str, fecha_nacimiento: datetime) -> str:
        """Generate RFC (Registro Federal de Contribuyentes) for a person.

        Format: AAAA######XXX
        - 4 letters from name
        - 6 digits from birth date (YYMMDD)
        - 3 random characters
        """
        # Extract initials: First 2 from apellido_paterno, 1 from apellido_materno, 1 from nombre
        rfc = (
            apellido_paterno[:2].upper() +
            apellido_materno[:1].upper() +
            nombre[:1].upper()
        )

        # Add birth date YYMMDD
        rfc += fecha_nacimiento.strftime("%y%m%d")

        # Add homoclave (3 random alphanumeric)
        rfc += ''.join(random.choices(string.ascii_uppercase + string.digits, k=3))

        return rfc

    def generate_rfc_company(self, razon_social: str, fecha_constitucion: datetime) -> str:
        """Generate RFC for a company.

        Format: AAA######XXX
        - 3 letters from company name
        - 6 digits from incorporation date (YYMMDD)
        - 3 random characters
        """
        # Extract 3 meaningful letters from company name
        words = razon_social.upper().split()
        rfc = ''.join(word[0] for word in words[:3] if word).ljust(3, 'X')[:3]

        # Add incorporation date YYMMDD
        rfc += fecha_constitucion.strftime("%y%m%d")

        # Add homoclave
        rfc += ''.join(random.choices(string.ascii_uppercase + string.digits, k=3))

        return rfc

    def generate_curp(self, nombre: str, apellido_paterno: str,
                     apellido_materno: str, fecha_nacimiento: datetime,
                     sexo: str, estado: str) -> str:
        """Generate CURP (Clave Única de Registro de Población).

        Format: AAAA######HMMMMM##
        - 4 letters from name
        - 6 digits from birth date
        - 1 char gender (H/M)
        - 2 chars state code
        - 3 consonants from name
        - 2 random digits
        """
        curp = (
            apellido_paterno[:2].upper() +
            apellido_materno[:1].upper() +
            nombre[:1].upper()
        )

        curp += fecha_nacimiento.strftime("%y%m%d")
        curp += sexo[0].upper()  # H or M
        curp += estado[:2].upper()  # State code

        # Internal consonants
        def get_consonants(text: str) -> str:
            consonants = [c for c in text[1:].upper() if c.isalpha() and c not in 'AEIOU']
            return ''.join(consonants[:3]).ljust(3, 'X')[:3]

        curp += (
            get_consonants(apellido_paterno)[0] +
            get_consonants(apellido_materno)[0] +
            get_consonants(nombre)[0]
        )

        curp += ''.join(random.choices(string.digits, k=2))

        return curp

    def generate_person(self, include_rfc: bool = True, include_curp: bool = True) -> Dict:
        """Generate complete Mexican person data.

        Returns:
            Dictionary with person data including RFC and CURP
        """
        sexo = random.choice(['M', 'H'])
        nombre = self.faker.first_name_male() if sexo == 'H' else self.faker.first_name_female()
        apellido_paterno = self.faker.last_name()
        apellido_materno = self.faker.last_name()
        fecha_nacimiento = self.faker.date_of_birth(minimum_age=18, maximum_age=80)

        # Mexican states codes
        estados = ['AG', 'BC', 'BS', 'CM', 'CS', 'CH', 'CO', 'CL', 'DF', 'DG',
                  'GT', 'GR', 'HG', 'JC', 'MC', 'MN', 'MS', 'NT', 'NL', 'OC',
                  'PL', 'QT', 'QR', 'SP', 'SL', 'SR', 'TC', 'TL', 'TM', 'VZ', 'YN', 'ZS']
        estado = random.choice(estados)

        person = {
            'nombre_completo': f"{nombre} {apellido_paterno} {apellido_materno}",
            'nombre': nombre,
            'apellido_paterno': apellido_paterno,
            'apellido_materno': apellido_materno,
            'fecha_nacimiento': fecha_nacimiento.strftime("%d/%m/%Y"),
            'sexo': 'Masculino' if sexo == 'H' else 'Femenino',
            'telefono': self.faker.phone_number(),
            'correo': self.faker.email(),
            'direccion': self.generate_mexican_address(),
        }

        if include_rfc:
            person['rfc'] = self.generate_rfc_person(nombre, apellido_paterno,
                                                     apellido_materno, fecha_nacimiento)

        if include_curp:
            person['curp'] = self.generate_curp(nombre, apellido_paterno, apellido_materno,
                                               fecha_nacimiento, sexo, estado)

        return person

    def generate_company(self, include_rfc: bool = True) -> Dict:
        """Generate Mexican company data.

        Returns:
            Dictionary with company data including RFC
        """
        razon_social = self.faker.company()
        fecha_constitucion = self.faker.date_between(start_date='-30y', end_date='-1y')

        company = {
            'razon_social': razon_social,
            'fecha_constitucion': fecha_constitucion.strftime("%d/%m/%Y"),
            'telefono': self.faker.phone_number(),
            'correo': self.faker.company_email(),
            'direccion': self.generate_mexican_address(),
        }

        if include_rfc:
            company['rfc'] = self.generate_rfc_company(razon_social, fecha_constitucion)

        return company

    def generate_mexican_address(self) -> str:
        """Generate realistic Mexican address."""
        calle = self.faker.street_name()
        numero = random.randint(1, 9999)
        colonia = f"Col. {self.faker.city()}"
        municipio = self.faker.city()
        estado = self.faker.state()
        cp = f"{random.randint(10000, 99999)}"

        return f"{calle} No. {numero}, {colonia}, Municipio {municipio}, {estado}, C.P. {cp}"

    def generate_authority(self, authority_siglas: Optional[str] = None) -> Dict:
        """Generate Mexican government authority data.

        Args:
            authority_siglas: Specific authority to use (IMSS, SAT, UIF, etc.) or None for random

        Returns:
            Dictionary with authority information (SAT, FGR, UIF, etc.)
        """
        # Use catalog if available, otherwise fallback to hardcoded
        if self.authorities_catalog:
            if authority_siglas:
                # Specific authority requested
                if authority_siglas in self.authorities_catalog:
                    auth_data = self.authorities_catalog[authority_siglas]
                else:
                    # Fallback to random if not found
                    authority_siglas = random.choice(list(self.authorities_catalog.keys()))
                    auth_data = self.authorities_catalog[authority_siglas]
            else:
                # Random authority
                authority_siglas = random.choice(list(self.authorities_catalog.keys()))
                auth_data = self.authorities_catalog[authority_siglas]

            area = random.choice(auth_data['areas'])

            return {
                'nombre': auth_data['nombre'],
                'siglas': auth_data['siglas'],
                'tipo': auth_data['tipo'],
                'area': area,
                'direccion': self.generate_mexican_address(),
                'legal_articles': auth_data.get('legal_articles', []),
                'requirement_types': auth_data.get('requirement_types', []),
            }

        # Fallback to hardcoded authorities if catalog not loaded
        authorities = [
            {
                'nombre': 'Servicio de Administración Tributaria',
                'siglas': 'SAT',
                'tipo': 'fiscal',
                'areas': [
                    'Administración General de Auditoría Fiscal Federal',
                    'Administración Desconcentrada de Auditoría Fiscal',
                    'Administración Local de Auditoría Fiscal',
                ]
            },
            {
                'nombre': 'Fiscalía General de la República',
                'siglas': 'FGR',
                'tipo': 'judicial',
                'areas': [
                    'Fiscalía Especializada en Materia de Delincuencia Organizada',
                    'Subprocuraduría Especializada en Investigación de Delitos Federales',
                ]
            },
            {
                'nombre': 'Unidad de Inteligencia Financiera',
                'siglas': 'UIF',
                'tipo': 'pld',
                'areas': [
                    'Dirección General de Análisis de Operaciones',
                    'Dirección General de Investigación',
                ]
            },
            {
                'nombre': 'Poder Judicial de la Federación',
                'siglas': 'PJF',
                'tipo': 'judicial',
                'areas': [
                    'Juzgado Federal',
                    'Tribunal Colegiado',
                ]
            },
        ]

        authority = random.choice(authorities)
        area = random.choice(authority['areas'])

        return {
            'nombre': authority['nombre'],
            'siglas': authority['siglas'],
            'tipo': authority['tipo'],
            'area': area,
            'direccion': self.generate_mexican_address(),
        }

    def get_available_authorities(self) -> List[str]:
        """Get list of available authority codes.

        Returns:
            List of authority siglas (IMSS, SAT, UIF, etc.)
        """
        return list(self.authorities_catalog.keys())

    def generate_folio_siara(self) -> str:
        """Generate realistic SIARA folio number.

        Format: AUTHORITY/YYYY/######
        Examples: AGAFADAFSON2/2025/000084, UIF/2025/123456
        """
        prefixes = [
            'AGAFADAFSON2',  # SAT Auditoría
            'AGAFF',         # SAT General
            'UIF',           # Unidad de Inteligencia Financiera
            'FGR',           # Fiscalía General
            'SEIDO',         # Subprocuraduría
            'PJF',           # Poder Judicial
        ]

        prefix = random.choice(prefixes)
        year = random.randint(2023, 2025)
        number = random.randint(1, 999999)

        return f"{prefix}/{year}/{number:06d}"

    def generate_numero_expediente(self) -> str:
        """Generate realistic case/file number."""
        return f"EXP-{random.randint(1000, 9999)}-{random.randint(2020, 2025)}"

    def generate_creditos_fiscales(self, count: int = 5) -> List[str]:
        """Generate list of tax credit numbers."""
        return [f"{random.randint(10000000, 99999999)}" for _ in range(count)]

    def generate_periodos(self, count: int = 6) -> List[str]:
        """Generate list of fiscal periods (MM/YYYY format)."""
        start_date = datetime.now() - timedelta(days=365*2)
        periodos = []

        for _ in range(count):
            periodo = start_date + timedelta(days=random.randint(0, 730))
            periodos.append(periodo.strftime("%m/%Y"))

        return sorted(periodos)

    def generate_monto(self, min_amount: float = 100000, max_amount: float = 10000000) -> Dict:
        """Generate monetary amount with text representation.

        Returns:
            Dictionary with 'cantidad' (number) and 'letra' (text)
        """
        cantidad = round(random.uniform(min_amount, max_amount), 2)

        # Simplified number-to-text (Spanish)
        # In production, use a library like num2words
        letra = f"${cantidad:,.2f} MN"

        return {
            'cantidad': cantidad,
            'cantidad_formatted': f"${cantidad:,.2f}",
            'letra': letra,
        }
