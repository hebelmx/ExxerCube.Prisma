#!/usr/bin/env python3
"""
Command-line interface for date extraction.
This script can be called from C# to extract dates from text files.
"""
import sys
import os
import argparse
from pathlib import Path

# Add the ocr_modules directory to the Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'ocr_modules'))

try:
    from date_extractor import extract_dates
except ImportError as e:
    print(f"Error importing date_extractor: {e}", file=sys.stderr)
    sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description='Extract dates from text file')
    parser.add_argument('--input', required=True, help='Input text file path')
    parser.add_argument('--output', required=True, help='Output directory path')
    
    args = parser.parse_args()
    
    try:
        # Read input file
        with open(args.input, 'r', encoding='utf-8') as f:
            text = f.read()
        
        # Extract dates
        dates = extract_dates(text)
        
        # Create output directory
        output_dir = Path(args.output)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Write result
        output_file = output_dir / "dates.txt"
        if dates:
            with open(output_file, 'w', encoding='utf-8') as f:
                for date in dates:
                    f.write(f"{date}\n")
            print(f"Dates extracted: {len(dates)} found")
        else:
            # Create empty file to indicate no dates found
            output_file.touch()
            print("No dates found")
            
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
