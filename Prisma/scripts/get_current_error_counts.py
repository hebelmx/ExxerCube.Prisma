#!/usr/bin/env python3
"""
Get current error counts by temporarily disabling warnings-as-errors
"""

import subprocess
import re
import os
from pathlib import Path

def disable_warnings_as_errors():
    """Temporarily disable warnings as errors"""
    project_files = [
        "Src/Tests/Core/Application.UnitTests/Application.UnitTests.csproj",
        "Src/Tests/Presentation/IndTrace.Oee.Tests/IndTrace.Oee.Tests/IndTrace.Oee.Tests.csproj"
    ]
    
    modified_files = []
    
    for proj_file in project_files:
        proj_path = Path(proj_file)
        if proj_path.exists():
            try:
                with open(proj_path, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                if '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>' in content:
                    modified_content = content.replace(
                        '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>',
                        '<!-- <TreatWarningsAsErrors>true</TreatWarningsAsErrors> TEMPORARILY DISABLED -->'
                    )
                    
                    with open(proj_path, 'w', encoding='utf-8') as f:
                        f.write(modified_content)
                    modified_files.append(proj_path)
                    print(f"Disabled warnings-as-errors in: {proj_file}")
            except Exception as e:
                print(f"Error processing {proj_file}: {e}")
    
    return modified_files

def restore_warnings_as_errors(modified_files):
    """Restore warnings as errors"""
    for proj_path in modified_files:
        try:
            with open(proj_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            restored_content = content.replace(
                '<!-- <TreatWarningsAsErrors>true</TreatWarningsAsErrors> TEMPORARILY DISABLED -->',
                '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>'
            )
            
            with open(proj_path, 'w', encoding='utf-8') as f:
                f.write(restored_content)
            print(f"Restored warnings-as-errors in: {proj_path}")
        except Exception as e:
            print(f"Error restoring {proj_path}: {e}")

def get_current_errors():
    """Get current error counts"""
    cmd = ['dotnet', 'build', 'Src/IndTrace.sln', '-v:m']
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, cwd='.')
        output = result.stdout + result.stderr
        
        # Extract error patterns
        error_patterns = {
            'CS8602': r"error CS8602:",
            'CS8625': r"error CS8625:", 
            'CS8620': r"error CS8620:",
            'CS4014': r"error CS4014:",
            'CS8619': r"error CS8619:",
            'CS0414': r"error CS0414:",
            'CS8604': r"error CS8604:",
            'CS8601': r"error CS8601:",
            'CS1998': r"error CS1998:",
            'CS0252': r"error CS0252:",
        }
        
        error_counts = {}
        for error_code, pattern in error_patterns.items():
            count = len(re.findall(pattern, output))
            if count > 0:
                error_counts[error_code] = count
        
        return error_counts, output
        
    except Exception as e:
        print(f"Error running build: {e}")
        return {}, ""

def main():
    os.chdir(Path(__file__).parent.parent)
    print("Getting current error counts...")
    
    # Disable warnings as errors temporarily
    modified_files = disable_warnings_as_errors()
    
    try:
        # Get error counts
        error_counts, build_output = get_current_errors()
        
        print("\nCURRENT ERROR COUNTS:")
        print("=" * 40)
        
        total_errors = 0
        if error_counts:
            sorted_errors = sorted(error_counts.items(), key=lambda x: x[1], reverse=True)
            for error_code, count in sorted_errors:
                print(f"{error_code}: {count} errors")
                total_errors += count
        else:
            print("No errors found!")
        
        print(f"\nTOTAL ERRORS: {total_errors}")
        
        # Show some CS8602 examples
        if 'CS8602' in error_counts and error_counts['CS8602'] > 0:
            print(f"\nFirst few CS8602 examples:")
            cs8602_lines = [line for line in build_output.split('\n') if 'CS8602:' in line]
            for line in cs8602_lines[:5]:
                print(f"  {line.strip()}")
                
    finally:
        # Always restore warnings as errors
        restore_warnings_as_errors(modified_files)

if __name__ == '__main__':
    main()