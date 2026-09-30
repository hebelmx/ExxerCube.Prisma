#!/usr/bin/env python3
"""
Analyze remaining build errors after smart dependency fixes
"""

import subprocess
import re
from collections import defaultdict
from datetime import datetime
import json
from pathlib import Path

def extract_build_errors():
    """Extract all CS errors from dotnet build output"""
    print("Running dotnet build to capture errors...")
    
    result = subprocess.run(
        ["dotnet", "build", "--no-incremental"],
        cwd="F:/Dynamic/ExxerAi/ExxerAI",
        capture_output=True,
        text=True
    )
    
    # Parse errors
    errors = []
    error_pattern = re.compile(r'(.+?)\((\d+),(\d+)\): error (CS\d+): (.+)')
    
    for line in result.stdout.split('\n') + result.stderr.split('\n'):
        match = error_pattern.search(line)
        if match:
            errors.append({
                'file': match.group(1).strip(),
                'line': int(match.group(2)),
                'column': int(match.group(3)),
                'code': match.group(4),
                'message': match.group(5).strip()
            })
    
    return errors

def analyze_errors(errors):
    """Analyze errors by type and category"""
    analysis = {
        'total_errors': len(errors),
        'by_code': defaultdict(list),
        'by_project': defaultdict(list),
        'missing_types': set(),
        'ambiguous_references': [],
        'other_errors': []
    }
    
    for error in errors:
        code = error['code']
        analysis['by_code'][code].append(error)
        
        # Extract project name
        project = 'Unknown'
        if '\\code\\src\\' in error['file']:
            parts = error['file'].split('\\')
            for i, part in enumerate(parts):
                if part == 'src' and i + 2 < len(parts):
                    project = parts[i + 2]
                    break
        elif '\\code\\test\\' in error['file'] or '\\tests\\' in error['file']:
            parts = error['file'].split('\\')
            for i, part in enumerate(parts):
                if part in ['test', 'tests'] and i + 2 < len(parts):
                    project = parts[i + 2]
                    break
        
        analysis['by_project'][project].append(error)
        
        # Categorize errors
        if code == 'CS0246':  # Type or namespace not found
            match = re.search(r"type or namespace name '(\w+)'", error['message'])
            if match:
                analysis['missing_types'].add(match.group(1))
        elif code == 'CS0103':  # Name does not exist in context
            match = re.search(r"name '(\w+)' does not exist", error['message'])
            if match:
                analysis['missing_types'].add(match.group(1))
        elif code == 'CS0104':  # Ambiguous reference
            analysis['ambiguous_references'].append(error)
        else:
            analysis['other_errors'].append(error)
    
    return analysis

def print_analysis(analysis):
    """Print detailed analysis of errors"""
    print(f"\n{'=' * 80}")
    print(f"REMAINING BUILD ERRORS ANALYSIS")
    print(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    print(f"Total Errors: {analysis['total_errors']}")
    
    # Errors by code
    print("\nErrors by Type:")
    for code, errors in sorted(analysis['by_code'].items()):
        print(f"  {code}: {len(errors)} errors")
        # Show first example
        if errors:
            print(f"    Example: {errors[0]['message']}")
    
    # Errors by project
    print("\nErrors by Project:")
    for project, errors in sorted(analysis['by_project'].items()):
        print(f"  {project}: {len(errors)} errors")
    
    # Missing types
    if analysis['missing_types']:
        print(f"\nMissing Types ({len(analysis['missing_types'])}):")
        for type_name in sorted(analysis['missing_types']):
            print(f"  - {type_name}")
    
    # Ambiguous references
    if analysis['ambiguous_references']:
        print(f"\nAmbiguous References ({len(analysis['ambiguous_references'])}):")
        for error in analysis['ambiguous_references'][:5]:  # Show first 5
            print(f"  - {error['file']}")
            print(f"    {error['message']}")
    
    # Other errors
    if analysis['other_errors']:
        print(f"\nOther Errors ({len(analysis['other_errors'])}):")
        error_types = defaultdict(int)
        for error in analysis['other_errors']:
            error_types[error['code']] += 1
        for code, count in sorted(error_types.items()):
            print(f"  {code}: {count} errors")

def save_analysis(errors, analysis):
    """Save analysis to JSON for further processing"""
    output = {
        'timestamp': datetime.now().isoformat(),
        'total_errors': len(errors),
        'errors': errors,
        'analysis': {
            'by_code': dict(analysis['by_code']),
            'by_project': dict(analysis['by_project']),
            'missing_types': list(analysis['missing_types']),
            'ambiguous_references': analysis['ambiguous_references'],
            'other_errors': analysis['other_errors']
        }
    }
    
    # Convert defaultdict to dict for JSON serialization
    output['analysis']['by_code'] = {k: len(v) for k, v in analysis['by_code'].items()}
    output['analysis']['by_project'] = {k: len(v) for k, v in analysis['by_project'].items()}
    
    with open('remaining_errors_analysis.json', 'w') as f:
        json.dump(output, f, indent=2)
    
    print(f"\nAnalysis saved to: remaining_errors_analysis.json")

def suggest_fixes(analysis):
    """Suggest potential fixes for common error patterns"""
    print(f"\n{'=' * 80}")
    print("SUGGESTED FIXES")
    print(f"{'=' * 80}\n")
    
    # Missing types
    missing_types = analysis['missing_types']
    if missing_types:
        print("Missing Type Suggestions:")
        
        # Check for common patterns
        if 'DocumentType' in missing_types or 'FieldType' in missing_types:
            print("  - DocumentType/FieldType: Likely from ExxerAI.Domain.DocumentProcessing")
        
        if 'API' in missing_types:
            print("  - API: Could be from multiple sources - check context")
        
        if 'NetArchTest' in missing_types:
            print("  - NetArchTest: Test framework - add NuGet package NetArchTest.Rules")
        
        if 'UglyToad' in missing_types:
            print("  - UglyToad: PDF library - likely UglyToad.PdfPig")
    
    # Ambiguous references
    if analysis['ambiguous_references']:
        print("\nAmbiguous Reference Fixes:")
        print("  - Add explicit using aliases or fully qualify types")
        print("  - Consider removing duplicate global usings")
    
    # CS0234 errors (missing in namespace)
    if 'CS0234' in analysis['by_code']:
        print("\nNamespace Issues (CS0234):")
        print("  - Check if types were moved or renamed")
        print("  - Verify project references are correct")

def main():
    """Main analysis function"""
    # Extract errors
    errors = extract_build_errors()
    
    if not errors:
        print("No build errors found!")
        return
    
    # Analyze errors
    analysis = analyze_errors(errors)
    
    # Print analysis
    print_analysis(analysis)
    
    # Save for further processing
    save_analysis(errors, analysis)
    
    # Suggest fixes
    suggest_fixes(analysis)

if __name__ == "__main__":
    main()