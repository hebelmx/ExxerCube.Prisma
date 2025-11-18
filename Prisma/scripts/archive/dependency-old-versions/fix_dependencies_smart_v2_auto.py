#!/usr/bin/env python3
"""
Enhanced Smart Dependency Fixer v2 - Non-interactive version
Same as v2 but without user prompts for automation
"""

import os
import sys

# Import the main fixer class and dependencies
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fix_dependencies_smart_v2 import EnhancedSmartDependencyFixer

def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Enhanced smart dependency fixer (non-interactive)')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--report', default='enhanced_dependency_analysis.json',
                       help='Path to the enhanced analysis report')
    parser.add_argument('--dry-run', action='store_true', default=True,
                       help='Run in dry-run mode (default: true)')
    parser.add_argument('--apply', action='store_true',
                       help='Actually apply the fixes (disables dry-run)')
    
    args = parser.parse_args()
    
    dry_run = not args.apply
    
    if not dry_run:
        print("⚠️  SAFETY CHECKS:")
        print("  - Git status will be checked")
        print("  - Uncommitted changes will be committed")
        print("  - Files will be backed up")
        print("  - GlobalUsings.cs files will be modified")
        print("")
        print("Proceeding with modifications...")
    
    fixer = EnhancedSmartDependencyFixer(args.base_path, dry_run=dry_run)
    fixer.fix_dependencies(args.report)


if __name__ == "__main__":
    main()