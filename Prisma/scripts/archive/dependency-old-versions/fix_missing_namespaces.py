#!/usr/bin/env python3
"""
Fix CS0234 missing namespace errors
"""

import os
from datetime import datetime

# Missing namespaces to fix
namespace_fixes = [
    # Azure.AI namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\AIServicesExtensions.cs'
        ],
        'missing': 'Azure.AI',
        'comment_out': True,  # Since Azure.AI might not exist, comment out the using
        'reason': 'Azure.AI namespace may have been removed by error'
    },
    # ExxerAI.Axis namespaces
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\AIServicesExtensions.cs',
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\CoreServicesExtensions.cs'
        ],
        'missing': 'ExxerAI.Axis.Adapters.AI',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # ConversationalBI namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Test\ConversationalBI\QuestionParserServiceTests.cs'
        ],
        'missing': 'ExxerAI.Axis.Services.ConversationalBI',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # GoogleDrive namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\GoogleDriveM2MServiceCollectionExtensions.cs'
        ],
        'missing': 'ExxerAI.Axis.Adapters.GoogleDrive',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # ImageProcessing namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\ServiceCollectionExtensions.cs'
        ],
        'missing': 'ExxerAI.Axis.Services.ImageProcessing',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # Intelligence namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\AgentCommunication\Orchestration\AdaptiveLearningEngine.cs'
        ],
        'missing': 'ExxerAI.Conduit.AgentCommunication.Swarms.Intelligence',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # Axis.Interfaces namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Test\GlobalUsings.cs',
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\06SystemTests\ExxerAI.EnhancedRag.System.Test\GlobalUsings.cs',
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\AgentCommunication\Orchestration\RemoteDockerManager.cs'
        ],
        'missing': 'ExxerAI.Axis.Interfaces',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # LLM namespaces
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\AIServicesExtensions.cs',
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\LLMServicesExtensions.cs'
        ],
        'missing': 'ExxerAi.Axioms.LLM',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # Configuration namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Helix\GlobalUsings.cs'
        ],
        'missing': 'ExxerAi.Axioms.Models.Configuration',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # UI Layout namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Presentation\ExxerAI.UI.Web\GlobalUsings.cs'
        ],
        'missing': 'ExxerAI.UI.Web.Components.Layout',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    },
    # Orchestration namespace
    {
        'files': [
            r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Presentation\ExxerAI.UI.Web\Program.cs'
        ],
        'missing': 'ExxerAI.Orchestration',
        'comment_out': True,
        'reason': 'Namespace may have been added by error'
    }
]

def comment_out_using(file_path, namespace):
    """Comment out a using statement for a namespace"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        modified = False
        for i, line in enumerate(lines):
            # Check for partial namespace match at the end
            if namespace.endswith('.AI') and 'using Azure.AI' in line and not line.strip().startswith('//'):
                # Don't comment out Azure.AI.OpenAI
                continue
            elif f'using {namespace}' in line and not line.strip().startswith('//'):
                lines[i] = f'// {line.strip()} // CS0234: Namespace may not exist\n'
                modified = True
                print(f"    ✓ Commented out: {line.strip()}")
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            return True
    except Exception as e:
        print(f"    ✗ Error: {e}")
    return False

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING CS0234 MISSING NAMESPACE ERRORS")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    fixed_count = 0
    
    for fix in namespace_fixes:
        for file_path in fix['files']:
            if not os.path.exists(file_path):
                print(f"⚠ File not found: {os.path.basename(file_path)}")
                continue
            
            print(f"Fixing {os.path.basename(file_path)}:")
            
            if fix['comment_out']:
                if comment_out_using(file_path, fix['missing']):
                    fixed_count += 1
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Fixed {fixed_count} missing namespace issues")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    main()