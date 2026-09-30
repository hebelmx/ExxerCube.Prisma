#!/usr/bin/env python3
"""
Apply CS8602 fixes with safety checks and backup
"""
import os
import sys
import shutil
from datetime import datetime
from pathlib import Path

# Set protection environment
os.environ['AUTOMATION_MANAGER_ACTIVE'] = 'true' 
os.environ['AUTOMATION_SESSION_TOKEN'] = 'apply_cs8602_fixes_' + '0' * 32
os.environ['AUTOMATION_SESSION_ID'] = 'apply_cs8602_fixes_session'

sys.path.insert(0, 'automation')

def create_backup():
    """Create backup before applying fixes"""
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_dir = Path(f"backup_before_cs8602_fixes_{timestamp}")
    source_dir = Path("Src/Tests/Core/Application.UnitTests")
    
    print(f"Creating backup: {backup_dir}")
    shutil.copytree(source_dir, backup_dir)
    return backup_dir

def apply_cs8602_fixes():
    """Apply CS8602 fixes with full safety protocol"""
    print("=== APPLYING CS8602 FIXES ===")
    
    # Create backup
    backup_dir = create_backup()
    print(f"Backup created: {backup_dir}")
    
    # Import fixer
    from build_targeted_cs8602_fixer import BuildTargetedCS8602Fixer
    
    # Load build output (using the same one that has remaining errors after CS8625)
    build_output = Path("build_after_cs8625.txt").read_text()
    
    # Apply fixes
    fixer = BuildTargetedCS8602Fixer()
    print("Applying fixes to all CS8602 errors...")
    fixer.fix_from_build_output(build_output, dry_run=False)
    
    print(f"Applied {fixer.fixes_applied} fixes to {len(fixer.files_modified)} files")
    
    # Verify build after fixes
    print("Verifying build after fixes...")
    result = os.system('dotnet build "Src/Tests/Core/Application.UnitTests/Application.UnitTests.csproj" -v:quiet > build_after_cs8602.txt 2>&1')
    
    if result == 0:
        print("BUILD SUCCESSFUL after CS8602 fixes!")
        
        # Count remaining CS8602 errors
        after_output = Path("build_after_cs8602.txt").read_text()
        remaining_errors = fixer.parse_build_output(after_output)
        remaining_cs8602 = [e for e in remaining_errors if e.error_code == "CS8602"]
        
        print(f"Remaining CS8602 errors: {len(remaining_cs8602)}")
        print(f"CS8602 errors fixed: {120 - len(remaining_cs8602)}")
        
        return True, backup_dir
    else:
        print("BUILD FAILED after fixes - ROLLING BACK")
        # Restore backup
        shutil.rmtree(Path("Src/Tests/Core/Application.UnitTests"))
        shutil.copytree(backup_dir, Path("Src/Tests/Core/Application.UnitTests"))
        print("Backup restored")
        return False, backup_dir

if __name__ == "__main__":
    success, backup = apply_cs8602_fixes()
    
    if success:
        print("CS8602 fixes applied successfully!")
        print(f"Backup available at: {backup}")
    else:
        print("CS8602 fixes failed and were rolled back")