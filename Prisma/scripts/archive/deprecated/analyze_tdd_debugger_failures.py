#!/usr/bin/env python3
"""
TddDebuggerAgent test failure analysis script for ExxerAI.

Analyzes the specific failures in TddDebuggerAgent.Tests and provides
detailed breakdown of the sealed class mocking issue.
"""

from pathlib import Path
import json
import re
from typing import Dict, List, Optional

def analyze_tdd_failures():
    """Analyze TddDebuggerAgent test failures."""
    
    # The specific log content from the test run
    log_content = """failed ExxerAI.Agents.TddDebuggerAgent.Tests.TddDebuggerAgentTests.DebugTestFailuresAsync_ShouldHandleSolverFailures_WhenResolutionFails (3s 124ms)
    Xunit.Runner.InProc.SystemConsole.TestingPlatform.XunitException : System.TypeLoadException : Could not load type 'Castle.Proxies.FailurePatternMatcherProxy' from assembly 'DynamicProxyGenAssembly2, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' because the parent type is sealed.
        at ExxerAI.Agents.TddDebuggerAgent.Tests.TddDebuggerAgentTests..ctor() in F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\tests\\TddDebuggerAgent.Tests\\TddDebuggerAgentTests.cs:34"""
    
    # Pattern to extract the specific file and line information
    source_pattern = r"in ([^:]+):(\d+)"
    test_name_pattern = r"failed ([^(]+)"
    
    failures = []
    
    for match in re.finditer(test_name_pattern, log_content):
        test_name = match.group(1).strip()
        
        # Extract source location
        source_match = re.search(source_pattern, log_content)
        if source_match:
            file_path = source_match.group(1)
            line_number = int(source_match.group(2))
        else:
            file_path = "Unknown"
            line_number = 0
            
        failure = {
            "test_name": test_name,
            "file_path": file_path,
            "line_number": line_number,
            "failure_type": "Sealed Class Mocking Issue",
            "error_message": "Could not load type 'Castle.Proxies.FailurePatternMatcherProxy' because the parent type is sealed",
            "root_cause": "NSubstitute cannot create proxy for sealed FailurePatternMatcher class",
            "solution_category": "Architecture Fix - Interface Extraction",
            "severity": "High",
            "impact": "Blocks all TddDebuggerAgent tests"
        }
        failures.append(failure)
    
    # There are 6 failing tests, all with the same issue
    test_methods = [
        "DebugTestFailuresAsync_ShouldHandleSolverFailures_WhenResolutionFails",
        "DebugTestFailuresAsync_ShouldHandleValidationFailures_WhenSafetyValidationFails", 
        "DebugTestFailuresAsync_ShouldHandleExceptions_WhenUnexpectedErrorOccurs",
        "DebugTestFailuresAsync_ShouldAnalyzeFailuresAndCreatePlan_WhenValidRequestProvided",
        "DebugTestFailuresAsync_ShouldReturnFailure_WhenAnalysisFails",
        "CompleteTddDebuggingWorkflow_ShouldResolveFailures_WhenValidScenarioProvided"
    ]
    
    # Create detailed failure records for each test
    failures = []
    for test_method in test_methods:
        failure = {
            "test_name": f"ExxerAI.Agents.TddDebuggerAgent.Tests.TddDebuggerAgentTests.{test_method}",
            "file_path": "F:\\Dynamic\\ExxerAi\\ExxerAI\\code\\src\\tests\\TddDebuggerAgent.Tests\\TddDebuggerAgentTests.cs",
            "line_number": 34,
            "failure_type": "Sealed Class Mocking Issue",
            "error_message": "System.TypeLoadException: Could not load type 'Castle.Proxies.FailurePatternMatcherProxy' because the parent type is sealed",
            "root_cause": "NSubstitute cannot create proxy for sealed FailurePatternMatcher class",
            "solution_category": "Architecture Fix - Interface Extraction",
            "severity": "High", 
            "impact": "Blocks all TddDebuggerAgent tests",
            "dependencies": ["FailurePatternMatcher", "TestFailureAnalyzer", "TestSolverAgent", "SafetyNetworkValidator"],
            "affected_services": ["TestFailureAnalyzer", "TestSolverAgent", "SafetyNetworkValidator"]
        }
        failures.append(failure)
    
    return {
        "summary": {
            "total_failures": len(failures),
            "failure_types": {
                "Sealed Class Mocking Issue": len(failures)
            },
            "severity_breakdown": {
                "High": len(failures),
                "Medium": 0,
                "Low": 0
            },
            "files_affected": ["TddDebuggerAgentTests.cs"],
            "services_affected": ["FailurePatternMatcher", "TestFailureAnalyzer", "TestSolverAgent", "SafetyNetworkValidator"]
        },
        "failures": failures,
        "recommendations": [
            {
                "category": "Immediate Fix",
                "action": "Extract IFailurePatternMatcher interface",
                "priority": "High",
                "description": "Create interface for FailurePatternMatcher to enable mocking"
            },
            {
                "category": "Architecture Improvement", 
                "action": "Remove sealed modifier or use composition pattern",
                "priority": "Medium",
                "description": "Consider if sealed modifier is necessary for service classes"
            },
            {
                "category": "Test Strategy",
                "action": "Review mocking strategy for service classes",
                "priority": "Medium", 
                "description": "Establish patterns for testing sealed classes"
            }
        ],
        "detailed_analysis": {
            "problem": "Castle DynamicProxy (used by NSubstitute) cannot create inheritance-based proxies for sealed classes",
            "current_state": "FailurePatternMatcher is sealed, preventing test mocking",
            "required_changes": [
                "Extract IFailurePatternMatcher interface",
                "Update TestFailureAnalyzer constructor to accept interface",
                "Update test to mock interface instead of concrete class"
            ],
            "validation_steps": [
                "Run dotnet build to ensure no compilation errors",
                "Run TddDebuggerAgent tests to verify fixes",
                "Verify all dependencies are properly injected"
            ]
        }
    }

def main():
    """Generate the failure analysis report."""
    analysis = analyze_tdd_failures()
    
    # Output structured analysis
    print("=" * 80)
    print("TDD DEBUGGER AGENT TEST FAILURE ANALYSIS")
    print("=" * 80)
    print()
    
    print("SUMMARY")
    print(f"  Total Failures: {analysis['summary']['total_failures']}")
    print(f"  Primary Issue: Sealed Class Mocking (Castle DynamicProxy limitation)")
    print(f"  Affected File: TddDebuggerAgentTests.cs:34")
    print(f"  Impact: All TddDebuggerAgent tests blocked")
    print()
    
    print("ROOT CAUSE ANALYSIS") 
    print(f"  Issue: NSubstitute.For<FailurePatternMatcher>() fails")
    print(f"  Reason: FailurePatternMatcher is sealed class")
    print(f"  Castle Proxy Error: Cannot inherit from sealed types")
    print()
    
    print("RECOMMENDED SOLUTIONS")
    for i, rec in enumerate(analysis['recommendations'], 1):
        print(f"  {i}. {rec['action']} ({rec['priority']} Priority)")
        print(f"     {rec['description']}")
    print()
    
    print("DETAILED FAILURES")
    for i, failure in enumerate(analysis['failures'], 1):
        test_name = failure['test_name'].split('.')[-1]  # Get just the method name
        print(f"  {i}. {test_name}")
        print(f"     Error: {failure['error_message'][:80]}...")
        print(f"     Location: Line {failure['line_number']}")
    print()
    
    # Export to JSON for programmatic use
    output_file = Path("tdd_debugger_failure_analysis.json")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(analysis, f, indent=2)
    
    print(f"Analysis exported to: {output_file.absolute()}")
    print()
    print("NEXT STEPS:")
    print("   1. Create IFailurePatternMatcher interface")
    print("   2. Update service registrations to use interface")
    print("   3. Update test mocks to use interface") 
    print("   4. Run tests to validate fix")

if __name__ == "__main__":
    main()