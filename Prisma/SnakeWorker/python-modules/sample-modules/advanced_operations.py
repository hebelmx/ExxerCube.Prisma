"""
Advanced Operations Module for SnakeWorker

This module demonstrates more complex operations including data analysis,
image processing simulation, network utilities, and algorithm implementations.
"""

import time
import random
import hashlib
import base64
import json
import statistics
from typing import Dict, Any, List, Optional, Tuple
from datetime import datetime, timezone, timedelta
from collections import Counter, defaultdict
import urllib.parse


def data_analysis(dataset: List[Dict[str, Any]]) -> Dict[str, Any]:
    """Perform statistical analysis on a dataset."""
    start_time = time.time()
    
    if not dataset:
        return {
            "error": "Empty dataset provided",
            "function": "data_analysis",
            "success": False,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    
    # Extract numeric fields
    numeric_fields = defaultdict(list)
    categorical_fields = defaultdict(list)
    
    for record in dataset:
        for key, value in record.items():
            if isinstance(value, (int, float)) and key not in ('id', 'index'):
                numeric_fields[key].append(value)
            elif isinstance(value, str):
                categorical_fields[key].append(value)
    
    # Analyze numeric fields
    numeric_analysis = {}
    for field, values in numeric_fields.items():
        if values:
            numeric_analysis[field] = {
                "count": len(values),
                "mean": statistics.mean(values),
                "median": statistics.median(values),
                "mode": statistics.mode(values) if len(set(values)) < len(values) else None,
                "std_dev": statistics.stdev(values) if len(values) > 1 else 0,
                "min": min(values),
                "max": max(values),
                "range": max(values) - min(values),
                "percentiles": {
                    "25th": statistics.quantiles(values, n=4)[0] if len(values) >= 4 else None,
                    "75th": statistics.quantiles(values, n=4)[2] if len(values) >= 4 else None
                }
            }
    
    # Analyze categorical fields
    categorical_analysis = {}
    for field, values in categorical_fields.items():
        counter = Counter(values)
        categorical_analysis[field] = {
            "count": len(values),
            "unique_values": len(counter),
            "most_common": counter.most_common(5),
            "distribution": dict(counter)
        }
    
    end_time = time.time()
    
    return {
        "dataset_size": len(dataset),
        "numeric_fields": len(numeric_fields),
        "categorical_fields": len(categorical_fields),
        "numeric_analysis": numeric_analysis,
        "categorical_analysis": categorical_analysis,
        "processing_time": end_time - start_time,
        "function": "data_analysis",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": True
    }


def simulate_image_processing(width: int = 1920, height: int = 1080, operations: List[str] = None) -> Dict[str, Any]:
    """Simulate image processing operations."""
    start_time = time.time()
    
    if operations is None:
        operations = ["resize", "blur", "contrast", "brightness"]
    
    # Simulate image data (just dimensions and metadata)
    image_data = {
        "width": width,
        "height": height,
        "pixels": width * height,
        "size_mb": (width * height * 3) / (1024 * 1024),  # Assume RGB
        "format": "RGB"
    }
    
    processing_results = []
    total_processing_time = 0
    
    for operation in operations:
        op_start = time.time()
        
        # Simulate processing time based on image size and operation
        base_time = (width * height) / 1000000  # Base time per megapixel
        operation_multipliers = {
            "resize": 0.1,
            "blur": 0.3,
            "contrast": 0.05,
            "brightness": 0.02,
            "sharpen": 0.25,
            "rotate": 0.15,
            "crop": 0.01,
            "filter": 0.2
        }
        
        multiplier = operation_multipliers.get(operation, 0.1)
        processing_time = base_time * multiplier
        time.sleep(min(processing_time, 0.1))  # Cap at 100ms for simulation
        
        op_end = time.time()
        actual_time = op_end - op_start
        total_processing_time += actual_time
        
        processing_results.append({
            "operation": operation,
            "estimated_time": processing_time,
            "actual_time": actual_time,
            "efficiency": processing_time / actual_time if actual_time > 0 else 0,
            "status": "completed"
        })
    
    end_time = time.time()
    
    return {
        "image_info": image_data,
        "operations_performed": operations,
        "processing_results": processing_results,
        "total_processing_time": total_processing_time,
        "total_duration": end_time - start_time,
        "operations_count": len(operations),
        "pixels_per_second": image_data["pixels"] / total_processing_time if total_processing_time > 0 else 0,
        "function": "simulate_image_processing",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": True
    }


def cryptography_operations(data: str, operations: List[str] = None) -> Dict[str, Any]:
    """Demonstrate various cryptographic operations."""
    start_time = time.time()
    
    if operations is None:
        operations = ["md5", "sha256", "base64_encode", "url_encode"]
    
    results = {}
    original_data = data
    
    for operation in operations:
        op_start = time.time()
        
        try:
            if operation == "md5":
                result = hashlib.md5(data.encode()).hexdigest()
            elif operation == "sha1":
                result = hashlib.sha1(data.encode()).hexdigest()
            elif operation == "sha256":
                result = hashlib.sha256(data.encode()).hexdigest()
            elif operation == "sha512":
                result = hashlib.sha512(data.encode()).hexdigest()
            elif operation == "base64_encode":
                result = base64.b64encode(data.encode()).decode()
            elif operation == "base64_decode":
                # Only if data looks like base64
                try:
                    result = base64.b64decode(data).decode()
                except:
                    result = "Cannot decode - not valid base64"
            elif operation == "url_encode":
                result = urllib.parse.quote(data)
            elif operation == "url_decode":
                result = urllib.parse.unquote(data)
            else:
                result = f"Unknown operation: {operation}"
            
            op_end = time.time()
            results[operation] = {
                "result": result,
                "input_length": len(data),
                "output_length": len(str(result)),
                "processing_time": op_end - op_start,
                "status": "success"
            }
            
        except Exception as e:
            op_end = time.time()
            results[operation] = {
                "error": str(e),
                "processing_time": op_end - op_start,
                "status": "failed"
            }
    
    end_time = time.time()
    
    return {
        "original_data": original_data,
        "operations_requested": operations,
        "results": results,
        "total_processing_time": end_time - start_time,
        "successful_operations": sum(1 for r in results.values() if r["status"] == "success"),
        "failed_operations": sum(1 for r in results.values() if r["status"] == "failed"),
        "function": "cryptography_operations",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": True
    }


def algorithm_benchmark(algorithm: str = "sorting", size: int = 1000) -> Dict[str, Any]:
    """Benchmark different algorithms."""
    start_time = time.time()
    
    # Generate test data
    if algorithm in ["sorting", "search"]:
        test_data = [random.randint(1, size * 10) for _ in range(size)]
    else:
        test_data = list(range(size))
    
    results = {}
    
    if algorithm == "sorting":
        algorithms = {
            "builtin_sort": lambda x: sorted(x),
            "bubble_sort": bubble_sort,
            "quick_sort": quick_sort
        }
        
        for alg_name, alg_func in algorithms.items():
            alg_start = time.time()
            try:
                if alg_name == "builtin_sort":
                    sorted_data = alg_func(test_data.copy())
                else:
                    sorted_data = alg_func(test_data.copy())
                alg_end = time.time()
                
                results[alg_name] = {
                    "duration": alg_end - alg_start,
                    "elements": len(sorted_data),
                    "is_sorted": all(sorted_data[i] <= sorted_data[i+1] for i in range(len(sorted_data)-1)),
                    "status": "success"
                }
            except Exception as e:
                alg_end = time.time()
                results[alg_name] = {
                    "duration": alg_end - alg_start,
                    "error": str(e),
                    "status": "failed"
                }
    
    elif algorithm == "fibonacci":
        fib_number = min(size, 35)  # Cap to prevent extremely long execution
        alg_start = time.time()
        fib_result = fibonacci(fib_number)
        alg_end = time.time()
        
        results["fibonacci"] = {
            "input": fib_number,
            "result": fib_result,
            "duration": alg_end - alg_start,
            "status": "success"
        }
    
    elif algorithm == "prime_generation":
        alg_start = time.time()
        primes = generate_primes(size)
        alg_end = time.time()
        
        results["prime_generation"] = {
            "limit": size,
            "primes_found": len(primes),
            "largest_prime": max(primes) if primes else 0,
            "duration": alg_end - alg_start,
            "status": "success"
        }
    
    end_time = time.time()
    
    return {
        "algorithm": algorithm,
        "input_size": size,
        "test_data_generated": len(test_data),
        "results": results,
        "total_duration": end_time - start_time,
        "function": "algorithm_benchmark",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": True
    }


def network_simulation(endpoints: List[str] = None, simulate_latency: bool = True) -> Dict[str, Any]:
    """Simulate network operations and API calls."""
    start_time = time.time()
    
    if endpoints is None:
        endpoints = [
            "https://api.example.com/users",
            "https://api.example.com/posts",
            "https://api.example.com/comments"
        ]
    
    results = []
    
    for endpoint in endpoints:
        request_start = time.time()
        
        # Simulate network latency
        if simulate_latency:
            latency = random.uniform(0.01, 0.1)  # 10-100ms
            time.sleep(latency)
        
        # Simulate different response scenarios
        scenarios = ["success", "timeout", "error", "slow_response"]
        scenario_weights = [0.7, 0.1, 0.1, 0.1]  # 70% success rate
        scenario = random.choices(scenarios, weights=scenario_weights)[0]
        
        if scenario == "success":
            # Simulate successful response
            response_data = {
                "status": 200,
                "data": {"id": random.randint(1, 1000), "name": f"item_{random.randint(1, 100)}"},
                "headers": {"content-type": "application/json", "server": "nginx"},
                "size_bytes": random.randint(100, 5000)
            }
            status = "success"
            error = None
        elif scenario == "timeout":
            time.sleep(0.2)  # Simulate timeout
            response_data = None
            status = "timeout"
            error = "Request timeout after 200ms"
        elif scenario == "error":
            response_data = {"status": 500, "error": "Internal Server Error"}
            status = "error"
            error = "Server returned error 500"
        else:  # slow_response
            time.sleep(0.3)  # Simulate slow response
            response_data = {
                "status": 200,
                "data": {"message": "slow response"},
                "size_bytes": random.randint(1000, 10000)
            }
            status = "slow"
            error = None
        
        request_end = time.time()
        request_duration = request_end - request_start
        
        results.append({
            "endpoint": endpoint,
            "scenario": scenario,
            "status": status,
            "duration": request_duration,
            "response": response_data,
            "error": error,
            "timestamp": datetime.now(timezone.utc).isoformat()
        })
    
    end_time = time.time()
    
    # Calculate statistics
    successful_requests = [r for r in results if r["status"] == "success"]
    failed_requests = [r for r in results if r["status"] in ["timeout", "error"]]
    
    return {
        "endpoints_tested": len(endpoints),
        "results": results,
        "statistics": {
            "total_requests": len(results),
            "successful_requests": len(successful_requests),
            "failed_requests": len(failed_requests),
            "success_rate": len(successful_requests) / len(results) if results else 0,
            "average_duration": statistics.mean([r["duration"] for r in results]) if results else 0,
            "total_data_bytes": sum(r["response"].get("size_bytes", 0) for r in successful_requests if r["response"])
        },
        "total_duration": end_time - start_time,
        "function": "network_simulation",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": True
    }


# Helper functions for algorithms
def bubble_sort(arr):
    """Simple bubble sort implementation."""
    n = len(arr)
    for i in range(n):
        for j in range(0, n - i - 1):
            if arr[j] > arr[j + 1]:
                arr[j], arr[j + 1] = arr[j + 1], arr[j]
    return arr


def quick_sort(arr):
    """Quick sort implementation."""
    if len(arr) <= 1:
        return arr
    
    pivot = arr[len(arr) // 2]
    left = [x for x in arr if x < pivot]
    middle = [x for x in arr if x == pivot]
    right = [x for x in arr if x > pivot]
    
    return quick_sort(left) + middle + quick_sort(right)


def fibonacci(n):
    """Calculate fibonacci number."""
    if n <= 1:
        return n
    return fibonacci(n - 1) + fibonacci(n - 2)


def generate_primes(limit):
    """Generate prime numbers up to limit using Sieve of Eratosthenes."""
    if limit < 2:
        return []
    
    sieve = [True] * (limit + 1)
    sieve[0] = sieve[1] = False
    
    for i in range(2, int(limit**0.5) + 1):
        if sieve[i]:
            for j in range(i*i, limit + 1, i):
                sieve[j] = False
    
    return [i for i in range(2, limit + 1) if sieve[i]]


# Sample data for testing
SAMPLE_DATASET = [
    {"id": 1, "name": "Alice", "age": 25, "salary": 50000, "department": "Engineering"},
    {"id": 2, "name": "Bob", "age": 30, "salary": 60000, "department": "Marketing"},
    {"id": 3, "name": "Charlie", "age": 35, "salary": 70000, "department": "Engineering"},
    {"id": 4, "name": "Diana", "age": 28, "salary": 55000, "department": "Sales"},
    {"id": 5, "name": "Eve", "age": 32, "salary": 65000, "department": "Marketing"},
    {"id": 6, "name": "Frank", "age": 29, "salary": 52000, "department": "Engineering"},
    {"id": 7, "name": "Grace", "age": 26, "salary": 48000, "department": "Sales"},
    {"id": 8, "name": "Henry", "age": 34, "salary": 72000, "department": "Engineering"},
    {"id": 9, "name": "Iris", "age": 31, "salary": 58000, "department": "Marketing"},
    {"id": 10, "name": "Jack", "age": 27, "salary": 51000, "department": "Sales"}
]


if __name__ == "__main__":
    # Test functions when run directly
    print("Testing SnakeWorker advanced_operations module...")
    
    # Test data analysis
    result = data_analysis(SAMPLE_DATASET)
    print(f"✓ data_analysis: Analyzed {result['dataset_size']} records")
    
    # Test algorithm benchmark
    result = algorithm_benchmark("sorting", 100)
    print(f"✓ algorithm_benchmark: Benchmarked {len(result['results'])} algorithms")
    
    # Test cryptography operations
    result = cryptography_operations("Hello SnakeWorker!")
    print(f"✓ cryptography_operations: Performed {result['successful_operations']} operations")
    
    print("All tests completed!")