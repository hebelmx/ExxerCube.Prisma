#!/usr/bin/env python3
"""
Test the CS8620 fixer with sample errors
"""
import os
import sys
from pathlib import Path

# Set protection environment
os.environ['AUTOMATION_MANAGER_ACTIVE'] = 'true' 
os.environ['AUTOMATION_SESSION_TOKEN'] = 'test_cs8620_' + '0' * 32
os.environ['AUTOMATION_SESSION_ID'] = 'test_cs8620_session'

sys.path.insert(0, 'automation')

def test_cs8620_fixer():
    """Test CS8620 fixer with sample from build output"""
    print("=== TESTING CS8620 FIXER ===")
    
    from build_targeted_cs8620_fixer import BuildTargetedCS8620Fixer
    
    # Use actual build output
    build_output = Path("build_after_cs8625.txt").read_text()
    
    # Create fixer
    fixer = BuildTargetedCS8620Fixer()
    errors = fixer.parse_build_output(build_output)
    
    print(f"Found {len(errors)} CS8620 errors")
    
    # Test with first 3 errors
    sample_errors = errors[:3]
    print(f"\nTesting with first {len(sample_errors)} errors:")
    
    for i, error in enumerate(sample_errors):
        print(f"\n{i+1}. {error.file_path}:{error.line}:{error.column}")
        
        # Check if file exists and get the line
        file_path = Path(error.file_path)
        if file_path.exists():
            try:
                lines = file_path.read_text().split('\n')
                if error.line <= len(lines):
                    error_line = lines[error.line - 1]
                    print(f"   Line: '{error_line.strip()}'")
                    
                    # Test type extraction
                    type_info = fixer.extract_mismatch_types_from_message(error.message)
                    if type_info:
                        print(f"   Source: {type_info['source_type']}")
                        print(f"   Target: {type_info['target_type']}")
                        print(f"   Needs fix: {type_info['needs_nullability_change']}")
                        
                        # Test line matching
                        line_match = fixer.find_generic_type_in_lines(lines, error.line - 1, error.column)
                        if line_match:
                            print(f"   Found: '{line_match['match']}' at pos {line_match['start']}")
                            
                            # Test fix generation
                            fix_result = fixer.fix_generic_nullability(lines, type_info, line_match)
                            if fix_result:
                                modified_lines, fix_description = fix_result
                                print(f"   Fix: {fix_description}")
                            else:
                                print("   Could not generate fix")
                        else:
                            print("   Could not find generic type in line")
                    else:
                        print("   Could not extract type info")
                else:
                    print("   Line number out of range")
            except Exception as e:
                print(f"   Error reading file: {e}")
        else:
            print("   File not found")

if __name__ == "__main__":
    test_cs8620_fixer()