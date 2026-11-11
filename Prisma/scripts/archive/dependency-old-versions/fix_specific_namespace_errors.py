#!/usr/bin/env python3
"""
Fix CS0234 specific namespace errors based on error messages
"""

import os
import re
from datetime import datetime

# Map of file:line to namespace error fixes
namespace_line_fixes = [
    # Based on Errors.txt line 51-52
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\AIServicesExtensions.cs',
        'line': 5,  # Azure.AI error
        'action': 'comment'
    },
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\AIServicesExtensions.cs', 
        'line': 1,  # ExxerAI.Axis.Adapters.AI error
        'action': 'comment'
    },
    # Line 53
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\CoreServicesExtensions.cs',
        'line': 4,
        'action': 'comment'
    },
    # Line 54
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Helix\GlobalUsings.cs',
        'line': 28,
        'action': 'comment'
    },
    # Line 55
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Test\ConversationalBI\QuestionParserServiceTests.cs',
        'line': 3,
        'action': 'comment'
    },
    # Line 56
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\GoogleDriveM2MServiceCollectionExtensions.cs',
        'line': 1,
        'action': 'comment'
    },
    # Line 57
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\ServiceCollectionExtensions.cs',
        'line': 4,
        'action': 'comment'
    },
    # Line 58
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\AgentCommunication\Orchestration\AdaptiveLearningEngine.cs',
        'line': 2,
        'action': 'comment'
    },
    # Line 59-61
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Test\GlobalUsings.cs',
        'line': 37,
        'action': 'comment'
    },
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\AgentCommunication\Orchestration\RemoteDockerManager.cs',
        'line': 1,
        'action': 'comment'
    },
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\06SystemTests\ExxerAI.EnhancedRag.System.Test\GlobalUsings.cs',
        'line': 37,
        'action': 'comment'
    },
    # Line 62
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Presentation\ExxerAI.UI.Web\GlobalUsings.cs',
        'line': 10,
        'action': 'comment'
    },
    # Line 63-64
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\AIServicesExtensions.cs',
        'line': 6,
        'action': 'comment'
    },
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\LLMServicesExtensions.cs',
        'line': 1,
        'action': 'comment'
    },
    # Line 65-69
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Presentation\ExxerAI.UI.Web\Program.cs',
        'lines': [4, 5, 6, 7, 8],
        'action': 'comment'
    }
]

def comment_line(file_path, line_nums):
    """Comment out specific lines in a file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        if not isinstance(line_nums, list):
            line_nums = [line_nums]
        
        modified = False
        for line_num in line_nums:
            idx = line_num - 1  # Convert to 0-based index
            if 0 <= idx < len(lines):
                line = lines[idx]
                if 'using' in line and not line.strip().startswith('//'):
                    lines[idx] = f'// {line.strip()} // CS0234: Missing namespace\n'
                    modified = True
                    print(f"    ✓ Line {line_num}: Commented out {line.strip()}")
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            return True
    except Exception as e:
        print(f"    ✗ Error: {e}")
    return False

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING CS0234 SPECIFIC NAMESPACE ERRORS")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    fixed_count = 0
    processed_files = set()
    
    for fix in namespace_line_fixes:
        file_path = fix['file']
        
        if not os.path.exists(file_path):
            print(f"⚠ File not found: {os.path.basename(file_path)}")
            continue
        
        if file_path not in processed_files:
            print(f"\nFixing {os.path.basename(file_path)}:")
            processed_files.add(file_path)
        
        if 'line' in fix:
            if comment_line(file_path, fix['line']):
                fixed_count += 1
        elif 'lines' in fix:
            if comment_line(file_path, fix['lines']):
                fixed_count += 1
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Fixed {fixed_count} specific namespace errors")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    main()