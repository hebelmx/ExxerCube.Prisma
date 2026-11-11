#!/usr/bin/env python3
"""
Google API Usage Analysis for ExxerAI Project
Analyzes Google API implementations and classifies them for M2M migration strategy.
Based on the existing failure analysis scripts architecture.
"""

import os
import re
import json
from collections import defaultdict, Counter
from dataclasses import dataclass
from typing import List, Dict, Optional, Set
from datetime import datetime
import ast

@dataclass
class GoogleApiUsage:
    file_path: str
    class_name: str
    api_type: str  # "Google.Apis.Drive.v3", "Google.Apis.Auth", etc.
    usage_pattern: str  # "OAuth", "ServiceAccount", "DirectAPI", "MCP", etc.
    is_deprecated: bool
    requires_m2m: bool
    complexity_score: int  # 1-5 scale
    dependencies: List[str]
    recommended_action: str  # "Keep", "Migrate", "Deprecate", "Modernize"

@dataclass
class CredentialAnalysis:
    credential_type: str  # "OAuth", "ServiceAccount", "APIKey", "ADC"
    file_location: str
    is_modern: bool
    security_level: str  # "High", "Medium", "Low"
    deprecation_risk: str  # "High", "Medium", "Low", "None"

def scan_csharp_files(base_path: str) -> List[str]:
    """Scan for all C# files in the project"""
    cs_files = []
    for root, dirs, files in os.walk(base_path):
        # Skip common generated/temporary directories
        skip_dirs = {'bin', 'obj', 'TestResults', 'artifacts', 'packages', '.git', '.vs'}
        dirs[:] = [d for d in dirs if d not in skip_dirs]
        
        for file in files:
            if file.endswith('.cs'):
                cs_files.append(os.path.join(root, file))
    
    return cs_files

def analyze_google_imports(file_path: str) -> List[str]:
    """Extract Google API imports from a C# file"""
    google_imports = []
    try:
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
        
        # Find using statements for Google APIs
        using_pattern = r'using\s+(Google\.[^;]+);'
        matches = re.findall(using_pattern, content, re.MULTILINE)
        google_imports.extend(matches)
        
        # Also look for fully qualified names
        qualified_pattern = r'Google\.[A-Za-z0-9_.]+(?=\s*[(\[;])'
        qualified_matches = re.findall(qualified_pattern, content)
        google_imports.extend(qualified_matches)
        
    except Exception as e:
        print(f"Error reading {file_path}: {e}")
    
    return list(set(google_imports))

def determine_api_pattern(file_path: str, content: str) -> str:
    """Determine the Google API usage pattern"""
    patterns = {
        "OAuth": [r'GoogleWebAuthorizationBroker', r'OAuth', r'AuthorizeAsync'],
        "ServiceAccount": [r'ServiceAccountCredential', r'GoogleCredential\.FromServiceAccount'],
        "ADC": [r'GoogleCredential\.GetApplicationDefault', r'ApplicationDefaultCredentials'],
        "DirectAPI": [r'DriveService', r'new.*Service\('],
        "MCP": [r'MCP', r'ModelContextProtocol'],
        "Testing": [r'Test', r'Mock', r'Fake', r'Stub']
    }
    
    detected_patterns = []
    for pattern_name, regexes in patterns.items():
        for regex in regexes:
            if re.search(regex, content, re.IGNORECASE):
                detected_patterns.append(pattern_name)
                break
    
    return ",".join(detected_patterns) if detected_patterns else "Unknown"

def calculate_complexity_score(content: str, api_imports: List[str]) -> int:
    """Calculate complexity score (1-5) based on API usage"""
    score = 1
    
    # Base score for number of Google APIs used
    score += min(len(api_imports), 3)
    
    # Complexity indicators
    complexity_indicators = [
        r'async\s+Task',  # Async operations
        r'CancellationToken',  # Cancellation support
        r'Result<',  # Result pattern
        r'Exception',  # Error handling
        r'ILogger',  # Logging
        r'OAuth',  # OAuth complexity
        r'Credential',  # Credential management
    ]
    
    for indicator in complexity_indicators:
        if re.search(indicator, content, re.IGNORECASE):
            score += 0.5
    
    return min(int(score), 5)

def determine_deprecation_risk(api_imports: List[str], usage_pattern: str) -> str:
    """Determine deprecation risk level"""
    high_risk_patterns = ["OAuth", "GoogleWebAuthorizationBroker"]
    medium_risk_apis = ["Google.Apis.Drive.v3"]
    
    if any(pattern in usage_pattern for pattern in high_risk_patterns):
        return "High"
    
    if any(api in str(api_imports) for api in medium_risk_apis):
        return "Medium"
    
    return "Low"

def recommend_action(usage: GoogleApiUsage) -> str:
    """Recommend action based on analysis"""
    if "Testing" in usage.usage_pattern:
        return "Deprecate - Test Only"
    
    if usage.is_deprecated or "OAuth" in usage.usage_pattern:
        return "Migrate to M2M"
    
    if "ADC" in usage.usage_pattern or "ServiceAccount" in usage.usage_pattern:
        return "Keep - Modern"
    
    if "MCP" in usage.usage_pattern:
        return "Keep - MCP Protocol"
    
    if usage.complexity_score <= 2:
        return "Modernize"
    
    return "Review Required"

def analyze_file(file_path: str) -> Optional[GoogleApiUsage]:
    """Analyze a single C# file for Google API usage"""
    try:
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
        
        google_imports = analyze_google_imports(file_path)
        if not google_imports:
            return None
        
        # Extract class name
        class_match = re.search(r'class\s+([A-Za-z0-9_]+)', content)
        class_name = class_match.group(1) if class_match else os.path.basename(file_path)
        
        # Determine primary API type
        api_type = "Google.Apis.Core"  # Default
        for import_name in google_imports:
            if "Drive" in import_name:
                api_type = "Google.Apis.Drive.v3"
                break
            elif "Auth" in import_name:
                api_type = "Google.Apis.Auth"
                break
        
        usage_pattern = determine_api_pattern(file_path, content)
        complexity_score = calculate_complexity_score(content, google_imports)
        
        # Determine if deprecated based on patterns
        is_deprecated = "OAuth" in usage_pattern or "GoogleWebAuthorizationBroker" in content
        
        # Check if requires M2M migration
        requires_m2m = is_deprecated or "Test" not in usage_pattern
        
        usage = GoogleApiUsage(
            file_path=file_path,
            class_name=class_name,
            api_type=api_type,
            usage_pattern=usage_pattern,
            is_deprecated=is_deprecated,
            requires_m2m=requires_m2m,
            complexity_score=complexity_score,
            dependencies=google_imports,
            recommended_action=""
        )
        
        usage.recommended_action = recommend_action(usage)
        
        return usage
        
    except Exception as e:
        print(f"Error analyzing {file_path}: {e}")
        return None

def analyze_credentials_configuration(base_path: str) -> List[CredentialAnalysis]:
    """Analyze credential configuration files"""
    credential_files = []
    
    # Look for credential files
    for root, dirs, files in os.walk(base_path):
        for file in files:
            if any(pattern in file.lower() for pattern in ['credential', 'google', 'oauth', 'service-account']):
                if file.endswith(('.json', '.cs', '.config')):
                    credential_files.append(os.path.join(root, file))
    
    analyses = []
    for file_path in credential_files:
        try:
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
            
            # Determine credential type
            if 'client_secret' in content and 'client_id' in content:
                cred_type = "OAuth"
                is_modern = False
                security_level = "Low"
                deprecation_risk = "High"
            elif 'private_key' in content and 'client_email' in content:
                cred_type = "ServiceAccount"
                is_modern = True
                security_level = "High"
                deprecation_risk = "None"
            elif 'ApplicationDefaultCredentials' in content:
                cred_type = "ADC"
                is_modern = True
                security_level = "High"
                deprecation_risk = "None"
            else:
                cred_type = "Unknown"
                is_modern = False
                security_level = "Unknown"
                deprecation_risk = "Unknown"
            
            analyses.append(CredentialAnalysis(
                credential_type=cred_type,
                file_location=file_path,
                is_modern=is_modern,
                security_level=security_level,
                deprecation_risk=deprecation_risk
            ))
            
        except Exception as e:
            print(f"Error analyzing credential file {file_path}: {e}")
    
    return analyses

def categorize_by_action(usages: List[GoogleApiUsage]) -> Dict[str, List[GoogleApiUsage]]:
    """Categorize usages by recommended action"""
    categories = defaultdict(list)
    for usage in usages:
        categories[usage.recommended_action].append(usage)
    return dict(categories)

def generate_migration_plan(usages: List[GoogleApiUsage]) -> List[str]:
    """Generate specific migration recommendations"""
    recommendations = []
    
    # Analyze patterns
    oauth_count = sum(1 for u in usages if "OAuth" in u.usage_pattern)
    test_count = sum(1 for u in usages if "Testing" in u.usage_pattern)
    mcp_count = sum(1 for u in usages if "MCP" in u.usage_pattern)
    
    if oauth_count > 0:
        recommendations.append(f"[!] CRITICAL: {oauth_count} files using obsolete OAuth flows")
        recommendations.append("   - Priority: HIGH - Replace with Service Account or ADC")
        recommendations.append("   - These will fail with 'Error 400: invalid_request' OAuth flow issues")
        recommendations.append("   - Migrate to Google.Apis.Auth v1.71.0+ with M2M authentication")
    
    if test_count > 0:
        recommendations.append(f"[*] TESTING: {test_count} test-related Google API files")
        recommendations.append("   - Priority: LOW - These can be deprecated after migration")
        recommendations.append("   - Consider mocking Google APIs in tests instead")
    
    if mcp_count > 0:
        recommendations.append(f"[+] MCP: {mcp_count} Model Context Protocol implementations")
        recommendations.append("   - Priority: KEEP - These are modern M2M implementations")
        recommendations.append("   - Ensure they use latest OAuth protocols")
    
    return recommendations

def generate_detailed_report(usages: List[GoogleApiUsage], credentials: List[CredentialAnalysis]) -> str:
    """Generate detailed analysis report"""
    categories = categorize_by_action(usages)
    
    report = "=" * 80 + "\n"
    report += "GOOGLE API USAGE ANALYSIS REPORT\n"
    report += f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n"
    report += "=" * 80 + "\n\n"
    
    # Summary
    report += f"📊 SUMMARY:\n"
    report += f"  Total Files Analyzed: {len(usages)}\n"
    report += f"  Credential Files Found: {len(credentials)}\n"
    report += f"  Unique API Types: {len(set(u.api_type for u in usages))}\n"
    report += f"  Average Complexity: {sum(u.complexity_score for u in usages) / len(usages):.1f}/5\n"
    report += "\n"
    
    # Breakdown by action
    report += "🎯 RECOMMENDED ACTIONS:\n"
    report += "-" * 40 + "\n"
    for action, files in categories.items():
        report += f"  {action}: {len(files)} files\n"
    report += "\n"
    
    # Detailed breakdown
    for action, files in categories.items():
        report += f"📁 {action.upper()} ({len(files)} files):\n"
        report += "-" * 50 + "\n"
        for usage in files:
            relative_path = usage.file_path.replace(os.getcwd(), "").lstrip(os.sep)
            report += f"  📄 {usage.class_name}\n"
            report += f"      Path: {relative_path}\n"
            report += f"      API: {usage.api_type}\n"
            report += f"      Pattern: {usage.usage_pattern}\n"
            report += f"      Complexity: {usage.complexity_score}/5\n"
            report += f"      Deprecated: {'Yes' if usage.is_deprecated else 'No'}\n"
            report += f"      Dependencies: {', '.join(usage.dependencies[:3])}{'...' if len(usage.dependencies) > 3 else ''}\n"
            report += "\n"
    
    # Credential analysis
    if credentials:
        report += "🔐 CREDENTIAL ANALYSIS:\n"
        report += "-" * 40 + "\n"
        for cred in credentials:
            relative_path = cred.file_location.replace(os.getcwd(), "").lstrip(os.sep)
            report += f"  🔑 {cred.credential_type}\n"
            report += f"      Location: {relative_path}\n"
            report += f"      Modern: {'Yes' if cred.is_modern else 'No'}\n"
            report += f"      Security: {cred.security_level}\n"
            report += f"      Deprecation Risk: {cred.deprecation_risk}\n"
            report += "\n"
    
    return report

def main():
    print("[*] GOOGLE API USAGE ANALYSIS FOR EXXERAI")
    print("=" * 60)
    
    # Configuration
    base_path = r"F:\Dynamic\ExxerAi\ExxerAI\code\src"
    output_dir = r"F:\Dynamic\ExxerAi\ExxerAI\scripts"
    
    try:
        print(f"[+] Scanning C# files in: {base_path}")
        cs_files = scan_csharp_files(base_path)
        print(f"[+] Found {len(cs_files)} C# files")
        
        # Analyze Google API usage
        print("[*] Analyzing Google API usage...")
        usages = []
        for file_path in cs_files:
            usage = analyze_file(file_path)
            if usage:
                usages.append(usage)
        
        print(f"[+] Found Google API usage in {len(usages)} files")
        
        # Analyze credentials
        print("[*] Analyzing credential configurations...")
        credentials = analyze_credentials_configuration(os.path.dirname(base_path))
        print(f"[+] Found {len(credentials)} credential configurations")
        
        if not usages and not credentials:
            print("[X] No Google API usage found in the project")
            return
        
        # Generate migration recommendations
        migration_plan = generate_migration_plan(usages)
        
        # Generate detailed report
        detailed_report = generate_detailed_report(usages, credentials)
        
        # Print summary to console
        categories = categorize_by_action(usages)
        print("\n[+] ANALYSIS RESULTS:")
        print("-" * 40)
        for action, files in categories.items():
            print(f"  {action}: {len(files)} files")
        
        print("\n[*] MIGRATION RECOMMENDATIONS:")
        print("-" * 40)
        for rec in migration_plan:
            print(rec)
        
        # Save detailed results
        output_data = {
            "analysis_date": datetime.now().isoformat(),
            "summary": {
                "total_files": len(usages),
                "credential_files": len(credentials),
                "categories": {action: len(files) for action, files in categories.items()}
            },
            "usages": [
                {
                    "file_path": u.file_path,
                    "class_name": u.class_name,
                    "api_type": u.api_type,
                    "usage_pattern": u.usage_pattern,
                    "is_deprecated": u.is_deprecated,
                    "requires_m2m": u.requires_m2m,
                    "complexity_score": u.complexity_score,
                    "dependencies": u.dependencies,
                    "recommended_action": u.recommended_action
                }
                for u in usages
            ],
            "credentials": [
                {
                    "credential_type": c.credential_type,
                    "file_location": c.file_location,
                    "is_modern": c.is_modern,
                    "security_level": c.security_level,
                    "deprecation_risk": c.deprecation_risk
                }
                for c in credentials
            ],
            "migration_plan": migration_plan
        }
        
        # Save JSON report
        json_file = os.path.join(output_dir, "google_api_analysis.json")
        with open(json_file, 'w', encoding='utf-8') as f:
            json.dump(output_data, f, indent=2, ensure_ascii=False)
        
        # Save detailed text report
        txt_file = os.path.join(output_dir, "google_api_analysis_detailed.txt")
        with open(txt_file, 'w', encoding='utf-8') as f:
            f.write(detailed_report)
        
        print(f"\n[+] Detailed analysis saved to:")
        print(f"  JSON: {json_file}")
        print(f"  TXT:  {txt_file}")
        
    except Exception as e:
        print(f"[X] Error during analysis: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()