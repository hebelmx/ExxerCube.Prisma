#!/usr/bin/env python3
"""
Full dry-run validation of all CS8620 errors
"""
import os
import sys
from pathlib import Path

# Set protection environment
os.environ['AUTOMATION_MANAGER_ACTIVE'] = 'true' 
os.environ['AUTOMATION_SESSION_TOKEN'] = 'full_cs8620_dry_run_' + '0' * 32
os.environ['AUTOMATION_SESSION_ID'] = 'full_cs8620_dry_run_session'

sys.path.insert(0, 'automation')

def full_cs8620_dry_run():
    """Full dry-run validation of all CS8620 errors"""
    print("=== FULL CS8620 DRY-RUN VALIDATION ===")
    
    from build_targeted_cs8620_fixer import BuildTargetedCS8620Fixer
    
    # Read build output
    build_output = Path("build_after_cs8625.txt").read_text()
    
    # Create fixer and run full dry-run
    fixer = BuildTargetedCS8620Fixer()
    fixer.fix_from_build_output(build_output, dry_run=True)
    
    # Generate summary
    successful = len(fixer.dry_run_results)
    total_errors = len(fixer.parse_build_output(build_output))
    failed = total_errors - successful
    
    print(f"\n=== FULL CS8620 DRY-RUN RESULTS ===")
    print(f"Total CS8620 errors: {total_errors}")
    print(f"Successful validations: {successful}")
    print(f"Failed validations: {failed}")
    print(f"Success rate: {successful/total_errors*100:.1f}%")
    
    # Show sample fixes
    if fixer.dry_run_results:
        print(f"\nSample fixes:")
        for i, fix in enumerate(fixer.dry_run_results[:5]):
            print(f"{i+1}. {fix['file']}:{fix['line']}")
            print(f"   Original: {fix['original']}")
            print(f"   Fixed:    {fix['fixed']}")
            print(f"   Desc:     {fix['fix_description']}")
    
    # Save detailed results
    with open("cs8620_full_dry_run_results.txt", "w") as f:
        f.write(f"CS8620 Full Dry-Run Results\n")
        f.write(f"==========================\n")
        f.write(f"Total errors: {total_errors}\n")
        f.write(f"Successful: {successful}\n")
        f.write(f"Failed: {failed}\n")
        f.write(f"Success rate: {successful/total_errors*100:.1f}%\n\n")
        
        for fix in fixer.dry_run_results:
            f.write(f"{fix['file']}:{fix['line']} - {fix['fix_description']}\n")
    
    return successful >= total_errors * 0.90  # 90% success threshold

if __name__ == "__main__":
    success = full_cs8620_dry_run()
    print(f"Full dry-run result: {'PASS' if success else 'NEEDS_INVESTIGATION'}")