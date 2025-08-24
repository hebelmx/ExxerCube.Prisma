"""
Sample Python module for mathematical operations.
This module demonstrates basic Python integration with CSnakes.
"""

import time
import random
import logging
from typing import Dict, List, Any, Union

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


def add_numbers(a: float, b: float) -> Dict[str, Any]:
    """
    Add two numbers and return result with metadata.
    
    Args:
        a: First number
        b: Second number
        
    Returns:
        Dictionary with result and metadata
    """
    logger.info(f"Adding {a} + {b}")
    
    result = {
        "operation": "addition",
        "inputs": {"a": a, "b": b},
        "result": a + b,
        "timestamp": time.time(),
        "success": True
    }
    
    logger.info(f"Result: {result['result']}")
    return result


def multiply_numbers(a: float, b: float) -> Dict[str, Any]:
    """
    Multiply two numbers and return result with metadata.
    
    Args:
        a: First number
        b: Second number
        
    Returns:
        Dictionary with result and metadata
    """
    logger.info(f"Multiplying {a} * {b}")
    
    result = {
        "operation": "multiplication",
        "inputs": {"a": a, "b": b},
        "result": a * b,
        "timestamp": time.time(),
        "success": True
    }
    
    logger.info(f"Result: {result['result']}")
    return result


def calculate_fibonacci(n: int) -> Dict[str, Any]:
    """
    Calculate Fibonacci sequence up to n terms.
    
    Args:
        n: Number of terms to calculate
        
    Returns:
        Dictionary with sequence and metadata
    """
    logger.info(f"Calculating Fibonacci sequence for {n} terms")
    
    if n <= 0:
        return {
            "operation": "fibonacci",
            "inputs": {"n": n},
            "error": "Number of terms must be positive",
            "success": False,
            "timestamp": time.time()
        }
    
    sequence = []
    a, b = 0, 1
    
    for i in range(n):
        sequence.append(a)
        a, b = b, a + b
    
    result = {
        "operation": "fibonacci",
        "inputs": {"n": n},
        "sequence": sequence,
        "last_value": sequence[-1] if sequence else 0,
        "timestamp": time.time(),
        "success": True
    }
    
    logger.info(f"Generated {len(sequence)} Fibonacci numbers")
    return result


def simulate_heavy_computation(duration_seconds: float = 2.0) -> Dict[str, Any]:
    """
    Simulate a heavy computation that takes time.
    
    Args:
        duration_seconds: How long to simulate work
        
    Returns:
        Dictionary with computation results
    """
    logger.info(f"Starting heavy computation for {duration_seconds} seconds")
    
    start_time = time.time()
    iterations = 0
    
    # Simulate work
    while time.time() - start_time < duration_seconds:
        # Do some meaningless computation
        _ = sum(random.randint(1, 100) for _ in range(1000))
        iterations += 1
    
    end_time = time.time()
    actual_duration = end_time - start_time
    
    result = {
        "operation": "heavy_computation",
        "inputs": {"duration_seconds": duration_seconds},
        "actual_duration": actual_duration,
        "iterations": iterations,
        "operations_per_second": iterations / actual_duration,
        "timestamp": end_time,
        "success": True
    }
    
    logger.info(f"Heavy computation completed: {iterations} iterations in {actual_duration:.2f}s")
    return result


def process_list_data(data: List[Union[int, float]]) -> Dict[str, Any]:
    """
    Process a list of numeric data and return statistics.
    
    Args:
        data: List of numbers to process
        
    Returns:
        Dictionary with statistical analysis
    """
    logger.info(f"Processing list with {len(data)} items")
    
    if not data:
        return {
            "operation": "list_processing",
            "inputs": {"data_length": 0},
            "error": "Data list is empty",
            "success": False,
            "timestamp": time.time()
        }
    
    try:
        result = {
            "operation": "list_processing",
            "inputs": {"data_length": len(data)},
            "statistics": {
                "count": len(data),
                "sum": sum(data),
                "mean": sum(data) / len(data),
                "min": min(data),
                "max": max(data),
                "median": sorted(data)[len(data) // 2]
            },
            "timestamp": time.time(),
            "success": True
        }
        
        logger.info(f"Processed {len(data)} items: mean={result['statistics']['mean']:.2f}")
        return result
        
    except Exception as e:
        return {
            "operation": "list_processing",
            "inputs": {"data_length": len(data)},
            "error": str(e),
            "success": False,
            "timestamp": time.time()
        }


def simulate_error(error_type: str = "generic") -> Dict[str, Any]:
    """
    Simulate different types of errors for testing error handling.
    
    Args:
        error_type: Type of error to simulate
        
    Returns:
        Dictionary with error information
        
    Raises:
        Various exceptions based on error_type
    """
    logger.warning(f"Simulating error of type: {error_type}")
    
    if error_type == "value_error":
        raise ValueError("This is a simulated ValueError")
    elif error_type == "type_error":
        raise TypeError("This is a simulated TypeError")
    elif error_type == "runtime_error":
        raise RuntimeError("This is a simulated RuntimeError")
    elif error_type == "timeout":
        time.sleep(10)  # This should trigger timeout
    elif error_type == "memory":
        # Try to allocate a lot of memory
        big_list = [0] * (10**8)  # This might cause memory issues
        return {"data": big_list}
    else:
        raise Exception(f"Simulated generic error: {error_type}")


def get_system_info() -> Dict[str, Any]:
    """
    Get system information from Python perspective.
    
    Returns:
        Dictionary with system information
    """
    import sys
    import os
    import platform
    
    logger.info("Gathering system information")
    
    result = {
        "operation": "system_info",
        "python_version": sys.version,
        "python_executable": sys.executable,
        "platform": platform.platform(),
        "architecture": platform.architecture(),
        "processor": platform.processor(),
        "current_directory": os.getcwd(),
        "environment_variables": dict(os.environ),
        "python_path": sys.path,
        "timestamp": time.time(),
        "success": True
    }
    
    logger.info("System information gathered successfully")
    return result