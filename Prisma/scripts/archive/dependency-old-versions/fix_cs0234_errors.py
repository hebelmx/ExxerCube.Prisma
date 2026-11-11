#!/usr/bin/env python3
"""
Fix CS0234 missing namespace errors
Remove incorrect using statements or fix them
"""

import os
import re
from pathlib import Path

# Files and their fixes based on the error list
FIXES = {
    # AIServicesExtensions.cs - Remove non-existent namespaces
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\AIServicesExtensions.cs": {
        "remove_lines": [
            "using ExxerAI.Axis.Adapters.AI;",  # Doesn't exist
            "using ExxerAi.Axioms.LLM;"  # Doesn't exist
        ]
    },
    
    # CoreServicesExtensions.cs
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\CoreServicesExtensions.cs": {
        "remove_lines": [
            "using ExxerAI.Axis.Adapters.AI;"  # Doesn't exist
        ]
    },
    
    # GoogleDriveM2MServiceCollectionExtensions.cs
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\GoogleDriveM2MServiceCollectionExtensions.cs": {
        "remove_lines": [
            "using ExxerAI.Axis.Adapters.GoogleDrive;"  # Check if exists
        ]
    },
    
    # ServiceCollectionExtensions.cs
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\ServiceCollectionExtensions.cs": {
        "remove_lines": [
            "using ExxerAI.Axis.Services.ImageProcessing;"  # Check if exists
        ]
    },
    
    # LLMServicesExtensions.cs
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\Extensions\LLMServicesExtensions.cs": {
        "remove_lines": [
            "using ExxerAi.Axioms.LLM;"  # Doesn't exist
        ]
    },
    
    # AdaptiveLearningEngine.cs
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\AgentCommunication\Orchestration\AdaptiveLearningEngine.cs": {
        "remove_lines": [
            "using ExxerAI.Conduit.AgentCommunication.Swarms.Intelligence;"  # Check if exists
        ]
    },
    
    # RemoteDockerManager.cs
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\AgentCommunication\Orchestration\RemoteDockerManager.cs": {
        "remove_lines": [
            "using ExxerAI.Axis.Interfaces;"  # Check if exists
        ]
    },
    
    # QuestionParserServiceTests.cs
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Test\ConversationalBI\QuestionParserServiceTests.cs": {
        "remove_lines": [
            "using ExxerAI.Axis.Services.ConversationalBI;"  # Check if exists
        ]
    },
    
    # GlobalUsings files with missing namespaces
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Helix\GlobalUsings.cs": {
        "replace": {
            "global using ExxerAi.Axioms.Models.Configuration;": "// Removed: global using ExxerAi.Axioms.Models.Configuration; // Namespace doesn't exist"
        }
    },
    
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Test\GlobalUsings.cs": {
        "remove_lines": [
            "global using ExxerAI.Axis.Interfaces;"
        ]
    },
    
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\06SystemTests\ExxerAI.EnhancedRag.System.Test\GlobalUsings.cs": {
        "remove_lines": [
            "global using ExxerAI.Axis.Interfaces;"
        ]
    },
    
    # UI.Web Program.cs - Missing Orchestration namespace
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Presentation\ExxerAI.UI.Web\Program.cs": {
        "remove_lines": [
            "using ExxerAI.Orchestration;",
            "using ExxerAI.Orchestration.Adapters;",
            "using ExxerAI.Orchestration.Aggregators;",
            "using ExxerAI.Orchestration.Extensions;",
            "using ExxerAI.Orchestration.Services;"
        ]
    },
    
    # UI.Web GlobalUsings.cs - Missing Layout namespace
    r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Presentation\ExxerAI.UI.Web\GlobalUsings.cs": {
        "remove_lines": [
            "global using ExxerAI.UI.Web.Components.Layout;"
        ]
    }
}

def fix_file(file_path, fixes):
    """Apply fixes to a file"""
    if not os.path.exists(file_path):
        print(f"  ⚠ File not found: {file_path}")
        return False
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        modified = False
        
        # Remove specified lines
        if "remove_lines" in fixes:
            new_lines = []
            for line in lines:
                should_remove = False
                for remove_pattern in fixes["remove_lines"]:
                    if remove_pattern in line:
                        print(f"  - Removing: {line.strip()}")
                        should_remove = True
                        modified = True
                        break
                
                if not should_remove:
                    new_lines.append(line)
            
            lines = new_lines
        
        # Replace specified lines
        if "replace" in fixes:
            new_lines = []
            for line in lines:
                replaced = False
                for old, new in fixes["replace"].items():
                    if old in line:
                        new_lines.append(line.replace(old, new))
                        print(f"  - Replaced: {old} -> {new}")
                        replaced = True
                        modified = True
                        break
                
                if not replaced:
                    new_lines.append(line)
            
            lines = new_lines
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            print(f"  ✓ Fixed {os.path.basename(file_path)}")
        
        return modified
    
    except Exception as e:
        print(f"  ✗ Error fixing {file_path}: {e}")
        return False

def main():
    print("\n" + "=" * 80)
    print("FIXING CS0234 NAMESPACE ERRORS")
    print("=" * 80 + "\n")
    
    fixed_count = 0
    
    for file_path, fixes in FIXES.items():
        print(f"Processing: {os.path.basename(file_path)}")
        if fix_file(file_path, fixes):
            fixed_count += 1
        print()
    
    print(f"Fixed {fixed_count} files")
    
    # Check for Azure.AI.OpenAI package
    print("\n" + "=" * 80)
    print("PACKAGE REFERENCES NEEDED:")
    print("=" * 80)
    print("\nIf Azure.AI.OpenAI errors persist, add to relevant project files:")
    print('  <PackageReference Include="Azure.AI.OpenAI" />')
    print("\nFor Microsoft.Extensions.AI:")
    print('  <PackageReference Include="Microsoft.Extensions.AI" />')

if __name__ == "__main__":
    main()