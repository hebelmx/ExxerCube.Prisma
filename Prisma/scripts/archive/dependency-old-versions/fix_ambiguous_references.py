#!/usr/bin/env python3
"""
Fix CS0104 ambiguous reference errors
"""

import os
from datetime import datetime

# Ambiguous references and their resolutions
ambiguous_fixes = [
    # AlertSeverity - use CommunicationAlertSeverity for realtime
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\AgentCommunication\Communications\HealthMonitor.cs',
        'old': 'AlertSeverity',
        'new': 'CommunicationAlertSeverity',
        'line': 422
    },
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.RealTimeCommunication.Test\Adapters\SignalR\SignalRAdapterTests.cs',
        'old': 'AlertSeverity.Critical',
        'new': 'CommunicationAlertSeverity.Critical',
        'line': 467
    },
    # DatabaseConfiguration - use specific one based on context
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure\ExxerAI.Conduit\AgentCommunication\Orchestration\LocalAIKeyManager.cs',
        'old': 'DatabaseConfiguration',
        'new': 'ExxerAI.Application.DTOs.DatabaseConfiguration',
        'line': 189
    },
    # InMemoryBm25SearchService - use Nexus.Library version
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\06SystemTests\ExxerAI.EnhancedRag.System.Test\Services\Bm25SearchServiceTests.cs',
        'old': 'InMemoryBm25SearchService',
        'new': 'ExxerAI.Nexus.Library.Services.InMemoryBm25SearchService',
        'line': 12
    },
    # KpiCalculationEngine - use Nexus.Library version
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\09Standalone\ExxerAI.BusinessIntelligence.Test\Kpis\KpiCalculationEngineTests.cs',
        'old': 'KpiCalculationEngine',
        'new': 'ExxerAI.Nexus.Library.Services.KpiCalculationEngine',
        'lines': [19, 28, 30, 76]
    },
    # ReciprocalRankFusionService - use Nexus.Library version
    {
        'file': r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\06SystemTests\ExxerAI.EnhancedRag.System.Test\Services\RankFusionServiceTests.cs',
        'old': 'ReciprocalRankFusionService',
        'new': 'ExxerAI.Nexus.Library.Services.ReciprocalRankFusionService',
        'line': 12
    }
]

def fix_ambiguous_reference(file_path, old_text, new_text, line_nums):
    """Fix ambiguous references in a file at specific lines"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        modified = False
        if isinstance(line_nums, list):
            for line_num in line_nums:
                idx = line_num - 1
                if 0 <= idx < len(lines) and old_text in lines[idx]:
                    lines[idx] = lines[idx].replace(old_text, new_text)
                    modified = True
                    print(f"    - Fixed line {line_num}")
        else:
            idx = line_nums - 1
            if 0 <= idx < len(lines) and old_text in lines[idx]:
                lines[idx] = lines[idx].replace(old_text, new_text)
                modified = True
                print(f"    - Fixed line {line_nums}")
        
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            return True
    except Exception as e:
        print(f"    ✗ Error: {e}")
    return False

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING CS0104 AMBIGUOUS REFERENCES")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    fixed_count = 0
    
    for fix in ambiguous_fixes:
        file_path = fix['file']
        if os.path.exists(file_path):
            print(f"Fixing {os.path.basename(file_path)}:")
            line_nums = fix.get('lines', fix.get('line'))
            if fix_ambiguous_reference(file_path, fix['old'], fix['new'], line_nums):
                fixed_count += 1
        else:
            print(f"  ⚠ File not found: {file_path}")
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Fixed {fixed_count} files with ambiguous references")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    main()