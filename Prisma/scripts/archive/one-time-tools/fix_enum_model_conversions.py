#!/usr/bin/env python3
"""
Fix CS0315 - EnumModel conversion errors
Convert regular enums to inherit from EnumModel
"""

import os
from datetime import datetime

# Enums that need to be converted to EnumModel
enums_to_fix = [
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\05IntegrationTests\ExxerAI.Analytics.Integration.Test\PlaceholderTests.cs',
        'enum_name': 'TimeRange',
        'namespace': 'ExxerAI.Analytics.IntegrationTests',
        'values': ['Day', 'Week', 'Month', 'Year']
    },
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\03UnitTests\ExxerAI.Configuration.Test\ConfigurationValidationTests.cs', 
        'enum_name': 'EnvironmentType',
        'namespace': 'ExxerAI.Configuration.Tests',
        'values': ['Development', 'Staging', 'Production']
    }
]

def create_enum_model_class(file_path, enum_name, namespace, values):
    """Create an EnumModel-based enumeration class"""
    
    # Find the namespace declaration and insert after it
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Find where to insert the new enum class
        insert_index = -1
        namespace_found = False
        
        for i, line in enumerate(lines):
            if f'namespace {namespace}' in line:
                namespace_found = True
                # Check if it's file-scoped namespace (ends with ;)
                if ';' in line:
                    insert_index = i + 2  # Insert after the namespace line and blank line
                elif '{' in line:
                    insert_index = i + 1
                else:
                    # Look for opening brace on next lines
                    for j in range(i+1, min(i+5, len(lines))):
                        if '{' in lines[j]:
                            insert_index = j + 1
                            break
                break
        
        if insert_index == -1:
            print(f"  ⚠ Could not find namespace {namespace} in {os.path.basename(file_path)}")
            return False
        
        # Generate the EnumModel class
        enum_class = f'''
/// <summary>
/// {enum_name} enumeration for testing.
/// </summary>
public sealed class {enum_name} : EnumModel
{{
    private {enum_name}(int value, string name) : base(value, name) {{ }}

'''
        
        # Add static properties for each value
        for i, value in enumerate(values):
            enum_class += f'    public static readonly {enum_name} {value} = new({i + 1}, nameof({value}));\n'
        
        enum_class += '\n'
        
        # Add GetAll method
        enum_class += f'    public static IEnumerable<{enum_name}> GetAll() => GetAll<{enum_name}>();\n'
        enum_class += '}\n\n'
        
        # Insert the enum class
        lines.insert(insert_index, enum_class)
        
        # Write back the file
        with open(file_path, 'w', encoding='utf-8') as f:
            f.writelines(lines)
        
        print(f"  ✓ Created {enum_name} EnumModel class")
        return True
        
    except Exception as e:
        print(f"  ✗ Error creating enum model: {e}")
        return False

def fix_enum_usages(file_path, enum_name):
    """Fix usages of the enum to use nameof"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Common patterns to fix
        replacements = [
            # InlineData with enum values
            (f'[InlineData({enum_name}.', f'[InlineData(nameof({enum_name}.'),
            # Direct enum value usage in FromName
            (f'FromName<{enum_name}>({enum_name}.', f'FromName<{enum_name}>(nameof({enum_name}.)'),
        ]
        
        modified = False
        for old_pattern, new_pattern in replacements:
            if old_pattern in content:
                # Find all occurrences and fix them
                import re
                pattern = re.escape(old_pattern) + r'(\w+)'
                replacement = new_pattern + r'\1)'
                content = re.sub(pattern, replacement, content)
                modified = True
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"  ✓ Fixed {enum_name} usages to use nameof()")
            return True
            
    except Exception as e:
        print(f"  ✗ Error fixing enum usages: {e}")
        return False

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING CS0315 ENUM MODEL CONVERSION ERRORS")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    fixed_count = 0
    
    for enum_info in enums_to_fix:
        file_path = enum_info['file']
        if not os.path.exists(file_path):
            print(f"⚠ File not found: {os.path.basename(file_path)}")
            continue
        
        print(f"Fixing {enum_info['enum_name']} in {os.path.basename(file_path)}:")
        
        # Create the EnumModel class
        if create_enum_model_class(file_path, enum_info['enum_name'], 
                                   enum_info['namespace'], enum_info['values']):
            fixed_count += 1
            
            # Fix the usages
            if fix_enum_usages(file_path, enum_info['enum_name']):
                fixed_count += 1
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Fixed {fixed_count} enum model issues")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    main()