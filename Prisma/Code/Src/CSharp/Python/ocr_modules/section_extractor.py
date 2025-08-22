"""
Section extraction module for finding specific document sections.
Single Responsibility: Extract labeled sections from text.
"""
from typing import List, Optional, Tuple
from unidecode import unidecode


def normalize_for_matching(text: str) -> str:
    """
    Normalize text for case-insensitive, accent-insensitive matching.
    Pure function for text normalization.
    
    Args:
        text: Input text
        
    Returns:
        Normalized text for matching
    """
    return unidecode(text.upper())


def find_section_boundaries(normalized_text: str, 
                          start_aliases: List[str], 
                          end_aliases: List[str]) -> Tuple[int, int]:
    """
    Find the start and end positions of a section.
    Pure function for boundary detection.
    
    Args:
        normalized_text: Normalized text to search in
        start_aliases: List of possible section headers
        end_aliases: List of possible next section headers
        
    Returns:
        Tuple of (start_index, end_index), or (-1, -1) if not found
    """
    # Normalize aliases
    norm_starts = [normalize_for_matching(alias) for alias in start_aliases]
    norm_ends = [normalize_for_matching(alias) for alias in end_aliases]
    
    # Find earliest matching start
    start_idx = -1
    for alias in norm_starts:
        idx = normalized_text.find(alias)
        if idx != -1 and (start_idx == -1 or idx < start_idx):
            start_idx = idx
    
    if start_idx == -1:
        return (-1, -1)
    
    # Find next section or end of text
    end_idx = len(normalized_text)
    search_start = start_idx + 1
    
    for alias in norm_ends:
        idx = normalized_text.find(alias, search_start)
        if idx != -1 and idx < end_idx:
            end_idx = idx
    
    return (start_idx, end_idx)


def extract_section_content(original_text: str, start_idx: int, end_idx: int) -> str:
    """
    Extract section content from original text using indices.
    Pure function for content extraction.
    
    Args:
        original_text: Original text with formatting
        start_idx: Start index in normalized text
        end_idx: End index in normalized text
        
    Returns:
        Extracted section content
    """
    return original_text[start_idx:end_idx].strip()


def clean_section_content(content: str, header_aliases: List[str]) -> str:
    """
    Remove the header from section content.
    Pure function for content cleaning.
    
    Args:
        content: Section content including header
        header_aliases: Possible headers to remove
        
    Returns:
        Content without header
    """
    # Try to remove the header if it's at the beginning
    content_upper = content.upper()
    for alias in header_aliases:
        alias_upper = alias.upper()
        if content_upper.startswith(alias_upper):
            # Remove header and any following colons or spaces
            content = content[len(alias):]
            content = content.lstrip(':').strip()
            break
    
    return content


def extract_section(text: str, 
                   start_aliases: List[str], 
                   end_aliases: List[str],
                   include_header: bool = True) -> Optional[str]:
    """
    Extract a section from text based on header aliases.
    Pure function: deterministic section extraction.
    
    Args:
        text: Full document text
        start_aliases: List of possible section headers
        end_aliases: List of possible next section headers
        include_header: Whether to include the header in output
        
    Returns:
        Extracted section text or None if not found
    """
    if not text or not start_aliases:
        return None
    
    # Normalize text for searching
    normalized = normalize_for_matching(text)
    
    # Find section boundaries
    start_idx, end_idx = find_section_boundaries(normalized, start_aliases, end_aliases)
    
    if start_idx == -1:
        return None
    
    # Extract content
    content = extract_section_content(text, start_idx, end_idx)
    
    # Optionally remove header
    if not include_header:
        content = clean_section_content(content, start_aliases)
    
    return content if content else None


def extract_multiple_sections(text: str, 
                            sections: List[Tuple[str, List[str], List[str]]]) -> dict:
    """
    Extract multiple named sections from text.
    Pure function for batch extraction.
    
    Args:
        text: Full document text
        sections: List of (name, start_aliases, end_aliases) tuples
        
    Returns:
        Dictionary mapping section names to extracted content
    """
    results = {}
    
    for name, start_aliases, end_aliases in sections:
        content = extract_section(text, start_aliases, end_aliases, include_header=False)
        results[name] = content
    
    return results