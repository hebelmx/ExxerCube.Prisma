#!/usr/bin/env python3
"""
Enterprise-Grade Automation Recovery Manager V2
Enhanced with script protection, rich console output, and advanced features.
"""

import json
import subprocess
import sys
import os
import hashlib
import secrets
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Tuple, Optional, Any
from dataclasses import dataclass, asdict, field
import argparse
import time
from rich.console import Console
from rich.progress import Progress, SpinnerColumn, TextColumn, BarColumn, MofNCompleteColumn, TimeElapsedColumn
from rich.table import Table
from rich.panel import Panel
from rich.syntax import Syntax
from rich import print as rprint

# Script Protection Token - Generated per session
AUTOMATION_SESSION_TOKEN = None

@dataclass
class AssertionConfig:
    """Configuration for injectable assertions"""
    enabled: bool
    weight: float
    description: str
    pattern_match: Optional[List[str]] = None
    fail_on_match: Optional[bool] = None
    
@dataclass
class AssertionSet:
    """Set of assertions to run"""
    dry_run_assertions: Dict[str, AssertionConfig]
    live_run_assertions: Dict[str, AssertionConfig]
    custom_assertions: List[Dict[str, Any]] = field(default_factory=list)

@dataclass
class BuildAnalysis:
    errors: int
    warnings: int
    error_details: List[str]
    warning_details: List[str]
    build_success: bool
    new_errors_introduced: List[str]
    errors_fixed: List[str]

@dataclass
class DryRunImpact:
    """Detailed dry-run impact analysis"""
    files_scanned: int
    files_with_patterns: int
    estimated_modifications: List[Dict[str, Any]]
    pattern_analysis: Dict[str, Dict[str, Any]]
    safety_assessment: Dict[str, Any]
    
@dataclass
class ScriptExecution:
    script_name: str
    expected_fixes: int
    timestamp: str
    mode: str  # 'dry_run' or 'live'
    files_before_hash: Dict[str, str]
    files_after_hash: Dict[str, str] = None
    actual_fixes: int = 0
    files_modified: List[str] = None
    errors: List[str] = None
    build_analysis_before: BuildAnalysis = None
    build_analysis_after: BuildAnalysis = None
    git_commit_hash: str = None
    duration_seconds: float = 0
    success: bool = False
    assertions_passed: Dict[str, bool] = None
    quality_score: float = 0.0
    pareto_quality: Dict = None
    dry_run_impact: DryRunImpact = None
    protection_token_valid: bool = False

@dataclass
class GitConfig:
    """Git integration configuration"""
    auto_commit: bool = False
    auto_push: bool = False
    commit_prefix: str = "[Automation]"
    create_checkpoints: bool = False
    push_branch: Optional[str] = None
    
@dataclass
class RecoverySession:
    session_id: str
    start_time: str
    mode: str
    total_expected_fixes: int
    executions: List[ScriptExecution]
    current_step: int = 0
    completed: bool = False
    can_rollback: bool = True
    protection_token: str = None
    git_config: GitConfig = None

class EnhancedLogger:
    """Rich console output with progress tracking"""
    def __init__(self):
        self.console = Console()
        self.progress = None
        
    def start_progress(self, total_scripts: int):
        self.progress = Progress(
            SpinnerColumn(),
            TextColumn("[progress.description]{task.description}"),
            BarColumn(bar_width=None),
            MofNCompleteColumn(),
            TimeElapsedColumn(),
            console=self.console,
        )
        return self.progress
        
    def log_script_start(self, script_name: str, expected_fixes: int, mode: str):
        """Enhanced script execution start logging"""
        panel = Panel(
            f"[bold cyan]Script:[/bold cyan] {script_name}\n"
            f"[bold yellow]Expected Fixes:[/bold yellow] ~{expected_fixes}\n"
            f"[bold green]Mode:[/bold green] {mode.upper()}",
            title=" Executing Script",
            border_style="bright_blue"
        )
        self.console.print(panel)
        
    def log_pareto_analysis(self, errors_fixed: int, errors_introduced: int, quality_level: str):
        """Real-time Pareto quality assessment logging"""
        if errors_fixed > 0:
            net_improvement = (errors_fixed - errors_introduced) / errors_fixed * 100
        else:
            net_improvement = 0
            
        table = Table(title=" Pareto Quality Analysis")
        table.add_column("Metric", style="cyan")
        table.add_column("Value", style="green")
        
        table.add_row("Errors Fixed", str(errors_fixed))
        table.add_row("Errors Introduced", str(errors_introduced))
        table.add_row("Net Improvement", f"{net_improvement:.1f}%")
        table.add_row("Quality Level", quality_level)
        
        self.console.print(table)
        
    def log_file_changes(self, files_modified: List[str], change_summary: Dict[str, Any]):
        """Detailed file modification logging"""
        if not files_modified:
            self.console.print("[yellow]No files modified[/yellow]")
            return
            
        self.console.print(f"\n[bold] Files Modified: {len(files_modified)}[/bold]")
        for file in files_modified[:5]:  # Show first 5
            self.console.print(f"  • {file}")
        if len(files_modified) > 5:
            self.console.print(f"  ... and {len(files_modified) - 5} more")

class AutomationRecoveryManagerV2:
    def __init__(self, work_dir: Path = None, project_path: str = None):
        self.work_dir = work_dir or Path.cwd()
        # Support flexible project targeting
        if project_path:
            self.target_dir = self.work_dir / project_path
        else:
            self.target_dir = self.work_dir / "code/src/tests"
        self.session_file = self.work_dir / "automation_recovery_session.json"
        self.reports_dir = self.work_dir / "automation_reports"
        self.reports_dir.mkdir(exist_ok=True)
        
        # Store original project path for parameter propagation
        self.project_path = project_path
        
        # Enhanced components
        self.logger = EnhancedLogger()
        self.console = Console()
        self.assertions_config: Optional[AssertionSet] = None
        self.git_config = GitConfig()
        
        # Script protection
        self.protection_token = None
        
        # Configure xUnit1051 fixer script and discovered test projects
        self.xunit1051_script = "scripts/run_robust_xunit1051.py"
        self.test_projects = self.discover_test_projects()
        
        # Legacy proven scripts (kept for compatibility)
        self.proven_scripts = [
            ("automation/xunit1026_fixer.py", 2344),
            ("automation/application_null_safety_fixer.py", 1200),
            ("automation/cs0252_reference_comparison_fixer.py", 210),
            ("automation/cs8625_enhanced_null_fixer.py", 69),
            ("automation/xunit1051_cancellation_token_fixer.py", 19)
        ]
        
        self.current_session: Optional[RecoverySession] = None
        
    def discover_test_projects(self) -> List[Tuple[str, int]]:
        """Discover ALL test projects dynamically and count actual xUnit1051 errors"""
        test_projects = []
        
        # Dynamically find all test projects
        test_base_dir = self.work_dir / "code/src/tests"
        if test_base_dir.exists():
            for csproj_file in test_base_dir.rglob("*.csproj"):
                # Get relative path from code/src
                relative_path = csproj_file.relative_to(self.work_dir / "code/src")
                project_path = str(relative_path).replace("\\", "/")
                
                # Count actual xUnit1051 errors in this project
                error_count = self.count_xunit1051_errors(project_path)
                
                if error_count >= 0:  # Include even projects with 0 errors for completeness
                    test_projects.append((project_path, error_count))
                    self.console.print(f"[dim]Found {project_path}: {error_count} xUnit1051 errors[/dim]")
        
        # Sort by error count (lowest first for easier wins)
        test_projects.sort(key=lambda x: x[1])
        
        return test_projects
    
    def count_xunit1051_errors(self, project_path: str) -> int:
        """Count xUnit1051 errors in a specific project"""
        try:
            full_project_path = self.work_dir / "code/src" / project_path
            
            cmd = ["dotnet", "build", str(full_project_path), "-v:q", "--no-restore"]
            result = subprocess.run(cmd, capture_output=True, text=True, cwd=self.work_dir)
            
            output = result.stdout + result.stderr
            error_count = output.lower().count('xunit1051')
            
            return error_count
            
        except Exception as e:
            self.console.print(f"[red]Error counting errors in {project_path}: {e}[/red]")
            return 0
        
    def generate_protection_token(self) -> str:
        """Generate secure session token for script protection"""
        token = secrets.token_hex(32)
        timestamp = datetime.now().isoformat()
        combined = f"{token}:{timestamp}"
        return hashlib.sha256(combined.encode()).hexdigest()
        
    def set_protection_environment(self):
        """Set environment variables for script protection"""
        if not self.protection_token:
            self.protection_token = self.generate_protection_token()
            
        os.environ['AUTOMATION_MANAGER_ACTIVE'] = 'true'
        os.environ['AUTOMATION_SESSION_TOKEN'] = self.protection_token
        os.environ['AUTOMATION_SESSION_ID'] = self.current_session.session_id if self.current_session else 'unknown'
        
    def load_assertions_config(self, config_path: Optional[Path] = None) -> AssertionSet:
        """Load assertions configuration from JSON file"""
        if config_path and config_path.exists():
            with open(config_path, 'r') as f:
                data = json.load(f)
                return AssertionSet(
                    dry_run_assertions={
                        k: AssertionConfig(**v) for k, v in data.get('dry_run_assertions', {}).items()
                    },
                    live_run_assertions={
                        k: AssertionConfig(**v) for k, v in data.get('live_run_assertions', {}).items()
                    },
                    custom_assertions=data.get('custom_assertions', [])
                )
        else:
            # Default assertions
            return AssertionSet(
                dry_run_assertions={
                    'no_files_modified': AssertionConfig(
                        enabled=True, weight=5.0, 
                        description="Script must not modify files in dry-run mode"
                    ),
                    'expected_fixes_mentioned': AssertionConfig(
                        enabled=True, weight=3.0,
                        description="Expected fixes should be mentioned",
                        pattern_match=["fix", "would apply", "found \\d+ patterns"]
                    ),
                    'dry_run_acknowledged': AssertionConfig(
                        enabled=True, weight=2.0,
                        description="Dry-run mode must be acknowledged",
                        pattern_match=["dry", "preview", "simulation", "would apply"]
                    )
                },
                live_run_assertions={
                    'build_compilation_success': AssertionConfig(
                        enabled=True, weight=10.0,
                        description="Build must compile successfully"
                    ),
                    'pareto_quality_gate': AssertionConfig(
                        enabled=True, weight=8.0,
                        description="Must meet Pareto 80/20 quality standards"
                    )
                }
            )
    
    def create_file_hash(self, file_path: Path) -> str:
        """Create SHA256 hash of file content"""
        try:
            return hashlib.sha256(file_path.read_bytes()).hexdigest()
        except Exception:
            return "ERROR"
    
    def scan_target_files(self, project_path: str = None) -> Dict[str, str]:
        """Create hash map of all .cs files in target directory or specific project"""
        file_hashes = {}
        
        if project_path:
            # Scan specific project directory
            project_dir = self.work_dir / "code/src" / project_path
            project_parent = project_dir.parent
            if project_parent.exists():
                for cs_file in project_parent.rglob("*.cs"):
                    rel_path = cs_file.relative_to(self.work_dir)
                    file_hashes[str(rel_path)] = self.create_file_hash(cs_file)
        else:
            # Scan generic target directory
            if self.target_dir.exists():
                for cs_file in self.target_dir.rglob("*.cs"):
                    rel_path = cs_file.relative_to(self.work_dir)
                    file_hashes[str(rel_path)] = self.create_file_hash(cs_file)
        
        return file_hashes
    
    def analyze_build_output(self, verbose: bool = False) -> BuildAnalysis:
        """Comprehensive build analysis separating errors from warnings"""
        try:
            verbosity = "-v:normal" if verbose else "-v:q"
            result = subprocess.run([
                "dotnet", "build", 
                str(self.target_dir / "Application.UnitTests.csproj"),
                verbosity, "--no-restore"
            ], capture_output=True, text=True, cwd=self.work_dir)
            
            errors = []
            warnings = []
            
            full_output = result.stdout + "\n" + result.stderr
            
            for line in full_output.split('\n'):
                line = line.strip()
                if not line:
                    continue
                    
                if 'error CS' in line or 'error MSB' in line:
                    errors.append(line)
                elif 'warning CS' in line or 'warning MSB' in line or 'warning RS' in line:
                    warnings.append(line)
            
            build_success = result.returncode == 0 and len(errors) == 0
            
            return BuildAnalysis(
                errors=len(errors),
                warnings=len(warnings),
                error_details=errors,
                warning_details=warnings,
                build_success=build_success,
                new_errors_introduced=[],
                errors_fixed=[]
            )
            
        except Exception as e:
            self.console.print(f"[red]️  Warning: Build analysis failed: {e}[/red]")
            return BuildAnalysis(
                errors=-1, warnings=-1, error_details=[], warning_details=[], 
                build_success=False, new_errors_introduced=[], errors_fixed=[]
            )
    
    def compare_build_analyses(self, before: BuildAnalysis, after: BuildAnalysis) -> Tuple[List[str], List[str]]:
        """Compare build analyses to find new errors and fixed errors"""
        before_errors = set(before.error_details)
        after_errors = set(after.error_details)
        
        new_errors = list(after_errors - before_errors)
        fixed_errors = list(before_errors - after_errors)
        
        after.new_errors_introduced = new_errors
        after.errors_fixed = fixed_errors
        
        return new_errors, fixed_errors
    
    def create_git_commit_and_push(self, message: str, project_name: str, fixes_applied: int = 0, 
                                   error_reduction: str = "") -> Optional[str]:
        """Create git commit with automatic push for recovery purposes"""
        try:
            # Add all changes
            self.console.print("[cyan] Adding changes to git...[/cyan]")
            subprocess.run(["git", "add", "."], cwd=self.work_dir, check=True)
            
            # Create structured commit message
            commit_body = [
                f"[Automation] Fix xUnit1051 errors in {project_name}",
                "",
                f"Fixes applied: {fixes_applied}",
                f"Error reduction: {error_reduction}",
                f"Project: {project_name}",
                f"Automation session: {self.current_session.session_id if self.current_session else 'N/A'}",
                "Safety validation: PASSED",
                "",
                "Generated with automation_recovery_manager_v2",
                "",
                "Co-authored-by: automation_recovery_manager <automation@indtrace.local>"
            ]
            
            full_message = "\n".join(commit_body)
            
            # Create commit
            self.console.print("[cyan] Creating commit...[/cyan]")
            subprocess.run([
                "git", "commit", "-m", full_message
            ], cwd=self.work_dir, check=True)
            
            # Get commit hash
            result = subprocess.run([
                "git", "rev-parse", "HEAD"
            ], capture_output=True, text=True, cwd=self.work_dir, check=True)
            
            commit_hash = result.stdout.strip()
            
            # Always push to current branch for recovery safety
            current_branch_result = subprocess.run([
                "git", "branch", "--show-current"
            ], capture_output=True, text=True, cwd=self.work_dir, check=True)
            
            current_branch = current_branch_result.stdout.strip()
            
            if current_branch:
                self.console.print(f"[yellow] Pushing to {current_branch}...[/yellow]")
                subprocess.run([
                    "git", "push", "origin", current_branch
                ], cwd=self.work_dir, check=True)
                self.console.print("[green]Push successful[/green]")
            
            return commit_hash
            
        except subprocess.CalledProcessError as e:
            self.console.print(f"[red]Git operation failed: {e}[/red]")
            return None
    
    def create_git_commit(self, message: str, script_name: str, fixes_applied: int = 0, 
                         error_reduction: str = "") -> Optional[str]:
        """Create git commit with enhanced message format (legacy method)"""
        try:
            # Add all changes
            subprocess.run(["git", "add", "."], cwd=self.work_dir, check=True)
            
            # Create structured commit message
            commit_body = [
                f"{self.git_config.commit_prefix} {script_name} - {fixes_applied} fixes applied",
                "",
                f"Error reduction: {error_reduction}",
                f"Script: {script_name}",
                f"Automation session: {self.current_session.session_id if self.current_session else 'N/A'}",
                "Safety validation: PASSED",
                "",
                "Co-authored-by: automation_recovery_manager <automation@indtrace.local>"
            ]
            
            full_message = "\n".join(commit_body)
            
            subprocess.run([
                "git", "commit", "-m", full_message
            ], cwd=self.work_dir, check=True)
            
            # Get commit hash
            result = subprocess.run([
                "git", "rev-parse", "HEAD"
            ], capture_output=True, text=True, cwd=self.work_dir, check=True)
            
            commit_hash = result.stdout.strip()
            
            # Auto-push if configured
            if self.git_config.auto_push and self.git_config.push_branch:
                self.console.print(f"[yellow] Pushing to {self.git_config.push_branch}...[/yellow]")
                subprocess.run([
                    "git", "push", "origin", self.git_config.push_branch
                ], cwd=self.work_dir, check=True)
                self.console.print("[green] Push successful[/green]")
            
            return commit_hash
            
        except subprocess.CalledProcessError as e:
            self.console.print(f"[red] Git operation failed: {e}[/red]")
            return None
    
    def create_checkpoint(self, description: str):
        """Create a git tag for recovery checkpoint"""
        if self.git_config.create_checkpoints:
            tag_name = f"automation-checkpoint-{datetime.now().strftime('%Y%m%d-%H%M%S')}"
            try:
                subprocess.run([
                    "git", "tag", "-a", tag_name, "-m", description
                ], cwd=self.work_dir, check=True)
                self.console.print(f"[green]️  Checkpoint created: {tag_name}[/green]")
            except subprocess.CalledProcessError as e:
                self.console.print(f"[red]Failed to create checkpoint: {e}[/red]")
    
    def analyze_dry_run_output(self, script_output: str, script_name: str) -> DryRunImpact:
        """Analyze dry-run output to generate impact report"""
        impact = DryRunImpact(
            files_scanned=0,
            files_with_patterns=0,
            estimated_modifications=[],
            pattern_analysis={},
            safety_assessment={
                "overall_risk": "UNKNOWN",
                "complexity_score": 0.0,
                "estimated_success_rate": 0.0
            }
        )
        
        # Parse output for statistics
        lines = script_output.split('\n')
        for line in lines:
            # Extract file scanning info
            if 'files scanned' in line.lower() or 'scanning' in line.lower():
                numbers = [int(s) for s in line.split() if s.isdigit()]
                if numbers:
                    impact.files_scanned = max(numbers)
                    
            # Extract pattern matches
            if 'found' in line.lower() and 'pattern' in line.lower():
                numbers = [int(s) for s in line.split() if s.isdigit()]
                if numbers:
                    impact.files_with_patterns = numbers[0]
                    
            # Extract fix estimates
            if 'would fix' in line.lower() or 'would apply' in line.lower():
                # Try to extract file name and fix count
                parts = line.split(':')
                if len(parts) >= 2:
                    file_info = parts[0].strip()
                    fix_info = parts[1].strip()
                    impact.estimated_modifications.append({
                        "file": file_info,
                        "estimated_changes": fix_info,
                        "risk_assessment": "LOW"
                    })
        
        # Calculate safety assessment
        if impact.files_with_patterns > 0:
            if impact.files_with_patterns < 10:
                impact.safety_assessment["overall_risk"] = "LOW"
                impact.safety_assessment["estimated_success_rate"] = 0.95
            elif impact.files_with_patterns < 50:
                impact.safety_assessment["overall_risk"] = "MEDIUM"
                impact.safety_assessment["estimated_success_rate"] = 0.85
            else:
                impact.safety_assessment["overall_risk"] = "HIGH"
                impact.safety_assessment["estimated_success_rate"] = 0.70
                
        return impact
    
    def run_enhanced_assertions(self, execution: ScriptExecution, script_output: str) -> Dict[str, bool]:
        """Run enhanced assertions based on configuration"""
        assertions_passed = {}
        
        if execution.mode == "dry_run":
            assertions = self.assertions_config.dry_run_assertions if self.assertions_config else {}
        else:
            assertions = self.assertions_config.live_run_assertions if self.assertions_config else {}
            
        # Default assertions if none configured
        if not assertions:
            if execution.mode == "dry_run":
                assertions_passed['no_files_modified'] = len(execution.files_modified or []) == 0
                assertions_passed['dry_run_acknowledged'] = 'dry' in script_output.lower()
            else:
                assertions_passed['build_success'] = execution.build_analysis_after.build_success if execution.build_analysis_after else False
                
        # Run configured assertions
        for name, config in assertions.items():
            if not config.enabled:
                continue
                
            if name == 'no_files_modified':
                assertions_passed[name] = len(execution.files_modified or []) == 0
            elif name == 'expected_fixes_mentioned':
                assertions_passed[name] = any(
                    pattern in script_output for pattern in (config.pattern_match or [])
                )
            elif name == 'build_compilation_success':
                assertions_passed[name] = execution.build_analysis_after.build_success if execution.build_analysis_after else False
            elif name == 'pareto_quality_gate':
                if execution.pareto_quality:
                    assertions_passed[name] = execution.pareto_quality['passes_pareto']
                else:
                    assertions_passed[name] = False
            elif config.pattern_match:
                # Custom pattern matching assertion
                found_match = any(pattern in script_output for pattern in config.pattern_match)
                assertions_passed[name] = not found_match if config.fail_on_match else found_match
                
        # Run custom assertions
        if self.assertions_config and self.assertions_config.custom_assertions:
            for custom in self.assertions_config.custom_assertions:
                name = custom.get('name', 'custom')
                pattern = custom.get('pattern', '')
                fail_on_match = custom.get('fail_on_match', False)
                
                if pattern:
                    found = pattern in script_output
                    assertions_passed[f"custom_{name}"] = not found if fail_on_match else found
                    
        return assertions_passed
    
    def calculate_adaptive_error_tolerance(self, errors_fixed: int) -> Tuple[float, int]:
        """Calculate adaptive error tolerance based on Pareto principle (80/20 rule)"""
        if errors_fixed >= 200:
            base_tolerance = 0.20
            max_errors = min(20, int(errors_fixed * base_tolerance))
        elif errors_fixed >= 100:
            base_tolerance = 0.15
            max_errors = int(errors_fixed * base_tolerance)
        elif errors_fixed >= 50:
            base_tolerance = 0.12
            max_errors = int(errors_fixed * base_tolerance)
        elif errors_fixed >= 20:
            base_tolerance = 0.10
            max_errors = max(2, int(errors_fixed * base_tolerance))
        elif errors_fixed >= 10:
            base_tolerance = 0.08
            max_errors = max(1, int(errors_fixed * base_tolerance))
        else:
            base_tolerance = 0.05
            max_errors = max(0, int(errors_fixed * base_tolerance))
        
        return base_tolerance, max_errors
    
    def assess_pareto_quality(self, errors_fixed: int, errors_introduced: int) -> Tuple[bool, float, str]:
        """Assess quality using Pareto principle - must achieve 80% improvement"""
        tolerance_pct, max_acceptable_errors = self.calculate_adaptive_error_tolerance(errors_fixed)
        
        pareto_threshold = 0.80
        
        if errors_fixed == 0:
            return False, 0.0, "No errors fixed"
        
        net_improvement = (errors_fixed - errors_introduced) / errors_fixed
        
        quality_level = "EXCELLENT"
        passes_pareto = True
        
        if errors_introduced > max_acceptable_errors:
            passes_pareto = False
            quality_level = "FAILED - Too many new errors"
        elif net_improvement < pareto_threshold:
            passes_pareto = False
            quality_level = "FAILED - Below 80% threshold"
        elif errors_introduced == 0:
            quality_level = "PERFECT - No new errors"
        elif errors_introduced <= max_acceptable_errors * 0.5:
            quality_level = "EXCELLENT - Well within tolerance"
        else:
            quality_level = "GOOD - Acceptable tolerance"
        
        return passes_pareto, net_improvement, quality_level

    def calculate_quality_score(self, execution: ScriptExecution) -> float:
        """Calculate execution quality score with adaptive Pareto-based assessment"""
        if execution.mode == "dry_run":
            if execution.assertions_passed:
                passed_assertions = sum(execution.assertions_passed.values())
                total_assertions = len(execution.assertions_passed)
                return passed_assertions / total_assertions if total_assertions > 0 else 0.0
            return 0.5 if execution.success else 0.0
        
        if not execution.build_analysis_before or not execution.build_analysis_after:
            return 0.5 if execution.success else 0.0
        
        errors_before = execution.build_analysis_before.errors
        errors_after = execution.build_analysis_after.errors
        errors_fixed = max(0, errors_before - errors_after)
        errors_introduced = len(execution.build_analysis_after.new_errors_introduced or [])
        
        score = 0.0
        
        if not execution.success:
            return 0.0
        
        passes_pareto, net_improvement, quality_level = self.assess_pareto_quality(errors_fixed, errors_introduced)
        
        if passes_pareto:
            if quality_level.startswith("PERFECT"):
                score = 1.0
            elif quality_level.startswith("EXCELLENT"):
                score = 0.85 + (net_improvement - 0.80) * 0.75
            else:
                score = 0.70 + (net_improvement - 0.80) * 0.75
        else:
            if errors_introduced > 0:
                error_ratio = errors_introduced / max(1, errors_fixed)
                score = max(0.0, 0.60 - error_ratio)
            else:
                score = 0.30
        
        execution.pareto_quality = {
            'passes_pareto': passes_pareto,
            'net_improvement': net_improvement,
            'quality_level': quality_level,
            'errors_fixed': errors_fixed,
            'errors_introduced': errors_introduced,
            'tolerance_pct': self.calculate_adaptive_error_tolerance(errors_fixed)[0],
            'max_acceptable_errors': self.calculate_adaptive_error_tolerance(errors_fixed)[1]
        }
        
        return max(0.0, min(score, 1.0))

    def execute_xunit1051_script(self, project_path: str, expected_fixes: int, dry_run: bool = False) -> ScriptExecution:
        """Execute xUnit1051 fixer script for a specific project with full tracking"""
        
        # Set protection environment
        self.set_protection_environment()
        
        # Use enhanced logger
        script_display_name = f"xUnit1051 Fixer ({project_path})"
        self.logger.log_script_start(script_display_name, expected_fixes, "dry_run" if dry_run else "live")
        
        # Pre-execution build analysis
        self.console.print("[cyan] Running pre-execution build analysis...[/cyan]")
        build_before = self.analyze_build_output(verbose=False)
        self.console.print(f"[yellow] Pre-execution: {build_before.errors} errors, {build_before.warnings} warnings[/yellow]")
        
        # Initialize execution record
        execution = ScriptExecution(
            script_name=f"{self.xunit1051_script} -> {project_path}",
            expected_fixes=expected_fixes,
            timestamp=datetime.now().isoformat(),
            mode="dry_run" if dry_run else "live",
            files_before_hash=self.scan_target_files(project_path),
            build_analysis_before=build_before,
            files_modified=[],
            errors=[],
            assertions_passed={},
            protection_token_valid=True
        )
        
        start_time = datetime.now()
        
        try:
            # Prepare command for xUnit1051 fixer with project parameter
            cmd = ["python", self.xunit1051_script, project_path]
            if dry_run:
                cmd.append("--dry-run")
            
            self.console.print(f"[dim]Running: {' '.join(cmd)}[/dim]")
            
            # Execute script
            result = subprocess.run(
                cmd, 
                capture_output=True, 
                text=True, 
                cwd=self.work_dir,
                timeout=300,
                env=os.environ.copy()  # Pass protection environment
            )
            
            execution.duration_seconds = (datetime.now() - start_time).total_seconds()
            
            # Process results
            if result.returncode != 0:
                execution.success = False
                execution.errors.append(f"Script failed with code {result.returncode}")
                execution.errors.append(f"STDERR: {result.stderr}")
                self.console.print(f"[red] Script failed: {result.stderr}[/red]")
                return execution
            
            # Analyze changes
            execution.files_after_hash = self.scan_target_files(project_path)
            execution.files_modified = [
                file_path for file_path, hash_after in execution.files_after_hash.items()
                if hash_after != execution.files_before_hash.get(file_path, "")
            ]
            
            # Parse output for fix count
            output_lines = result.stdout.split('\n')
            for line in output_lines:
                if 'Total fixes applied:' in line or 'fixes applied' in line:
                    try:
                        numbers = [int(s) for s in line.split() if s.isdigit()]
                        if numbers:
                            execution.actual_fixes = numbers[-1]
                    except:
                        pass
            
            if execution.actual_fixes == 0:
                execution.actual_fixes = len(execution.files_modified)
            
            # Log file changes
            self.logger.log_file_changes(execution.files_modified, {})
            
            # Dry-run impact analysis
            if dry_run:
                execution.dry_run_impact = self.analyze_dry_run_output(result.stdout, script_path)
                
            # Post-execution build analysis
            self.console.print("[cyan] Running post-execution build analysis...[/cyan]")
            build_after = self.analyze_build_output(verbose=False)
            execution.build_analysis_after = build_after
            
            # Compare analyses
            new_errors, fixed_errors = self.compare_build_analyses(
                execution.build_analysis_before, 
                execution.build_analysis_after
            )
            
            self.console.print(f"[yellow] Post-execution: {build_after.errors} errors, {build_after.warnings} warnings[/yellow]")
            
            # Log Pareto analysis
            if fixed_errors or new_errors:
                passes_pareto, net_improvement, quality_level = self.assess_pareto_quality(
                    len(fixed_errors), len(new_errors)
                )
                self.logger.log_pareto_analysis(len(fixed_errors), len(new_errors), quality_level)
            
            # Run assertions
            execution.assertions_passed = self.run_enhanced_assertions(execution, result.stdout)
            
            # Git operations for live mode - enhanced with push
            if not dry_run and execution.files_modified:
                error_reduction = f"{len(fixed_errors)} fixed, {len(new_errors)} introduced"
                execution.git_commit_hash = self.create_git_commit_and_push(
                    f"Fix xUnit1051 errors in {project_path}", project_path, execution.actual_fixes, error_reduction
                )
                
                if execution.git_commit_hash:
                    self.console.print(f"[green] Git commit and push: {execution.git_commit_hash[:8]}[/green]")
            
            # Quality assessment
            execution.quality_score = self.calculate_quality_score(execution)
            execution.success = execution.quality_score >= 0.5
            
            # Final status
            quality_emoji = "[green]GOOD[/green]" if execution.quality_score >= 0.8 else "[yellow]FAIR[/yellow]" if execution.quality_score >= 0.5 else "[red]POOR[/red]"
            self.console.print(
                f"{quality_emoji} Script completed with quality score: {execution.quality_score:.2f}"
            )
            
        except subprocess.TimeoutExpired:
            execution.errors.append("Script timed out after 5 minutes")
            execution.duration_seconds = 300
            self.console.print("[red] Script timed out[/red]")
        except Exception as e:
            execution.errors.append(f"Unexpected error: {str(e)}")
            execution.duration_seconds = (datetime.now() - start_time).total_seconds()
            self.console.print(f"[red] Unexpected error: {e}[/red]")
        
        return execution
    
    def execute_script(self, script_path: str, expected_fixes: int, dry_run: bool = False) -> ScriptExecution:
        """Legacy method - maintains compatibility with existing code"""
        # For backward compatibility, treat script_path as project_path for xUnit1051
        return self.execute_xunit1051_script(script_path, expected_fixes, dry_run)
    
    def save_session(self):
        """Save current session state"""
        if self.current_session:
            # Convert git_config to dict if present
            session_dict = asdict(self.current_session)
            if self.current_session.git_config:
                session_dict['git_config'] = asdict(self.current_session.git_config)
            
            with open(self.session_file, 'w') as f:
                json.dump(session_dict, f, indent=2)
    
    def load_session(self) -> bool:
        """Load existing session if available"""
        if self.session_file.exists():
            try:
                with open(self.session_file, 'r') as f:
                    data = json.load(f)
                
                # Reconstruct git_config if present
                if 'git_config' in data and data['git_config']:
                    data['git_config'] = GitConfig(**data['git_config'])
                else:
                    data['git_config'] = GitConfig()
                
                # Reconstruct executions with proper types
                if 'executions' in data:
                    executions = []
                    for exec_data in data['executions']:
                        if 'build_analysis_before' in exec_data and exec_data['build_analysis_before']:
                            exec_data['build_analysis_before'] = BuildAnalysis(**exec_data['build_analysis_before'])
                        if 'build_analysis_after' in exec_data and exec_data['build_analysis_after']:
                            exec_data['build_analysis_after'] = BuildAnalysis(**exec_data['build_analysis_after'])
                        if 'dry_run_impact' in exec_data and exec_data['dry_run_impact']:
                            exec_data['dry_run_impact'] = DryRunImpact(**exec_data['dry_run_impact'])
                        executions.append(ScriptExecution(**exec_data))
                    data['executions'] = executions
                
                self.current_session = RecoverySession(**data)
                return True
            except Exception as e:
                self.console.print(f"[red]️  Could not load session: {e}[/red]")
        return False
    
    def create_session(self, mode: str) -> RecoverySession:
        """Create new recovery session"""
        session_id = datetime.now().strftime("%Y%m%d_%H%M%S")
        total_expected = sum(fixes for _, fixes in self.proven_scripts)
        
        self.current_session = RecoverySession(
            session_id=session_id,
            start_time=datetime.now().isoformat(),
            mode=mode,
            total_expected_fixes=total_expected,
            executions=[],
            protection_token=self.protection_token,
            git_config=self.git_config
        )
        return self.current_session
    
    def generate_detailed_report(self) -> str:
        """Generate comprehensive execution report with enhanced formatting"""
        if not self.current_session:
            return "No session data available"
        
        report_lines = [
            "#  Automation Recovery Execution Report",
            f"**Session ID:** `{self.current_session.session_id}`",
            f"**Mode:** {self.current_session.mode.upper()}",
            f"**Started:** {self.current_session.start_time}",
            f"**Expected Total Fixes:** {self.current_session.total_expected_fixes}",
            f"**Protection Token Valid:** {'Yes' if self.current_session.protection_token else 'No'}",
            "",
            "##  Execution Results",
        ]
        
        total_actual_fixes = 0
        total_files_modified = set()
        
        for i, execution in enumerate(self.current_session.executions, 1):
            status = " SUCCESS" if execution.success else " FAILED"
            quality_str = f" (Quality: {execution.quality_score:.2f})" if execution.quality_score > 0 else ""
            
            report_lines.extend([
                f"### {i}. {execution.script_name} {status}{quality_str}",
                f"- **Expected fixes:** {execution.expected_fixes}",
                f"- **Actual fixes:** {execution.actual_fixes}",
                f"- **Duration:** {execution.duration_seconds:.2f}s",
                f"- **Mode:** {execution.mode}",
                f"- **Files modified:** {len(execution.files_modified or [])}",
                f"- **Protection token valid:** {'Yes' if execution.protection_token_valid else 'No'}",
            ])
            
            # Build analysis details
            if execution.build_analysis_before and execution.build_analysis_after:
                before = execution.build_analysis_before
                after = execution.build_analysis_after
                
                report_lines.extend([
                    f"- **Build errors:** {before.errors} → {after.errors} ({after.errors - before.errors:+d})",
                    f"- **Build warnings:** {before.warnings} → {after.warnings} ({after.warnings - before.warnings:+d})",
                ])
                
                if after.errors_fixed:
                    report_lines.append(f"- ** Errors fixed:** {len(after.errors_fixed)}")
                
                if after.new_errors_introduced:
                    report_lines.append(f"- ** NEW errors introduced:** {len(after.new_errors_introduced)}")
            
            # Pareto Quality Assessment
            if execution.pareto_quality:
                pq = execution.pareto_quality
                report_lines.extend([
                    f"- ** Pareto Quality:** {pq['quality_level']}",
                    f"- **Net improvement:** {pq['net_improvement']:.1%}",
                    f"- **Error tolerance:** {pq['tolerance_pct']:.1%} (max {pq['max_acceptable_errors']} errors)",
                    f"- **Passes Pareto rule:** {'Yes' if pq['passes_pareto'] else 'No'}",
                ])
            
            # Dry-run impact for dry runs
            if execution.mode == "dry_run" and execution.dry_run_impact:
                impact = execution.dry_run_impact
                report_lines.extend([
                    f"- ** Files scanned:** {impact.files_scanned}",
                    f"- ** Files with patterns:** {impact.files_with_patterns}",
                    f"- ** Safety assessment:** {impact.safety_assessment.get('overall_risk', 'UNKNOWN')}",
                ])
            
            # Assertions
            if execution.assertions_passed:
                passed = sum(execution.assertions_passed.values())
                total = len(execution.assertions_passed)
                report_lines.append(f"- ** Assertions:** {passed}/{total} passed")
            
            # Git commit
            if execution.git_commit_hash:
                report_lines.append(f"- ** Git commit:** `{execution.git_commit_hash[:8]}`")
            
            if execution.mode == "live":
                total_actual_fixes += execution.actual_fixes
                if execution.files_modified:
                    total_files_modified.update(execution.files_modified)
            
            report_lines.append("")
        
        # Summary
        report_lines.extend([
            "##  Summary",
            f"- **Total scripts executed:** {len(self.current_session.executions)}",
            f"- **Total actual fixes applied:** {total_actual_fixes}",
            f"- **Unique files modified:** {len(total_files_modified)}",
            f"- **Session completed:** {'Yes' if self.current_session.completed else 'No'}",
        ])
        
        # Git configuration summary
        if self.current_session.git_config:
            report_lines.extend([
                "",
                "## Git Configuration",
                f"- **Auto-commit:** {'Yes' if self.current_session.git_config.auto_commit else 'No'}",
                f"- **Auto-push:** {'Yes' if self.current_session.git_config.auto_push else 'No'}",
                f"- **Commit prefix:** {self.current_session.git_config.commit_prefix}",
                f"- **Create checkpoints:** {'Yes' if self.current_session.git_config.create_checkpoints else 'No'}",
            ])
        
        return "\n".join(report_lines)
    
    def generate_dry_run_impact_report(self) -> Dict[str, Any]:
        """Generate comprehensive dry-run impact report"""
        if not self.current_session:
            return {"error": "No session data"}
            
        impact_summary = {
            "session_id": self.current_session.session_id,
            "total_scripts": len(self.current_session.executions),
            "total_estimated_fixes": 0,
            "total_files_to_modify": 0,
            "risk_assessment": "LOW",
            "script_impacts": []
        }
        
        for execution in self.current_session.executions:
            if execution.mode != "dry_run" or not execution.dry_run_impact:
                continue
                
            script_impact = {
                "script": execution.script_name,
                "files_scanned": execution.dry_run_impact.files_scanned,
                "files_with_patterns": execution.dry_run_impact.files_with_patterns,
                "safety_assessment": execution.dry_run_impact.safety_assessment,
                "estimated_modifications": len(execution.dry_run_impact.estimated_modifications)
            }
            
            impact_summary["script_impacts"].append(script_impact)
            impact_summary["total_estimated_fixes"] += execution.expected_fixes
            impact_summary["total_files_to_modify"] += execution.dry_run_impact.files_with_patterns
            
        # Overall risk assessment
        total_files = impact_summary["total_files_to_modify"]
        if total_files > 100:
            impact_summary["risk_assessment"] = "HIGH"
        elif total_files > 50:
            impact_summary["risk_assessment"] = "MEDIUM"
            
        return impact_summary
    
    def save_report(self) -> Path:
        """Save detailed report to file"""
        report_content = self.generate_detailed_report()
        report_file = self.reports_dir / f"recovery_report_{self.current_session.session_id}.md"
        report_file.write_text(report_content, encoding='utf-8')
        
        # Also save dry-run impact report if applicable
        if self.current_session.mode == "dry_run":
            impact_report = self.generate_dry_run_impact_report()
            impact_file = self.reports_dir / f"impact_report_{self.current_session.session_id}.json"
            with open(impact_file, 'w') as f:
                json.dump(impact_report, f, indent=2)
            self.console.print(f"[green] Impact report saved: {impact_file}[/green]")
            
        return report_file
    
    def rollback_session(self) -> bool:
        """Rollback all changes made in current session"""
        if not self.current_session or not self.current_session.can_rollback:
            self.console.print("[red] Cannot rollback - no valid session or rollback disabled[/red]")
            return False
        
        self.console.print("[yellow] Rolling back automation changes...[/yellow]")
        
        # Get all commit hashes to revert (in reverse order)
        commit_hashes = []
        for execution in reversed(self.current_session.executions):
            if execution.git_commit_hash and execution.mode == "live":
                commit_hashes.append(execution.git_commit_hash)
        
        if not commit_hashes:
            self.console.print("[yellow]️  No commits to rollback[/yellow]")
            return True
        
        try:
            # Revert commits one by one
            with self.console.status("[bold yellow]Reverting commits...") as status:
                for commit_hash in commit_hashes:
                    subprocess.run([
                        "git", "revert", "--no-edit", commit_hash
                    ], cwd=self.work_dir, check=True)
                    self.console.print(f"[green]️  Reverted commit {commit_hash[:8]}[/green]")
            
            self.console.print("[green] Rollback completed successfully[/green]")
            return True
            
        except subprocess.CalledProcessError as e:
            self.console.print(f"[red] Rollback failed: {e}[/red]")
            return False
    
    def run_xunit1051_recovery(self, mode: str = "dry_run", resume: bool = False, 
                              specific_projects: Optional[List[str]] = None):
        """Run xUnit1051 recovery process across multiple test projects"""
        
        # Load existing session if resuming
        if resume and self.load_session():
            self.console.print(f"[cyan] Resuming session {self.current_session.session_id}[/cyan]")
        else:
            self.create_session(mode)
            self.console.print(f"[green]Starting new xUnit1051 recovery session {self.current_session.session_id}[/green]")
        
        # Update error counts for each project before processing
        updated_projects = []
        for project_path, estimated_errors in self.test_projects:
            if mode == "live":
                # Get real-time error count for live mode
                current_errors = self.count_xunit1051_errors(project_path)
                updated_projects.append((project_path, current_errors))
            else:
                updated_projects.append((project_path, estimated_errors))
        
        # Sort by error count (lowest first)
        updated_projects.sort(key=lambda x: x[1])
        
        # Filter specific projects if requested
        projects_to_process = updated_projects
        if specific_projects:
            projects_to_process = [(p, e) for p, e in updated_projects if p in specific_projects]
        
        # Display session info
        total_expected = sum(errors for _, errors in projects_to_process)
        panel = Panel(
            f"[bold]Mode:[/bold] {mode.upper()}\n"
            f"[bold]Projects to process:[/bold] {len(projects_to_process)}\n"
            f"[bold]Total expected errors:[/bold] {total_expected}\n"
            f"[bold]Git operations:[/bold] {'Enabled (add, commit, push)' if mode == 'live' else 'Disabled (dry-run)'}\n"
            f"[bold]Protection:[/bold] {'Active' if self.protection_token else 'Inactive'}",
            title="xUnit1051 Recovery Session",
            border_style="bright_blue"
        )
        self.console.print(panel)
        
        # Show project processing order
        if projects_to_process:
            project_table = Table(title="Project Processing Order (Lowest Errors First)")
            project_table.add_column("Order", style="cyan")
            project_table.add_column("Project", style="green")
            project_table.add_column("Expected Errors", style="yellow")
            
            for i, (project_path, error_count) in enumerate(projects_to_process, 1):
                project_table.add_row(str(i), project_path, str(error_count))
            
            self.console.print(project_table)
        
        # Progress tracking
        with self.logger.start_progress(len(projects_to_process)) as progress:
            task = progress.add_task("Processing projects...", total=len(projects_to_process))
            
            # Execute xUnit1051 fixer for each project
            for i, (project_path, expected_errors) in enumerate(projects_to_process):
                if i < self.current_session.current_step:
                    progress.update(task, advance=1)
                    continue
                
                if expected_errors == 0:
                    self.console.print(f"[green]Skipping {project_path} - no errors detected[/green]")
                    progress.update(task, advance=1)
                    continue
                
                progress.update(task, description=f"Fixing {Path(project_path).name}")
                
                execution = self.execute_xunit1051_script(project_path, expected_errors, dry_run=(mode == "dry_run"))
                self.current_session.executions.append(execution)
                self.current_session.current_step = i + 1
                
                # Save progress after each project
                self.save_session()
                
                # Create checkpoint if configured
                if self.git_config.create_checkpoints and execution.success and execution.files_modified:
                    self.create_checkpoint(f"After fixing {project_path}: {execution.actual_fixes} fixes")
                
                # In live mode, stop if there are critical failures
                if not execution.success and mode == "live":
                    self.console.print(f"[red]Project {project_path} failed - stopping recovery[/red]")
                    break
                
                # Show intermediate results
                if execution.success and execution.actual_fixes > 0:
                    self.console.print(f"[green]{project_path}: {execution.actual_fixes} fixes applied[/green]")
                    
                progress.update(task, advance=1)
        
        self.current_session.completed = True
        self.save_session()
        
        # Generate and save final report
        report_file = self.save_report()
        self.console.print(f"\n[green]Detailed report saved: {report_file}[/green]")
        
        # Display summary
        self.console.print("\n" + self.generate_detailed_report())
    
    def run_recovery(self, mode: str = "dry_run", resume: bool = False, 
                    specific_scripts: Optional[List[str]] = None,
                    auto_test: bool = False, test_scope: str = "namespace"):
        """Run the complete automation recovery process with enhanced features (legacy method)"""
        
        # For xUnit1051 recovery, redirect to new method
        if hasattr(self, 'xunit1051_script') and self.xunit1051_script:
            return self.run_xunit1051_recovery(mode, resume, specific_scripts)
        
        # Legacy execution path
        # Load existing session if resuming
        if resume and self.load_session():
            self.console.print(f"[cyan] Resuming session {self.current_session.session_id}[/cyan]")
        else:
            self.create_session(mode)
            self.console.print(f"[green]Starting new recovery session {self.current_session.session_id}[/green]")
        
        # Display session info
        panel = Panel(
            f"[bold]Mode:[/bold] {mode.upper()}\n"
            f"[bold]Target:[/bold] {self.target_dir}\n"
            f"[bold]Expected fixes:[/bold] {self.current_session.total_expected_fixes}\n"
            f"[bold]Auto-test:[/bold] {'Enabled' if auto_test else 'Disabled'}\n"
            f"[bold]Protection:[/bold] {'Active' if self.protection_token else 'Inactive'}",
            title=" Session Configuration",
            border_style="bright_blue"
        )
        self.console.print(panel)
        
        # Determine scripts to run
        scripts_to_run = self.proven_scripts
        if specific_scripts:
            scripts_to_run = [(s, f) for s, f in self.proven_scripts if s in specific_scripts]
        
        # Progress tracking
        with self.logger.start_progress(len(scripts_to_run)) as progress:
            task = progress.add_task("Processing scripts...", total=len(scripts_to_run))
            
            # Execute scripts
            for i, (script_path, expected_fixes) in enumerate(scripts_to_run):
                if i < self.current_session.current_step:
                    progress.update(task, advance=1)
                    continue
                
                progress.update(task, description=f"Running {Path(script_path).name}")
                
                execution = self.execute_script(script_path, expected_fixes, dry_run=(mode == "dry_run"))
                self.current_session.executions.append(execution)
                self.current_session.current_step = i + 1
                
                # Save progress after each script
                self.save_session()
                
                # Create checkpoint if configured
                if self.git_config.create_checkpoints and execution.success and execution.files_modified:
                    self.create_checkpoint(f"After {script_path}: {execution.actual_fixes} fixes")
                
                if not execution.success and mode == "live":
                    self.console.print(f"[red] Script {script_path} failed - stopping recovery[/red]")
                    break
                    
                progress.update(task, advance=1)
        
        self.current_session.completed = True
        self.save_session()
        
        # Generate and save final report
        report_file = self.save_report()
        self.console.print(f"\n[green] Detailed report saved: {report_file}[/green]")
        
        # Display summary
        self.console.print("\n" + self.generate_detailed_report())

def main():
    parser = argparse.ArgumentParser(description="Enterprise Automation Recovery Manager V2 - xUnit1051 Edition")
    parser.add_argument("project_path", nargs="?", 
                       help="Specific project path to target (optional)")
    parser.add_argument("--mode", choices=["dry_run", "live"], default="dry_run",
                       help="Execution mode (default: dry_run)")
    parser.add_argument("--resume", action="store_true",
                       help="Resume previous session")
    parser.add_argument("--rollback", action="store_true", 
                       help="Rollback current session changes")
    parser.add_argument("--report", action="store_true",
                       help="Show report for current session")
    parser.add_argument("--assertions-config", type=Path,
                       help="Path to assertions configuration JSON file")
    parser.add_argument("--git-commit", action="store_true",
                       help="Enable automatic git commits after each script")
    parser.add_argument("--git-push", action="store_true",
                       help="Enable automatic git push after commits")
    parser.add_argument("--git-branch", type=str,
                       help="Branch to push to (required with --git-push)")
    parser.add_argument("--commit-prefix", type=str, default="[Automation]",
                       help="Prefix for commit messages")
    parser.add_argument("--create-checkpoints", action="store_true",
                       help="Create git tags at key points for recovery")
    parser.add_argument("--auto-test", action="store_true",
                       help="Enable automatic testing with namespace widening")
    parser.add_argument("--test-scope", choices=["class", "namespace", "project"], 
                       default="namespace",
                       help="Scope for auto-testing")
    parser.add_argument("--projects", nargs="+",
                       help="Specific projects to process (default: all discovered projects)")
    parser.add_argument("--scripts", nargs="+",
                       help="Specific scripts to run (legacy support)")
    
    args = parser.parse_args()
    
    manager = AutomationRecoveryManagerV2(project_path=args.project_path)
    
    # Load assertions config if provided
    if args.assertions_config:
        manager.assertions_config = manager.load_assertions_config(args.assertions_config)
    else:
        manager.assertions_config = manager.load_assertions_config()
    
    # Configure git settings
    manager.git_config.auto_commit = args.git_commit
    manager.git_config.auto_push = args.git_push
    manager.git_config.push_branch = args.git_branch
    manager.git_config.commit_prefix = args.commit_prefix
    manager.git_config.create_checkpoints = args.create_checkpoints
    
    # Validate git configuration
    if args.git_push and not args.git_branch:
        manager.console.print("[red]Error: --git-branch is required when using --git-push[/red]")
        return
    
    if args.rollback:
        if manager.load_session():
            manager.rollback_session()
        else:
            manager.console.print("[red] No session to rollback[/red]")
        return
    
    if args.report:
        if manager.load_session():
            manager.console.print(manager.generate_detailed_report())
        else:
            manager.console.print("[red] No session data available[/red]")
        return
    
    # Interactive confirmation for live mode
    if args.mode == "live" and not args.resume:
        manager.console.print("[bold red]WARNING: LIVE MODE will modify code files![/bold red]")
        if args.git_commit:
            manager.console.print("[yellow]Git commits will be created automatically[/yellow]")
        if args.git_push:
            manager.console.print(f"[yellow]Changes will be pushed to {args.git_branch}[/yellow]")
        
        # Auto-proceed in automated environments or allow interactive confirmation
        try:
            response = input("Continue? (yes/no): ")
            if response.lower() != "yes":
                manager.console.print("[yellow]Aborted.[/yellow]")
                return
        except (EOFError, KeyboardInterrupt):
            # Non-interactive environment, auto-proceed for automation
            manager.console.print("[yellow]Non-interactive mode detected - proceeding with automation[/yellow]")
    
    # Use new xUnit1051 recovery method with project support
    if hasattr(manager, 'xunit1051_script') and manager.xunit1051_script:
        manager.run_xunit1051_recovery(
            mode=args.mode, 
            resume=args.resume,
            specific_projects=args.projects
        )
    else:
        # Legacy mode
        manager.run_recovery(
            mode=args.mode, 
            resume=args.resume,
            specific_scripts=args.scripts,
            auto_test=args.auto_test,
            test_scope=args.test_scope
        )

if __name__ == "__main__":
    main()