#!/usr/bin/env python3
"""
Check what's causing build failures beyond warnings
"""

import subprocess
import re
import os
from pathlib import Path
import glob

def find_and_disable_warnings_as_errors():
    """Find and disable warnings as errors in all project files"""
    modified_files = []
    
    # Find all .csproj files
    csproj_pattern = "Src/**/*.csproj"
    project_files = glob.glob(csproj_pattern, recursive=True)
    
    # Exclude backup folders
    project_files = [f for f in project_files if '_backup' not in f]
    
    print(f"Processing {len(project_files)} project files (excluding backups)")
    
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
        except Exception as e:
            print(f"Error restoring {proj_path}: {e}")
    
    print(f"Restored warnings-as-errors in {len(modified_files)} files")

def check_build_issues():
    """Check what's causing build failures"""
    cmd = ['dotnet', 'build', 'Src/IndTrace.sln', '-v:n']
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, cwd='.')
        output = result.stdout + result.stderr
        
        print("BUILD OUTPUT ANALYSIS:")
        print("=" * 60)
        
        # Look for different types of issues
        lines = output.split('\n')
        
        # Package restore issues
        package_errors = [line for line in lines if 'NU1902' in line or 'package' in line.lower() and 'error' in line.lower()]
        if package_errors:
            print("PACKAGE/RESTORE ISSUES:")
            for error in package_errors[:5]:
                print(f"  {error.strip()}")
            print()
        
        # Compilation errors (not warnings)
        compilation_errors = [line for line in lines if 'error CS' in line and 'CS8' not in line and 'CS0252' not in line]
        if compilation_errors:
            print("COMPILATION ERRORS:")
            for error in compilation_errors[:5]:
                print(f"  {error.strip()}")
            print()
        
        # Other errors
        other_errors = [line for line in lines if 'error' in line.lower() and 'CS8' not in line and 'NU1902' not in line]
        if other_errors:
            print("OTHER ERRORS:")
            for error in other_errors[:5]:
                print(f"  {error.strip()}")
            print()
        
        # Success/failure summary
        success_lines = [line for line in lines if 'Build succeeded' in line or 'Build FAILED' in line]
        if success_lines:
            print("BUILD RESULT:")
            for line in success_lines:
                print(f"  {line.strip()}")
            print()
        
        # Check for warnings that might be treated as errors
        warning_patterns = {
            'CS8602': r"(warning|error) CS8602:",
            'CS8625': r"(warning|error) CS8625:", 
            'CS8620': r"(warning|error) CS8620:",
            'CS4014': r"(warning|error) CS4014:",
            'CS8619': r"(warning|error) CS8619:",
            'CS0414': r"(warning|error) CS0414:",
            'CS8604': r"(warning|error) CS8604:",
            'CS8601': r"(warning|error) CS8601:",
            'CS1998': r"(warning|error) CS1998:",
            'CS0252': r"(warning|error) CS0252:",
        }
        
        warning_counts = {}
        for warning_code, pattern in warning_patterns.items():
            matches = re.findall(pattern, output, re.IGNORECASE)
            if matches:
                warning_counts[warning_code] = len(matches)
        
        if warning_counts:
            print("WARNING/ERROR COUNTS:")
            total_warnings = 0
            for code, count in sorted(warning_counts.items(), key=lambda x: x[1], reverse=True):
                print(f"  {code}: {count}")
                total_warnings += count
            print(f"  TOTAL: {total_warnings}")
        else:
            print("NO COMPILATION WARNINGS/ERRORS FOUND!")
        
        return result.returncode == 0, output
        
    except Exception as e:
        print(f"Error running build: {e}")
        return False, ""

def main():
    os.chdir(Path(__file__).parent.parent)
    print("=== BUILD ISSUE ANALYSIS ===")
    
    # Disable warnings as errors temporarily
    modified_files = find_and_disable_warnings_as_errors()
    
    try:
        # Check build issues
        build_success, build_output = check_build_issues()
        
        print(f"\nFINAL BUILD SUCCESS: {build_success}")
        
        if build_success:
            print("\n*** PERFECT BUILD! ***")
            print("Build succeeded with warnings-as-errors disabled")
        else:
            print("\nBuild failed for reasons other than warnings")
            
    finally:
        # Always restore warnings as errors
        restore_warnings_as_errors(modified_files)

if __name__ == '__main__':
    main()