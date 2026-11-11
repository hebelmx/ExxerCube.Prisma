"""
Document Validator Module - Single Responsibility: Validating extracted documents.
"""

from typing import Dict, Any, Optional, List
from pydantic import BaseModel, ValidationError
from datetime import datetime
import re


class DocumentValidator:
    """
    Responsible ONLY for validating document data.
    Single Responsibility: Data validation and verification.
    """
    
    def __init__(self, schema: Optional[BaseModel] = None):
        self.schema = schema
        self.validation_rules = {}
        self._setup_default_rules()
    
    def _setup_default_rules(self):
        """Setup default validation rules."""
        self.validation_rules = {
            'date': self._validate_date,
            'email': self._validate_email,
            'phone': self._validate_phone,
            'url': self._validate_url,
            'numeric': self._validate_numeric,
            'required': self._validate_required,
            'length': self._validate_length
        }
    
    def validate(
        self,
        data: Dict[str, Any],
        schema: Optional[BaseModel] = None
    ) -> tuple[bool, Dict[str, Any], List[str]]:
        """
        Validate document data against schema.
        
        Args:
            data: Document data to validate
            schema: Pydantic schema (uses default if not provided)
            
        Returns:
            (is_valid, validated_data, errors)
        """
        errors = []
        validated_data = {}
        
        # Use provided schema or default
        validation_schema = schema or self.schema
        
        if validation_schema:
            try:
                validated_instance = validation_schema(**data)
                validated_data = validated_instance.model_dump()
                return True, validated_data, []
            except ValidationError as e:
                errors = [f"{err['loc'][0]}: {err['msg']}" for err in e.errors()]
                return False, data, errors
        
        # If no schema, perform basic validation
        for key, value in data.items():
            field_errors = self._validate_field(key, value)
            if field_errors:
                errors.extend(field_errors)
            else:
                validated_data[key] = value
        
        return len(errors) == 0, validated_data, errors
    
    def validate_required_fields(
        self,
        data: Dict[str, Any],
        required_fields: List[str]
    ) -> tuple[bool, List[str]]:
        """
        Check if required fields are present and non-empty.
        
        Args:
            data: Document data
            required_fields: List of required field names
            
        Returns:
            (is_valid, missing_fields)
        """
        missing = []
        
        for field in required_fields:
            if field not in data:
                missing.append(f"{field} is required")
            elif not data[field] or data[field] == 'unknown':
                missing.append(f"{field} is empty or unknown")
        
        return len(missing) == 0, missing
    
    def _validate_field(self, key: str, value: Any) -> List[str]:
        """Validate a single field based on its name."""
        errors = []
        
        # Check common field patterns
        if 'date' in key.lower() or 'fecha' in key.lower():
            if not self._validate_date(value):
                errors.append(f"Invalid date format for {key}")
        
        elif 'email' in key.lower() or 'correo' in key.lower():
            if not self._validate_email(value):
                errors.append(f"Invalid email format for {key}")
        
        elif 'phone' in key.lower() or 'telefono' in key.lower():
            if not self._validate_phone(value):
                errors.append(f"Invalid phone format for {key}")
        
        return errors
    
    def _validate_date(self, value: Any) -> bool:
        """Validate date format."""
        if not value or value == 'unknown':
            return True  # Allow unknown
        
        date_patterns = [
            r'\d{4}-\d{2}-\d{2}',  # YYYY-MM-DD
            r'\d{2}/\d{2}/\d{4}',  # DD/MM/YYYY
            r'\d{2}-\d{2}-\d{4}',  # DD-MM-YYYY
        ]
        
        str_value = str(value)
        return any(re.match(pattern, str_value) for pattern in date_patterns)
    
    def _validate_email(self, value: Any) -> bool:
        """Validate email format."""
        if not value or value == 'unknown':
            return True
        
        pattern = r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$'
        return bool(re.match(pattern, str(value)))
    
    def _validate_phone(self, value: Any) -> bool:
        """Validate phone number format."""
        if not value or value == 'unknown':
            return True
        
        # Remove common separators
        phone = re.sub(r'[\s\-\(\)]+', '', str(value))
        
        # Check if it's mostly digits
        return len(phone) >= 7 and phone.replace('+', '').isdigit()
    
    def _validate_url(self, value: Any) -> bool:
        """Validate URL format."""
        if not value or value == 'unknown':
            return True
        
        pattern = r'^https?://[^\s]+$'
        return bool(re.match(pattern, str(value)))
    
    def _validate_numeric(self, value: Any) -> bool:
        """Validate numeric value."""
        try:
            float(value)
            return True
        except (ValueError, TypeError):
            return False
    
    def _validate_required(self, value: Any) -> bool:
        """Check if value is present and not empty."""
        return value is not None and value != '' and value != 'unknown'
    
    def _validate_length(self, value: Any, min_len: int = 0, max_len: int = 1000) -> bool:
        """Validate string length."""
        if not value:
            return min_len == 0
        
        str_value = str(value)
        return min_len <= len(str_value) <= max_len
    
    def add_custom_rule(self, name: str, validator_func):
        """
        Add a custom validation rule.
        
        Args:
            name: Rule name
            validator_func: Function that takes value and returns bool
        """
        self.validation_rules[name] = validator_func
    
    def validate_batch(
        self,
        documents: List[Dict[str, Any]]
    ) -> tuple[List[Dict[str, Any]], List[Dict[str, Any]], Dict[str, Any]]:
        """
        Validate multiple documents.
        
        Args:
            documents: List of document data
            
        Returns:
            (valid_docs, invalid_docs, statistics)
        """
        valid = []
        invalid = []
        
        for doc in documents:
            is_valid, validated_data, errors = self.validate(doc)
            
            if is_valid:
                valid.append(validated_data)
            else:
                invalid.append({'data': doc, 'errors': errors})
        
        stats = {
            'total': len(documents),
            'valid': len(valid),
            'invalid': len(invalid),
            'success_rate': len(valid) / len(documents) if documents else 0
        }
        
        return valid, invalid, stats