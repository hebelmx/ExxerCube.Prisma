#!/usr/bin/env python3
"""
Fix remaining build errors after CS0246 resolution
"""

import re
import os
from pathlib import Path
from datetime import datetime

# Error patterns and fixes
AMBIGUOUS_FIXES = {
    # File: HealthMonitor.cs - AlertSeverity ambiguity - use CommunicationAlertSeverity for realtime
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\AgentCommunication\\Communications\\HealthMonitor\.cs": {
        "AlertSeverity": "CommunicationAlertSeverity"
    },
    # File: LocalAIKeyManager.cs - DatabaseConfiguration ambiguity
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\AgentCommunication\\Orchestration\\LocalAIKeyManager\.cs": {
        "DatabaseConfiguration": "ExxerAI.Domain.Configurations.DatabaseConfiguration"
    },
    # File: Bm25SearchServiceTests.cs - InMemoryBm25SearchService ambiguity
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\tests\\06SystemTests\\ExxerAI\.EnhancedRag\.System\.Test\\Services\\Bm25SearchServiceTests\.cs": {
        "InMemoryBm25SearchService": "ExxerAI.Vault.Services.InMemoryBm25SearchService"
    },
    # File: KpiCalculationEngineTests.cs - KpiCalculationEngine ambiguity
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\tests\\09Standalone\\ExxerAI\.BusinessIntelligence\.Test\\Kpis\\KpiCalculationEngineTests\.cs": {
        "KpiCalculationEngine": "ExxerAI.Vault.Services.KpiCalculationEngine"
    },
    # File: RankFusionServiceTests.cs - ReciprocalRankFusionService ambiguity
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\tests\\06SystemTests\\ExxerAI\.EnhancedRag\.System\.Test\\Services\\RankFusionServiceTests\.cs": {
        "ReciprocalRankFusionService": "ExxerAI.Vault.Services.ReciprocalRankFusionService"
    }
}

# Missing namespace fixes
NAMESPACE_FIXES = {
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\Extensions\\AIServicesExtensions\.cs": [
        "using Azure.AI.OpenAI;",
        "using ExxerAI.Axis.Adapters.AI;",
        "using ExxerAi.Axioms.LLM;"
    ],
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\Extensions\\CoreServicesExtensions\.cs": [
        "using ExxerAI.Axis.Adapters.AI;"
    ],
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Helix\\GlobalUsings\.cs": {
        "old": "global using ExxerAi.Axioms.Models.Configuration;",
        "new": "global using ExxerAI.Axioms.Models;"
    },
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\tests\\09Standalone\\ExxerAI\.BusinessIntelligence\.Test\\ConversationalBI\\QuestionParserServiceTests\.cs": [
        "using ExxerAI.Axis.Services.ConversationalBI;"
    ],
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\Extensions\\GoogleDriveM2MServiceCollectionExtensions\.cs": [
        "using ExxerAI.Axis.Adapters.GoogleDrive;"
    ],
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\Extensions\\ServiceCollectionExtensions\.cs": [
        "using ExxerAI.Axis.Services.ImageProcessing;"
    ],
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\Extensions\\LLMServicesExtensions\.cs": [
        "using ExxerAi.Axioms.LLM;"
    ],
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\AgentCommunication\\Orchestration\\AdaptiveLearningEngine\.cs": [
        "using ExxerAI.Conduit.AgentCommunication.Swarms.Intelligence;"
    ],
    r"F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\Infrastructure\\ExxerAI\.Conduit\\AgentCommunication\\Orchestration\\RemoteDockerManager\.cs": [
        "using ExxerAI.Axis.Interfaces;"
    ]
}

def fix_ambiguous_references(file_path, fixes):
    """Fix ambiguous references in a file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        modified = False
        for ambiguous_type, full_type in fixes.items():
            # Replace standalone type references
            pattern = r'\b' + re.escape(ambiguous_type) + r'\b(?![.\w])'
            new_content = re.sub(pattern, full_type, content)
            if new_content != content:
                content = new_content
                modified = True
                print(f"  - Replaced {ambiguous_type} with {full_type}")
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"  ✓ Fixed ambiguous references in {os.path.basename(file_path)}")
        
        return modified
    except Exception as e:
        print(f"  ✗ Error fixing {file_path}: {e}")
        return False

def add_missing_usings(file_path, usings):
    """Add missing using statements to a file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Find the last using statement
        last_using_idx = -1
        for i, line in enumerate(lines):
            if line.strip().startswith('using ') and line.strip().endswith(';'):
                last_using_idx = i
        
        if last_using_idx >= 0:
            # Insert new usings after the last using
            for using in reversed(usings):
                if not any(using in line for line in lines):
                    lines.insert(last_using_idx + 1, using + '\n')
                    print(f"  - Added: {using}")
        else:
            # Insert at the beginning
            for i, using in enumerate(usings):
                if not any(using in line for line in lines):
                    lines.insert(i, using + '\n')
                    print(f"  - Added: {using}")
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.writelines(lines)
        
        print(f"  ✓ Added missing usings to {os.path.basename(file_path)}")
        return True
    except Exception as e:
        print(f"  ✗ Error adding usings to {file_path}: {e}")
        return False

def fix_global_using(file_path, old_using, new_using):
    """Fix a global using statement"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        if old_using in content:
            content = content.replace(old_using, new_using)
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"  ✓ Fixed global using in {os.path.basename(file_path)}")
            return True
    except Exception as e:
        print(f"  ✗ Error fixing global using in {file_path}: {e}")
    return False

def fix_enum_conversion_error(file_path):
    """Fix EnumModel conversion errors by using Enum.Parse instead"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Pattern to find EnumModel.FromName<T>(name) calls
        pattern = r'EnumModel\.FromName<([^>]+)>\(([^)]+)\)'
        
        def replace_enum_parse(match):
            enum_type = match.group(1)
            name_param = match.group(2)
            return f'Enum.Parse<{enum_type}>({name_param})'
        
        new_content = re.sub(pattern, replace_enum_parse, content)
        
        if new_content != content:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"  ✓ Fixed EnumModel conversion in {os.path.basename(file_path)}")
            return True
    except Exception as e:
        print(f"  ✗ Error fixing enum conversion in {file_path}: {e}")
    return False

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING REMAINING BUILD ERRORS")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    fixed_count = 0
    
    # Fix ambiguous references
    print("1. Fixing Ambiguous References (CS0104):")
    for file_path, fixes in AMBIGUOUS_FIXES.items():
        if os.path.exists(file_path):
            if fix_ambiguous_references(file_path, fixes):
                fixed_count += 1
        else:
            print(f"  ⚠ File not found: {file_path}")
    
    # Fix missing namespaces
    print("\n2. Fixing Missing Namespaces (CS0234):")
    for file_path, fix in NAMESPACE_FIXES.items():
        if os.path.exists(file_path):
            if isinstance(fix, list):
                if add_missing_usings(file_path, fix):
                    fixed_count += 1
            elif isinstance(fix, dict):
                if fix_global_using(file_path, fix['old'], fix['new']):
                    fixed_count += 1
        else:
            print(f"  ⚠ File not found: {file_path}")
    
    # Fix enum conversion errors
    print("\n3. Fixing EnumModel Conversion Errors (CS0315):")
    enum_error_files = [
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\05IntegrationTests\ExxerAI.Analytics.Integration.Test\PlaceholderTests.cs",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\03UnitTests\ExxerAI.Configuration.Test\ConfigurationValidationTests.cs"
    ]
    
    for file_path in enum_error_files:
        if os.path.exists(file_path):
            if fix_enum_conversion_error(file_path):
                fixed_count += 1
        else:
            print(f"  ⚠ File not found: {file_path}")
    
    # Fix null reference warning
    print("\n4. Fixing Null Reference Warning (CS8602):")
    reranking_file = r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\05IntegrationTests\ExxerAI.EnhancedRag.Integration.Test\Services\RerankingServiceTests.cs"
    if os.path.exists(reranking_file):
        try:
            with open(reranking_file, 'r', encoding='utf-8') as f:
                lines = f.readlines()
            
            # Fix line 265 - add null check
            if len(lines) > 264:
                line_265 = lines[264]
                if 'result.Value' in line_265 and 'ShouldNotBeNull()' not in lines[263]:
                    # Insert null check before the problematic line
                    lines.insert(264, '        result.Value.ShouldNotBeNull();\n')
                    with open(reranking_file, 'w', encoding='utf-8') as f:
                        f.writelines(lines)
                    print(f"  ✓ Added null check in {os.path.basename(reranking_file)}")
                    fixed_count += 1
        except Exception as e:
            print(f"  ✗ Error fixing null reference: {e}")
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Fixed {fixed_count} issues")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    print("\nRemaining manual fixes needed:")
    print("1. Interface implementation errors (CS0535, CS0738) - Need to update stub implementations")
    print("2. Missing methods in IConfigurationValidator (CS1061) - Interface may have changed")
    print("3. Project reference issues (NETSDK1150) - ExxerAI.Nexus needs to be a library, not executable")
    print("4. Missing test infrastructure project (MSB9008)")
    print("5. SignalR AlertSeverity conversion (CS1503) - Type mismatch needs manual fix")
    
    # Fix SignalR test AlertSeverity conversion
    print("\n5. Fixing SignalR AlertSeverity Conversion (CS1503):")
    signalr_file = r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.RealTimeCommunication.Test\Adapters\SignalR\SignalRAdapterTests.cs"
    if os.path.exists(signalr_file):
        try:
            with open(signalr_file, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Replace AlertSeverity.High with CommunicationAlertSeverity.High on line 467
            pattern = r'AlertSeverity\.(Critical|High|Medium|Low|Info)'
            replacement = r'CommunicationAlertSeverity.\1'
            new_content = re.sub(pattern, replacement, content)
            
            if new_content != content:
                with open(signalr_file, 'w', encoding='utf-8') as f:
                    f.write(new_content)
                print(f"  ✓ Fixed AlertSeverity conversion in SignalRAdapterTests.cs")
                fixed_count += 1
        except Exception as e:
            print(f"  ✗ Error fixing SignalR test: {e}")

if __name__ == "__main__":
    main()