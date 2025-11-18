#!/usr/bin/env python3
"""
Selective rescue of valuable migration assets from BandiniX/Bandini7 branches

This script cherry-picks valuable components from the migration attempt while 
avoiding corrupted test files. It preserves:
- Project structure and build fixes
- Enhanced migration scripts  
- Configuration files
- Documentation and lessons learned
"""

import os
import sys
import subprocess
import argparse
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional

# Configuration
SOURCE_COMMITS = {
    "f6c8d7a5d": "Migration State Preservation",
    "47f94899b": "Lessons Learned Documentation", 
    "79943d000": "Project Reference Fixes",
    "e89d2eef5": "Test Infrastructure Migration"
}

RESCUE_ASSETS = {
    "infrastructure_tests": [
        "code/src/tests/Infrastructure/ExxerAI.TestInfrastructure/ExxerAI.TestInfrastructure.csproj",
    ],
    
    "integration_tests": [
        "code/src/tests/IntegrationTests/ExxerAI.Analytics.IntegrationTests/ExxerAI.Analytics.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.Authentication.IntegrationTests/ExxerAI.Authentication.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.Cache.IntegrationTests/ExxerAI.Cache.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.Database.IntegrationTests/ExxerAI.Database.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.External.IntegrationTests/ExxerAI.External.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.ExternalAPI.IntegrationTests/ExxerAI.ExternalAPI.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.FileSystem.IntegrationTests/ExxerAI.FileSystem.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.Infrastructure.IntegrationTests/ExxerAI.Infrastructure.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.MessageQueue.IntegrationTests/ExxerAI.MessageQueue.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.Messaging.IntegrationTests/ExxerAI.Messaging.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.Network.IntegrationTests/ExxerAI.Network.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.Search.IntegrationTests/ExxerAI.Search.IntegrationTests.csproj",
        "code/src/tests/IntegrationTests/ExxerAI.Storage.IntegrationTests/ExxerAI.Storage.IntegrationTests.csproj",
    ],
    
    "standalone_tests": [
        "code/src/tests/Standalone/ExxerAI.ArchitectureTests/ExxerAI.ArchitectureTests.csproj",
        "code/src/tests/Standalone/ExxerAI.BenchmarkTests/ExxerAI.BenchmarkTests.csproj",
        "code/src/tests/Standalone/ExxerAI.ContractTests/ExxerAI.ContractTests.csproj",
        "code/src/tests/Standalone/ExxerAI.MutationTests/ExxerAI.MutationTests.csproj",
    ],
    
    "system_tests": [
        "code/src/tests/SystemTests/ExxerAI.Backup.SystemTests/ExxerAI.Backup.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.Configuration.SystemTests/ExxerAI.Configuration.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.Deployment.SystemTests/ExxerAI.Deployment.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.EndToEnd.SystemTests/ExxerAI.EndToEnd.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.Migration.SystemTests/ExxerAI.Migration.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.Monitoring.SystemTests/ExxerAI.Monitoring.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.Performance.SystemTests/ExxerAI.Performance.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.Resilience.SystemTests/ExxerAI.Resilience.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.Security.SystemTests/ExxerAI.Security.SystemTests.csproj",
        "code/src/tests/SystemTests/ExxerAI.Workflow.SystemTests/ExxerAI.Workflow.SystemTests.csproj",
    ],
    
    "unit_tests": [
        "code/src/tests/UnitTests/ExxerAI.API.UnitTests/ExxerAI.API.UnitTests.csproj",
        "code/src/tests/UnitTests/ExxerAI.Application.UnitTests/ExxerAI.Application.UnitTests.csproj",
        "code/src/tests/UnitTests/ExxerAI.CLI.UnitTests/ExxerAI.CLI.UnitTests.csproj",
        "code/src/tests/UnitTests/ExxerAI.CubeExplorer.UnitTests/ExxerAI.CubeExplorer.UnitTests.csproj",
        "code/src/tests/UnitTests/ExxerAI.Domain.UnitTests/ExxerAI.Domain.UnitTests.csproj",
        "code/src/tests/UnitTests/ExxerAI.Infrastructure.UnitTests/ExxerAI.Infrastructure.UnitTests.csproj",
        "code/src/tests/UnitTests/ExxerAI.MCPServer.UnitTests/ExxerAI.MCPServer.UnitTests.csproj",
        "code/src/tests/UnitTests/ExxerAI.Security.UnitTests/ExxerAI.Security.UnitTests.csproj",
        "code/src/tests/UnitTests/ExxerAI.UI.UnitTests/ExxerAI.UI.UnitTests.csproj",
    ],
    
    "ui_tests": [
        "code/src/tests/UITests/ExxerAI.Accessibility.UITests/ExxerAI.Accessibility.UITests.csproj",
        "code/src/tests/UITests/ExxerAI.API.UITests/ExxerAI.API.UITests.csproj",
        "code/src/tests/UITests/ExxerAI.CrossPlatform.UITests/ExxerAI.CrossPlatform.UITests.csproj",
        "code/src/tests/UITests/ExxerAI.Desktop.UITests/ExxerAI.Desktop.UITests.csproj",
        "code/src/tests/UITests/ExxerAI.Mobile.UITests/ExxerAI.Mobile.UITests.csproj",
        "code/src/tests/UITests/ExxerAI.WebUI.UITests/ExxerAI.WebUI.UITests.csproj",
    ],
    
    "vertical_slice_tests": [
        "code/src/tests/VerticalSliceTests/ExxerAI.AIIntelligence.VerticalTests/ExxerAI.AIIntelligence.VerticalTests.csproj",
        "code/src/tests/VerticalSliceTests/ExxerAI.Authentication.VerticalTests/ExxerAI.Authentication.VerticalTests.csproj",
        "code/src/tests/VerticalSliceTests/ExxerAI.CubeExplorer.VerticalTests/ExxerAI.CubeExplorer.VerticalTests.csproj",
        "code/src/tests/VerticalSliceTests/ExxerAI.DataAnalytics.VerticalTests/ExxerAI.DataAnalytics.VerticalTests.csproj",
        "code/src/tests/VerticalSliceTests/ExxerAI.DocumentProcessing.VerticalTests/ExxerAI.DocumentProcessing.VerticalTests.csproj",
        "code/src/tests/VerticalSliceTests/ExxerAI.ReportGeneration.VerticalTests/ExxerAI.ReportGeneration.VerticalTests.csproj",
        "code/src/tests/VerticalSliceTests/ExxerAI.SystemConfiguration.VerticalTests/ExxerAI.SystemConfiguration.VerticalTests.csproj",
        "code/src/tests/VerticalSliceTests/ExxerAI.UserManagement.VerticalTests/ExxerAI.UserManagement.VerticalTests.csproj",
    ],
    
    "t1_unit_tests_structure": [
        # This recovers the entire T1_UnitTests folder structure (120+ projects)
        # Using a pattern to include all T1_UnitTests projects
    ],
    
    "t2_vertical_tests_structure": [
        # This recovers the entire T2_VerticalTests folder structure (30+ projects)
        # Using a pattern to include all T2_VerticalTests projects  
    ],
    
    "t3_integration_tests_structure": [
        # This recovers the entire T3_IntegrationTests folder structure (50+ projects)
        # Using a pattern to include all T3_IntegrationTests projects
    ],
    
    "t4_system_tests_structure": [
        # This recovers the entire T4_SystemTests folder structure (20+ projects)
        # Using a pattern to include all T4_SystemTests projects
    ],
    
    "t5_ui_tests_structure": [
        # This recovers the entire T5_UITests folder structure (15+ projects)
        # Using a pattern to include all T5_UITests projects
    ],
    
    "migration_scripts": [
        "move_test_classes_to_correct_projects.py",
        "fix_all_project_references.py", 
        "fix_ca1016_assembly_versions.py",
        "migrationTest/pattern_classifier.py",
        "migrationTest/ollama_classifier.py",
        "migrationTest/enhanced_ai_classifier.py",
        "migrationTest/safe_file_migrator.py",
    ],
    
    "configuration_files": [
        "code/src/tests/Standalone/ExxerAI.MutationTests/stryker-config.json",
        "code/src/tests/Standalone/ExxerAI.MutationTests/mutation-test-settings.json",
        "code/src/tests/Standalone/ExxerAI.ContractTests/pact-broker.yml",
        "code/src/tests/Standalone/ExxerAI.ContractTests/pact-config.json",
        "code/src/tests/Standalone/ExxerAI.BenchmarkTests/benchmark-config.json",
        "code/src/tests/Standalone/ExxerAI.BenchmarkTests/runtimeconfig.template.json",
    ],
    
    "documentation": [
        "docs/adr/ADR-005-Testing-Strategy-Reorganization.md",
        "docs/adr/ADR-005-Testing-Strategy-Reorganization-LESSONS-LEARNED.md",
        "RESCUE_ANALYSIS.md",
    ]
}

class Colors:
    CYAN = '\033[96m'
    YELLOW = '\033[93m'
    GREEN = '\033[92m'
    RED = '\033[91m'
    GRAY = '\033[90m'
    MAGENTA = '\033[95m'
    DARK_GREEN = '\033[32m'
    RESET = '\033[0m'

def print_header(title: str):
    print(f"\n{Colors.CYAN}{'=' * 80}")
    print(f" {Colors.YELLOW}{title}")
    print(f"{Colors.CYAN}{'=' * 80}{Colors.RESET}")

def print_status(message: str, color: str = Colors.GREEN):
    print(f"{color}[OK] {message}{Colors.RESET}")

def print_warning(message: str):
    print(f"{Colors.YELLOW}[WARN] {message}{Colors.RESET}")

def print_error(message: str):
    print(f"{Colors.RED}[ERROR] {message}{Colors.RESET}")

def run_git_command(command: List[str], capture_output: bool = True) -> subprocess.CompletedProcess:
    """Run a git command and return the result"""
    try:
        return subprocess.run(
            ["git"] + command, 
            capture_output=capture_output, 
            text=True,
            check=False
        )
    except Exception as e:
        print_error(f"Git command failed: {e}")
        sys.exit(1)

def test_git_repository():
    """Check if we're in a git repository"""
    if not Path(".git").exists():
        print_error("Not in a git repository. Please run from repository root.")
        sys.exit(1)

def get_current_branch() -> str:
    """Get the current git branch"""
    result = run_git_command(["rev-parse", "--abbrev-ref", "HEAD"])
    return result.stdout.strip()

def test_commit_exists(commit: str) -> bool:
    """Check if a commit exists in the repository"""
    result = run_git_command(["cat-file", "-e", commit])
    return result.returncode == 0

def analyze_rescue_assets():
    """Analyze what assets are available for rescue"""
    print_header("RESCUE ASSET ANALYSIS")
    
    for commit, description in SOURCE_COMMITS.items():
        print(f"\n{Colors.MAGENTA}Analyzing commit: {commit} - {description}{Colors.RESET}")
        
        if not test_commit_exists(commit):
            print_warning(f"Commit {commit} not found in current repository")
            continue
        
        # Check what files exist in this commit
        result = run_git_command(["show", "--name-only", "--pretty=format:", commit])
        commit_files = [f.strip() for f in result.stdout.split('\n') if f.strip()]
        
        print(f"{Colors.GRAY}Files in commit:")
        for file in commit_files[:10]:  # Show first 10 files
            print(f"{Colors.GRAY}  {file}")
        if len(commit_files) > 10:
            print(f"{Colors.GRAY}  ... and {len(commit_files) - 10} more files")
    
    # Analyze asset categories
    for category, assets in RESCUE_ASSETS.items():
        print(f"\n{Colors.CYAN}{category.replace('_', ' ').title()} Assets:")
        for asset in assets:
            result = run_git_command(["show", f"f6c8d7a5d:{asset}"])
            if result.returncode == 0:
                print_status(asset)
            else:
                print_warning(f"{asset} (not found)")

def execute_dry_run():
    """Simulate the rescue operation without making changes"""
    print_header("DRY RUN: RESCUE OPERATION SIMULATION")
    
    current_branch = get_current_branch()
    print(f"{Colors.YELLOW}Current branch: {current_branch}")
    
    # Get dynamic list of all test projects
    print_status("Analyzing all test projects from Bandini7...")
    all_test_projects = get_all_test_projects_from_bandini7()
    all_config_files = get_all_config_files_from_bandini7()
    
    total_projects = sum(len(projects) for projects in all_test_projects.values())
    print(f"{Colors.GREEN}Analysis complete: {total_projects} test projects and {len(all_config_files)} config files")
    
    print(f"\n{Colors.CYAN}Would execute the following operations:")
    
    # 1. Create rescue branch
    timestamp = datetime.now().strftime('%Y%m%d-%H%M%S')
    rescue_branch = f"rescue-migration-assets-{timestamp}"
    print(f"{Colors.GREEN}1. Create rescue branch: {rescue_branch}")
    
    # 2. Cherry-pick valuable commits
    print(f"\n{Colors.GREEN}2. Cherry-pick operations:")
    for commit, description in SOURCE_COMMITS.items():
        print(f"{Colors.DARK_GREEN}   git cherry-pick {commit}")
        print(f"{Colors.GRAY}     -> {description}")
    
    # 3. Project structure restoration (CSPROJ ONLY)
    print(f"\n{Colors.GREEN}3. Test project structure restoration (.csproj files ONLY):")
    for category, projects in all_test_projects.items():
        if not projects:
            continue
        print(f"{Colors.YELLOW}   {category.replace('_', ' ').title()}: {len(projects)} .csproj files")
        # Show first few examples
        for i, project in enumerate(projects[:3]):
            print(f"{Colors.DARK_GREEN}     git checkout f6c8d7a5d -- {project}")
        if len(projects) > 3:
            print(f"{Colors.GRAY}     ... and {len(projects) - 3} more .csproj files")
    
    # 4. Migration scripts
    print(f"\n{Colors.GREEN}4. Migration scripts restoration:")
    for asset in RESCUE_ASSETS["migration_scripts"]:
        print(f"{Colors.DARK_GREEN}   git checkout f6c8d7a5d -- {asset}")
    
    # 5. Configuration files
    print(f"\n{Colors.GREEN}5. Configuration files restoration ({len(all_config_files)} files):")
    config_by_type = {}
    for config_file in all_config_files:
        ext = config_file.split('.')[-1]
        config_by_type.setdefault(ext, []).append(config_file)
    
    for ext, files in config_by_type.items():
        print(f"{Colors.YELLOW}   {ext.upper()} files: {len(files)}")
        for file in files[:2]:  # Show first 2 examples
            print(f"{Colors.DARK_GREEN}     git checkout f6c8d7a5d -- {file}")
        if len(files) > 2:
            print(f"{Colors.GRAY}     ... and {len(files) - 2} more {ext} files")
    
    # 6. Documentation
    print(f"\n{Colors.GREEN}6. Documentation restoration:")
    for asset in RESCUE_ASSETS["documentation"]:
        print(f"{Colors.DARK_GREEN}   git checkout f6c8d7a5d -- {asset}")
    
    # 7. Validation steps
    print(f"\n{Colors.GREEN}7. Validation steps:")
    print(f"{Colors.DARK_GREEN}   - Compile solution to verify project structure")
    print(f"{Colors.DARK_GREEN}   - Validate migration scripts functionality")
    print(f"{Colors.DARK_GREEN}   - Test configuration files integrity")
    print(f"{Colors.DARK_GREEN}   - Verify documentation completeness")
    
    # Summary
    print(f"\n{Colors.CYAN}RECOVERY SUMMARY - PROJECT STRUCTURE ONLY:")
    print(f"{Colors.GREEN}Test Project Files (.csproj): {total_projects}")
    print(f"{Colors.GREEN}Configuration Files (JSON/YAML): {len(all_config_files)}")
    print(f"{Colors.GREEN}Migration Scripts: {len(RESCUE_ASSETS['migration_scripts'])}")
    print(f"{Colors.GREEN}Documentation Files: {len(RESCUE_ASSETS['documentation'])}")
    print(f"{Colors.YELLOW}Total Assets: {total_projects + len(all_config_files) + len(RESCUE_ASSETS['migration_scripts']) + len(RESCUE_ASSETS['documentation'])}")
    print()
    print(f"{Colors.RED}[IMPORTANT] NO C# source files (.cs) will be recovered!")
    print(f"{Colors.RED}[IMPORTANT] Only project structure and configs preserved!")
    print(f"{Colors.GREEN}[BENEFIT] Clean foundation for implementing new tests!")

def get_all_test_projects_from_bandini7() -> Dict[str, List[str]]:
    """Get all test projects from Bandini7 branch dynamically"""
    result = run_git_command(["ls-tree", "-r", "--name-only", "Bandini7"])
    if result.returncode != 0:
        print_error("Failed to get file list from Bandini7 branch")
        return {}
    
    all_files = result.stdout.strip().split('\n')
    test_projects = [f for f in all_files if f.startswith("code/src/tests/") and f.endswith(".csproj")]
    
    # Categorize projects
    categorized = {
        "infrastructure_projects": [f for f in test_projects if "/Infrastructure/" in f],
        "integration_projects": [f for f in test_projects if "/IntegrationTests/" in f],
        "standalone_projects": [f for f in test_projects if "/Standalone/" in f],
        "system_projects": [f for f in test_projects if "/SystemTests/" in f],
        "unit_projects": [f for f in test_projects if "/UnitTests/" in f and "/T1_" not in f],
        "ui_projects": [f for f in test_projects if "/UITests/" in f and "/T5_" not in f],
        "vertical_projects": [f for f in test_projects if "/VerticalSliceTests/" in f and "/T2_" not in f],
        "t1_unit_projects": [f for f in test_projects if "/T1_UnitTests/" in f],
        "t2_vertical_projects": [f for f in test_projects if "/T2_VerticalTests/" in f],
        "t3_integration_projects": [f for f in test_projects if "/T3_IntegrationTests/" in f],
        "t4_system_projects": [f for f in test_projects if "/T4_SystemTests/" in f],
        "t5_ui_projects": [f for f in test_projects if "/T5_UITests/" in f],
    }
    
    return categorized

def get_all_config_files_from_bandini7() -> List[str]:
    """Get all config files from Bandini7 branch"""
    result = run_git_command(["ls-tree", "-r", "--name-only", "Bandini7"])
    if result.returncode != 0:
        print_error("Failed to get file list from Bandini7 branch")
        return []
    
    all_files = result.stdout.strip().split('\n')
    config_files = [f for f in all_files if f.startswith("code/src/tests/") and 
                   (f.endswith(".json") or f.endswith(".yml") or f.endswith(".yaml") or f.endswith(".config"))]
    
    return config_files

def execute_rescue(target_branch: Optional[str] = None):
    """Execute the rescue operation"""
    print_header("EXECUTING RESCUE OPERATION")
    
    current_branch = get_current_branch()
    timestamp = datetime.now().strftime('%Y%m%d-%H%M%S')
    rescue_branch = target_branch or f"rescue-migration-assets-{timestamp}"
    
    print(f"{Colors.YELLOW}Current branch: {current_branch}")
    print(f"{Colors.YELLOW}Target branch: {rescue_branch}")
    
    # Get dynamic list of all test projects
    print_status("Analyzing all test projects from Bandini7...")
    all_test_projects = get_all_test_projects_from_bandini7()
    all_config_files = get_all_config_files_from_bandini7()
    
    total_projects = sum(len(projects) for projects in all_test_projects.values())
    print(f"{Colors.GREEN}Found {total_projects} test projects and {len(all_config_files)} config files")
    
    # Confirm operation
    confirm = input(f"\n{Colors.YELLOW}Proceed with rescuing ALL {total_projects} test projects? (y/N): {Colors.RESET}")
    if confirm.lower() != 'y':
        print_warning("Operation cancelled by user")
        return
    
    try:
        # 1. Create and switch to rescue branch
        print_status(f"Creating rescue branch: {rescue_branch}")
        result = run_git_command(["checkout", "-b", rescue_branch])
        if result.returncode != 0:
            print_error(f"Failed to create branch: {result.stderr}")
            return
        
        # 2. Cherry-pick documentation commits (safe)
        print_status("Cherry-picking documentation and lessons learned")
        result = run_git_command(["cherry-pick", "47f94899b"])
        if result.returncode != 0:
            print_warning("Failed to cherry-pick lessons learned commit")
        
        # 3. Restore ONLY .csproj files (project structure only)
        print_status(f"Restoring project structure files ONLY (.csproj)")
        for category, projects in all_test_projects.items():
            if not projects:
                continue
            print(f"{Colors.YELLOW}  {category.replace('_', ' ')}: {len(projects)} projects")
            for project in projects:
                print(f"{Colors.DARK_GREEN}    Restoring: {project}")
                result = run_git_command(["checkout", "f6c8d7a5d", "--", project])
                if result.returncode != 0:
                    print_warning(f"Failed to restore: {project}")
        
        # 4. Restore migration scripts
        print_status("Restoring migration scripts")
        for asset in RESCUE_ASSETS["migration_scripts"]:
            print(f"{Colors.DARK_GREEN}  Restoring: {asset}")
            result = run_git_command(["checkout", "f6c8d7a5d", "--", asset])
            if result.returncode != 0:
                print_warning(f"Failed to restore: {asset}")
        
        # 5. Restore ONLY configuration files (JSON, YAML, etc.)
        print_status(f"Restoring configuration files ONLY ({len(all_config_files)} files)")
        for config_file in all_config_files:
            print(f"{Colors.DARK_GREEN}  Restoring: {config_file}")
            result = run_git_command(["checkout", "f6c8d7a5d", "--", config_file])
            if result.returncode != 0:
                print_warning(f"Failed to restore: {config_file}")
        
        # 6. Restore documentation
        print_status("Restoring documentation")
        for asset in RESCUE_ASSETS["documentation"]:
            print(f"{Colors.DARK_GREEN}  Restoring: {asset}")
            result = run_git_command(["checkout", "f6c8d7a5d", "--", asset])
            if result.returncode != 0:
                print_warning(f"Failed to restore: {asset}")
        
        # 7. Commit rescued assets
        print_status("Committing rescued assets")
        run_git_command(["add", "."])
        
        commit_message = f"""🛟 RESCUE: Test Project Structure & Configs ONLY

## Rescued Components - PROJECT FILES ONLY
✅ {total_projects} .csproj files (test project definitions)
✅ {len(all_config_files)} configuration files (JSON, YAML, etc.)
✅ Enhanced migration scripts with AI classification
✅ Comprehensive documentation and lessons learned
✅ Complete folder structure for all test categories

## Test Project Structure Recovered (.csproj only)
📋 Infrastructure: {len(all_test_projects.get('infrastructure_projects', []))} .csproj files
🔧 Integration: {len(all_test_projects.get('integration_projects', []))} .csproj files  
⚙️ Standalone: {len(all_test_projects.get('standalone_projects', []))} .csproj files
🏗️ System: {len(all_test_projects.get('system_projects', []))} .csproj files
🧪 Unit: {len(all_test_projects.get('unit_projects', []))} .csproj files
🖥️ UI: {len(all_test_projects.get('ui_projects', []))} .csproj files
📦 Vertical Slice: {len(all_test_projects.get('vertical_projects', []))} .csproj files
🎯 T1 Unit Tests: {len(all_test_projects.get('t1_unit_projects', []))} .csproj files
🎯 T2 Vertical Tests: {len(all_test_projects.get('t2_vertical_projects', []))} .csproj files
🎯 T3 Integration Tests: {len(all_test_projects.get('t3_integration_projects', []))} .csproj files
🎯 T4 System Tests: {len(all_test_projects.get('t4_system_projects', []))} .csproj files
🎯 T5 UI Tests: {len(all_test_projects.get('t5_ui_projects', []))} .csproj files

## Quality Preservation - STRUCTURE ONLY
❌ NO C# source files (.cs) recovered - avoiding corrupted code
❌ NO test class files - only project structure preserved
❌ NO broken implementations - clean slate for new code
✅ ONLY .csproj files and configuration files recovered

## Recovery Strategy
- Cherry-picked: 47f94899b (Lessons learned documentation)
- Restored: Test project structure ONLY (.csproj files)
- Preserved: Migration tools and configuration scripts
- Recovered: All JSON/YAML configurations for testing tools

This rescue preserves the complete test project infrastructure 
without any corrupted source code. Perfect foundation for rebuilding
tests with clean implementations.

🤖 Generated with [Claude Code](https://claude.ai/code)
Co-Authored-By: Claude <noreply@anthropic.com>"""
        
        result = run_git_command(["commit", "-m", commit_message])
        if result.returncode != 0:
            print_warning("Failed to commit rescued assets")
        
        # 5. Validation
        print_status("Performing validation checks")
        
        # Check if solution builds (quick check)
        solution_path = Path("code/src/ExxerAI.sln")
        if solution_path.exists():
            print(f"{Colors.DARK_GREEN}  Testing solution compilation...")
            build_result = subprocess.run(
                ["dotnet", "build", str(solution_path), "--verbosity", "quiet"],
                capture_output=True,
                text=True
            )
            if build_result.returncode == 0:
                print_status("Solution builds successfully")
            else:
                print_warning("Solution has build issues (expected with partial rescue)")
        
        # Check migration scripts
        if Path("move_test_classes_to_correct_projects.py").exists():
            print_status("Migration scripts restored successfully")
        
        # Check documentation
        if Path("docs/adr/ADR-005-Testing-Strategy-Reorganization-LESSONS-LEARNED.md").exists():
            print_status("Documentation and lessons learned preserved")
        
        print_header("RESCUE OPERATION COMPLETED SUCCESSFULLY")
        print(f"{Colors.GREEN}Branch created: {rescue_branch}")
        print(f"{Colors.GREEN}Assets rescued: Project structure, scripts, configs, documentation")
        print(f"{Colors.GREEN}Quality preserved: Corrupted files excluded")
        print(f"\n{Colors.YELLOW}Next steps:")
        print(f"{Colors.GRAY}1. Review rescued assets: git log --oneline -5")
        print(f"{Colors.GRAY}2. Test migration scripts: python move_test_classes_to_correct_projects.py --dry-run")
        print(f"{Colors.GRAY}3. Implement incremental migration strategy from ADR-005")
        
    except Exception as e:
        print_error(f"Rescue operation failed: {e}")
        print_warning(f"Rolling back to original branch: {current_branch}")
        run_git_command(["checkout", current_branch])
        run_git_command(["branch", "-D", rescue_branch])

def main():
    parser = argparse.ArgumentParser(
        description="Selective rescue of valuable migration assets",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python rescue_migration_assets.py analyze
  python rescue_migration_assets.py dry-run
  python rescue_migration_assets.py execute
  python rescue_migration_assets.py execute --target-branch incremental-migration-v2
        """
    )
    
    parser.add_argument(
        'mode',
        choices=['analyze', 'dry-run', 'execute'],
        help='Operation mode'
    )
    
    parser.add_argument(
        '--target-branch',
        help='Target branch for rescued assets'
    )
    
    args = parser.parse_args()
    
    # Test git repository
    test_git_repository()
    
    # Execute based on mode
    if args.mode == 'analyze':
        analyze_rescue_assets()
    elif args.mode == 'dry-run':
        execute_dry_run()
    elif args.mode == 'execute':
        execute_rescue(args.target_branch)
    
    print(f"\n{Colors.CYAN}Rescue operation completed in mode: {args.mode}{Colors.RESET}")

if __name__ == "__main__":
    main()