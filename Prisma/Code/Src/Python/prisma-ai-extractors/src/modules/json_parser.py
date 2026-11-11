"""
JSON Parser Module - Single Responsibility: Parsing and cleaning JSON from text.
"""

import json
import re
from typing import Dict, Any, Optional, Union


class JsonParser:
    """
    Responsible ONLY for parsing JSON from various text formats.
    Single Responsibility: JSON extraction and parsing.
    """
    
    def __init__(self):
        self.json_patterns = [
            r'\{[^{}]*\}',  # Simple JSON object
            r'\{(?:[^{}]|(?:\{[^{}]*\}))*\}',  # Nested JSON
            r'```json\s*(.*?)\s*```',  # Markdown code block
            r'```\s*(.*?)\s*```',  # Generic code block
        ]
    
    def parse(self, text: str) -> Dict[str, Any]:
        """
        Parse JSON from text, trying multiple strategies.
        
        Args:
            text: Input text containing JSON
            
        Returns:
            Parsed JSON dictionary
            
        Raises:
            json.JSONDecodeError: If no valid JSON found
        """
        # Strategy 1: Direct parsing
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            pass
        
        # Strategy 2: Extract JSON from text
        extracted = self.extract_json(text)
        if extracted:
            try:
                return json.loads(extracted)
            except json.JSONDecodeError:
                pass
        
        # Strategy 3: Clean and parse
        cleaned = self.clean_json_text(text)
        try:
            return json.loads(cleaned)
        except json.JSONDecodeError:
            pass
        
        # Strategy 4: Flexible parsing with repairs
        return self.parse_flexible(text)
    
    def extract_json(self, text: str) -> Optional[str]:
        """
        Extract JSON substring from text.
        
        Args:
            text: Input text
            
        Returns:
            Extracted JSON string or None
        """
        # Try each pattern
        for pattern in self.json_patterns:
            matches = re.findall(pattern, text, re.DOTALL)
            if matches:
                # Return the longest match (likely most complete)
                return max(matches, key=len)
        
        # Look for JSON-like structure
        start_idx = text.find('{')
        end_idx = text.rfind('}')
        
        if start_idx != -1 and end_idx != -1 and end_idx > start_idx:
            return text[start_idx:end_idx + 1]
        
        return None
    
    def clean_json_text(self, text: str) -> str:
        """
        Clean text to make it valid JSON.
        
        Args:
            text: Input text
            
        Returns:
            Cleaned text
        """
        # Remove markdown formatting
        text = re.sub(r'```(?:json)?\s*', '', text)
        text = re.sub(r'```\s*$', '', text)
        
        # Remove common prefixes
        text = re.sub(r'^.*?(?=\{)', '', text, flags=re.DOTALL)
        
        # Fix common issues
        text = text.replace("'", '"')  # Single to double quotes
        text = re.sub(r'(\w+):', r'"\1":', text)  # Quote unquoted keys
        text = re.sub(r',\s*}', '}', text)  # Remove trailing commas
        text = re.sub(r',\s*]', ']', text)
        
        return text.strip()
    
    def parse_flexible(self, text: str) -> Dict[str, Any]:
        """
        Flexible parsing with automatic repairs.
        
        Args:
            text: Input text
            
        Returns:
            Parsed dictionary
            
        Raises:
            json.JSONDecodeError: If parsing fails
        """
        # Extract potential JSON
        json_str = self.extract_json(text) or text
        
        # Try different cleaning strategies
        strategies = [
            lambda s: s,  # No change
            lambda s: self.clean_json_text(s),
            lambda s: self._fix_quotes(s),
            lambda s: self._fix_structure(s),
            lambda s: self._extract_key_values(s)
        ]
        
        for strategy in strategies:
            try:
                cleaned = strategy(json_str)
                return json.loads(cleaned)
            except (json.JSONDecodeError, ValueError):
                continue
        
        # Last resort: parse as key-value pairs
        return self._fallback_parse(text)
    
    def _fix_quotes(self, text: str) -> str:
        """Fix quote issues in JSON."""
        # Replace single quotes with double quotes
        text = re.sub(r"'([^']*)'", r'"\1"', text)
        
        # Fix unquoted keys
        text = re.sub(r'(\w+):\s*', r'"\1": ', text)
        
        return text
    
    def _fix_structure(self, text: str) -> str:
        """Fix structural issues in JSON."""
        # Ensure it starts and ends with braces
        if not text.startswith('{'):
            text = '{' + text
        if not text.endswith('}'):
            text = text + '}'
        
        # Fix nested structures
        text = re.sub(r'}\s*{', '}, {', text)
        
        return text
    
    def _extract_key_values(self, text: str) -> str:
        """Extract key-value pairs and build JSON."""
        pairs = {}
        
        # Common patterns for key-value extraction
        patterns = [
            r'"?(\w+)"?\s*:\s*"([^"]*)"',
            r'"?(\w+)"?\s*:\s*([^,}\s]+)',
            r'(\w+)\s*=\s*"([^"]*)"',
            r'(\w+)\s*=\s*([^,\s]+)'
        ]
        
        for pattern in patterns:
            matches = re.findall(pattern, text)
            for key, value in matches:
                pairs[key] = value
        
        return json.dumps(pairs)
    
    def _fallback_parse(self, text: str) -> Dict[str, Any]:
        """
        Fallback parsing for completely broken JSON.
        
        Args:
            text: Input text
            
        Returns:
            Dictionary with raw output
        """
        # Return the raw text as a fallback
        return {
            'raw_output': text,
            'parse_error': 'Could not parse as valid JSON'
        }
    
    def validate_json(self, data: Union[str, dict]) -> tuple[bool, Optional[str]]:
        """
        Validate JSON data.
        
        Args:
            data: JSON string or dictionary
            
        Returns:
            (is_valid, error_message)
        """
        try:
            if isinstance(data, str):
                json.loads(data)
            elif isinstance(data, dict):
                json.dumps(data)
            else:
                return False, f"Invalid type: {type(data)}"
            
            return True, None
            
        except (json.JSONDecodeError, TypeError) as e:
            return False, str(e)