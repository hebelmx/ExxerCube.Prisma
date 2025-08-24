# SnakeWorker - Python-C# Integration Testing Framework

A comprehensive background worker service for testing Python module integration with C# using CSnakes, featuring enterprise-level logging, metrics, health monitoring, and retry mechanisms.

## 🎯 Purpose

SnakeWorker is a "kitchen sink" toy project designed to test Python modules for C# integration before production deployment. It provides a robust testing environment with comprehensive monitoring and error handling.

## 🏗️ Architecture

### Project Structure

```
SnakeWorker/
├── src/
│   ├── SnakeWorker.Core/          # Domain models and interfaces
│   ├── SnakeWorker.Python/        # CSnakes integration layer
│   ├── SnakeWorker.Host/          # Main host application
│   └── SnakeWorker.Tests/         # Unit and integration tests
├── python-modules/
│   ├── sample-modules/            # Example Python modules
│   └── test-modules/              # Test-specific modules
└── logs/                          # Application logs
```

### Core Components

1. **Task Queue System**: In-memory queue with priority support
2. **Python Executor**: CSnakes-based Python code execution
3. **Background Workers**: Configurable concurrent task processing
4. **Metrics Collection**: Counters, gauges, and histograms
5. **Health Monitoring**: Python environment and queue health checks
6. **Retry Logic**: Exponential backoff with jitter

## 🚀 Features

### Background Processing
- **Concurrent Workers**: Configurable number of parallel workers
- **Task Scheduling**: Delayed task execution support
- **Retry Mechanism**: Exponential backoff with configurable limits
- **Graceful Shutdown**: Clean worker termination

### Monitoring & Observability
- **Structured Logging**: Serilog with console and file outputs
- **Metrics Collection**: Task processing metrics with tags
- **Health Checks**: Python environment and queue health
- **Startup Information**: Detailed environment logging

### Python Integration
- **CSnakes Integration**: Seamless Python-C# interoperability
- **Module Management**: Dynamic module loading and execution
- **Error Handling**: Comprehensive exception management
- **Timeout Support**: Configurable execution timeouts

## 🛠️ Configuration

### Application Settings (`appsettings.json`)

```json
{
  "Worker": {
    "ConcurrentWorkers": 4,
    "PollingIntervalMs": 1000,
    "DefaultMaxRetries": 3,
    "DefaultTimeoutSeconds": 300
  },
  "CSnakes": {
    "PythonDll": null,
    "PythonHome": null,
    "PythonPaths": [
      "./python-modules/sample-modules",
      "./python-modules/test-modules"
    ],
    "MaxConcurrentExecutions": 4,
    "EnableDebugMode": false,
    "PreloadModules": [
      "math_operations",
      "text_processor"
    ]
  },
  "MetricsReporting": {
    "ReportingIntervalSeconds": 60,
    "OutputFilePath": "metrics/metrics-report.json",
    "ExternalEndpoint": null,
    "IncludeHistogramDetails": true
  }
}
```

## 📦 Dependencies

### C# Dependencies
- **CSnakes**: Python integration
- **Serilog**: Structured logging
- **Microsoft.Extensions.Hosting**: Background services
- **Microsoft.Extensions.DependencyInjection**: Dependency injection
- **Microsoft.Extensions.Diagnostics.HealthChecks**: Health monitoring

### Python Dependencies
No specific requirements - uses standard library modules for examples.

## 🏃 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Python 3.8+
- Windows/Linux/macOS

### Running the Application

1. **Build the solution**:
   ```bash
   dotnet build
   ```

2. **Run the host application**:
   ```bash
   dotnet run --project src/SnakeWorker.Host
   ```

3. **Monitor logs**:
   - Console output for real-time monitoring
   - File logs in `logs/snakeworker-*.log`
   - Metrics reports in `metrics/metrics-report.json`

### Adding Custom Python Modules

1. Create your Python module in `python-modules/sample-modules/`:

```python
# my_module.py
import time
from typing import Dict, Any

def process_data(input_data: Dict[str, Any]) -> Dict[str, Any]:
    """Example function that processes input data."""
    result = {
        "processed_at": time.time(),
        "input_received": input_data,
        "status": "completed",
        "module": "my_module"
    }
    return result
```

2. Add the module to `CSnakes.PreloadModules` in configuration

3. Create tasks programmatically or through the API (when implemented)

## 📊 Metrics

### Available Metrics

- **tasks.dequeued**: Number of tasks dequeued from queue
- **tasks.processed**: Total tasks processed
- **tasks.completed**: Successfully completed tasks
- **tasks.failed**: Failed tasks (per attempt)
- **tasks.failed_final**: Tasks that failed after all retries
- **tasks.retried**: Task retry attempts
- **tasks.duration_ms**: Task execution duration histogram
- **tasks.memory_usage_bytes**: Memory usage per task

### Metric Tags
- `worker_id`: Worker thread identifier
- `task_type`: Type of task being processed
- `task_id`: Unique task identifier
- `retry_count`: Current retry attempt

## 🏥 Health Checks

### Python Health Check
Validates Python environment by checking availability of core modules:
- `sys` module availability
- `os` module availability

### Queue Health Check
Monitors task queue status:
- **Healthy**: Queue size < 1000
- **Degraded**: Queue size 1000-5000
- **Unhealthy**: Queue size > 5000

## 🐍 Sample Python Modules

### Math Operations (`math_operations.py`)
- Basic arithmetic operations
- Advanced mathematical functions
- Statistical calculations
- Error handling examples

### Text Processing (`text_processor.py`)
- String manipulation functions
- Text analysis utilities
- Encoding/decoding operations
- Regular expression examples

## 🔧 Development

### Adding New Features

1. **Domain Models**: Add to `SnakeWorker.Core/Models/`
2. **Interfaces**: Define in `SnakeWorker.Core/Interfaces/`
3. **Services**: Implement in respective projects
4. **Configuration**: Update options classes and appsettings.json

### Testing

Run tests with:
```bash
dotnet test
```

### Debugging

Enable debug logging by setting:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "SnakeWorker": "Debug"
      }
    }
  },
  "CSnakes": {
    "EnableDebugMode": true
  }
}
```

## 📈 Performance Considerations

- **Worker Scaling**: Adjust `ConcurrentWorkers` based on CPU cores
- **Python Path**: Optimize module loading paths
- **Memory Management**: Monitor `tasks.memory_usage_bytes` metric
- **Queue Size**: Monitor queue health to prevent memory issues

## 🚨 Production Readiness

This is a **toy project** for testing. For production use, consider:

- Persistent task storage (database, Redis)
- Distributed queue systems
- Advanced monitoring (Prometheus, Grafana)
- Container deployment
- Security hardening
- Load balancing
- Backup and recovery procedures

## 📝 License

This project is part of the Prisma OCR system and is intended for internal testing and development purposes.

## 🤝 Contributing

This is a testing framework. Contributions should focus on:
- Additional Python module examples
- Enhanced monitoring capabilities
- Performance optimizations
- Documentation improvements