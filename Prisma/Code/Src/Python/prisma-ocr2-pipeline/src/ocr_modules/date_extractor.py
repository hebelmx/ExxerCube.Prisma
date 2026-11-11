"""
Date extraction and normalization module.
Single Responsibility: Extract and normalize Spanish date formats.
"""
import re
from typing import List, Dict, Optional, Tuple
from unidecode import unidecode


# Spanish month names mapping
SPANISH_MONTHS: Dict[str, str] = {
    "enero": "01",
    "febrero": "02", 
    "marzo": "03",
    "abril": "04",
    "mayo": "05",
    "junio": "06",
    "julio": "07",
    "agosto": "08",
    "septiembre": "09",
    "setiembre": "09",  # Alternative spelling
    "octubre": "10",
    "noviembre": "11",
    "diciembre": "12"
}


def normalize_month_name(month_name: str) -> Optional[str]:
    """
    Convert Spanish month name to month number.
    Pure function for month conversion.
    
    Args:
        month_name: Spanish month name
        
    Returns:
        Two-digit month number or None if not recognized
    """
    normalized = unidecode(month_name.lower().strip())
    return SPANISH_MONTHS.get(normalized)


def parse_spanish_date(day: str, month_name: str, year: str) -> Optional[str]:
    """
    Parse Spanish date format "DD de MONTH de YYYY".
    Pure function for date parsing.
    
    Args:
        day: Day string
        month_name: Spanish month name
        year: Year string
        
    Returns:
        ISO format date (YYYY-MM-DD) or None if invalid
    """
    month_num = normalize_month_name(month_name)
    if not month_num:
        return None
    
    try:
        day_int = int(day)
        year_int = int(year)
        
        # Basic validation
        if not (1 <= day_int <= 31 and 1900 <= year_int <= 2100):
            return None
            
        return f"{year_int:04d}-{month_num}-{day_int:02d}"
    except ValueError:
        return None


def extract_spanish_dates(text: str) -> List[Tuple[str, str]]:
    """
    Extract dates in Spanish format from text.
    Pure function for date extraction.
    
    Args:
        text: Input text
        
    Returns:
        List of tuples (original_text, normalized_date)
    """
    # Pattern for "DD de MONTH de YYYY"
    pattern = r'(\d{1,2})\s+de\s+([A-Za-zñÑáéíóúÁÉÍÓÚ]+)\s+de\s+(\d{4})'
    dates = []
    
    for match in re.finditer(pattern, text, re.IGNORECASE):
        original = match.group(0)
        day, month_name, year = match.groups()
        
        normalized = parse_spanish_date(day, month_name, year)
        if normalized:
            dates.append((original, normalized))
    
    return dates


def extract_iso_dates(text: str) -> List[Tuple[str, str]]:
    """
    Extract dates already in ISO or similar formats.
    Pure function for ISO date extraction.
    
    Args:
        text: Input text
        
    Returns:
        List of tuples (original_text, normalized_date)
    """
    dates = []
    
    # Pattern for YYYY-MM-DD or YYYY/MM/DD
    pattern1 = r'(\d{4})[-/](\d{1,2})[-/](\d{1,2})'
    for match in re.finditer(pattern1, text):
        original = match.group(0)
        year, month, day = match.groups()
        try:
            normalized = f"{int(year):04d}-{int(month):02d}-{int(day):02d}"
            dates.append((original, normalized))
        except ValueError:
            pass
    
    # Pattern for DD-MM-YYYY or DD/MM/YYYY
    pattern2 = r'(\d{1,2})[-/](\d{1,2})[-/](\d{4})'
    for match in re.finditer(pattern2, text):
        original = match.group(0)
        day, month, year = match.groups()
        try:
            normalized = f"{int(year):04d}-{int(month):02d}-{int(day):02d}"
            dates.append((original, normalized))
        except ValueError:
            pass
    
    return dates


def deduplicate_dates(dates: List[str]) -> List[str]:
    """
    Remove duplicate dates while preserving order.
    Pure function for deduplication.
    
    Args:
        dates: List of date strings
        
    Returns:
        List of unique dates in original order
    """
    seen = set()
    unique = []
    for date in dates:
        if date not in seen:
            seen.add(date)
            unique.append(date)
    return unique


def extract_dates(text: str, 
                 extract_spanish: bool = True,
                 extract_iso: bool = True,
                 return_original: bool = False) -> List[str]:
    """
    Extract and normalize dates from text.
    Pure function: deterministic date extraction.
    
    Args:
        text: Input text
        extract_spanish: Extract Spanish format dates
        extract_iso: Extract ISO format dates
        return_original: Return original text instead of normalized
        
    Returns:
        List of date strings (normalized or original)
    """
    all_dates = []
    
    if extract_spanish:
        spanish_dates = extract_spanish_dates(text)
        if return_original:
            all_dates.extend([orig for orig, _ in spanish_dates])
        else:
            all_dates.extend([norm for _, norm in spanish_dates])
    
    if extract_iso:
        iso_dates = extract_iso_dates(text)
        if return_original:
            all_dates.extend([orig for orig, _ in iso_dates])
        else:
            all_dates.extend([norm for _, norm in iso_dates])
    
    # Remove duplicates and sort
    unique_dates = deduplicate_dates(all_dates)
    return sorted(unique_dates)