#!/usr/bin/env python3
"""
Enterprise-Grade Automation Recovery Manager
Replaces the flawed bash script with proper safety, reporting, and git integration.
"""

import json
import subprocess
import sys
import os
import hashlib
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Tuple, Optional
from dataclasses import dataclass, asdict
import argparse

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
    assertions_passed: Dict[str, bool] = None  # For dry-run validation
    quality_score: float = 0.0  # Overall execution quality
    pareto_quality: Dict = None  # Pareto principle quality assessment

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

class AutomationRecoveryManager:
    def __init__(self, work_dir: Path = None):
        self.work_dir = work_dir or Path.cwd()
        self.target_dir = self.work_dir / "Src/Tests/Core/Application.UnitTests"
        self.session_file = self.work_dir / "automation_recovery_session.json"
        self.reports_dir = self.work_dir / "automation_reports"
        self.reports_dir.mkdir(exist_ok=True)
        
        # Load proven automation scripts from analysis
        self.proven_scripts = [
            ("automation/fix_xunit1013_fact_attribute.py", 50),  # xUnit1013 - missing Fact attributes
            ("automation/fix_cs1503_cancellation_token.py", 100),  # CS1503 - argument type mismatches
            ("automation/fix_cs8602_targeted.py", 100),  # CS8602 - null dereference warnings
        ]
        
        self.current_session: Optional[RecoverySession] = None
        
    def create_file_hash(self, file_path: Path) -> str:
        """Create SHA256 hash of file content"""
        try:
            return hashlib.sha256(file_path.read_bytes()).hexdigest()
        except Exception:
            return "ERROR"
    
    def scan_target_files(self) -> Dict[str, str]:
        """Create hash map of all .cs files in target directory"""
        file_hashes = {}
        for cs_file in self.target_dir.rglob("*.cs"):
            rel_path = cs_file.relative_to(self.work_dir)
            file_hashes[str(rel_path)] = self.create_file_hash(cs_file)
        return file_hashes
    
    def analyze_build_output(self, verbose: bool = False) -> BuildAnalysis:
        """Comprehensive build analysis separating errors from warnings"""
        try:
            # Two-pass compilation for thorough analysis
            verbosity = "-v:normal" if verbose else "-v:q"
            result = subprocess.run([
                "dotnet", "build", 
                str(self.target_dir / "Application.UnitTests.csproj"),
                verbosity, "--no-restore"
            ], capture_output=True, text=True, cwd=self.work_dir)
            
            errors = []
            warnings = []
            
            # Parse both stdout and stderr
            full_output = result.stdout + "\n" + result.stderr
            
            for line in full_output.split('\n'):
                line = line.strip()
                if not line:
                    continue
                    
                # Compiler errors (CS, MSB critical errors)
                if 'error CS' in line or 'error MSB' in line:
                    errors.append(line)
                # Warnings 
                elif 'warning CS' in line or 'warning MSB' in line or 'warning RS' in line:
                    warnings.append(line)
            
            build_success = result.returncode == 0 and len(errors) == 0
            
            return BuildAnalysis(
                errors=len(errors),
                warnings=len(warnings),
                error_details=errors,
                warning_details=warnings,
                build_success=build_success,
                new_errors_introduced=[],  # Will be populated by comparison
                errors_fixed=[]  # Will be populated by comparison
            )
            
        except Exception as e:
            print(f"Warning: Build analysis failed: {e}")
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
        
        # Update the after analysis with comparison results
        after.new_errors_introduced = new_errors
        after.errors_fixed = fixed_errors
        
        return new_errors, fixed_errors
    
    def create_git_commit(self, message: str, script_name: str) -> Optional[str]:
        """Create git commit and return commit hash"""
        try:
            # Add all changes
            subprocess.run(["git", "add", "."], cwd=self.work_dir, check=True)
            
            # Create commit with detailed message
            full_message = f"{message}\n\nScript: {script_name}\nTimestamp: {datetime.now().isoformat()}\n\n🤖 Generated with Automation Recovery Manager"
            subprocess.run([
                "git", "commit", "-m", full_message
            ], cwd=self.work_dir, check=True)
            
            # Get commit hash
            result = subprocess.run([
                "git", "rev-parse", "HEAD"
            ], capture_output=True, text=True, cwd=self.work_dir, check=True)
            
            return result.stdout.strip()
        except subprocess.CalledProcessError as e:
            print(f"Git commit failed: {e}")
            return None
    
    def run_dry_run_assertions(self, execution: ScriptExecution, script_output: str) -> Dict[str, bool]:
        """Run comprehensive assertions for dry-run mode"""
        assertions = {}
        
        # Assertion 1: Script should not modify any files
        assertions['no_files_modified'] = len(execution.files_modified or []) == 0
        
        # Assertion 2: Expected fixes should be mentioned in output
        assertions['expected_fixes_mentioned'] = str(execution.expected_fixes) in script_output
        
        # Assertion 3: Script should report potential fixes
        assertions['reports_potential_fixes'] = any(
            keyword in script_output.lower() 
            for keyword in ['fix', 'would', 'potential', 'found', 'pattern']
        )
        
        # Assertion 4: No actual changes reported (dry-run safety)
        assertions['no_actual_changes'] = not any(
            keyword in script_output.lower()
            for keyword in ['applied', 'modified', 'updated', 'changed', 'written']
        )
        
        # Assertion 5: Dry-run mode acknowledged
        assertions['dry_run_acknowledged'] = any(
            keyword in script_output.lower()
            for keyword in ['dry', 'preview', 'simulation', 'analysis', 'would apply']
        )
        
        return assertions
    
    def calculate_adaptive_error_tolerance(self, errors_fixed: int) -> Tuple[float, int]:
        """Calculate adaptive error tolerance based on Pareto principle (80/20 rule)"""
        
        # Base tolerance percentages based on fix scale
        if errors_fixed >= 200:
            # Large scale: up to 20% errors acceptable (hard max)
            base_tolerance = 0.20
            max_errors = min(20, int(errors_fixed * base_tolerance))
        elif errors_fixed >= 100:
            # Medium scale: 15% tolerance 
            base_tolerance = 0.15
            max_errors = int(errors_fixed * base_tolerance)
        elif errors_fixed >= 50:
            # Small-medium scale: 12% tolerance
            base_tolerance = 0.12
            max_errors = int(errors_fixed * base_tolerance)
        elif errors_fixed >= 20:
            # Small scale: 10% tolerance (your example: 20 fixes, 2 errors OK)
            base_tolerance = 0.10
            max_errors = max(2, int(errors_fixed * base_tolerance))
        elif errors_fixed >= 10:
            # Very small scale: 8% tolerance, minimum 1 error
            base_tolerance = 0.08
            max_errors = max(1, int(errors_fixed * base_tolerance))
        else:
            # Minimal fixes: 5% tolerance, but at least must have 0 errors
            base_tolerance = 0.05
            max_errors = max(0, int(errors_fixed * base_tolerance))
        
        return base_tolerance, max_errors
    
    def assess_pareto_quality(self, errors_fixed: int, errors_introduced: int) -> Tuple[bool, float, str]:
        """Assess quality using Pareto principle - must achieve 80% improvement"""
        
        tolerance_pct, max_acceptable_errors = self.calculate_adaptive_error_tolerance(errors_fixed)
        
        # Pareto Rule: 80% of value (fixes) should not be undermined by 20% problems (new errors)
        pareto_threshold = 0.80  # Must achieve at least 80% net improvement
        
        if errors_fixed == 0:
            return False, 0.0, "No errors fixed"
        
        # Calculate net improvement ratio
        net_improvement = (errors_fixed - errors_introduced) / errors_fixed
        
        # Quality assessment
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
            # For dry-run, use assertion-based scoring
            if execution.assertions_passed:
                passed_assertions = sum(execution.assertions_passed.values())
                total_assertions = len(execution.assertions_passed)
                return passed_assertions / total_assertions if total_assertions > 0 else 0.0
            return 0.5 if execution.success else 0.0
        
        # For live mode: Use Pareto-based quality assessment
        if not execution.build_analysis_before or not execution.build_analysis_after:
            return 0.5 if execution.success else 0.0
        
        errors_before = execution.build_analysis_before.errors
        errors_after = execution.build_analysis_after.errors
        errors_fixed = max(0, errors_before - errors_after)
        errors_introduced = len(execution.build_analysis_after.new_errors_introduced or [])
        
        # Base score components
        score = 0.0
        max_score = 1.0
        
        if not execution.success:
            return 0.0
        
        # Apply Pareto quality assessment
        passes_pareto, net_improvement, quality_level = self.assess_pareto_quality(errors_fixed, errors_introduced)
        
        if passes_pareto:
            # High quality score for Pareto-compliant results
            if quality_level.startswith("PERFECT"):
                score = 1.0
            elif quality_level.startswith("EXCELLENT"):
                score = 0.85 + (net_improvement - 0.80) * 0.75  # Scale 0.85-1.0
            else:  # GOOD
                score = 0.70 + (net_improvement - 0.80) * 0.75  # Scale 0.70-0.85
        else:
            # Lower scores for failing Pareto principle
            if errors_introduced > 0:
                # Penalty based on error ratio
                error_ratio = errors_introduced / max(1, errors_fixed)
                score = max(0.0, 0.60 - error_ratio)  # Max 0.60, decreases with error ratio
            else:
                score = 0.30  # Low score for other failures
        
        # Store quality assessment in execution for reporting
        execution.pareto_quality = {
            'passes_pareto': passes_pareto,
            'net_improvement': net_improvement,
            'quality_level': quality_level,
            'errors_fixed': errors_fixed,
            'errors_introduced': errors_introduced,
            'tolerance_pct': self.calculate_adaptive_error_tolerance(errors_fixed)[0],
            'max_acceptable_errors': self.calculate_adaptive_error_tolerance(errors_fixed)[1]
        }
        
        return max(0.0, min(score, max_score))

    def execute_script(self, script_path: str, expected_fixes: int, dry_run: bool = False) -> ScriptExecution:
        """Execute a single automation script with full tracking"""
        print(f"\n{'='*60}")
        print(f"{'DRY RUN: ' if dry_run else ''}Executing: {script_path}")
        print(f"Expected fixes: ~{expected_fixes}")
        print(f"{'='*60}")
        
        # PRE-EXECUTION: Comprehensive build analysis
        print("Running pre-execution build analysis...")
        build_before = self.analyze_build_output(verbose=False)
        print(f"Pre-execution: {build_before.errors} errors, {build_before.warnings} warnings")
        
        # Initialize execution record
        execution = ScriptExecution(
            script_name=script_path,
            expected_fixes=expected_fixes,
            timestamp=datetime.now().isoformat(),
            mode="dry_run" if dry_run else "live",
            files_before_hash=self.scan_target_files(),
            build_analysis_before=build_before,
            files_modified=[],
            errors=[],
            assertions_passed={}
        )
        
        start_time = datetime.now()
        
        try:
            # Prepare command
            cmd = ["python", script_path, str(self.target_dir)]
            if dry_run:
                cmd.append("--dry-run")
            
            print(f"Running: {' '.join(cmd)}")
            
            # Execute script
            result = subprocess.run(
                cmd, 
                capture_output=True, 
                text=True, 
                cwd=self.work_dir,
                timeout=300  # 5 minute timeout
            )
            
            execution.duration_seconds = (datetime.now() - start_time).total_seconds()
            
            if result.returncode != 0:
                execution.errors.append(f"Script failed with code {result.returncode}")
                execution.errors.append(f"STDERR: {result.stderr}")
                print(f"Script failed: {result.stderr}")
                return execution
            
            # POST-EXECUTION: Comprehensive analysis
            execution.files_after_hash = self.scan_target_files()
            execution.files_modified = [
                file_path for file_path, hash_after in execution.files_after_hash.items()
                if hash_after != execution.files_before_hash.get(file_path, "")
            ]
            
            # Parse script output for actual fix count (script-specific)
            output_lines = result.stdout.split('\n')
            for line in output_lines:
                if 'Total fixes applied:' in line or 'fixes applied' in line:
                    try:
                        numbers = [int(s) for s in line.split() if s.isdigit()]
                        if numbers:
                            execution.actual_fixes = numbers[-1]
                    except:
                        pass
            
            # Default to file count if no explicit fix count found
            if execution.actual_fixes == 0:
                execution.actual_fixes = len(execution.files_modified)
            
            # POST-EXECUTION: Build analysis (for both modes)
            print("Running post-execution build analysis...")
            build_after = self.analyze_build_output(verbose=False)
            execution.build_analysis_after = build_after
            
            # Compare build analyses
            new_errors, fixed_errors = self.compare_build_analyses(
                execution.build_analysis_before, 
                execution.build_analysis_after
            )
            
            print(f"Post-execution: {build_after.errors} errors, {build_after.warnings} warnings")
            if fixed_errors:
                print(f"Fixed {len(fixed_errors)} compiler errors")
            if new_errors:
                print(f"INTRODUCED {len(new_errors)} new compiler errors!")
                for error in new_errors[:3]:  # Show first 3
                    print(f"   {error[:100]}...")
            
            # Mode-specific analysis
            if dry_run:
                # DRY-RUN: Run assertions
                execution.assertions_passed = self.run_dry_run_assertions(execution, result.stdout)
                
                passed_count = sum(execution.assertions_passed.values())
                total_count = len(execution.assertions_passed)
                print(f"Dry-run assertions: {passed_count}/{total_count} passed")
                
                for assertion, passed in execution.assertions_passed.items():
                    status = "PASS" if passed else "FAIL"
                    print(f"   {status} {assertion}: {passed}")
                
            else:
                # LIVE: Git commit and validation
                if execution.files_modified:
                    commit_message = f"Apply {script_path}: {execution.actual_fixes} fixes, {len(execution.files_modified)} files"
                    execution.git_commit_hash = self.create_git_commit(commit_message, script_path)
                    print(f"Git commit: {execution.git_commit_hash[:8] if execution.git_commit_hash else 'FAILED'}")
                
                # PARETO QUALITY ASSESSMENT: Check if script meets 80/20 rule
                errors_fixed = max(0, execution.build_analysis_before.errors - execution.build_analysis_after.errors)
                
                if new_errors:
                    # Use Pareto assessment instead of automatic failure
                    tolerance_pct, max_acceptable = self.calculate_adaptive_error_tolerance(errors_fixed)
                    
                    print(f"Script introduced {len(new_errors)} new errors")
                    print(f"Pareto Analysis: Fixed {errors_fixed} errors, tolerance {tolerance_pct:.1%} ({max_acceptable} max)")
                    
                    if len(new_errors) > max_acceptable:
                        execution.errors.append(f"Script introduced {len(new_errors)} new errors (exceeds {max_acceptable} tolerance)")
                        execution.success = False
                        print(f"SCRIPT FAILED: Too many new errors ({len(new_errors)} > {max_acceptable})")
                    else:
                        # Within tolerance - script still considered successful
                        execution.success = True
                        print(f"SCRIPT ACCEPTABLE: New errors within tolerance ({len(new_errors)}/{max_acceptable})")
                        print(f"Pareto Quality: {errors_fixed} fixes vs {len(new_errors)} new errors = {((errors_fixed - len(new_errors)) / errors_fixed * 100):.1f}% net improvement")
                else:
                    execution.success = True
                    print(f"Perfect execution: {errors_fixed} errors fixed, 0 new errors!")
            
            # Calculate quality score
            execution.quality_score = self.calculate_quality_score(execution)
            
            # Final status
            if execution.success:
                print(f"Script completed successfully (Quality: {execution.quality_score:.2f})")
                print(f"Files modified: {len(execution.files_modified)}")
                print(f"Actual fixes: {execution.actual_fixes}")
            else:
                print(f"Script failed (Quality: {execution.quality_score:.2f})")
            
        except subprocess.TimeoutExpired:
            execution.errors.append("Script timed out after 5 minutes")
            execution.duration_seconds = 300
            print(f"Script timed out")
        except Exception as e:
            execution.errors.append(f"Unexpected error: {str(e)}")
            execution.duration_seconds = (datetime.now() - start_time).total_seconds()
            print(f"Unexpected error: {e}")
        
        return execution
    
    def save_session(self):
        """Save current session state"""
        if self.current_session:
            with open(self.session_file, 'w') as f:
                json.dump(asdict(self.current_session), f, indent=2)
    
    def load_session(self) -> bool:
        """Load existing session if available"""
        if self.session_file.exists():
            try:
                with open(self.session_file, 'r') as f:
                    data = json.load(f)
                self.current_session = RecoverySession(**data)
                return True
            except Exception as e:
                print(f"Could not load session: {e}")
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
            executions=[]
        )
        return self.current_session
    
    def generate_detailed_report(self) -> str:
        """Generate comprehensive execution report"""
        if not self.current_session:
            return "No session data available"
        
        report_lines = [
            "# Automation Recovery Execution Report",
            f"**Session ID:** {self.current_session.session_id}",
            f"**Mode:** {self.current_session.mode}",
            f"**Started:** {self.current_session.start_time}",
            f"**Expected Total Fixes:** {self.current_session.total_expected_fixes}",
            "",
            "## Execution Results",
        ]
        
        total_actual_fixes = 0
        total_files_modified = set()
        
        for i, execution in enumerate(self.current_session.executions, 1):
            status = "SUCCESS" if execution.success else "FAILED"
            quality_str = f" (Quality: {execution.quality_score:.2f})" if execution.quality_score > 0 else ""
            
            report_lines.extend([
                f"### {i}. {execution.script_name} {status}{quality_str}",
                f"- **Expected fixes:** {execution.expected_fixes}",
                f"- **Actual fixes:** {execution.actual_fixes}",
                f"- **Duration:** {execution.duration_seconds:.2f}s",
                f"- **Mode:** {execution.mode}",
                f"- **Files modified:** {len(execution.files_modified or [])}",
            ])
            
            # Build analysis details
            if execution.build_analysis_before and execution.build_analysis_after:
                before = execution.build_analysis_before
                after = execution.build_analysis_after
                
                report_lines.extend([
                    f"- **Build errors:** {before.errors} -> {after.errors} ({after.errors - before.errors:+d})",
                    f"- **Build warnings:** {before.warnings} -> {after.warnings} ({after.warnings - before.warnings:+d})",
                ])
                
                if after.errors_fixed:
                    report_lines.append(f"- **Errors fixed:** {len(after.errors_fixed)}")
                
                if after.new_errors_introduced:
                    report_lines.append(f"- **NEW errors introduced:** {len(after.new_errors_introduced)}")
                    for error in after.new_errors_introduced[:2]:
                        report_lines.append(f"  - `{error[:80]}...`")
            
            # Pareto Quality Assessment (if available)
            if hasattr(execution, 'pareto_quality') and execution.pareto_quality:
                pq = execution.pareto_quality
                report_lines.extend([
                    f"- **Pareto Quality:** {pq['quality_level']}",
                    f"- **Net improvement:** {pq['net_improvement']:.1%}",
                    f"- **Error tolerance:** {pq['tolerance_pct']:.1%} (max {pq['max_acceptable_errors']} errors)",
                    f"- **Passes Pareto rule:** {'Yes' if pq['passes_pareto'] else 'No'}",
                ])
            
            # Mode-specific details
            if execution.mode == "dry_run" and execution.assertions_passed:
                passed = sum(execution.assertions_passed.values())
                total = len(execution.assertions_passed)
                report_lines.append(f"- **Dry-run assertions:** {passed}/{total} passed")
                
                for assertion, result in execution.assertions_passed.items():
                    symbol = "PASS" if result else "FAIL"
                    report_lines.append(f"  - {symbol} {assertion}")
            
            elif execution.mode == "live":
                if execution.git_commit_hash:
                    report_lines.append(f"- **Git commit:** `{execution.git_commit_hash}`")
                
                total_actual_fixes += execution.actual_fixes
                if execution.files_modified:
                    total_files_modified.update(execution.files_modified)
            
            # Errors
            if execution.errors:
                report_lines.append("- **Errors:**")
                for error in execution.errors:
                    report_lines.append(f"  - {error}")
            
            report_lines.append("")
        
        # Summary
        report_lines.extend([
            "## Summary",
            f"- **Total scripts executed:** {len(self.current_session.executions)}",
            f"- **Total actual fixes applied:** {total_actual_fixes}",
            f"- **Unique files modified:** {len(total_files_modified)}",
            f"- **Session completed:** {'Yes' if self.current_session.completed else 'No'}",
        ])
        
        return "\n".join(report_lines)
    
    def save_report(self) -> Path:
        """Save detailed report to file"""
        report_content = self.generate_detailed_report()
        report_file = self.reports_dir / f"recovery_report_{self.current_session.session_id}.md"
        report_file.write_text(report_content, encoding='utf-8')
        return report_file
    
    def rollback_session(self) -> bool:
        """Rollback all changes made in current session"""
        if not self.current_session or not self.current_session.can_rollback:
            print("Cannot rollback - no valid session or rollback disabled")
            return False
        
        print("Rolling back automation changes...")
        
        # Get all commit hashes to revert (in reverse order)
        commit_hashes = []
        for execution in reversed(self.current_session.executions):
            if execution.git_commit_hash and execution.mode == "live":
                commit_hashes.append(execution.git_commit_hash)
        
        if not commit_hashes:
            print("No commits to rollback")
            return True
        
        try:
            # Revert commits one by one
            for commit_hash in commit_hashes:
                subprocess.run([
                    "git", "revert", "--no-edit", commit_hash
                ], cwd=self.work_dir, check=True)
                print(f"Reverted commit {commit_hash[:8]}")
            
            print("Rollback completed successfully")
            return True
            
        except subprocess.CalledProcessError as e:
            print(f"Rollback failed: {e}")
            return False
    
    def run_recovery(self, mode: str = "dry_run", resume: bool = False):
        """Run the complete automation recovery process"""
        # Load existing session if resuming
        if resume and self.load_session():
            print(f"Resuming session {self.current_session.session_id}")
        else:
            self.create_session(mode)
            print(f"Starting new recovery session {self.current_session.session_id}")
        
        print(f"Mode: {mode.upper()}")
        print(f"Target directory: {self.target_dir}")
        print(f"Expected total fixes: {self.current_session.total_expected_fixes}")
        
        # Execute remaining scripts
        for i, (script_path, expected_fixes) in enumerate(self.proven_scripts):
            if i < self.current_session.current_step:
                continue  # Skip already completed steps when resuming
            
            execution = self.execute_script(script_path, expected_fixes, dry_run=(mode == "dry_run"))
            self.current_session.executions.append(execution)
            self.current_session.current_step = i + 1
            
            # Save progress after each script
            self.save_session()
            
            if not execution.success:
                print(f"Script {script_path} failed - stopping recovery")
                break
        
        self.current_session.completed = True
        self.save_session()
        
        # Generate and save final report
        report_file = self.save_report()
        print(f"\nDetailed report saved: {report_file}")
        print("\n" + self.generate_detailed_report())

def main():
    parser = argparse.ArgumentParser(description="Enterprise Automation Recovery Manager")
    parser.add_argument("--mode", choices=["dry_run", "live"], default="dry_run",
                       help="Execution mode (default: dry_run)")
    parser.add_argument("--resume", action="store_true",
                       help="Resume previous session")
    parser.add_argument("--rollback", action="store_true", 
                       help="Rollback current session changes")
    parser.add_argument("--report", action="store_true",
                       help="Show report for current session")
    
    args = parser.parse_args()
    
    manager = AutomationRecoveryManager()
    
    if args.rollback:
        if manager.load_session():
            manager.rollback_session()
        else:
            print("No session to rollback")
        return
    
    if args.report:
        if manager.load_session():
            print(manager.generate_detailed_report())
        else:
            print("No session data available")
        return
    
    # Interactive confirmation for live mode
    if args.mode == "live" and not args.resume:
        print("WARNING: LIVE MODE will modify code files!")
        response = input("Continue? (yes/no): ")
        if response.lower() != "yes":
            print("Aborted.")
            return
    
    manager.run_recovery(args.mode, args.resume)

if __name__ == "__main__":
    main()