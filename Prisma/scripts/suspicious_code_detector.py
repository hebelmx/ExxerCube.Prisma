#!/usr/bin/env python3
"""
Prisma Suspicious Code Detector
================================
Automated detection of suspicious code patterns including stubs, incomplete implementations,
and technical debt indicators in the Prisma codebase.

Adapted from ExxerAI tooling for Hexagonal Architecture (Ports & Adapters pattern).

Author: Code Quality Team
Version: 1.0 (Prisma Edition)
"""

import os
import re
import json
from datetime import datetime

# Configuration - Prisma Specific Paths
ROOT_DIR = r"F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp"
OUTPUT_JSON = r"F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\scripts\suspicious_code_analysis.json"

# Global counters for live statistics
files_processed = 0
total_findings = 0
findings_by_severity = {"HIGH": 0, "MEDIUM": 0, "LOW": 0}

# Comprehensive regex patterns to detect suspicious code patterns
patterns = {
    # 🔴 HIGH SEVERITY - Incomplete Implementations
    "not_implemented_exception": r"throw\s+new\s+NotImplementedException\s*\(",
    "not_supported_exception": r"throw\s+new\s+NotSupportedException\s*\(",
    "todo_comment": r"//\s*(TODO|FIXME|HACK)\b",
    "placeholder_comment": r"//\s*(Placeholder|placeholder|Temporary|temporary|Not implemented|not implemented)\b",
    "mock_in_comment": r"//\s*(Mock|mock|Stub|stub)\b",

    # 🔴 HIGH SEVERITY - Production Mocks/Stubs
    "mock_extracted_text": r'"Mock extracted text content"',
    "placeholder_interface": r"//\s*For now, this is a placeholder interface",
    "stub_class_name": r"(class|interface)\s+\w*Stub\w*\b",
    "mock_class_name": r"(class|interface)\s+\w*Mock\w*\b",
    "fake_class_name": r"(class|interface)\s+\w*Fake\w*\b",

    # 🟡 MEDIUM SEVERITY - Hardcoded Values & Magic Numbers
    "task_delay_simulation": r"await\s+Task\.Delay\s*\(\s*\d+.*//.*[Ss]imulate",
    "magic_percentage": r"\b(0\.[5-9]\d*|1\.0|2\.0)\b.*[*/+-]",
    "random_chance": r"Random\..*>\s*(0\.[5-9]|1\.0)",
    "hardcoded_localhost": r"(localhost|127\.0\.0\.1)",
    "hardcoded_timeout": r"TimeSpan\.FromSeconds\s*\(\s*\d+\s*\)",
    "magic_numbers": r"\b(100|1000|2000|5000|10000)\b(?!.*Timeout)",
    "hardcoded_credentials": r"(password|apikey|secret|token)\s*=\s*\"[^\"]+\"",

    # 🟡 MEDIUM SEVERITY - Poor Exception Handling
    "generic_exception_catch": r"catch\s*\(\s*Exception\s+\w+\s*\)",
    "empty_catch_block": r"catch\s*\([^)]*\)\s*\{\s*\}",
    "exception_swallowing": r"catch.*\{\s*//.*\}",

    # 🟡 MEDIUM SEVERITY - Questionable Returns
    "task_completed_task": r"return\s+Task\.CompletedTask\b",
    "task_from_result": r"return\s+Task\.FromResult\s*\(",
    "return_null": r"return\s+null\s*;",
    "return_default": r"return\s+default\s*;",
    "return_empty_string": r'return\s+(string\.Empty|"")\s*;',

    # 🟠 LOW SEVERITY - Test Quality Issues
    "test_skip_attribute": r'\[.*Skip\s*=\s*"',
    "assert_skip": r"Assert\.Skip\s*\(",
    "fact_skip": r'\[Fact\s*\(\s*Skip\s*=',
    "theory_skip": r'\[Theory\s*\(\s*Skip\s*=',

    # 🟠 LOW SEVERITY - Empty Implementations
    "empty_method": r"(public|private|protected|internal).*\{\s*\}",
    "empty_property": r"(get|set)\s*\{\s*\}",
    "empty_constructor": r"\w+\s*\([^)]*\)\s*:\s*base\([^)]*\)\s*\{\s*\}",

    # 🟢 LOW SEVERITY - Development Artifacts
    "claude_comment": r"//\s*(CLAUDE|Fix|Reason):",
    "simulation_comment": r"//.*[Ss]imulate",
    "temporary_comment": r"//.*[Tt]emporary",

    # 🔍 DETECTION - Common Stub Patterns
    "static_return_value": r"return\s+[0-9\"'a-zA-Z_]+\s*;",
    "hardcoded_string": r'return\s+"[^"]*"\s*;',
    "hardcoded_number": r"return\s+\d+(\.\d+)?\s*;",
    "fixed_seed_random": r"new\s+Random\s*\(\s*\d+\s*\)",
}

# Detect namespace, class, method
namespace_pattern = re.compile(r'namespace\s+([\w\.]+)')
class_pattern = re.compile(r'\b(public|internal|protected|private)?\s*(abstract|sealed)?\s*(class|struct|interface)\s+(\w+)')
method_pattern = re.compile(r'\b(public|private|protected|internal)?\s*(static\s+)?([\w<>\[\]]+\s+)?(\w+)\s*\(([^)]*)\)')

# Enhanced result structure with severity classification
suspected_stubs = {}

# Severity mapping for patterns
pattern_severity = {
    # 🔴 HIGH SEVERITY - Immediate Action Required
    "not_implemented_exception": "HIGH",
    "not_supported_exception": "HIGH",
    "mock_extracted_text": "HIGH",
    "placeholder_interface": "HIGH",
    "stub_class_name": "HIGH",
    "mock_class_name": "HIGH",
    "fake_class_name": "HIGH",

    # 🟡 MEDIUM SEVERITY - Should be addressed soon
    "todo_comment": "MEDIUM",
    "placeholder_comment": "MEDIUM",
    "mock_in_comment": "MEDIUM",
    "task_delay_simulation": "MEDIUM",
    "magic_percentage": "MEDIUM",
    "random_chance": "MEDIUM",
    "generic_exception_catch": "MEDIUM",
    "empty_catch_block": "MEDIUM",
    "exception_swallowing": "MEDIUM",
    "task_from_result": "MEDIUM",
    "return_null": "MEDIUM",
    "return_default": "MEDIUM",
    "hardcoded_localhost": "MEDIUM",
    "hardcoded_timeout": "MEDIUM",
    "hardcoded_credentials": "MEDIUM",
    "magic_numbers": "MEDIUM",

    # 🟠 LOW SEVERITY - Technical debt
    "task_completed_task": "LOW",
    "return_empty_string": "LOW",
    "test_skip_attribute": "LOW",
    "assert_skip": "LOW",
    "fact_skip": "LOW",
    "theory_skip": "LOW",
    "empty_method": "LOW",
    "empty_property": "LOW",
    "empty_constructor": "LOW",
    "claude_comment": "LOW",
    "simulation_comment": "LOW",
    "temporary_comment": "LOW",
    "static_return_value": "LOW",
    "hardcoded_string": "LOW",
    "hardcoded_number": "LOW",
    "fixed_seed_random": "LOW",
}

def get_pattern_category(pattern_tag):
    """Categorize patterns by type for better reporting"""
    if pattern_tag in ["not_implemented_exception", "not_supported_exception", "todo_comment", "placeholder_comment"]:
        return "Incomplete Implementation"
    elif pattern_tag in ["mock_extracted_text", "stub_class_name", "mock_class_name", "fake_class_name", "placeholder_interface"]:
        return "Production Mock/Stub"
    elif pattern_tag in ["task_delay_simulation", "magic_percentage", "random_chance", "hardcoded_localhost", "magic_numbers", "hardcoded_credentials"]:
        return "Hardcoded Values"
    elif pattern_tag in ["generic_exception_catch", "empty_catch_block", "exception_swallowing"]:
        return "Poor Exception Handling"
    elif pattern_tag in ["test_skip_attribute", "assert_skip", "fact_skip", "theory_skip"]:
        return "Test Quality Issues"
    elif pattern_tag in ["empty_method", "empty_property", "empty_constructor"]:
        return "Empty Implementation"
    else:
        return "Other"

def is_test_file(filepath):
    """Check if file is a test file based on path and name"""
    test_indicators = ["test", "tests", "spec", "specs", ".test.", ".tests."]
    filepath_lower = filepath.lower()
    return any(indicator in filepath_lower for indicator in test_indicators)

def get_recommendation(pattern_tag):
    """Provide specific recommendations for each pattern type"""
    recommendations = {
        # High Severity
        "not_implemented_exception": "🚨 CRITICAL: Replace with actual implementation or return Result<T>.Failure()",
        "mock_extracted_text": "🚨 CRITICAL: Replace mock data with real service implementation",
        "stub_class_name": "🚨 CRITICAL: Implement actual business logic, remove stub",
        "placeholder_interface": "🔧 Implement proper interface contract with methods",

        # Medium Severity
        "todo_comment": "📝 Complete implementation or create tracking ticket",
        "task_delay_simulation": "⚡ Replace simulation with actual async work",
        "generic_exception_catch": "🛡️ Use specific exception types and Result<T> pattern",
        "magic_percentage": "📊 Extract to configuration with documentation",
        "random_chance": "🎲 Replace with deterministic business logic",
        "hardcoded_credentials": "🔒 Move to secure configuration (Azure Key Vault, environment variables)",

        # Low Severity
        "test_skip_attribute": "✅ Enable test or remove if obsolete",
        "empty_method": "🏗️ Implement method body or mark as abstract",
        "static_return_value": "💡 Consider if this should be configurable",
        "claude_comment": "📚 Review and integrate or remove development comments"
    }
    return recommendations.get(pattern_tag, "📋 Review and address based on context")

def get_project_name(filepath):
    """Extract project name from file path"""
    try:
        # Look for ExxerCube.Prisma.* pattern
        parts = filepath.replace("\\", "/").split("/")
        for part in parts:
            if "ExxerCube.Prisma." in part and not part.endswith(".cs"):
                return part
        # Fallback: use parent directory name
        return os.path.basename(os.path.dirname(filepath))
    except:
        return "Unknown"

def parse_file(filepath):
    """Parse a single file for suspicious patterns"""
    global files_processed, total_findings, findings_by_severity

    try:
        with open(filepath, encoding="utf-8", errors="ignore") as f:
            content = f.read()
    except Exception as e:
        print(f"❌ Error reading file {filepath}: {e}")
        return

    files_processed += 1
    file_findings = 0
    relative_path = os.path.relpath(filepath, ROOT_DIR)

    # Console output for file processing
    print(f"🔍 [{files_processed:4d}] Analyzing: {relative_path}")

    namespace = None
    class_name = None
    lines = content.splitlines()

    for i, line in enumerate(lines):
        if not namespace:
            ns_match = namespace_pattern.search(line)
            if ns_match:
                namespace = ns_match.group(1)

        cls_match = class_pattern.search(line)
        if cls_match:
            class_name = cls_match.group(4)

        for tag, pattern in patterns.items():
            if re.search(pattern, line, re.IGNORECASE):
                method = find_enclosing_method(lines, i)
                full_key = f"{namespace}.{class_name}.{method}" if namespace and class_name and method else f"{relative_path}#{i+1}"

                # Get severity and update counters
                severity = pattern_severity.get(tag, "LOW")
                findings_by_severity[severity] += 1
                total_findings += 1
                file_findings += 1

                # Real-time console output for findings
                severity_icon = {"HIGH": "🚨", "MEDIUM": "⚠️", "LOW": "💡"}[severity]
                print(f"    {severity_icon} Line {i+1:4d}: {tag} - {line.strip()[:80]}{'...' if len(line.strip()) > 80 else ''}")

                # Enhanced stub entry with better path handling
                stub_entry = {
                    "project": get_project_name(filepath),
                    "namespace": namespace or "Unknown",
                    "class": class_name or "Unknown",
                    "method": method or "Unknown",
                    "line": i + 1,
                    "pattern": tag,
                    "severity": severity,
                    "category": get_pattern_category(tag),
                    "line_preview": line.strip(),
                    "full_path": os.path.abspath(filepath),
                    "relative_path": relative_path,
                    "file_name": os.path.basename(filepath),
                    "is_test_file": is_test_file(filepath),
                    "recommendation": get_recommendation(tag)
                }
                suspected_stubs.setdefault(full_key, []).append(stub_entry)

    # Summary for this file
    if file_findings > 0:
        print(f"    📊 Found {file_findings} suspicious patterns in {os.path.basename(filepath)}")

    # Show running totals every 10 files
    if files_processed % 10 == 0:
        print(f"\n📈 Progress: {files_processed} files | {total_findings} total findings | 🚨{findings_by_severity['HIGH']} ⚠️{findings_by_severity['MEDIUM']} 💡{findings_by_severity['LOW']}\n")

def find_enclosing_method(lines, current_index):
    """Scan backwards to locate method signature above the stub line"""
    for i in range(current_index, -1, -1):
        method_match = method_pattern.search(lines[i])
        if method_match:
            return method_match.group(4)
    return "UnknownMethod"

def scan_directory(root):
    """Enhanced directory scanning with console output"""
    global files_processed, total_findings

    print(f"\n🎯 Starting Prisma Suspicious Code Analysis")
    print(f"📁 Root Directory: {os.path.abspath(root)}")
    print(f"🔍 Searching for *.cs files...")

    if not os.path.exists(root):
        print(f"❌ ERROR: Root directory does not exist: {root}")
        print(f"💡 Please update ROOT_DIR in the script to the correct path")
        return

    # Count total files first
    cs_files = []
    for dirpath, _, filenames in os.walk(root):
        # Skip bin/obj directories
        if any(excluded in dirpath for excluded in ['bin', 'obj', 'TestResults']):
            continue
        for filename in filenames:
            if filename.endswith(".cs"):
                cs_files.append(os.path.join(dirpath, filename))

    total_files = len(cs_files)
    print(f"📊 Found {total_files} C# files to analyze")
    print(f"🚀 Starting analysis...\n")

    files_processed = 0
    total_findings = 0

    for filepath in cs_files:
        parse_file(filepath)

    print(f"\n✅ Analysis Complete!")
    print(f"📊 Final Statistics:")
    print(f"   📁 Files Processed: {files_processed}")
    print(f"   🎯 Total Findings: {total_findings}")
    print(f"   🚨 HIGH Severity: {findings_by_severity['HIGH']}")
    print(f"   ⚠️  MEDIUM Severity: {findings_by_severity['MEDIUM']}")
    print(f"   💡 LOW Severity: {findings_by_severity['LOW']}")
    print(f"   📈 Average per file: {total_findings/files_processed:.1f}" if files_processed > 0 else "   📈 No files processed")

def generate_summary():
    """Generate summary statistics for the analysis"""
    total_issues = sum(len(entries) for entries in suspected_stubs.values())
    severity_counts = {"HIGH": 0, "MEDIUM": 0, "LOW": 0}
    category_counts = {}
    project_counts = {}

    for entries in suspected_stubs.values():
        for entry in entries:
            severity_counts[entry["severity"]] += 1
            category = entry["category"]
            category_counts[category] = category_counts.get(category, 0) + 1
            project = entry["project"]
            project_counts[project] = project_counts.get(project, 0) + 1

    return {
        "total_suspicious_patterns": total_issues,
        "severity_breakdown": severity_counts,
        "category_breakdown": category_counts,
        "project_breakdown": project_counts,
        "analysis_timestamp": datetime.now().isoformat(),
        "patterns_detected": len(patterns),
        "files_analyzed": files_processed,
        "root_directory_analyzed": os.path.abspath(ROOT_DIR)
    }

# Main execution
if __name__ == "__main__":
    # Start the analysis
    scan_directory(ROOT_DIR)

    # Generate summary statistics
    summary = generate_summary()

    # Prepare final output with metadata
    output_data = {
        "metadata": {
            "generator": "Prisma Suspicious Code Detector",
            "version": "1.0",
            "description": "Automated detection of suspicious code patterns including mocks, stubs, and incomplete implementations",
            "architecture": "Hexagonal Architecture (Ports & Adapters)",
            "ittdd_aware": True,
            "analysis_timestamp": datetime.now().isoformat(),
            "root_directory": os.path.abspath(ROOT_DIR)
        },
        "summary": summary,
        "suspicious_patterns": suspected_stubs,
        "pattern_definitions": {tag: {"severity": pattern_severity.get(tag, "LOW"),
                                      "category": get_pattern_category(tag)}
                               for tag in patterns.keys()}
    }

    print(f"\n🎯 Prisma Suspicious Code Analysis Complete!")
    print(f"⏰ Analysis completed at: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")

    # Print top severity findings
    print(f"\n🚨 TOP HIGH SEVERITY FINDINGS:")
    high_severity_count = 0
    for key, entries in suspected_stubs.items():
        for entry in entries:
            if entry["severity"] == "HIGH" and high_severity_count < 5:
                print(f"   🔴 {entry['relative_path']}:{entry['line']} - {entry['pattern']}")
                print(f"      💬 {entry['line_preview'][:60]}...")
                print(f"      🛠️  {entry['recommendation']}")
                high_severity_count += 1

    if high_severity_count == 0:
        print("   ✅ No HIGH severity issues found!")

    print(f"\n📊 SEVERITY BREAKDOWN:")
    for severity, count in summary['severity_breakdown'].items():
        icon = {"HIGH": "🚨", "MEDIUM": "⚠️", "LOW": "💡"}[severity]
        percentage = (count / summary['total_suspicious_patterns'] * 100) if summary['total_suspicious_patterns'] > 0 else 0
        print(f"   {icon} {severity}: {count} ({percentage:.1f}%)")

    print(f"\n📂 TOP AFFECTED PROJECTS:")
    project_breakdown = summary['project_breakdown']
    sorted_projects = sorted(project_breakdown.items(), key=lambda x: x[1], reverse=True)[:5]
    for project, count in sorted_projects:
        percentage = (count / summary['total_suspicious_patterns'] * 100) if summary['total_suspicious_patterns'] > 0 else 0
        print(f"   📦 {project}: {count} issues ({percentage:.1f}%)")

    print(f"\n💾 Saving detailed report...")

    # Create output directory if it doesn't exist
    output_dir = os.path.dirname(OUTPUT_JSON)
    if output_dir and not os.path.exists(output_dir):
        os.makedirs(output_dir, exist_ok=True)
        print(f"📁 Created output directory: {output_dir}")

    # Write the enhanced JSON report
    try:
        with open(OUTPUT_JSON, "w", encoding="utf-8") as out_file:
            json.dump(output_data, out_file, indent=2)
        print(f"✅ Report saved successfully to: {os.path.abspath(OUTPUT_JSON)}")
    except Exception as e:
        print(f"❌ Error saving report: {e}")

    print(f"\n🎯 ITTDD TECHNICAL DEBT SUMMARY:")
    if summary['severity_breakdown']['HIGH'] > 0:
        print(f"🚨 CRITICAL: {summary['severity_breakdown']['HIGH']} HIGH severity issues require immediate attention!")
        print(f"   These could indicate incomplete ITTDD cycles (interfaces without implementations).")
    else:
        print(f"✅ CLEAN: No critical HIGH severity issues found.")

    print(f"\n📋 RECOMMENDATIONS:")
    print(f"   1. Address HIGH severity issues within 1 sprint")
    print(f"   2. Plan MEDIUM severity fixes for next 2-4 sprints")
    print(f"   3. Include LOW severity in technical debt backlog")
    print(f"   4. Run architecture tests to catch new violations")

    print(f"\n🏗️ Following Hexagonal Architecture (Ports & Adapters) principles")
    print(f"🎭 ITTDD Compliance: {'COMPLIANT' if summary['severity_breakdown']['HIGH'] == 0 else 'NEEDS ATTENTION'}")
    print(f"📊 Full analysis results available in: {os.path.basename(OUTPUT_JSON)}")

    print(f"\n" + "="*60)
    print(f"🎯 Analysis complete! Review the JSON report for detailed findings.")
    print(f"="*60)
