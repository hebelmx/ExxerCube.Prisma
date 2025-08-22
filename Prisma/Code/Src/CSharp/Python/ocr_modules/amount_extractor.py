"""
Amount extraction module for monetary values.
Single Responsibility: Extract and parse monetary amounts from text.
"""
import re
from typing import List, Tuple, Optional
from decimal import Decimal, InvalidOperation
from .models import AmountData


def normalize_number_format(number_str: str) -> str:
    """
    Normalize number string by removing thousand separators.
    Pure function for number normalization.
    
    Args:
        number_str: Number string with possible separators
        
    Returns:
        Normalized number string
    """
    # Remove spaces and common thousand separators
    return number_str.replace(" ", "").replace(",", "").replace(".", "")


def parse_amount_value(whole_part: str, decimal_part: Optional[str] = None) -> Optional[float]:
    """
    Parse amount value from whole and decimal parts.
    Pure function for amount parsing.
    
    Args:
        whole_part: Whole number part
        decimal_part: Optional decimal part
        
    Returns:
        Parsed amount as float or None if invalid
    """
    try:
        whole = normalize_number_format(whole_part)
        whole_num = int(whole)
        
        if decimal_part:
            decimal_num = int(decimal_part)
            # Ensure decimal part is 2 digits
            if len(decimal_part) == 1:
                decimal_num *= 10
            return float(f"{whole_num}.{decimal_num:02d}")
        else:
            return float(whole_num)
    except (ValueError, InvalidOperation):
        return None


def extract_currency_amounts(text: str, currency_symbol: str = "$") -> List[Tuple[str, str, Optional[str]]]:
    """
    Extract amounts with specific currency symbol.
    Pure function for currency extraction.
    
    Args:
        text: Input text
        currency_symbol: Currency symbol to look for
        
    Returns:
        List of tuples (original_text, whole_part, decimal_part)
    """
    # Escape currency symbol for regex
    escaped_symbol = re.escape(currency_symbol)
    
    # Pattern for amounts with optional thousand separators and decimals
    pattern = rf'{escaped_symbol}\s*([0-9]{{1,3}}(?:[.,\s][0-9]{{3}})*|[0-9]+)(?:[.,]([0-9]{{1,2}}))?'
    
    amounts = []
    for match in re.finditer(pattern, text):
        original = match.group(0)
        whole = match.group(1)
        decimal = match.group(2) if match.group(2) else None
        amounts.append((original, whole, decimal))
    
    return amounts


def extract_keyword_amounts(text: str, keywords: List[str]) -> List[Tuple[str, str, Optional[str]]]:
    """
    Extract amounts preceded by specific keywords.
    Pure function for keyword-based extraction.
    
    Args:
        text: Input text
        keywords: List of keywords that precede amounts
        
    Returns:
        List of tuples (original_text, whole_part, decimal_part)
    """
    amounts = []
    keywords_pattern = '|'.join(re.escape(kw) for kw in keywords)
    
    # Pattern for keyword followed by amount
    pattern = rf'({keywords_pattern})\s*:?\s*([0-9]{{1,3}}(?:[.,\s][0-9]{{3}})*|[0-9]+)(?:[.,]([0-9]{{1,2}}))?'
    
    for match in re.finditer(pattern, text, re.IGNORECASE):
        original = match.group(0)
        whole = match.group(2)
        decimal = match.group(3) if match.group(3) else None
        amounts.append((original, whole, decimal))
    
    return amounts


def create_amount_data(value: float, currency: str, original_text: str) -> AmountData:
    """
    Create AmountData object with validation.
    Pure function for data creation.
    
    Args:
        value: Numeric amount value
        currency: Currency code
        original_text: Original text representation
        
    Returns:
        AmountData object
    """
    return AmountData(
        currency=currency.upper(),
        value=value,
        original_text=original_text
    )


def deduplicate_amounts(amounts: List[AmountData]) -> List[AmountData]:
    """
    Remove duplicate amounts based on value and currency.
    Pure function for deduplication.
    
    Args:
        amounts: List of AmountData objects
        
    Returns:
        List of unique amounts
    """
    seen = set()
    unique = []
    
    for amount in amounts:
        key = (amount.currency, amount.value)
        if key not in seen:
            seen.add(key)
            unique.append(amount)
    
    return unique


def extract_amounts(text: str,
                   currencies: List[Tuple[str, str]] = [("$", "MXN"), ("USD", "USD"), ("€", "EUR")],
                   amount_keywords: List[str] = ["monto", "importe", "cantidad", "total"],
                   min_value: float = 0.0,
                   max_value: Optional[float] = None) -> List[AmountData]:
    """
    Extract monetary amounts from text with multiple strategies.
    Pure function: deterministic amount extraction.
    
    Args:
        text: Input text
        currencies: List of (symbol/code, currency_code) tuples
        amount_keywords: Keywords that indicate amounts
        min_value: Minimum amount to include
        max_value: Maximum amount to include (None for no limit)
        
    Returns:
        List of extracted amounts
    """
    all_amounts = []
    
    # Extract currency symbol amounts
    for symbol, currency_code in currencies:
        symbol_amounts = extract_currency_amounts(text, symbol)
        
        for original, whole, decimal in symbol_amounts:
            value = parse_amount_value(whole, decimal)
            if value is not None:
                all_amounts.append(create_amount_data(value, currency_code, original))
    
    # Extract keyword-based amounts (assume default currency)
    if amount_keywords:
        keyword_amounts = extract_keyword_amounts(text, amount_keywords)
        default_currency = currencies[0][1] if currencies else "MXN"
        
        for original, whole, decimal in keyword_amounts:
            value = parse_amount_value(whole, decimal)
            if value is not None:
                all_amounts.append(create_amount_data(value, default_currency, original))
    
    # Filter by value range
    filtered = []
    for amount in all_amounts:
        if amount.value >= min_value:
            if max_value is None or amount.value <= max_value:
                filtered.append(amount)
    
    # Remove duplicates
    return deduplicate_amounts(filtered)