"""
Config Manager Module - Single Responsibility: Configuration management.
"""

import json
import os
from pathlib import Path
from typing import Dict, Any, Optional, Union
import yaml


class ConfigManager:
    """
    Responsible ONLY for configuration management.
    Single Responsibility: Loading, validating, and providing configuration.
    """
    
    def __init__(self, config_path: Optional[Union[str, Path]] = None):
        self.config_path = Path(config_path) if config_path else None
        self.config_data: Dict[str, Any] = {}
        self.defaults = self._get_default_config()
        
        if self.config_path and self.config_path.exists():
            self.load_config()
        else:
            self.config_data = self.defaults.copy()
    
    def _get_default_config(self) -> Dict[str, Any]:
        """Get default configuration values."""
        return {
            'extractors': {
                'smolvlm': {
                    'model_id': 'HuggingFaceTB/SmolVLM2-2.2B-Instruct',
                    'max_new_tokens': 512,
                    'temperature': 0.1,
                    'device': 'auto',
                    'dtype': 'auto'
                },
                'got_ocr2': {
                    'model_path': 'ucaslcl/GOT-OCR2_0',
                    'device': 'auto',
                    'dtype': 'auto'
                },
                'paddleocr': {
                    'use_angle_cls': True,
                    'lang': 'es',
                    'use_gpu': True
                },
                'doctr': {
                    'det_arch': 'db_resnet50',
                    'reco_arch': 'crnn_vgg16_bn',
                    'pretrained': True
                }
            },
            'processing': {
                'batch_size': 1,
                'max_image_size': 2048,
                'timeout': 300,
                'retry_attempts': 3
            },
            'validation': {
                'required_fields': ['fecha', 'autoridadEmisora'],
                'allow_unknown': True,
                'strict_mode': False
            },
            'logging': {
                'level': 'INFO',
                'format': '%(asctime)s - %(name)s - %(levelname)s - %(message)s',
                'file': None
            },
            'performance': {
                'enable_monitoring': True,
                'memory_limit_mb': 4096,
                'gpu_memory_fraction': 0.8
            }
        }
    
    def load_config(self, config_path: Optional[Union[str, Path]] = None) -> Dict[str, Any]:
        """
        Load configuration from file.
        
        Args:
            config_path: Path to config file
            
        Returns:
            Loaded configuration
            
        Raises:
            FileNotFoundError: If config file doesn't exist
            ValueError: If config format is invalid
        """
        if config_path:
            self.config_path = Path(config_path)
        
        if not self.config_path or not self.config_path.exists():
            raise FileNotFoundError(f"Config file not found: {self.config_path}")
        
        try:
            with open(self.config_path, 'r', encoding='utf-8') as f:
                if self.config_path.suffix.lower() in ['.yaml', '.yml']:
                    config_data = yaml.safe_load(f)
                elif self.config_path.suffix.lower() == '.json':
                    config_data = json.load(f)
                else:
                    raise ValueError(f"Unsupported config format: {self.config_path.suffix}")
            
            # Merge with defaults
            self.config_data = self._merge_configs(self.defaults, config_data)
            
            return self.config_data
            
        except (json.JSONDecodeError, yaml.YAMLError) as e:
            raise ValueError(f"Invalid config format in {self.config_path}: {e}")
    
    def save_config(self, config_path: Optional[Union[str, Path]] = None) -> None:
        """
        Save current configuration to file.
        
        Args:
            config_path: Path to save config (uses current path if not provided)
        """
        save_path = Path(config_path) if config_path else self.config_path
        
        if not save_path:
            raise ValueError("No config path specified")
        
        save_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(save_path, 'w', encoding='utf-8') as f:
            if save_path.suffix.lower() in ['.yaml', '.yml']:
                yaml.dump(self.config_data, f, default_flow_style=False, indent=2)
            else:
                json.dump(self.config_data, f, indent=2, ensure_ascii=False)
    
    def get(self, key: str, default: Any = None) -> Any:
        """
        Get configuration value using dot notation.
        
        Args:
            key: Configuration key (supports dot notation)
            default: Default value if key not found
            
        Returns:
            Configuration value
        """
        keys = key.split('.')
        value = self.config_data
        
        for k in keys:
            if isinstance(value, dict) and k in value:
                value = value[k]
            else:
                return default
        
        return value
    
    def set(self, key: str, value: Any) -> None:
        """
        Set configuration value using dot notation.
        
        Args:
            key: Configuration key (supports dot notation)
            value: Value to set
        """
        keys = key.split('.')
        config = self.config_data
        
        # Navigate to parent
        for k in keys[:-1]:
            if k not in config:
                config[k] = {}
            config = config[k]
        
        # Set value
        config[keys[-1]] = value
    
    def get_extractor_config(self, extractor_name: str) -> Dict[str, Any]:
        """
        Get configuration for a specific extractor.
        
        Args:
            extractor_name: Name of the extractor
            
        Returns:
            Extractor configuration
        """
        return self.get(f'extractors.{extractor_name}', {})
    
    def get_processing_config(self) -> Dict[str, Any]:
        """Get processing configuration."""
        return self.get('processing', {})
    
    def get_validation_config(self) -> Dict[str, Any]:
        """Get validation configuration."""
        return self.get('validation', {})
    
    def _merge_configs(self, default: Dict[str, Any], override: Dict[str, Any]) -> Dict[str, Any]:
        """
        Recursively merge two configuration dictionaries.
        
        Args:
            default: Default configuration
            override: Override configuration
            
        Returns:
            Merged configuration
        """
        result = default.copy()
        
        for key, value in override.items():
            if key in result and isinstance(result[key], dict) and isinstance(value, dict):
                result[key] = self._merge_configs(result[key], value)
            else:
                result[key] = value
        
        return result
    
    def validate_config(self) -> tuple[bool, list]:
        """
        Validate current configuration.
        
        Returns:
            (is_valid, errors)
        """
        errors = []
        
        # Check required sections
        required_sections = ['extractors', 'processing', 'validation']
        for section in required_sections:
            if section not in self.config_data:
                errors.append(f"Missing required section: {section}")
        
        # Validate extractor configs
        extractors = self.config_data.get('extractors', {})
        for extractor_name, extractor_config in extractors.items():
            if not isinstance(extractor_config, dict):
                errors.append(f"Invalid config for extractor '{extractor_name}': must be dict")
        
        # Validate processing config
        processing = self.config_data.get('processing', {})
        if 'batch_size' in processing:
            if not isinstance(processing['batch_size'], int) or processing['batch_size'] <= 0:
                errors.append("processing.batch_size must be a positive integer")
        
        if 'timeout' in processing:
            if not isinstance(processing['timeout'], (int, float)) or processing['timeout'] <= 0:
                errors.append("processing.timeout must be a positive number")
        
        return len(errors) == 0, errors
    
    def load_from_env(self, prefix: str = 'PRISMA_') -> None:
        """
        Load configuration from environment variables.
        
        Args:
            prefix: Environment variable prefix
        """
        for key, value in os.environ.items():
            if key.startswith(prefix):
                config_key = key[len(prefix):].lower().replace('_', '.')
                
                # Try to parse as JSON, fallback to string
                try:
                    parsed_value = json.loads(value)
                except json.JSONDecodeError:
                    parsed_value = value
                
                self.set(config_key, parsed_value)
    
    def to_dict(self) -> Dict[str, Any]:
        """Get configuration as dictionary."""
        return self.config_data.copy()