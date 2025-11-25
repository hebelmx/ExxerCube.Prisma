#!/usr/bin/env python3
"""
Mock Audit Analyzer - Code Review Tool
======================================
Specialized script to find mocks and suspicious mock patterns
that indicate implementation gaps and technical doubts

Detects:
- Mock usage patterns (NSubstitute, Moq, etc.)
- Suspicious patterns indicating implementation gaps
- Technical debt indicators
- Missing production implementations

Author: Code Review Assistant
Version: 1.0
"""

import os
import re
import json
from pathlib import Path
from collections import defaultdict, Counter
from dataclasses import dataclass, asdict
from typing import List, Dict, Set, Tuple
from datetime import datetime

@dataclass
class MockUsage:
    """Represents a mock usage in test code"""
    file_path: str
    line_number: int
    mock_type: str  # "NSubstitute", "Moq", etc.
    mocked_interface: str
    context: str  # Surrounding code
    severity: str  # "suspicious", "normal", "critical"
    issues: List[str]  # List of detected issues

@dataclass
class TestClass:
    """Represents a test class with its mocks"""
    file_path: str
    class_name: str
    mock_count: int
    mocks: List[MockUsage]
    has_todos: bool
    has_fixmes: bool
    setup_count: int
    assertion_count: int
    suspicious_patterns: List[str]

class MockAuditAnalyzer:
    """Analyzes C# test code for mock patterns and implementation gaps"""

    # Mock framework patterns
    MOCK_PATTERNS = {
        'NSubstitute': [
            r'Substitute\.For<([^>]+)>\(\)',
            r'NSubstitute\.For<([^>]+)>\(\)',
            r'var\s+(\w+)\s*=\s*Substitute\.For<([^>]+)>\(\)',
        ],
        'Moq': [
            r'new\s+Mock<([^>]+)>\(\)',
            r'Mock<([^>]+)>',
        ],
        'FakeItEasy': [
            r'A\.Fake<([^>]+)>\(\)',
        ]
    }

    # Suspicious patterns
    SUSPICIOUS_PATTERNS = {
        'mock_returns_mock': r'\.Returns\s*\(\s*Substitute\.For',
        'empty_mock_setup': r'Substitute\.For<[^>]+>\(\)\s*;',
        'mock_without_behavior': r'var\s+\w+\s*=\s*Substitute\.For<[^>]+>\(\)\s*;(?!\s*\w+\.)',
        'todo_near_mock': r'(TODO|FIXME|HACK|XXX).*Substitute',
        'mock_concrete_class': r'Substitute\.For<(?!I)[A-Z]\w+>\(\)',  # Non-interface
    }

    # Implementation gap indicators
    GAP_INDICATORS = [
        r'\/\/\s*TODO:?\s*[Ii]mplement',
        r'\/\/\s*FIXME:?\s*[Mm]issing',
        r'throw\s+new\s+NotImplementedException',
        r'\/\/\s*Placeholder',
        r'\/\/\s*Not\s+implemented',
    ]

    def __init__(self, solution_path: str = "F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Code/Src/CSharp"):
        """Initialize the mock audit analyzer"""
        self.solution_path = Path(solution_path)
        self.test_files: List[Path] = []
        self.mock_usages: List[MockUsage] = []
        self.test_classes: List[TestClass] = []
        self.summary_stats: Dict[str, any] = {}

    def scan_test_files(self):
        """Scan for all test files in the solution"""
        print("🔍 Scanning for test files...")
        test_patterns = ['*Tests.cs', '*Test.cs', '*Spec.cs']

        for pattern in test_patterns:
            for test_file in self.solution_path.rglob(pattern):
                # Skip bin/obj directories
                if any(excluded in str(test_file) for excluded in ['bin', 'obj', 'TestResults']):
                    continue
                self.test_files.append(test_file)

        print(f"   Found {len(self.test_files)} test files\n")

    def analyze_file(self, file_path: Path) -> TestClass:
        """Analyze a single test file for mock usage"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
        except:
            return None

        lines = content.split('\n')

        # Extract class name
        class_match = re.search(r'public\s+(?:sealed\s+)?class\s+(\w+)', content)
        class_name = class_match.group(1) if class_match else file_path.stem

        # Find all mocks
        mocks = []
        for framework, patterns in self.MOCK_PATTERNS.items():
            for pattern in patterns:
                for match in re.finditer(pattern, content, re.MULTILINE):
                    line_number = content[:match.start()].count('\n') + 1
                    mocked_type = match.group(1) if match.groups() else "Unknown"

                    # Get context (5 lines before and after)
                    start_line = max(0, line_number - 6)
                    end_line = min(len(lines), line_number + 5)
                    context = '\n'.join(lines[start_line:end_line])

                    # Detect issues
                    issues = self._detect_issues(context, content, match.start())
                    severity = self._calculate_severity(issues)

                    mock = MockUsage(
                        file_path=str(file_path.relative_to(self.solution_path)),
                        line_number=line_number,
                        mock_type=framework,
                        mocked_interface=mocked_type,
                        context=context,
                        severity=severity,
                        issues=issues
                    )
                    mocks.append(mock)
                    self.mock_usages.append(mock)

        # Analyze test class characteristics
        has_todos = bool(re.search(r'\/\/\s*TODO', content, re.IGNORECASE))
        has_fixmes = bool(re.search(r'\/\/\s*FIXME', content, re.IGNORECASE))
        setup_count = len(re.findall(r'\[SetUp\]|\[TestInitialize\]|public\s+\w+\s+Setup\(', content))
        assertion_count = len(re.findall(r'\.(Should|Assert|Expect)', content))

        # Find suspicious patterns
        suspicious = []
        for pattern_name, pattern in self.SUSPICIOUS_PATTERNS.items():
            if re.search(pattern, content):
                suspicious.append(pattern_name)

        test_class = TestClass(
            file_path=str(file_path.relative_to(self.solution_path)),
            class_name=class_name,
            mock_count=len(mocks),
            mocks=mocks,
            has_todos=has_todos,
            has_fixmes=has_fixmes,
            setup_count=setup_count,
            assertion_count=assertion_count,
            suspicious_patterns=suspicious
        )

        return test_class

    def _detect_issues(self, context: str, full_content: str, match_position: int) -> List[str]:
        """Detect issues in mock usage"""
        issues = []

        # Check for mock returns mock
        if re.search(self.SUSPICIOUS_PATTERNS['mock_returns_mock'], context):
            issues.append("Mock returns another mock (code smell)")

        # Check for empty mock setup
        if re.search(self.SUSPICIOUS_PATTERNS['empty_mock_setup'], context):
            issues.append("Mock created but never configured")

        # Check for mock without behavior
        if re.search(self.SUSPICIOUS_PATTERNS['mock_without_behavior'], context):
            issues.append("Mock has no behavior setup (potential gap)")

        # Check for TODO/FIXME near mock
        if re.search(self.SUSPICIOUS_PATTERNS['todo_near_mock'], context):
            issues.append("TODO/FIXME comment near mock (implementation doubt)")

        # Check for mocking concrete class
        if re.search(self.SUSPICIOUS_PATTERNS['mock_concrete_class'], context):
            issues.append("Mocking concrete class instead of interface (design smell)")

        # Check for implementation gap indicators in nearby code
        context_window = full_content[max(0, match_position - 500):match_position + 500]
        for gap_pattern in self.GAP_INDICATORS:
            if re.search(gap_pattern, context_window):
                issues.append("Implementation gap indicator found nearby")
                break

        return issues

    def _calculate_severity(self, issues: List[str]) -> str:
        """Calculate severity based on issues"""
        if not issues:
            return "normal"

        critical_keywords = ["implementation gap", "returns another mock"]
        suspicious_keywords = ["TODO/FIXME", "never configured", "no behavior"]

        for issue in issues:
            for keyword in critical_keywords:
                if keyword.lower() in issue.lower():
                    return "critical"

        for issue in issues:
            for keyword in suspicious_keywords:
                if keyword.lower() in issue.lower():
                    return "suspicious"

        return "normal"

    def analyze_all_files(self):
        """Analyze all test files"""
        print("🔬 Analyzing test files for mocks and gaps...\n")

        for i, test_file in enumerate(self.test_files):
            if (i + 1) % 10 == 0:
                print(f"   Progress: {i + 1}/{len(self.test_files)} files analyzed")

            test_class = self.analyze_file(test_file)
            if test_class and test_class.mock_count > 0:
                self.test_classes.append(test_class)

        print(f"   ✓ Analyzed {len(self.test_files)} files\n")

    def generate_report(self):
        """Generate comprehensive mock audit report"""
        print("\n" + "="*80)
        print("MOCK AUDIT REPORT - CODE REVIEW")
        print("="*80)
        print(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"Solution: {self.solution_path}")
        print(f"Test files scanned: {len(self.test_files)}")
        print(f"Test classes with mocks: {len(self.test_classes)}")
        print(f"Total mocks found: {len(self.mock_usages)}\n")

        # Summary statistics
        critical_count = sum(1 for m in self.mock_usages if m.severity == "critical")
        suspicious_count = sum(1 for m in self.mock_usages if m.severity == "suspicious")

        print("📊 SEVERITY DISTRIBUTION:")
        print(f"   🔴 Critical:    {critical_count}")
        print(f"   🟡 Suspicious:  {suspicious_count}")
        print(f"   🟢 Normal:      {len(self.mock_usages) - critical_count - suspicious_count}\n")

        # Mock framework distribution
        framework_counts = Counter(m.mock_type for m in self.mock_usages)
        print("🛠️  MOCK FRAMEWORKS USED:")
        for framework, count in framework_counts.most_common():
            print(f"   {framework}: {count}")
        print()

        # Top issues
        all_issues = [issue for m in self.mock_usages for issue in m.issues]
        issue_counts = Counter(all_issues)
        print("⚠️  TOP ISSUES FOUND:")
        for issue, count in issue_counts.most_common(10):
            print(f"   [{count:3d}x] {issue}")
        print()

        # Critical findings
        critical_mocks = [m for m in self.mock_usages if m.severity == "critical"]
        if critical_mocks:
            print("🔴 CRITICAL FINDINGS (Implementation Gaps):")
            for i, mock in enumerate(critical_mocks[:10], 1):
                print(f"\n{i}. {mock.file_path}:{mock.line_number}")
                print(f"   Mock: {mock.mocked_interface}")
                print(f"   Issues: {', '.join(mock.issues)}")
            if len(critical_mocks) > 10:
                print(f"\n   ... and {len(critical_mocks) - 10} more critical findings")
            print()

        # Suspicious patterns
        suspicious_mocks = [m for m in self.mock_usages if m.severity == "suspicious"]
        if suspicious_mocks:
            print("🟡 SUSPICIOUS PATTERNS (Technical Doubts):")
            for i, mock in enumerate(suspicious_mocks[:10], 1):
                print(f"\n{i}. {mock.file_path}:{mock.line_number}")
                print(f"   Mock: {mock.mocked_interface}")
                print(f"   Issues: {', '.join(mock.issues)}")
            if len(suspicious_mocks) > 10:
                print(f"\n   ... and {len(suspicious_mocks) - 10} more suspicious patterns")
            print()

        # High mock count classes
        high_mock_classes = sorted([tc for tc in self.test_classes if tc.mock_count >= 5],
                                   key=lambda x: x.mock_count, reverse=True)
        if high_mock_classes:
            print("📈 TEST CLASSES WITH HIGH MOCK COUNT (Complexity Smell):")
            for tc in high_mock_classes[:15]:
                status = "🔧" if tc.has_todos or tc.has_fixmes else "  "
                print(f"   {status} {tc.class_name} ({tc.file_path})")
                print(f"      Mocks: {tc.mock_count} | Assertions: {tc.assertion_count}")
                if tc.suspicious_patterns:
                    print(f"      Suspicious: {', '.join(tc.suspicious_patterns)}")
            print()

        # Classes with TODOs/FIXMEs
        todo_classes = [tc for tc in self.test_classes if tc.has_todos or tc.has_fixmes]
        if todo_classes:
            print("🔧 TEST CLASSES WITH TODO/FIXME (Implementation Doubts):")
            for tc in todo_classes[:15]:
                markers = []
                if tc.has_todos:
                    markers.append("TODO")
                if tc.has_fixmes:
                    markers.append("FIXME")
                print(f"   [{'/'.join(markers)}] {tc.class_name}")
                print(f"      {tc.file_path} (Mocks: {tc.mock_count})")
            if len(todo_classes) > 15:
                print(f"   ... and {len(todo_classes) - 15} more")
            print()

    def export_json(self, output_path: str = "mock_audit_report.json"):
        """Export detailed findings to JSON"""
        report = {
            "metadata": {
                "generated_at": datetime.now().isoformat(),
                "solution_path": str(self.solution_path),
                "total_test_files": len(self.test_files),
                "total_test_classes": len(self.test_classes),
                "total_mocks": len(self.mock_usages)
            },
            "summary": {
                "critical_count": sum(1 for m in self.mock_usages if m.severity == "critical"),
                "suspicious_count": sum(1 for m in self.mock_usages if m.severity == "suspicious"),
                "normal_count": sum(1 for m in self.mock_usages if m.severity == "normal")
            },
            "mock_usages": [asdict(m) for m in self.mock_usages],
            "test_classes": [asdict(tc) for tc in self.test_classes],
        }

        output_file = Path(output_path)
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)

        print(f"📄 Detailed report exported to: {output_file.absolute()}\n")

def main():
    """Main execution"""
    # Initialize analyzer
    analyzer = MockAuditAnalyzer()

    # Scan and analyze
    analyzer.scan_test_files()
    analyzer.analyze_all_files()

    # Generate reports
    analyzer.generate_report()
    analyzer.export_json("mock_audit_report.json")

    print("="*80)
    print("Mock audit complete! Use the JSON report for detailed analysis.")
    print("="*80)

if __name__ == "__main__":
    main()
