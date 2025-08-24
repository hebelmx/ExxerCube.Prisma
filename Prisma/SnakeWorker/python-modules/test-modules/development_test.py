"""
Development Test Module for SnakeWorker

This module contains functions specifically designed for development and testing
of the SnakeWorker system. It includes various scenarios to test error handling,
timeouts, memory usage, and different data types.
"""

import time
import random
import threading
from typing import Dict, Any, List, Optional
from datetime import datetime, timezone
import sys
import os
import gc
import json


def simple_hello(name: str = "SnakeWorker") -> Dict[str, Any]:
    """Simple hello world function for basic testing."""
    return {
        "message": f"Hello, {name}!",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "function": "simple_hello",
        "success": True,
        "metadata": {
            "python_version": sys.version,
            "module_file": __file__
        }
    }


def simulate_work(duration_seconds: float = 1.0, memory_mb: int = 1) -> Dict[str, Any]:
    """Simulate work by consuming time and memory."""
    start_time = time.time()
    
    # Allocate memory
    memory_consumer = []
    if memory_mb > 0:
        # Each list item is roughly 1KB, so 1024 items ≈ 1MB
        items_per_mb = 1024
        total_items = memory_mb * items_per_mb
        memory_consumer = [f"data_item_{i:06d}" for i in range(total_items)]
    
    # Simulate CPU work
    iterations = 0
    while time.time() - start_time < duration_seconds:
        iterations += 1
        # Do some meaningless computation
        _ = sum(i * i for i in range(100))
        time.sleep(0.001)  # Small sleep to prevent tight loop
    
    end_time = time.time()
    actual_duration = end_time - start_time
    
    # Clean up memory
    del memory_consumer
    gc.collect()
    
    return {
        "requested_duration": duration_seconds,
        "actual_duration": actual_duration,
        "memory_allocated_mb": memory_mb,
        "iterations": iterations,
        "start_time": start_time,
        "end_time": end_time,
        "success": True,
        "function": "simulate_work"
    }


def simulate_error(error_type: str = "generic") -> Dict[str, Any]:
    """Simulate different types of errors for testing error handling."""
    error_types = {
        "value_error": ValueError("This is a test ValueError"),
        "type_error": TypeError("This is a test TypeError"),
        "key_error": KeyError("missing_key"),
        "index_error": IndexError("list index out of range"),
        "zero_division": ZeroDivisionError("division by zero"),
        "runtime_error": RuntimeError("This is a test RuntimeError"),
        "generic": Exception("This is a generic test exception")
    }
    
    if error_type not in error_types:
        error_type = "generic"
    
    # Log the error attempt
    error_info = {
        "error_type": error_type,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "function": "simulate_error",
        "about_to_raise": str(error_types[error_type])
    }
    
    # Actually raise the error
    raise error_types[error_type]


def random_success_failure(success_rate: float = 0.7) -> Dict[str, Any]:
    """Randomly succeed or fail based on success rate."""
    is_success = random.random() < success_rate
    
    result = {
        "success_rate": success_rate,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "function": "random_success_failure",
        "random_value": random.random()
    }
    
    if is_success:
        result.update({
            "success": True,
            "message": "Operation succeeded",
            "result": random.randint(1, 1000)
        })
        return result
    else:
        result.update({
            "success": False,
            "message": "Operation failed randomly",
            "error_code": random.choice(["ERR001", "ERR002", "ERR003"])
        })
        raise RuntimeError(f"Random failure occurred: {result['error_code']}")


def process_complex_data(data: Dict[str, Any]) -> Dict[str, Any]:
    """Process complex nested data structures."""
    start_time = time.time()
    
    processed_data = {}
    
    # Process different data types
    for key, value in data.items():
        if isinstance(value, (int, float)):
            processed_data[f"{key}_squared"] = value ** 2
            processed_data[f"{key}_sqrt"] = value ** 0.5 if value >= 0 else None
        elif isinstance(value, str):
            processed_data[f"{key}_length"] = len(value)
            processed_data[f"{key}_upper"] = value.upper()
            processed_data[f"{key}_words"] = len(value.split())
        elif isinstance(value, list):
            processed_data[f"{key}_count"] = len(value)
            processed_data[f"{key}_sum"] = sum(v for v in value if isinstance(v, (int, float)))
            processed_data[f"{key}_types"] = list(set(type(v).__name__ for v in value))
        elif isinstance(value, dict):
            processed_data[f"{key}_keys"] = len(value.keys())
            processed_data[f"{key}_nested"] = {k: str(v) for k, v in value.items()}
        else:
            processed_data[f"{key}_type"] = type(value).__name__
            processed_data[f"{key}_str"] = str(value)
    
    end_time = time.time()
    
    return {
        "original_data": data,
        "processed_data": processed_data,
        "processing_time": end_time - start_time,
        "function": "process_complex_data",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": True,
        "statistics": {
            "original_keys": len(data),
            "processed_keys": len(processed_data),
            "data_types_processed": len(set(type(v).__name__ for v in data.values()))
        }
    }


def concurrent_operation(thread_count: int = 3, operation_duration: float = 0.5) -> Dict[str, Any]:
    """Test concurrent operations within a single Python function call."""
    results = []
    threads = []
    
    def worker(worker_id: int):
        start = time.time()
        # Simulate some work
        total = 0
        for i in range(1000):
            total += i * i
            time.sleep(operation_duration / 1000)
        
        end = time.time()
        results.append({
            "worker_id": worker_id,
            "start_time": start,
            "end_time": end,
            "duration": end - start,
            "result": total
        })
    
    # Start threads
    start_time = time.time()
    for i in range(thread_count):
        thread = threading.Thread(target=worker, args=(i,))
        thread.start()
        threads.append(thread)
    
    # Wait for all threads to complete
    for thread in threads:
        thread.join()
    
    end_time = time.time()
    
    return {
        "thread_count": thread_count,
        "operation_duration": operation_duration,
        "total_duration": end_time - start_time,
        "worker_results": results,
        "function": "concurrent_operation",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": True,
        "statistics": {
            "avg_worker_duration": sum(r["duration"] for r in results) / len(results) if results else 0,
            "total_operations": len(results),
            "parallelism_efficiency": (sum(r["duration"] for r in results) / (end_time - start_time)) if (end_time - start_time) > 0 else 0
        }
    }


def file_operations(temp_dir: str = "temp_test") -> Dict[str, Any]:
    """Test file I/O operations."""
    import tempfile
    import shutil
    
    start_time = time.time()
    operations = []
    
    try:
        # Create temporary directory
        temp_path = os.path.join(tempfile.gettempdir(), temp_dir)
        os.makedirs(temp_path, exist_ok=True)
        operations.append(f"Created directory: {temp_path}")
        
        # Write test files
        test_files = []
        for i in range(3):
            file_path = os.path.join(temp_path, f"test_file_{i:02d}.txt")
            with open(file_path, 'w', encoding='utf-8') as f:
                content = f"Test file {i}\nCreated at: {datetime.now(timezone.utc).isoformat()}\n"
                content += "Sample data: " + " ".join(str(x) for x in range(i * 10, (i + 1) * 10))
                f.write(content)
            test_files.append(file_path)
            operations.append(f"Created file: {file_path}")
        
        # Read files back
        file_contents = {}
        for file_path in test_files:
            with open(file_path, 'r', encoding='utf-8') as f:
                file_contents[os.path.basename(file_path)] = f.read()
            operations.append(f"Read file: {file_path}")
        
        # List directory contents
        dir_contents = os.listdir(temp_path)
        operations.append(f"Listed directory contents: {len(dir_contents)} items")
        
        # Clean up
        shutil.rmtree(temp_path)
        operations.append(f"Cleaned up directory: {temp_path}")
        
        end_time = time.time()
        
        return {
            "temp_directory": temp_path,
            "files_created": len(test_files),
            "operations": operations,
            "file_contents": file_contents,
            "directory_contents": dir_contents,
            "duration": end_time - start_time,
            "function": "file_operations",
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "success": True
        }
        
    except Exception as e:
        # Clean up on error
        try:
            if os.path.exists(temp_path):
                shutil.rmtree(temp_path)
        except:
            pass
        
        return {
            "temp_directory": temp_path if 'temp_path' in locals() else None,
            "operations": operations,
            "error": str(e),
            "error_type": type(e).__name__,
            "function": "file_operations",
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "success": False
        }


def system_info() -> Dict[str, Any]:
    """Gather system information for debugging and monitoring."""
    import platform
    
    info = {
        "python": {
            "version": sys.version,
            "version_info": list(sys.version_info),
            "executable": sys.executable,
            "path": sys.path[:5],  # First 5 paths to avoid too much data
            "platform": platform.platform(),
            "architecture": platform.architecture(),
            "processor": platform.processor(),
            "machine": platform.machine()
        },
        "process": {
            "pid": os.getpid(),
            "ppid": os.getppid() if hasattr(os, 'getppid') else None,
            "cwd": os.getcwd(),
            "environment_vars": {
                key: value for key, value in os.environ.items() 
                if not key.upper().startswith(('PASSWORD', 'SECRET', 'TOKEN', 'KEY'))
            }
        },
        "memory": {
            "gc_stats": {
                "counts": gc.get_count(),
                "threshold": gc.get_threshold(),
                "objects": len(gc.get_objects())
            }
        },
        "function": "system_info",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": True
    }
    
    return info


# Example data for testing
SAMPLE_TEST_DATA = {
    "simple_string": "Hello, World!",
    "number": 42,
    "float_number": 3.14159,
    "boolean": True,
    "list_mixed": [1, "two", 3.0, True, None],
    "nested_dict": {
        "level1": {
            "level2": {
                "value": "deep_value",
                "numbers": [1, 2, 3, 4, 5]
            }
        }
    },
    "null_value": None
}


if __name__ == "__main__":
    # Test functions when run directly
    print("Testing SnakeWorker development_test module...")
    
    # Test simple hello
    result = simple_hello("Development Mode")
    print(f"✓ simple_hello: {result['message']}")
    
    # Test complex data processing
    result = process_complex_data(SAMPLE_TEST_DATA)
    print(f"✓ process_complex_data: Processed {result['statistics']['original_keys']} keys")
    
    # Test system info
    result = system_info()
    print(f"✓ system_info: Python {result['python']['version_info']}")
    
    print("All tests completed!")