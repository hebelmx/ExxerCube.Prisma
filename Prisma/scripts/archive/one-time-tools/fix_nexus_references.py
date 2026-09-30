#!/usr/bin/env python3
"""
Fix references from ExxerAI.Nexus to ExxerAI.Nexus.Library
"""

import os
import re
from datetime import datetime

def fix_project_references():
    """Update project references from Nexus to Nexus.Library"""
    
    project_files = [
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Composition\ExxerAI.Composition.csproj",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\ExxerAI.Conduit.csproj",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Helix\ExxerAI.Helix.csproj",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\00Domain\ExxerAI.Domain.Nexus.Test\ExxerAI.Domain.Nexus.Test.csproj",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\01Application\ExxerAI.Application.BusinessIntelligence.Test\ExxerAI.Application.BusinessIntelligence.Test.csproj",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\06SystemTests\ExxerAI.EnhancedRag.System.Test\ExxerAI.EnhancedRag.System.Test.csproj",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Test\ExxerAI.BusinessIntelligence.Test.csproj",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Tests.Moved\ExxerAI.BusinessIntelligence.Tests.csproj"
    ]
    
    fixed_count = 0
    
    print(f"\n{'=' * 80}")
    print(f"FIXING NEXUS REFERENCES")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    for proj_file in project_files:
        if not os.path.exists(proj_file):
            print(f"  ⚠ File not found: {os.path.basename(proj_file)}")
            continue
            
        try:
            with open(proj_file, 'r', encoding='utf-8') as f:
                content = f.read()
            
            original_content = content
            
            # Replace references to ExxerAI.Nexus with ExxerAI.Nexus.Library
            # But NOT for ExxerAI.Nexus.Library itself
            if 'ExxerAI.Nexus\\ExxerAI.Nexus.csproj' in content:
                content = content.replace(
                    'ExxerAI.Nexus\\ExxerAI.Nexus.csproj',
                    'ExxerAI.Nexus.Library\\ExxerAI.Nexus.Library.csproj'
                )
                print(f"  ✓ Fixed reference in {os.path.basename(proj_file)}")
                fixed_count += 1
            
            # Also check for forward slash version
            if 'ExxerAI.Nexus/ExxerAI.Nexus.csproj' in content:
                content = content.replace(
                    'ExxerAI.Nexus/ExxerAI.Nexus.csproj',
                    'ExxerAI.Nexus.Library/ExxerAI.Nexus.Library.csproj'
                )
                print(f"  ✓ Fixed reference in {os.path.basename(proj_file)}")
                fixed_count += 1
            
            if content != original_content:
                with open(proj_file, 'w', encoding='utf-8') as f:
                    f.write(content)
                    
        except Exception as e:
            print(f"  ✗ Error processing {os.path.basename(proj_file)}: {e}")
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Fixed {fixed_count} references")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    fix_project_references()