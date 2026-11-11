"""
JSON parsing and extraction utilities.
"""

import json
import re
from typing import Dict, Any, Optional


def parse_json_flexible(text: str) -> Dict[str, Any]:
    """
    Parse JSON from text with fallback strategies.
    
    Tries multiple strategies:
    1. Strict JSON parsing
    2. Extract JSON object from text
    3. Fix common JSON errors
    
    Args:
        text: Text potentially containing JSON
        
    Returns:
        Parsed JSON dictionary
        
    Raises:
        json.JSONDecodeError: If no valid JSON could be extracted
    """
    # Strategy 1: Try strict parsing
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        pass
    
    # Strategy 2: Extract JSON object from text
    json_obj = extract_json_from_text(text)
    if json_obj:
        try:
            return json.loads(json_obj)
        except json.JSONDecodeError:
            pass
    
    # Strategy 3: Fix common issues
    fixed_text = fix_common_json_errors(text)
    if fixed_text != text:
        try:
            return json.loads(fixed_text)
        except json.JSONDecodeError:
            pass
    
    # Last resort: try to extract and fix
    if json_obj:
        fixed_obj = fix_common_json_errors(json_obj)
        try:
            return json.loads(fixed_obj)
        except json.JSONDecodeError:
            pass
    
    raise json.JSONDecodeError("No valid JSON could be decoded", text, 0)


def extract_json_from_text(text: str) -> Optional[str]:
    """
    Extract the first JSON object from text.
    
    Args:
        text: Text containing JSON
        
    Returns:
        JSON string or None if not found
    """
    # Find outermost braces
    start = text.find("{")
    if start == -1:
        return None
    
    # Count braces to find matching closing brace
    brace_count = 0
    in_string = False
    escape_next = False
    
    for i in range(start, len(text)):
        char = text[i]
        
        if escape_next:
            escape_next = False
            continue
            
        if char == '\\':
            escape_next = True
            continue
            
        if char == '"' and not escape_next:
            in_string = not in_string
            continue
            
        if not in_string:
            if char == '{':
                brace_count += 1
            elif char == '}':
                brace_count -= 1
                if brace_count == 0:
                    return text[start:i+1]
    
    return None


def fix_common_json_errors(text: str) -> str:
    """
    Fix common JSON formatting errors.
    
    Args:
        text: Potentially malformed JSON text
        
    Returns:
        Fixed JSON text
    """
    # Replace single quotes with double quotes (careful with apostrophes)
    # Only replace quotes that look like JSON string delimiters
    text = re.sub(r"(?<=[{\[,:]\s)'([^']*)'(?=\s*[,:\]}])", r'"\1"', text)
    text = re.sub(r"(?<=:\s)'([^']*)'(?=\s*[,}])", r'"\1"', text)
    
    # Fix Python None, True, False
    text = text.replace(': None', ': null')
    text = text.replace(': True', ': true')
    text = text.replace(': False', ': false')
    
    # Remove trailing commas
    text = re.sub(r',\s*}', '}', text)
    text = re.sub(r',\s*]', ']', text)
    
    # Fix unquoted keys (simple cases)
    text = re.sub(r'(\w+):', r'"\1":', text)
    
    # Remove comments
    text = re.sub(r'//.*$', '', text, flags=re.MULTILINE)
    text = re.sub(r'/\*.*?\*/', '', text, flags=re.DOTALL)
    
    return text


def validate_json_schema(data: Dict[str, Any], schema: Dict[str, Any]) -> bool:
    """
    Validate JSON data against a simple schema.
    
    Args:
        data: JSON data to validate
        schema: Expected schema
        
    Returns:
        True if valid, False otherwise
    """
    for key, expected_type in schema.items():
        if key not in data:
            return False
        
        if expected_type == "str" and not isinstance(data[key], str):
            return False
        elif expected_type == "int" and not isinstance(data[key], int):
            return False
        elif expected_type == "float" and not isinstance(data[key], (int, float)):
            return False
        elif expected_type == "list" and not isinstance(data[key], list):
            return False
        elif expected_type == "dict" and not isinstance(data[key], dict):
            return False
    
    return True