"""Introduce realistic imperfections to simulate authentic bureaucratic documents."""

import random
import re
from typing import Dict, Any, List


class RealisticChaosSimulator:
    """Introduce authentic Mexican bureaucratic imperfections."""

    # Mexican-specific typos (missing accents, common mistakes)
    MEXICAN_TYPOS = {
        'á': 'a', 'é': 'e', 'í': 'i', 'ó': 'o', 'ú': 'u',
        'Á': 'A', 'É': 'E', 'Í': 'I', 'Ó': 'O', 'Ú': 'U',
        'ñ': 'n', 'Ñ': 'N',
        'ü': 'u', 'Ü': 'U',
    }

    # Common word typos in legal/bureaucratic Spanish
    WORD_TYPOS = {
        'número': ['numero', 'númer0', 'no.'],
        'México': ['Mexico', 'Méxic0', 'MEXICO'],
        'artículo': ['articulo', 'art.', 'Art.', 'ART.'],
        'crédito': ['credito', 'credito'],
        'información': ['informacion', 'infor mación'],
        'administración': ['administracion', 'admon', 'admin'],
        'federación': ['federacion', 'fed.'],
        'código': ['codigo', 'cödigo'],
        'público': ['publico', 'público'],
        'auditoría': ['auditoria', 'aud.'],
        'ección': ['eccion', 'eción'],
    }

    # Formatting errors
    FORMATTING_ERRORS = [
        'double_space',
        'missing_accent',
        'inconsistent_caps',
        'date_format_mix',
        'number_format_inconsistency',
        'extra_line_break',
    ]

    # Chaos levels with probability ranges
    CHAOS_LEVELS = {
        'none': {'typo_prob': 0.0, 'format_prob': 0.0, 'word_prob': 0.0},
        'low': {'typo_prob': 0.02, 'format_prob': 0.05, 'word_prob': 0.03},
        'medium': {'typo_prob': 0.05, 'format_prob': 0.10, 'word_prob': 0.07},
        'high': {'typo_prob': 0.10, 'format_prob': 0.20, 'word_prob': 0.12},
    }

    def __init__(self, seed: int = None):
        """Initialize chaos simulator.

        Args:
            seed: Random seed for reproducibility
        """
        if seed:
            random.seed(seed)

    def apply_chaos(self, data: Dict[str, Any], level: str = 'medium') -> Dict[str, Any]:
        """Apply controlled chaos to data.

        Args:
            data: Dictionary with document data
            level: Chaos level (none, low, medium, high)

        Returns:
            Modified data dictionary with imperfections
        """
        if level not in self.CHAOS_LEVELS:
            level = 'medium'

        chaos_config = self.CHAOS_LEVELS[level]
        chaotic_data = data.copy()

        # Apply chaos to text fields only
        for key, value in chaotic_data.items():
            if isinstance(value, str) and len(value) > 10:  # Only meaningful text
                # Apply different types of chaos
                value = self._apply_accent_chaos(value, chaos_config['typo_prob'])
                value = self._apply_word_chaos(value, chaos_config['word_prob'])
                value = self._apply_formatting_chaos(value, chaos_config['format_prob'])

                chaotic_data[key] = value

        return chaotic_data

    def _apply_accent_chaos(self, text: str, probability: float) -> str:
        """Remove accents randomly based on probability.

        Args:
            text: Input text
            probability: Probability of removing each accent

        Returns:
            Text with some accents removed
        """
        result = []
        for char in text:
            if char in self.MEXICAN_TYPOS and random.random() < probability:
                result.append(self.MEXICAN_TYPOS[char])
            else:
                result.append(char)

        return ''.join(result)

    def _apply_word_chaos(self, text: str, probability: float) -> str:
        """Replace words with common typos.

        Args:
            text: Input text
            probability: Probability of replacing each word

        Returns:
            Text with some word typos
        """
        for correct, typos in self.WORD_TYPOS.items():
            if correct.lower() in text.lower() and random.random() < probability:
                # Case-insensitive replacement
                pattern = re.compile(re.escape(correct), re.IGNORECASE)
                replacement = random.choice(typos)

                # Match case of original
                def replace_with_case(match):
                    original = match.group(0)
                    if original.isupper():
                        return replacement.upper()
                    elif original[0].isupper():
                        return replacement.capitalize()
                    else:
                        return replacement.lower()

                text = pattern.sub(replace_with_case, text, count=1)

        return text

    def _apply_formatting_chaos(self, text: str, probability: float) -> str:
        """Apply formatting errors.

        Args:
            text: Input text
            probability: Probability of applying each error type

        Returns:
            Text with formatting errors
        """
        if random.random() > probability:
            return text

        error_type = random.choice(self.FORMATTING_ERRORS)

        if error_type == 'double_space':
            # Add random double spaces
            words = text.split(' ')
            if len(words) > 3:
                idx = random.randint(1, len(words) - 2)
                words[idx] = words[idx] + ' '
                text = ' '.join(words)

        elif error_type == 'inconsistent_caps':
            # Randomly change case of some words
            words = text.split(' ')
            if len(words) > 2:
                idx = random.randint(0, len(words) - 1)
                if words[idx].isupper():
                    words[idx] = words[idx].capitalize()
                elif words[idx][0].isupper() and len(words[idx]) > 3:
                    words[idx] = words[idx].upper()
                text = ' '.join(words)

        elif error_type == 'date_format_mix':
            # Mix date formats: DD/MM/YYYY -> DD-MM-YYYY
            text = re.sub(r'(\d{2})/(\d{2})/(\d{4})',
                         lambda m: f"{m.group(1)}-{m.group(2)}-{m.group(3)}"
                         if random.random() < 0.5 else m.group(0),
                         text)

        elif error_type == 'number_format_inconsistency':
            # Remove commas from some numbers
            text = re.sub(r'\$(\d{1,3}),(\d{3})',
                         lambda m: f"${m.group(1)}{m.group(2)}"
                         if random.random() < 0.5 else m.group(0),
                         text)

        return text

    def apply_realistic_errors_to_fields(self, data: Dict[str, Any],
                                        level: str = 'medium') -> Dict[str, Any]:
        """Apply field-specific realistic errors common in Mexican documents.

        Args:
            data: Document data
            level: Chaos level

        Returns:
            Data with realistic field errors
        """
        if level == 'none':
            return data

        chaotic_data = data.copy()

        # RFC errors (wrong check digit, missing digit)
        if 'rfc' in chaotic_data and random.random() < 0.03:
            rfc = chaotic_data['rfc']
            if len(rfc) > 3:
                # Change last character
                chaotic_data['rfc'] = rfc[:-1] + random.choice('ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789')

        # Phone number formatting inconsistency
        if 'telefono' in chaotic_data and random.random() < 0.1:
            # Remove parentheses or dashes randomly
            phone = chaotic_data['telefono']
            phone = phone.replace('(', '').replace(')', '').replace('-', ' ')
            chaotic_data['telefono'] = phone

        # Email domain typo
        if 'correo' in chaotic_data and random.random() < 0.02:
            email = chaotic_data['correo']
            if '@' in email:
                local, domain = email.split('@', 1)
                # Common typos: .com -> .co, .mx -> .mx.mx
                if '.com' in domain and random.random() < 0.5:
                    domain = domain.replace('.com', '.co')
                chaotic_data['correo'] = f"{local}@{domain}"

        # Postal code with space or missing digit
        if 'cp' in str(chaotic_data.get('direccion', '')) and random.random() < 0.05:
            direccion = chaotic_data.get('direccion', '')
            # Add space in CP: 12345 -> 123 45
            direccion = re.sub(r'C\.P\.\s*(\d{2})(\d{3})',
                              r'C.P. \1 \2',
                              direccion)
            chaotic_data['direccion'] = direccion

        return chaotic_data

    def introduce_legal_text_chaos(self, legal_text: str, level: str = 'medium') -> str:
        """Apply chaos specifically to legal/bureaucratic text.

        Args:
            legal_text: Legal text to modify
            level: Chaos level

        Returns:
            Chaotic legal text with realistic errors
        """
        if level == 'none':
            return legal_text

        text = legal_text

        # Apply base chaos
        chaos_config = self.CHAOS_LEVELS[level]
        text = self._apply_accent_chaos(text, chaos_config['typo_prob'])
        text = self._apply_word_chaos(text, chaos_config['word_prob'])
        text = self._apply_formatting_chaos(text, chaos_config['format_prob'])

        # Legal-specific errors
        if random.random() < 0.1:
            # Inconsistent article capitalization
            text = re.sub(r'\bart\.\s+(\d+)',
                         lambda m: f"Art. {m.group(1)}" if random.random() < 0.5 else f"art. {m.group(1)}",
                         text, flags=re.IGNORECASE)

        if random.random() < 0.08:
            # Missing/extra spaces around punctuation
            text = re.sub(r',(\w)', r', \1', text)  # Add space after comma
            text = re.sub(r'\s+,', ',', text)  # Remove space before comma

        return text

    def get_chaos_summary(self, level: str) -> Dict[str, float]:
        """Get chaos configuration for specified level.

        Args:
            level: Chaos level name

        Returns:
            Dictionary with probability settings
        """
        return self.CHAOS_LEVELS.get(level, self.CHAOS_LEVELS['medium'])
