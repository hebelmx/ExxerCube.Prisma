"""
Text normalization module for cleaning OCR output.
Single Responsibility: Normalize and clean text formatting.
"""
import re
from typing import List, Tuple


def remove_extra_spaces(text: str) -> str:
    """
    Remove multiple consecutive spaces.
    Pure function for space normalization.
    
    Args:
        text: Input text
        
    Returns:
        Text with normalized spaces
    """
    return re.sub(r'\s{2,}', ' ', text)


def fix_punctuation_spacing(text: str) -> str:
    """
    Fix spacing around punctuation marks.
    Pure function for punctuation normalization.
    
    Args:
        text: Input text
        
    Returns:
        Text with corrected punctuation spacing
    """
    # Remove spaces before punctuation
    text = re.sub(r'\s+([,.;:!?])', r'\1', text)
    
    # Add space after punctuation if missing (except at end)
    text = re.sub(r'([,.;:!?])(?=[A-Za-zÀ-ÿ0-9])', r'\1 ', text)
    
    return text


def normalize_line_breaks(text: str) -> str:
    """
    Normalize different types of line breaks.
    Pure function for line break normalization.
    
    Args:
        text: Input text
        
    Returns:
        Text with normalized line breaks
    """
    # Convert different line break styles to \n
    text = re.sub(r'\r\n|\r', '\n', text)
    
    # Remove excessive line breaks
    text = re.sub(r'\n{3,}', '\n\n', text)
    
    return text


def capitalize_sentences(text: str) -> str:
    """
    Capitalize first letter of sentences.
    Pure function for sentence capitalization.
    
    Args:
        text: Input text
        
    Returns:
        Text with capitalized sentences
    """
    # Capitalize after sentence endings
    text = re.sub(r'(?<=[.!?]\s)([a-z])', lambda m: m.group(1).upper(), text)
    
    # Capitalize first letter if lowercase
    if text and text[0].islower():
        text = text[0].upper() + text[1:]
    
    return text


def normalize_text(text: str, 
                  fix_spaces: bool = True,
                  fix_punctuation: bool = True,
                  fix_line_breaks: bool = True,
                  capitalize: bool = False) -> str:
    """
    Normalize OCR text output with configurable options.
    Pure function: same input produces same output.
    
    Args:
        text: Input text to normalize
        fix_spaces: Remove extra spaces
        fix_punctuation: Fix punctuation spacing
        fix_line_breaks: Normalize line breaks
        capitalize: Capitalize sentences
        
    Returns:
        Normalized text
    """
    if not text:
        return ""
    
    # Apply normalizations in order
    if fix_line_breaks:
        text = normalize_line_breaks(text)
    
    if fix_punctuation:
        text = fix_punctuation_spacing(text)
        
    if fix_spaces:
        text = remove_extra_spaces(text)
    
    if capitalize:
        text = capitalize_sentences(text)
    
    return text.strip()


def split_into_lines(text: str) -> List[str]:
    """
    Split text into lines, removing empty lines.
    Pure function for text splitting.
    
    Args:
        text: Input text
        
    Returns:
        List of non-empty lines
    """
    lines = text.split('\n')
    return [line.strip() for line in lines if line.strip()]


def join_hyphenated_words(text: str) -> str:
    """
    Join words that were split with hyphens at line endings.
    Pure function for hyphenation fixing.
    
    Args:
        text: Input text
        
    Returns:
        Text with joined hyphenated words
    """
    # Join words split across lines with hyphen
    text = re.sub(r'(\w+)-\n(\w+)', r'\1\2', text)
    return text