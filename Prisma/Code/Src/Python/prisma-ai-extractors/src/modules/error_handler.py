"""
Error Handler Module - Single Responsibility: Error handling and recovery.
"""

import logging
import traceback
from typing import Dict, Any, Optional, Callable, Type
from functools import wraps
from enum import Enum


class ErrorSeverity(Enum):
    """Error severity levels."""
    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"
    CRITICAL = "critical"


class ErrorCategory(Enum):
    """Error categories for better classification."""
    VALIDATION = "validation"
    IO_ERROR = "io_error"
    MODEL_ERROR = "model_error"
    PARSING_ERROR = "parsing_error"
    CONFIG_ERROR = "config_error"
    RESOURCE_ERROR = "resource_error"
    UNKNOWN = "unknown"


class ErrorHandler:
    """
    Responsible ONLY for error handling and recovery strategies.
    Single Responsibility: Error management and recovery.
    """
    
    def __init__(self, logger: Optional[logging.Logger] = None):
        self.logger = logger or logging.getLogger(__name__)
        self.error_handlers: Dict[Type[Exception], Callable] = {}
        self.recovery_strategies: Dict[str, Callable] = {}
        self.error_stats = {
            'total_errors': 0,
            'by_category': {},
            'by_severity': {}
        }
        
        # Setup default error handlers
        self._setup_default_handlers()
    
    def _setup_default_handlers(self):
        """Setup default error handlers."""
        self.register_handler(FileNotFoundError, self._handle_file_not_found)
        self.register_handler(PermissionError, self._handle_permission_error)
        self.register_handler(ValueError, self._handle_value_error)
        self.register_handler(KeyError, self._handle_key_error)
        self.register_handler(ImportError, self._handle_import_error)
        self.register_handler(RuntimeError, self._handle_runtime_error)
    
    def register_handler(self, exception_type: Type[Exception], handler: Callable):
        """
        Register a handler for specific exception type.
        
        Args:
            exception_type: Exception class to handle
            handler: Handler function
        """
        self.error_handlers[exception_type] = handler
    
    def register_recovery_strategy(self, strategy_name: str, strategy: Callable):
        """
        Register a recovery strategy.
        
        Args:
            strategy_name: Name of the strategy
            strategy: Recovery function
        """
        self.recovery_strategies[strategy_name] = strategy
    
    def handle_error(
        self,
        error: Exception,
        context: Optional[Dict[str, Any]] = None,
        severity: ErrorSeverity = ErrorSeverity.MEDIUM,
        category: ErrorCategory = ErrorCategory.UNKNOWN,
        recovery_strategy: Optional[str] = None
    ) -> Dict[str, Any]:
        """
        Handle an error with context and recovery.
        
        Args:
            error: Exception to handle
            context: Additional context information
            severity: Error severity
            category: Error category
            recovery_strategy: Recovery strategy name
            
        Returns:
            Error handling result
        """
        # Update statistics
        self._update_stats(category, severity)
        
        # Build error information
        error_info = {
            'type': type(error).__name__,
            'message': str(error),
            'severity': severity.value,
            'category': category.value,
            'context': context or {},
            'traceback': traceback.format_exc(),
            'recovered': False,
            'recovery_action': None
        }
        
        # Log the error
        self._log_error(error_info)
        
        # Try specific handler
        handler = self.error_handlers.get(type(error))
        if handler:
            try:
                result = handler(error, context)
                error_info.update(result)
            except Exception as handler_error:
                self.logger.error(f"Error in error handler: {handler_error}")
        
        # Try recovery strategy
        if recovery_strategy and recovery_strategy in self.recovery_strategies:
            try:
                recovery_result = self.recovery_strategies[recovery_strategy](error, context)
                error_info['recovered'] = recovery_result.get('success', False)
                error_info['recovery_action'] = recovery_result.get('action')
            except Exception as recovery_error:
                self.logger.error(f"Error in recovery strategy: {recovery_error}")
        
        return error_info
    
    def with_error_handling(
        self,
        severity: ErrorSeverity = ErrorSeverity.MEDIUM,
        category: ErrorCategory = ErrorCategory.UNKNOWN,
        recovery_strategy: Optional[str] = None,
        reraise: bool = True
    ):
        """
        Decorator for automatic error handling.
        
        Args:
            severity: Error severity
            category: Error category
            recovery_strategy: Recovery strategy name
            reraise: Whether to re-raise the exception
        """
        def decorator(func):
            @wraps(func)
            def wrapper(*args, **kwargs):
                try:
                    return func(*args, **kwargs)
                except Exception as e:
                    context = {
                        'function': func.__name__,
                        'args': str(args)[:200],  # Limit length
                        'kwargs': str(kwargs)[:200]
                    }
                    
                    error_info = self.handle_error(
                        e, context, severity, category, recovery_strategy
                    )
                    
                    if reraise:
                        raise
                    
                    return {'error': error_info, 'success': False}
            
            return wrapper
        return decorator
    
    def _handle_file_not_found(self, error: FileNotFoundError, context: Optional[Dict] = None) -> Dict[str, Any]:
        """Handle file not found errors."""
        return {
            'suggestion': 'Check if the file path exists and is accessible',
            'category': ErrorCategory.IO_ERROR.value,
            'severity': ErrorSeverity.HIGH.value
        }
    
    def _handle_permission_error(self, error: PermissionError, context: Optional[Dict] = None) -> Dict[str, Any]:
        """Handle permission errors."""
        return {
            'suggestion': 'Check file permissions and user access rights',
            'category': ErrorCategory.IO_ERROR.value,
            'severity': ErrorSeverity.HIGH.value
        }
    
    def _handle_value_error(self, error: ValueError, context: Optional[Dict] = None) -> Dict[str, Any]:
        """Handle value errors."""
        return {
            'suggestion': 'Check input data format and values',
            'category': ErrorCategory.VALIDATION.value,
            'severity': ErrorSeverity.MEDIUM.value
        }
    
    def _handle_key_error(self, error: KeyError, context: Optional[Dict] = None) -> Dict[str, Any]:
        """Handle key errors."""
        missing_key = str(error).strip("'")\n        return {\n            'suggestion': f'Missing required key: {missing_key}',\n            'category': ErrorCategory.CONFIG_ERROR.value,\n            'severity': ErrorSeverity.MEDIUM.value,\n            'missing_key': missing_key\n        }\n    \n    def _handle_import_error(self, error: ImportError, context: Optional[Dict] = None) -> Dict[str, Any]:\n        \"\"\"Handle import errors.\"\"\"\n        return {\n            'suggestion': 'Install missing dependencies or check module paths',\n            'category': ErrorCategory.CONFIG_ERROR.value,\n            'severity': ErrorSeverity.HIGH.value\n        }\n    \n    def _handle_runtime_error(self, error: RuntimeError, context: Optional[Dict] = None) -> Dict[str, Any]:\n        \"\"\"Handle runtime errors.\"\"\"\n        return {\n            'suggestion': 'Check system resources and configuration',\n            'category': ErrorCategory.RESOURCE_ERROR.value,\n            'severity': ErrorSeverity.HIGH.value\n        }\n    \n    def _log_error(self, error_info: Dict[str, Any]):\n        \"\"\"Log error information.\"\"\"\n        severity = error_info['severity']\n        message = f\"[{error_info['category'].upper()}] {error_info['type']}: {error_info['message']}\"\n        \n        if severity == ErrorSeverity.CRITICAL.value:\n            self.logger.critical(message)\n        elif severity == ErrorSeverity.HIGH.value:\n            self.logger.error(message)\n        elif severity == ErrorSeverity.MEDIUM.value:\n            self.logger.warning(message)\n        else:\n            self.logger.info(message)\n        \n        # Log context if available\n        if error_info['context']:\n            self.logger.debug(f\"Context: {error_info['context']}\")\n    \n    def _update_stats(self, category: ErrorCategory, severity: ErrorSeverity):\n        \"\"\"Update error statistics.\"\"\"\n        self.error_stats['total_errors'] += 1\n        \n        cat_name = category.value\n        if cat_name not in self.error_stats['by_category']:\n            self.error_stats['by_category'][cat_name] = 0\n        self.error_stats['by_category'][cat_name] += 1\n        \n        sev_name = severity.value\n        if sev_name not in self.error_stats['by_severity']:\n            self.error_stats['by_severity'][sev_name] = 0\n        self.error_stats['by_severity'][sev_name] += 1\n    \n    def get_error_stats(self) -> Dict[str, Any]:\n        \"\"\"Get error statistics.\"\"\"\n        return self.error_stats.copy()\n    \n    def clear_stats(self):\n        \"\"\"Clear error statistics.\"\"\"\n        self.error_stats = {\n            'total_errors': 0,\n            'by_category': {},\n            'by_severity': {}\n        }\n    \n    def create_error_context(self, **kwargs) -> Dict[str, Any]:\n        \"\"\"Create error context dictionary.\"\"\"\n        return {\n            'timestamp': traceback.format_exc(),\n            **kwargs\n        }