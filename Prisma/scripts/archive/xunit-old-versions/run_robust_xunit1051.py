#!/usr/bin/env python3
"""
Simple runner for robust XUnit1051 fixer with safety checks
"""

import subprocess
import sys
from pathlib import Path

def run_build_test(project_path=None):
    """Test build to check for XUnit1051 errors"""
    if project_path:
        full_project_path = f'code/src/{project_path}'
        print(f"Checking for XUnit1051 errors in {project_path}...")
    else:
        full_project_path = 'Src/Tests/Core/Application.UnitTests/Application.UnitTests.csproj'
        print("Checking for XUnit1051 errors in default project...")
    
    cmd = ['dotnet', 'build', full_project_path, '-v:n', '--no-restore']
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, cwd='.')
        output = result.stdout + result.stderr
        
        # Count XUnit1051 errors
        xunit1051_count = output.lower().count('xunit1051')
        print(f"Found {xunit1051_count} XUnit1051 errors")
        
        # Show sample errors
        lines = output.split('\n')
        xunit_lines = [line for line in lines if 'xunit1051' in line.lower()]
        
        if xunit_lines:
            print("Sample errors:")
            for line in xunit_lines[:3]:
                print(f"  {line.strip()}")
                
        return xunit1051_count > 0
        
    except Exception as e:
        print(f"Error running build: {e}")
        return False

def main():
    import argparse
    parser = argparse.ArgumentParser(description="Robust XUnit1051 Fixer Runner")
    parser.add_argument("project_path", nargs="?", help="Project path to process")
    parser.add_argument("--dry-run", action="store_true", help="Perform dry run analysis only")
    
    args = parser.parse_args()
    
    print("=" * 60)
    print("Robust XUnit1051 Fixer - Safe Execution")  
    print("=" * 60)
    
    # Check if XUnit1051 errors exist
    has_errors = run_build_test(args.project_path)
    
    if not has_errors:
        print("No XUnit1051 errors found!")
        return
    
    if args.dry_run:
        print("\n=== DRY RUN MODE ===")
        print("Running fixer in dry-run mode...")
        try:
            cmd = ["python", "scripts/robust_xunit1051_fixer.py"]
            if args.project_path:
                cmd.append(args.project_path)
            cmd.append("--dry-run")
            
            result = subprocess.run(cmd, cwd='.', timeout=300)
            print("Dry run completed.")
            
        except Exception as e:
            print(f"Dry run failed: {e}")
        return
    
    # Ask for confirmation in live mode
    print(f"\nRunning robust XUnit1051 fixer for: {args.project_path or 'default project'}")
    
    try:
        response = input("Continue? (yes/no): ")
        if response.lower() != "yes":
            print("Aborted.")
            return
    except (EOFError, KeyboardInterrupt):
        # Non-interactive environment, auto-proceed for automation
        print("Non-interactive mode detected - proceeding with automation")
        
    # Run the fixer
    try:
        print("Executing robust_xunit1051_fixer.py...")
        cmd = ["python", "scripts/robust_xunit1051_fixer.py"]
        if args.project_path:
            cmd.append(args.project_path)
            
        result = subprocess.run(cmd, cwd='.', timeout=300)
        
        if result.returncode == 0:
            print("Fixer completed successfully!")
            
            # Check results
            print("Verifying results...")
            run_build_test(args.project_path)
            
        else:
            print(f"Fixer failed with return code: {result.returncode}")
            
    except subprocess.TimeoutExpired:
        print("Fixer timed out after 5 minutes")
    except Exception as e:
        print(f"Error running fixer: {e}")

if __name__ == "__main__":
    main()