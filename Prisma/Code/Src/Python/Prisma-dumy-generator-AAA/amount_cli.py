#!/usr/bin/env python3
"""
Command-line interface for amount extraction.
This script can be called from C# to extract amounts from text files.
"""
import sys
import os
import argparse
import json
from pathlib import Path

# Add the ocr_modules directory to the Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'ocr_modules'))

try:
    from amount_extractor import extract_amounts
except ImportError as e:
    print(f"Error importing amount_extractor: {e}", file=sys.stderr)
    sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description='Extract amounts from text file')
    parser.add_argument('--input', required=True, help='Input text file path')
    parser.add_argument('--output', required=True, help='Output directory path')
    
    args = parser.parse_args()
    
    try:
        # Read input file
        with open(args.input, 'r', encoding='utf-8') as f:
            text = f.read()
        
        # Extract amounts
        amounts = extract_amounts(text)
        
        # Create output directory
        output_dir = Path(args.output)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Write result as JSON
        output_file = output_dir / "amounts.json"
        if amounts:
            # Convert to JSON-serializable format
            json_amounts = []
            for amount in amounts:
                json_amounts.append({
                    'value': amount.value,
                    'currency': amount.currency,
                    'original_text': amount.original_text
                })
            
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump(json_amounts, f, indent=2)
            print(f"Amounts extracted: {len(amounts)} found")
        else:
            # Create empty JSON file
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump([], f)
            print("No amounts found")
            
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
