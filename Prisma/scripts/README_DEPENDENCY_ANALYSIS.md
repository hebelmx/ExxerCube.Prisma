# Dependency Analysis and Fixing Scripts

## Overview
This directory contains scripts for analyzing and fixing C# compilation errors (CS0246 and CS0103) in the ExxerAI project.

## Scripts

### 1. analyze_missing_dependencies.py
- **Purpose**: Comprehensive analyzer for CS0246/CS0103 errors
- **Features**:
  - Parses error files from Visual Studio output
  - Searches codebase for type definitions
  - Identifies NuGet packages for external types
  - Analyzes unused GlobalUsings.cs statements
  - Generates detailed JSON reports
- **Output**: `missing_dependencies_analysis.json`

### 2. fix_dependencies_smart.py
- **Purpose**: Applies fixes based on analysis report
- **Features**:
  - Reads JSON report from analyzer
  - Adds project references to .csproj files
  - Adds package references (respects Central Package Management)
  - Does NOT modify GlobalUsings.cs (respects Directory.Build.props)
  - Has dry-run mode for safety
  - Creates backups before modifications
- **Usage**:
  ```bash
  # Dry run (default)
  python fix_dependencies_smart.py --dry-run
  
  # Apply changes
  python fix_dependencies_smart.py --apply
  ```

### 3. analyze_dependencies_for_phase2.py
- **Purpose**: Phase 1 analysis script
- **Note**: This was the initial dependency analyzer

## Workflow

### Step 1: Export Errors from Visual Studio
1. Build the solution in Visual Studio
2. Copy all CS0246/CS0103 errors
3. Save to `F:\Dynamic\ExxerAi\ExxerAI\Errors\CS0246.txt` (tab-separated format)

### Step 2: Run Analysis
```bash
cd F:/Dynamic/ExxerAi/ExxerAI/scripts
python analyze_missing_dependencies.py --output missing_dependencies_analysis.json
```

### Step 3: Review and Apply Fixes
```bash
# Review what will be changed
python fix_dependencies_smart.py --dry-run

# Apply the changes
python fix_dependencies_smart.py --apply
```

### Step 4: Restore and Build
```bash
cd F:/Dynamic/ExxerAi/ExxerAI/code/src
dotnet restore ExxerAI.sln
dotnet build ExxerAI.sln
```

## Key Type Mappings

### Common NuGet Packages
```python
'Result': 'IndQuestResults',
'NullLogger': 'Microsoft.Extensions.Logging.Abstractions',
'ILogger': 'Microsoft.Extensions.Logging',
'TestContext': 'Meziantou.Extensions.Logging.Xunit.v3',
'Fact/Theory/InlineData': 'xunit.v3',
'Substitute/Arg': 'NSubstitute',
'SixLabors': 'SixLabors.ImageSharp',
'DocumentFormat': 'DocumentFormat.OpenXml'
```

### Project Reference Patterns
- Interfaces starting with 'I' + 'Service/Port/Repository' → ExxerAI.Application
- Document/OCR/Extraction types → ExxerAI.Domain
- Agent/Swarm types → ExxerAI.Infrastructure

## Important Notes

1. **GlobalUsings.cs**: The scripts do NOT modify GlobalUsings.cs files because namespaces are injected via Directory.Build.props
2. **Central Package Management**: All package versions are managed in Directory.Packages.props
3. **Backup**: fix_dependencies_smart.py creates backups in `smart_fix_backups/` directory
4. **Manual Investigation**: Types marked as "unknown" need manual investigation

## Results from October 30, 2024

- Total errors analyzed: 871
- Unique missing types: 233
- Project references added: 1
- Package references added: 3
- Types needing investigation: 168

Top missing types:
- KpiExxerproPatternSeed (54 occurrences)
- SixLabors (54)
- DocumentType (39)
- LearningEvent (38)

## Created by
These scripts were created as part of the ExxerAI compilation error resolution effort.