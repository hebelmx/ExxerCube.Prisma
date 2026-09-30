"""
Expediente (case file) extraction module.
Single Responsibility: Extract case file identifiers from text.
"""
import re
import argparse
import sys
from typing import Optional, List, Tuple
from pathlib import Path


def normalize_expediente_format(expediente: str) -> str:
    """
    Normalize expediente format for consistency.
    Pure function for format normalization.
    
    Args:
        expediente: Raw expediente string
        
    Returns:
        Normalized expediente string
    """
    # Remove extra spaces
    expediente = re.sub(r'\s+', ' ', expediente.strip())
    
    # Normalize slashes
    expediente = re.sub(r'\s*/\s*', '/', expediente)
    
    # Normalize dashes
    expediente = re.sub(r'\s*-\s*', '-', expediente)
    
    return expediente.upper()


def extract_expediente_patterns(text: str) -> List[Tuple[str, str]]:
    """
    Extract expediente numbers using various patterns.
    Pure function for pattern matching.
    
    Args:
        text: Input text
        
    Returns:
        List of tuples (full_match, expediente_value)
    """
    patterns = [
        # Pattern 1: "Expediente: ABC123/2023"
        r'\b(?:expediente|exp\.?|exped\.?)\s*[:#]?\s*([A-Za-z0-9\-\/\.]+)',
        
        # Pattern 2: "No. Expediente ABC-123-2023"
        r'\b(?:no\.?\s*)?expediente\s+([A-Za-z0-9\-\/\.]+)',
        
        # Pattern 3: "Exp. No. 12345/23"
        r'\bexp\.?\s+no\.?\s*([A-Za-z0-9\-\/\.]+)',
        
        # Pattern 4: "EXPEDIENTE NUM. ABC/123/2023"
        r'\bexpediente\s+num\.?\s*([A-Za-z0-9\-\/\.]+)',
    ]
    
    matches = []
    for pattern in patterns:
        for match in re.finditer(pattern, text, re.IGNORECASE):
            full_match = match.group(0)
            expediente = match.group(1)
            matches.append((full_match, expediente))
    
    return matches


def validate_expediente(expediente: str) -> bool:
    """
    Validate if extracted string is likely a valid expediente.
    Pure function for validation.
    
    Args:
        expediente: Expediente string to validate
        
    Returns:
        True if likely valid
    """
    # Remove spaces for validation
    clean = expediente.replace(" ", "")
    
    # Too short or too long
    if len(clean) < 3 or len(clean) > 50:
        return False
    
    # Must contain at least one alphanumeric character
    if not re.search(r'[A-Za-z0-9]', clean):
        return False
    
    # Check for common false positives
    false_positives = ['de', 'del', 'la', 'el', 'en', 'por', 'para']
    if clean.lower() in false_positives:
        return False
    
    return True


def score_expediente(expediente: str, context: str) -> float:
    """
    Score expediente likelihood based on format and context.
    Pure function for scoring.
    
    Args:
        expediente: Expediente string
        context: Surrounding context text
        
    Returns:
        Score between 0 and 1
    """
    score = 0.5  # Base score
    
    # Contains numbers - more likely
    if re.search(r'\d', expediente):
        score += 0.2
    
    # Contains year pattern (4 digits)
    if re.search(r'\b\d{4}\b', expediente):
        score += 0.1
    
    # Contains slash or dash separators
    if '/' in expediente or '-' in expediente:
        score += 0.1
    
    # Context contains formal keywords
    formal_keywords = ['tribunal', 'juzgado', 'corte', 'judicial', 'legal']
    context_lower = context.lower()
    if any(keyword in context_lower for keyword in formal_keywords):
        score += 0.1
    
    return min(score, 1.0)


def extract_expediente(text: str, 
                      min_score: float = 0.6,
                      return_context: bool = False) -> Optional[str]:
    """
    Extract the most likely expediente from text.
    Pure function: deterministic extraction.
    
    Args:
        text: Input text
        min_score: Minimum score threshold
        return_context: Return surrounding context
        
    Returns:
        Extracted expediente or None
    """
    if not text:
        return None
    
    # Extract all potential matches
    matches = extract_expediente_patterns(text)
    
    if not matches:
        return None
    
    # Score and filter matches
    scored_matches = []
    for full_match, expediente in matches:
        if not validate_expediente(expediente):
            continue
        
        # Get context (50 chars before and after)
        match_pos = text.find(full_match)
        context_start = max(0, match_pos - 50)
        context_end = min(len(text), match_pos + len(full_match) + 50)
        context = text[context_start:context_end]
        
        score = score_expediente(expediente, context)
        if score >= min_score:
            normalized = normalize_expediente_format(expediente)
            scored_matches.append((score, normalized, context))
    
    if not scored_matches:
        return None
    
    # Return highest scoring match
    scored_matches.sort(reverse=True)
    best_score, best_expediente, best_context = scored_matches[0]
    
    if return_context:
        return f"{best_expediente} [Context: {best_context}]"
    else:
        return best_expediente


def main():
    """
    Command-line interface for expediente extraction.
    """
    parser = argparse.ArgumentParser(description='Extract expediente from text file')
    parser.add_argument('--input', required=True, help='Input text file path')
    parser.add_argument('--output', required=True, help='Output directory path')
    
    args = parser.parse_args()
    
    try:
        # Read input file
        with open(args.input, 'r', encoding='utf-8') as f:
            text = f.read()
        
        # Extract expediente
        expediente = extract_expediente(text)
        
        # Create output directory
        output_dir = Path(args.output)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Write result
        output_file = output_dir / "expediente.txt"
        if expediente:
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(expediente)
            print(f"Expediente extracted: {expediente}")
        else:
            # Create empty file to indicate no expediente found
            output_file.touch()
            print("No expediente found")
            
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()