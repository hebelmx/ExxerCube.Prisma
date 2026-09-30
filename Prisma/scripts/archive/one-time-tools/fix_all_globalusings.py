#!/usr/bin/env python3
"""
Fix all GlobalUsings.cs files to include essential testing framework namespaces.
This script adds the missing testing framework imports that are causing 10,300+ errors.
"""

import os
import re
from pathlib import Path

def find_globalusings_files(base_path):
    """Find all GlobalUsings.cs files in test projects."""
    test_path = Path(base_path) / "code" / "src" / "tests"
    globalusings_files = list(test_path.glob("**/GlobalUsings.cs"))
    
    # Filter out backup files
    active_files = [f for f in globalusings_files if "smart_fix_backups" not in str(f)]
    
    print(f"Found {len(active_files)} active GlobalUsings.cs files in test projects:")
    for file in active_files:
        print(f"  {file}")
    
    return active_files

def has_testing_framework_imports(content):
    """Check if the file already has the essential testing framework imports."""
    required_imports = [
        "global using Xunit;",
        "global using NSubstitute;", 
        "global using Shouldly;",
        "global using Microsoft.Extensions.Logging;",
        "global using Meziantou.Extensions.Logging.Xunit.v3;"
    ]
    
    missing_imports = []
    for import_line in required_imports:
        if import_line not in content:
            missing_imports.append(import_line)
    
    return len(missing_imports) == 0, missing_imports

def add_testing_framework_imports(content):
    """Add essential testing framework imports to GlobalUsings.cs content."""
    
    # Essential testing framework imports to add
    testing_imports = """
// Testing framework namespaces
global using Xunit;
global using NSubstitute;
global using Shouldly;
global using Microsoft.Extensions.Logging;
global using Meziantou.Extensions.Logging.Xunit.v3;"""
    
    # Find where to insert the testing imports
    # Look for the existing "// Testing framework namespaces" section or add before IndQuestResults
    
    if "// Testing framework namespaces" in content:
        # Replace the existing testing framework section
        # Find the start and end of the testing framework section
        lines = content.split('\n')
        new_lines = []
        in_testing_section = False
        testing_section_added = False
        
        for line in lines:
            if line.strip() == "// Testing framework namespaces":
                if not testing_section_added:
                    new_lines.append(testing_imports.strip())
                    testing_section_added = True
                in_testing_section = True
                continue
            elif in_testing_section and (line.strip() == "" or line.startswith("// ") and "Testing framework" not in line):
                in_testing_section = False
                new_lines.append("")
                new_lines.append(line)
            elif not in_testing_section:
                new_lines.append(line)
        
        return '\n'.join(new_lines)
    else:
        # Find the IndQuestResults section and add testing imports before it
        indquest_pattern = r'(// Testing framework namespaces\s*\n)?(\s*global using IndQuestResults;)'
        
        if re.search(indquest_pattern, content):
            # Replace with proper testing framework section
            replacement = f"{testing_imports}\nglobal using IndQuestResults;"
            content = re.sub(indquest_pattern, replacement, content)
        else:
            # If no IndQuestResults found, add at the end
            content = content.rstrip() + f"\n{testing_imports}\nglobal using IndQuestResults;\nglobal using IndQuestResults.Operations;\nglobal using IndQuestResults.Validation;\n"
    
    return content

def fix_globalusings_file(file_path):
    """Fix a single GlobalUsings.cs file."""
    print(f"\nProcessing: {file_path}")
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        has_imports, missing_imports = has_testing_framework_imports(content)
        
        if has_imports:
            print("  ✅ Already has all required testing framework imports")
            return True
        
        print(f"  ❌ Missing {len(missing_imports)} testing framework imports:")
        for missing in missing_imports:
            print(f"    - {missing}")
        
        # Add the missing imports
        new_content = add_testing_framework_imports(content)
        
        # Write the updated content
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        
        print("  ✅ Fixed - Added missing testing framework imports")
        return True
        
    except Exception as e:
        print(f"  ❌ Error processing {file_path}: {e}")
        return False

def main():
    """Main function to fix all GlobalUsings.cs files."""
    base_path = "F:/Dynamic/ExxerAi/ExxerAI"
    
    print("🔧 Fixing all GlobalUsings.cs files to include essential testing framework namespaces...")
    print("=" * 80)
    
    # Find all GlobalUsings.cs files
    globalusings_files = find_globalusings_files(base_path)
    
    if not globalusings_files:
        print("❌ No GlobalUsings.cs files found!")
        return
    
    print(f"\n🚀 Processing {len(globalusings_files)} files...")
    
    successful = 0
    failed = 0
    
    for file_path in globalusings_files:
        if fix_globalusings_file(file_path):
            successful += 1
        else:
            failed += 1
    
    print("\n" + "=" * 80)
    print(f"✅ Successfully processed: {successful}")
    print(f"❌ Failed: {failed}")
    print(f"📊 Total: {len(globalusings_files)}")
    
    if failed == 0:
        print("\n🎉 All GlobalUsings.cs files have been fixed!")
        print("💡 This should resolve the 10,300+ compilation errors related to missing testing framework imports.")
    else:
        print(f"\n⚠️  {failed} files had issues and may need manual attention.")

if __name__ == "__main__":
    main()