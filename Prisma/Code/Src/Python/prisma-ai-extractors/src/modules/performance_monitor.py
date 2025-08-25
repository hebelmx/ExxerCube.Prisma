"""
Performance Monitor Module - Single Responsibility: Performance tracking and metrics.
"""

import time
import psutil
import torch
from typing import Dict, Any, Optional, List
from dataclasses import dataclass, field
from contextlib import contextmanager
from datetime import datetime


@dataclass
class PerformanceMetrics:
    """Performance metrics data class."""
    operation: str
    start_time: float
    end_time: float = 0
    duration: float = 0
    memory_before: float = 0
    memory_after: float = 0
    memory_delta: float = 0
    gpu_memory_before: float = 0
    gpu_memory_after: float = 0
    gpu_memory_delta: float = 0
    success: bool = True
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


class PerformanceMonitor:
    """
    Responsible ONLY for monitoring and tracking performance.
    Single Responsibility: Performance measurement and reporting.
    """
    
    def __init__(self):
        self.metrics_history: List[PerformanceMetrics] = []
        self.active_operations: Dict[str, PerformanceMetrics] = {}
        self.enable_gpu_monitoring = torch.cuda.is_available()
    
    @contextmanager
    def track_operation(self, operation_name: str, **metadata):
        """
        Context manager for tracking operation performance.
        
        Args:
            operation_name: Name of the operation
            **metadata: Additional metadata to store
            
        Yields:
            PerformanceMetrics instance
        """
        metrics = self.start_operation(operation_name, **metadata)
        
        try:
            yield metrics
            self.end_operation(operation_name, success=True)
        except Exception as e:
            self.end_operation(operation_name, success=False, error=str(e))
            raise
    
    def start_operation(self, operation_name: str, **metadata) -> PerformanceMetrics:
        """
        Start tracking an operation.
        
        Args:
            operation_name: Name of the operation
            **metadata: Additional metadata
            
        Returns:
            PerformanceMetrics instance
        """
        metrics = PerformanceMetrics(
            operation=operation_name,
            start_time=time.time(),
            metadata=metadata
        )
        
        # Capture memory state
        metrics.memory_before = self._get_memory_usage()
        
        if self.enable_gpu_monitoring:
            metrics.gpu_memory_before = self._get_gpu_memory_usage()
        
        self.active_operations[operation_name] = metrics
        
        return metrics
    
    def end_operation(
        self,
        operation_name: str,
        success: bool = True,
        error: Optional[str] = None
    ) -> Optional[PerformanceMetrics]:
        """
        End tracking an operation.
        
        Args:
            operation_name: Name of the operation
            success: Whether operation succeeded
            error: Error message if failed
            
        Returns:
            Completed metrics or None
        """
        if operation_name not in self.active_operations:
            return None
        
        metrics = self.active_operations.pop(operation_name)
        metrics.end_time = time.time()
        metrics.duration = metrics.end_time - metrics.start_time
        metrics.success = success
        metrics.error = error
        
        # Capture final memory state
        metrics.memory_after = self._get_memory_usage()
        metrics.memory_delta = metrics.memory_after - metrics.memory_before
        
        if self.enable_gpu_monitoring:
            metrics.gpu_memory_after = self._get_gpu_memory_usage()
            metrics.gpu_memory_delta = metrics.gpu_memory_after - metrics.gpu_memory_before
        
        self.metrics_history.append(metrics)
        
        return metrics
    
    def _get_memory_usage(self) -> float:
        """Get current memory usage in MB."""
        process = psutil.Process()
        return process.memory_info().rss / 1024 / 1024
    
    def _get_gpu_memory_usage(self) -> float:
        """Get current GPU memory usage in MB."""
        if not torch.cuda.is_available():
            return 0
        
        return torch.cuda.memory_allocated() / 1024 / 1024
    
    def get_operation_stats(self, operation_name: Optional[str] = None) -> Dict[str, Any]:
        """
        Get statistics for operations.
        
        Args:
            operation_name: Specific operation or None for all
            
        Returns:
            Statistics dictionary
        """
        if operation_name:
            metrics_list = [m for m in self.metrics_history if m.operation == operation_name]
        else:
            metrics_list = self.metrics_history
        
        if not metrics_list:
            return {'count': 0}
        
        durations = [m.duration for m in metrics_list]
        memory_deltas = [m.memory_delta for m in metrics_list]
        success_count = sum(1 for m in metrics_list if m.success)
        
        stats = {
            'count': len(metrics_list),
            'success_count': success_count,
            'failure_count': len(metrics_list) - success_count,
            'success_rate': success_count / len(metrics_list),
            'avg_duration': sum(durations) / len(durations),
            'min_duration': min(durations),
            'max_duration': max(durations),
            'avg_memory_delta': sum(memory_deltas) / len(memory_deltas),
            'total_duration': sum(durations)
        }
        
        if self.enable_gpu_monitoring:
            gpu_deltas = [m.gpu_memory_delta for m in metrics_list]
            stats['avg_gpu_memory_delta'] = sum(gpu_deltas) / len(gpu_deltas) if gpu_deltas else 0
        
        return stats
    
    def get_report(self) -> Dict[str, Any]:
        """
        Generate a comprehensive performance report.
        
        Returns:
            Performance report dictionary
        """
        report = {
            'summary': {
                'total_operations': len(self.metrics_history),
                'unique_operations': len(set(m.operation for m in self.metrics_history)),
                'total_time': sum(m.duration for m in self.metrics_history),
                'success_rate': sum(1 for m in self.metrics_history if m.success) / len(self.metrics_history) if self.metrics_history else 0
            },
            'by_operation': {}
        }
        
        # Group by operation type
        operations = set(m.operation for m in self.metrics_history)
        for op in operations:
            report['by_operation'][op] = self.get_operation_stats(op)
        
        # Add top slowest operations
        slowest = sorted(self.metrics_history, key=lambda m: m.duration, reverse=True)[:5]
        report['slowest_operations'] = [
            {
                'operation': m.operation,
                'duration': m.duration,
                'timestamp': datetime.fromtimestamp(m.start_time).isoformat(),
                'metadata': m.metadata
            }
            for m in slowest
        ]
        
        # Add memory statistics
        if self.metrics_history:
            report['memory'] = {
                'peak_usage': max(m.memory_after for m in self.metrics_history),
                'avg_delta': sum(m.memory_delta for m in self.metrics_history) / len(self.metrics_history)
            }
            
            if self.enable_gpu_monitoring:
                report['gpu_memory'] = {
                    'peak_usage': max(m.gpu_memory_after for m in self.metrics_history),
                    'avg_delta': sum(m.gpu_memory_delta for m in self.metrics_history) / len(self.metrics_history)
                }
        
        return report
    
    def clear_history(self):
        """Clear metrics history."""
        self.metrics_history.clear()
        self.active_operations.clear()
    
    def export_metrics(self) -> List[Dict[str, Any]]:
        """
        Export metrics for external analysis.
        
        Returns:
            List of metrics dictionaries
        """
        return [
            {
                'operation': m.operation,
                'start_time': m.start_time,
                'end_time': m.end_time,
                'duration': m.duration,
                'memory_before': m.memory_before,
                'memory_after': m.memory_after,
                'memory_delta': m.memory_delta,
                'gpu_memory_before': m.gpu_memory_before,
                'gpu_memory_after': m.gpu_memory_after,
                'gpu_memory_delta': m.gpu_memory_delta,
                'success': m.success,
                'error': m.error,
                'metadata': m.metadata
            }
            for m in self.metrics_history
        ]