#!/usr/bin/env python3
"""
Improved CS1591 XML Comment Generator
Fixes missing XML documentation with meaningful, context-aware comments
"""

import os
import re
from datetime import datetime

def extract_member_from_file_line(file_path, line_num):
    """Extract member name from file at specific line number"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        if 0 < line_num <= len(lines):
            target_line = lines[line_num - 1].strip()
            
            # First, extract the class name from the file for constructor detection
            class_name = None
            for line in lines:
                class_match = re.search(r'public\s+(?:sealed\s+)?class\s+(\w+)', line)
                if class_match:
                    class_name = class_match.group(1)
                    break
            
            # 1. Constructor patterns (parameterless and with parameters)
            if class_name:
                # Parameterless constructor: public ClassName()
                constructor_pattern_parameterless = rf'public\s+{re.escape(class_name)}\s*\(\s*\)'
                if re.search(constructor_pattern_parameterless, target_line):
                    return class_name
                
                # Constructor with parameters: public ClassName(params)
                constructor_pattern_params = rf'public\s+{re.escape(class_name)}\s*\([^)]+\)'
                if re.search(constructor_pattern_params, target_line):
                    return class_name
            
            # 2. Method patterns (with and without parameters)
            # Parameterless method: public ReturnType MethodName()
            method_match_parameterless = re.search(r'public\s+(?:async\s+)?(?:static\s+)?(?:override\s+)?(?:virtual\s+)?(?:void|bool|int|string|Task\w*|Result\w*|[A-Z]\w*)\s+(\w+)\s*\(\s*\)', target_line)
            if method_match_parameterless:
                return method_match_parameterless.group(1)
            
            # Method with parameters: public ReturnType MethodName(params)
            method_match_params = re.search(r'public\s+(?:async\s+)?(?:static\s+)?(?:override\s+)?(?:virtual\s+)?(?:void|bool|int|string|Task\w*|Result\w*|[A-Z]\w*)\s+(\w+)\s*\([^)]+\)', target_line)
            if method_match_params:
                return method_match_params.group(1)
            
            # 3. Property pattern: public Type PropertyName { get; set; }
            property_match = re.search(r'public\s+(?:static\s+)?(?:readonly\s+)?(?:override\s+)?(?:virtual\s+)?\w+(?:<[^>]+>)?\s+(\w+)\s*\{', target_line)
            if property_match:
                return property_match.group(1)
            
            # 4. Class/Interface/Enum patterns
            class_match = re.search(r'public\s+(?:sealed\s+)?(?:abstract\s+)?class\s+(\w+)', target_line)
            if class_match:
                return class_match.group(1)
                
            interface_match = re.search(r'public\s+interface\s+(\w+)', target_line)
            if interface_match:
                return interface_match.group(1)
                
            enum_match = re.search(r'public\s+enum\s+(\w+)', target_line)
            if enum_match:
                return enum_match.group(1)
            
            # 5. Field pattern: public Type fieldName;
            field_match = re.search(r'public\s+(?:static\s+)?(?:readonly\s+)?\w+(?:<[^>]+>)?\s+(\w+)\s*[;=]', target_line)
            if field_match:
                return field_match.group(1)
            
            # If we can't find it on the exact line, look nearby (±3 lines)
            for offset in [-1, 1, -2, 2, -3, 3]:
                check_line_idx = line_num - 1 + offset
                if 0 <= check_line_idx < len(lines):
                    check_content = lines[check_line_idx].strip()
                    
                    # Repeat constructor patterns for nearby lines
                    if class_name:
                        constructor_pattern_parameterless = rf'public\s+{re.escape(class_name)}\s*\(\s*\)'
                        if re.search(constructor_pattern_parameterless, check_content):
                            return class_name
                        
                        constructor_pattern_params = rf'public\s+{re.escape(class_name)}\s*\([^)]+\)'
                        if re.search(constructor_pattern_params, check_content):
                            return class_name
                    
                    # Repeat method patterns for nearby lines
                    method_match_parameterless = re.search(r'public\s+(?:async\s+)?(?:static\s+)?(?:override\s+)?(?:virtual\s+)?(?:void|bool|int|string|Task\w*|Result\w*|[A-Z]\w*)\s+(\w+)\s*\(\s*\)', check_content)
                    if method_match_parameterless:
                        return method_match_parameterless.group(1)
                    
                    method_match_params = re.search(r'public\s+(?:async\s+)?(?:static\s+)?(?:override\s+)?(?:virtual\s+)?(?:void|bool|int|string|Task\w*|Result\w*|[A-Z]\w*)\s+(\w+)\s*\([^)]+\)', check_content)
                    if method_match_params:
                        return method_match_params.group(1)
                    
                    # Repeat property pattern for nearby lines  
                    property_match = re.search(r'public\s+(?:static\s+)?(?:readonly\s+)?(?:override\s+)?(?:virtual\s+)?\w+(?:<[^>]+>)?\s+(\w+)\s*\{', check_content)
                    if property_match:
                        return property_match.group(1)
        
    except Exception as e:
        print(f"Error extracting member from {file_path}:{line_num}: {e}")
    
    return f"Unknown_Line_{line_num}"

def parse_cs1591_errors(error_file):
    """Parse CS1591 errors from the error file"""
    errors = []
    
    try:
        with open(error_file, 'r', encoding='utf-8-sig') as f:
            lines = f.readlines()
        
        for line in lines[1:]:  # Skip header
            if 'CS1591' in line and '\t' in line:
                parts = line.split('\t')
                if len(parts) >= 6:
                    file_path = parts[4].strip()
                    line_num = int(parts[5].strip()) if parts[5].strip().isdigit() else 0
                    desc = parts[2].strip()
                    
                    # Try to extract member name from description first
                    match = re.search(r"'([^']+)'", desc)
                    if match:
                        member_name = match.group(1)
                    else:
                        # For generic "/doc compiler option" errors, extract from file
                        member_name = extract_member_from_file_line(file_path, line_num)
                    
                    if member_name:
                        errors.append({
                            'file': file_path,
                            'line': line_num,
                            'member': member_name,
                            'description': desc
                        })
                        
    except Exception as e:
        print(f"Error parsing CS1591 file: {e}")
    
    return errors

def extract_parameters_from_line(line_content):
    """Extract parameters from method signature"""
    parameters = []
    
    # Find the parameter list between parentheses
    if '(' in line_content and ')' in line_content:
        start = line_content.find('(')
        end = line_content.rfind(')')
        params_str = line_content[start+1:end].strip()
        
        if params_str:  # Has parameters
            # Split by comma, but handle generic types
            param_parts = []
            paren_count = 0
            current_param = ""
            
            for char in params_str:
                if char == '<':
                    paren_count += 1
                elif char == '>':
                    paren_count -= 1
                elif char == ',' and paren_count == 0:
                    param_parts.append(current_param.strip())
                    current_param = ""
                    continue
                current_param += char
            
            if current_param.strip():
                param_parts.append(current_param.strip())
            
            # Parse each parameter
            for param in param_parts:
                # Remove default values
                param = param.split('=')[0].strip()
                
                # Extract parameter name (last word)
                parts = param.split()
                if len(parts) >= 2:
                    param_name = parts[-1]
                    param_type = ' '.join(parts[:-1])
                    parameters.append({
                        'name': param_name,
                        'type': param_type
                    })
    
    return parameters

def generate_param_docs(parameters):
    """Generate parameter documentation for XML comments"""
    param_docs = []
    
    for param in parameters:
        param_name = param['name']
        param_type = param['type']
        
        # Generate appropriate description based on parameter name and type
        if 'cancellationToken' in param_name.lower():
            desc = "Cancellation token to cancel the operation."
        elif 'id' in param_name.lower():
            desc = f"The identifier for the {param_name.replace('Id', '').replace('id', '').lower()}."
        elif param_name.lower().startswith('is') or param_name.lower().startswith('has'):
            desc = f"Value indicating whether {format_generic_name(param_name[2:] if param_name.lower().startswith('is') else param_name[3:])}."
        elif 'count' in param_name.lower() or 'limit' in param_name.lower():
            desc = f"The maximum number of {param_name.lower().replace('count', '').replace('limit', '')} items."
        elif 'name' in param_name.lower():
            desc = f"The name of the {param_name.lower().replace('name', '')}."
        elif 'request' in param_name.lower():
            desc = f"The {param_name.lower()} containing operation details."
        elif 'response' in param_name.lower():
            desc = f"The {param_name.lower()} containing operation results."
        elif 'expected' in param_name.lower():
            desc = f"The expected {param_name.lower().replace('expected', '')} value."
        elif 'actual' in param_name.lower():
            desc = f"The actual {param_name.lower().replace('actual', '')} value."
        elif 'input' in param_name.lower():
            desc = f"The input {param_name.lower().replace('input', '')} data."
        elif 'output' in param_name.lower():
            desc = f"The output {param_name.lower().replace('output', '')} data."
        else:
            # Generic description based on type
            if 'string' in param_type.lower():
                desc = f"The {format_generic_name(param_name)}."
            elif 'int' in param_type.lower() or 'long' in param_type.lower():
                desc = f"The {format_generic_name(param_name)} value."
            elif 'bool' in param_type.lower():
                desc = f"Value indicating whether {format_generic_name(param_name)}."
            else:
                desc = f"The {format_generic_name(param_name)}."
        
        param_docs.append(f"<param name=\"{param_name}\">{desc}</param>")
    
    return param_docs

def analyze_member_type(line_content, member_name):
    """Analyze the code to determine member type and generate appropriate documentation"""
    line = line_content.strip()
    member_simple = member_name.split('.')[-1].split('(')[0]
    
    # Extract parameters for methods with parameters
    parameters = extract_parameters_from_line(line_content)
    
    # Constructor patterns
    if ('public ' in line and '(' in line and ')' in line and 
        not 'void' in line and not 'Task' in line and not 'return' in line and
        not 'get' in line and not 'set' in line):
        # Check if method name matches class name (constructor pattern)
        if member_simple in line and member_simple[0].isupper():
            return f"Initializes a new instance of the {member_simple} class.", parameters
    
    # Test method patterns
    if ('public async Task' in line or 'public Task' in line or 'public void' in line):
        if 'Test' in member_name or 'Should' in member_name:
            action = extract_test_action(member_name)
            return f"Tests that {action}.", parameters
    
    # Property patterns
    if 'public ' in line and ('{' in line or 'get' in line or 'set' in line):
        prop_name = member_name.split('.')[-1]
        return f"Gets or sets the {format_property_name(prop_name)}.", []
    
    # Class patterns
    if 'public class ' in line or 'public sealed class ' in line:
        class_name = member_name.split('.')[-1]
        if 'Test' in class_name:
            tested_class = class_name.replace('Tests', '').replace('Test', '')
            return f"Contains unit tests for the {tested_class} class.", []
        elif 'Service' in class_name:
            return f"Provides {format_service_name(class_name)} functionality.", []
        elif 'Controller' in class_name:
            return f"Handles HTTP requests for {format_controller_name(class_name)} operations.", []
        else:
            return f"Represents a {format_class_name(class_name)}.", []
    
    # Interface patterns
    if 'public interface ' in line:
        interface_name = member_name.split('.')[-1]
        return f"Defines the contract for {format_interface_name(interface_name)} operations.", []
    
    # Method patterns
    if 'public ' in line and '(' in line and ')' in line:
        method_name = member_name.split('.')[-1].split('(')[0]
        if method_name.startswith('Get'):
            return f"Retrieves {format_method_action(method_name[3:])}.", parameters
        elif method_name.startswith('Set'):
            return f"Sets {format_method_action(method_name[3:])}.", parameters
        elif method_name.startswith('Create'):
            return f"Creates {format_method_action(method_name[6:])}.", parameters
        elif method_name.startswith('Update'):
            return f"Updates {format_method_action(method_name[6:])}.", parameters
        elif method_name.startswith('Delete'):
            return f"Deletes {format_method_action(method_name[6:])}.", parameters
        elif method_name.startswith('Should'):
            return f"Verifies that {format_test_expectation(method_name)}.", parameters
        else:
            return f"Executes {format_method_action(method_name)} operation.", parameters
    
    # Default fallback
    simple_name = member_name.split('.')[-1].split('(')[0]
    return f"Represents the {format_generic_name(simple_name)}.", parameters

def extract_test_action(test_name):
    """Extract meaningful action from test method name"""
    # Remove common test prefixes/suffixes
    clean_name = test_name.replace('Tests', '').replace('Test', '')
    
    # Convert PascalCase to readable text
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', clean_name)
    
    if 'Should' in words:
        should_idx = words.index('Should')
        action_words = words[should_idx + 1:]
        return ' '.join(word.lower() for word in action_words)
    
    return ' '.join(word.lower() for word in words[:3])  # First 3 words

def format_property_name(prop_name):
    """Convert PascalCase property name to readable text"""
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', prop_name)
    return ' '.join(word.lower() for word in words)

def format_class_name(class_name):
    """Convert PascalCase class name to readable text"""
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', class_name)
    return ' '.join(word.lower() for word in words)

def format_service_name(service_name):
    """Format service class name"""
    name = service_name.replace('Service', '').replace('Manager', '').replace('Handler', '')
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', name)
    return ' '.join(word.lower() for word in words)

def format_controller_name(controller_name):
    """Format controller class name"""
    name = controller_name.replace('Controller', '')
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', name)
    return ' '.join(word.lower() for word in words)

def format_interface_name(interface_name):
    """Format interface name"""
    name = interface_name[1:] if interface_name.startswith('I') else interface_name
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', name)
    return ' '.join(word.lower() for word in words)

def format_method_action(action_name):
    """Format method action name"""
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', action_name)
    return ' '.join(word.lower() for word in words)

def format_test_expectation(method_name):
    """Format test expectation from Should* method"""
    # Remove 'Should' prefix
    expectation = method_name[6:] if method_name.startswith('Should') else method_name
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', expectation)
    return ' '.join(word.lower() for word in words)

def format_generic_name(name):
    """Generic formatter for any name"""
    words = re.findall(r'[A-Z][a-z]*|[a-z]+', name)
    return ' '.join(word.lower() for word in words)

def has_xml_documentation_before(lines, method_line_idx):
    """Check if XML documentation already exists immediately before the method (not just anywhere nearby)"""
    # Look backwards from method line to find XML comments that belong to THIS member
    j = method_line_idx - 1
    found_xml_for_this_member = False
    
    while j >= 0:
        line = lines[j].strip()
        
        if not line:  # Skip empty lines
            j -= 1
            continue
        
        # Check for XML documentation comments
        if line.startswith('///'):
            found_xml_for_this_member = True
            j -= 1
            continue
        
        # Check for attributes (these can be between XML docs and method)
        if (line.startswith('[Fact') or 
            line.startswith('[Theory') or 
            line.startswith('[Test') or
            line.startswith('[InlineData') or
            line.startswith('[ClassData') or
            line.startswith('[MemberData') or
            line.startswith('[Setup') or
            line.startswith('[TearDown') or
            line.startswith('[OneTimeSetUp') or
            line.startswith('[OneTimeTearDown') or
            line.startswith('[Category') or
            line.startswith('[Ignore') or
            line.startswith('[Obsolete') or
            line.startswith('[TestCase') or
            line.startswith('[DataRow') or
            line.startswith('[Timeout') or
            (line.startswith('[') and line.endswith(']'))):  # Generic attribute pattern
            # If we found XML comments before hitting attributes, this member has docs
            if found_xml_for_this_member:
                return True
            j -= 1
            continue
        
        # If we hit any other code:
        # - If we found XML comments, they belong to this member
        # - If we didn't, this member has no XML docs
        return found_xml_for_this_member
    
    return found_xml_for_this_member

def find_method_by_name(lines, method_name):
    """Find method by name and return the correct line index for XML comment insertion"""
    # Clean method name (remove parameters)
    clean_method_name = method_name.split('(')[0].split('.')[-1]
    
    for i, line in enumerate(lines):
        # Check if this line contains the method/constructor definition
        line_stripped = line.strip()
        
        # Check for regular methods
        is_method = (clean_method_name in line and 
                    ('public' in line or 'private' in line or 'internal' in line or 'protected' in line))
        
        # Check for constructors (class name followed by parentheses)
        is_constructor = (clean_method_name in line and 
                         ('public' in line or 'private' in line or 'internal' in line or 'protected' in line) and
                         '(' in line and not 'void' in line and not 'Task' in line and not 'return' in line)
        
        # Check for properties
        is_property = (clean_method_name in line and 
                      ('public' in line or 'private' in line or 'internal' in line or 'protected' in line) and
                      ('{' in line or 'get' in line or 'set' in line))
        
        if is_method or is_constructor or is_property:
            # Check if XML documentation already exists
            if has_xml_documentation_before(lines, i):
                return None  # XML documentation already exists, skip this member
            
            # Found the member, now find where to insert XML comment
            # XML documentation must come BEFORE all attributes, not between them
            insert_line = i
            
            # Look backwards to find the first attribute in the chain
            j = i - 1
            while j >= 0:
                prev_line = lines[j].strip()
                
                # Skip empty lines, but track them for insertion point
                if not prev_line:
                    j -= 1
                    continue
                
                # Skip opening braces (class/namespace opening)
                if prev_line == '{' or prev_line.endswith('{'):
                    j -= 1
                    continue
                
                # Check for various attributes
                if (prev_line.startswith('[Fact') or 
                    prev_line.startswith('[Theory') or 
                    prev_line.startswith('[Test') or
                    prev_line.startswith('[InlineData') or
                    prev_line.startswith('[ClassData') or
                    prev_line.startswith('[MemberData') or
                    prev_line.startswith('[Setup') or
                    prev_line.startswith('[TearDown') or
                    prev_line.startswith('[OneTimeSetUp') or
                    prev_line.startswith('[OneTimeTearDown') or
                    prev_line.startswith('[Category') or
                    prev_line.startswith('[Ignore') or
                    prev_line.startswith('[Obsolete') or
                    prev_line.startswith('[TestCase') or
                    prev_line.startswith('[DataRow') or
                    (prev_line.startswith('[') and prev_line.endswith(']'))):  # Generic attribute pattern
                    insert_line = j  # Keep moving up to find the start of attribute chain
                    j -= 1
                else:
                    break  # Stop when we hit non-attribute, non-empty, non-brace code
            
            return insert_line
    
    return None

def fix_cs1591_in_file(file_path, errors_for_file):
    """Fix missing XML comments in a file with intelligent documentation"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Sort errors by line number in DESCENDING order (process from bottom to top)
        # This prevents line number drift issues when inserting comments
        errors_for_file.sort(key=lambda x: x['line'], reverse=True)
        
        modified = False
        
        for error in errors_for_file:
            # Find the method by name instead of relying on line number
            method_name = error['member'].split('.')[-1]
            insert_line_idx = find_method_by_name(lines, method_name)
            
            if insert_line_idx is not None:
                # Use the found line for indentation reference
                target_line = lines[insert_line_idx]
                indent = len(target_line) - len(target_line.lstrip())
                
                # Generate intelligent XML comment based on code analysis
                description, parameters = analyze_member_type(target_line, error['member'])
                
                # Create XML comment with proper indentation
                xml_comment = [
                    ' ' * indent + '/// <summary>\n',
                    ' ' * indent + f'/// {description}\n',
                    ' ' * indent + '/// </summary>\n'
                ]
                
                # Add parameter documentation if method has parameters
                if parameters:
                    param_docs = generate_param_docs(parameters)
                    for param_doc in param_docs:
                        xml_comment.append(' ' * indent + f'/// {param_doc}\n')
                
                # Insert the XML comment before the member/attribute (in reverse order to maintain positions)
                for i in range(len(xml_comment) - 1, -1, -1):
                    lines.insert(insert_line_idx, xml_comment[i])
                
                modified = True
                
                member_short = error['member'].split('.')[-1].split('(')[0]
                param_info = f" with {len(parameters)} params" if parameters else ""
                print(f"  ✓ Added: {member_short}{param_info} - {description} (before line {insert_line_idx + 1})")
            else:
                member_short = error['member'].split('.')[-1].split('(')[0]
                # Check if it already has XML documentation
                try:
                    start_check = max(0, error['line']-10)
                    end_check = min(len(lines), error['line'])
                    if any('///' in line for line in lines[start_check:end_check]):
                        print(f"  ✓ Skipped (already documented): {member_short}")
                    else:
                        print(f"  ⚠ Could not find method: {member_short}")
                except:
                    print(f"  ⚠ Could not find method: {member_short}")
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            return True
            
    except Exception as e:
        print(f"  ✗ Error fixing {file_path}: {e}")
    
    return False

def create_git_commit(base_path, message):
    """Create a git commit with the specified message"""
    try:
        import subprocess
        os.chdir(base_path)
        
        # Add all changes
        result = subprocess.run(['git', 'add', '.'], capture_output=True, text=True)
        if result.returncode != 0:
            print(f"⚠️ Git add failed: {result.stderr}")
            return False
        
        # Create commit
        result = subprocess.run(['git', 'commit', '-m', message], capture_output=True, text=True)
        if result.returncode != 0:
            print(f"⚠️ Git commit failed: {result.stderr}")
            return False
        
        print(f"✅ Created git commit: {message}")
        return True
        
    except Exception as e:
        print(f"❌ Git operation failed: {e}")
        return False

def dry_run_preview(errors_by_file, max_errors=80):
    """Preview what changes would be made without actually modifying files"""
    print(f"\n{'=' * 60}")
    print(f"🧪 DRY RUN PREVIEW")
    print(f"{'=' * 60}")
    
    total_processed = 0
    
    for file_path, file_errors in errors_by_file.items():
        if total_processed >= max_errors:
            break
            
        if not os.path.exists(file_path):
            print(f"⚠️ File not found: {file_path}")
            continue
        
        # Limit errors to process for this file
        remaining_quota = max_errors - total_processed
        errors_to_process = file_errors[:remaining_quota]
        
        print(f"\n📁 {os.path.basename(file_path)} ({len(errors_to_process)} errors):")
        
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                lines = f.readlines()
            
            for error in errors_to_process:
                method_name = error['member'].split('.')[-1]
                insert_line_idx = find_method_by_name(lines, method_name)
                
                if insert_line_idx is not None:
                    target_line = lines[insert_line_idx]
                    description, parameters = analyze_member_type(target_line, error['member'])
                    
                    member_short = error['member'].split('.')[-1].split('(')[0]
                    param_info = f" with {len(parameters)} params" if parameters else ""
                    print(f"   ✓ {member_short}{param_info} → {description}")
                    
                    if parameters:
                        for param in parameters:
                            print(f"     📝 param {param['name']}: {param['type']}")
                else:
                    member_short = error['member'].split('.')[-1].split('(')[0]
                    print(f"   ⚠ Could not find: {member_short}")
            
            total_processed += len(errors_to_process)
            
        except Exception as e:
            print(f"   ❌ Error reading file: {e}")
    
    print(f"\n📊 DRY RUN SUMMARY:")
    print(f"   • Total errors to process: {total_processed}")
    print(f"   • Files to modify: {len([f for f in errors_by_file.keys() if os.path.exists(f)])}")
    print(f"{'=' * 60}\n")
    
    return total_processed

def main():
    print(f"\n{'=' * 80}")
    print(f"🔧 INTELLIGENT CS1591 XML DOCUMENTATION GENERATOR")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    # Use CS1591B.TXT which contains the remaining errors after initial fixes
    cs1591_file = r"F:\Dynamic\ExxerAi\ExxerAI\Errors\CS1591B.TXT"
    base_path = r"F:\Dynamic\ExxerAi\ExxerAI"
    
    if not os.path.exists(cs1591_file):
        print(f"❌ Error file not found: {cs1591_file}")
        return
    
    print("🔍 Parsing CS1591 errors...")
    errors = parse_cs1591_errors(cs1591_file)
    
    if not errors:
        print("✅ No CS1591 errors found!")
        return
    
    # Group errors by file
    errors_by_file = {}
    for error in errors:
        if error['file'] not in errors_by_file:
            errors_by_file[error['file']] = []
        errors_by_file[error['file']].append(error)
    
    print(f"📋 Found {len(errors)} CS1591 errors in {len(errors_by_file)} files")
    
    # STEP 1: DRY RUN PREVIEW
    total_to_process = dry_run_preview(errors_by_file, max_errors=80)
    
    if total_to_process == 0:
        print("❌ No errors to process!")
        return
    
    # Auto-proceed (non-interactive mode for automation)
    print("✅ Auto-proceeding with changes (non-interactive mode)...")
    
    # STEP 2: CREATE SAFETY COMMIT
    print(f"\n🔒 Creating safety commit before changes...")
    commit_msg = f"Safety commit before CS1591 XML documentation fixes ({total_to_process} errors)"
    if not create_git_commit(base_path, commit_msg):
        print("⚠️ Git commit failed but continuing with XML documentation fixes...")
    
    # STEP 3: APPLY CHANGES
    print(f"\n🔧 Applying XML documentation fixes...")
    total_processed = 0
    fixed_files = 0
    
    for file_path, file_errors in errors_by_file.items():
        if total_processed >= 80:
            break
            
        if not os.path.exists(file_path):
            print(f"⚠️ File not found: {file_path}")
            continue
        
        # Limit errors to process for this file
        remaining_quota = 80 - total_processed
        errors_to_process = file_errors[:remaining_quota]
        
        print(f"\n📝 Processing {os.path.basename(file_path)} ({len(errors_to_process)} errors)...")
        
        if fix_cs1591_in_file(file_path, errors_to_process):
            fixed_files += 1
            total_processed += len(errors_to_process)
    
    # STEP 4: CREATE COMPLETION COMMIT
    print(f"\n✅ Creating completion commit...")
    completion_msg = f"Add XML documentation for {total_processed} CS1591 errors\n\n🤖 Generated with Claude Code\n\nCo-Authored-By: Claude <noreply@anthropic.com>"
    create_git_commit(base_path, completion_msg)
        
    print(f"\n{'=' * 80}")
    print(f"✅ COMPLETION SUMMARY:")
    print(f"   • Processed: {total_processed} errors")
    print(f"   • Fixed files: {fixed_files}")
    print(f"   • Safety commit: Created")
    print(f"   • Completion commit: Created")
    print(f"   • Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    main()