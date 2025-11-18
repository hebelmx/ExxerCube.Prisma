#!/usr/bin/env python3
"""
Check for XUnit warnings in build output
"""

import subprocess
import re

def check_xunit_warnings():
    """Check what XUnit warnings exist in build output"""
    print("Checking for XUnit warnings in build output...")
    
    cmd = ['dotnet', 'build', 'Src/IndTrace.sln', '-v:n', '--no-restore']
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, cwd='.')
        output = result.stdout + result.stderr
        
        # Look for various XUnit patterns
        xunit_patterns = {
            'xUnit1051': r'xUnit1051:',
            'xUnit1031': r'xUnit1031:',
            'xUnit1026': r'xUnit1026:',
            'xUnit1004': r'xUnit1004:',
            'xUnit1013': r'xUnit1013:',
            'All xUnit': r'xUnit\d+:',
        }
        
        print("XUnit Warning Analysis:")
        print("=" * 50)
        
        for name, pattern in xunit_patterns.items():
            matches = re.findall(pattern, output)
            count = len(matches)
            print(f"{name}: {count} occurrences")
            
            # Show first few examples for xUnit1051
            if name == 'xUnit1051' and count > 0:
                print("  Examples:")
                lines = output.split('\n')
                examples = [line for line in lines if 'xUnit1051:' in line][:3]
                for example in examples:
                    print(f"    {example.strip()}")
        
        # Also check for general patterns
        cancellation_patterns = {
            'CancellationToken.None': r'CancellationToken\.None',
            'new CancellationToken()': r'new\s+CancellationToken\s*\(\s*\)',
            'TestContext.Current.CancellationToken': r'TestContext\.Current\.CancellationToken',
        }
        
        print("\nCancellationToken Pattern Analysis:")
        print("=" * 50)
        
        for name, pattern in cancellation_patterns.items():
            matches = re.findall(pattern, output)
            count = len(matches)
            print(f"{name}: {count} occurrences")
        
        # Check if warnings are being treated as errors
        warnings_as_errors = 'TreatWarningsAsErrors' in output or 'warning' in output.lower()
        print(f"\nWarnings in output: {warnings_as_errors}")
        
        # Look for any xUnit-related content at all
        all_xunit = [line for line in output.split('\n') if 'xunit' in line.lower()]
        print(f"\nTotal lines mentioning 'xunit': {len(all_xunit)}")
        
        if len(all_xunit) > 0:
            print("Sample xUnit-related lines:")
            for line in all_xunit[:5]:
                print(f"  {line.strip()}")
                
        return output
        
    except Exception as e:
        print(f"Error running build: {e}")
        return ""

if __name__ == "__main__":
    check_xunit_warnings()