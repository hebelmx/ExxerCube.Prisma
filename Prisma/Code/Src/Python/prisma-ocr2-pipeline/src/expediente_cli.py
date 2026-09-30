#!/usr/bin/env python3
"""
Command-line interface for expediente extraction.
This script can be called from C# to extract expediente from text files.
"""
import sys
import os
import argparse
from pathlib import Path

# Add the ocr_modules directory to the Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'ocr_modules'))

try:
    from expediente_extractor import extract_expediente
except ImportError as e:
    print(f"Error importing expediente_extractor: {e}", file=sys.stderr)
    sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description='Extract expediente from text file')
    parser.add_argument('--input', required=True, help='Input text file path')
    parser.add_argument('--output', required=True, help='Output directory path')
    
    args = parser.parse_args()
    
    try:
        # Read input file
        with open(args.input, 'r', encoding='utf-8') as f:
            text = f.read()
        
        # Extract expediente
        expediente = extract_expediente(text)
        
        # Create output directory
        output_dir = Path(args.output)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Write result
        output_file = output_dir / "expediente.txt"
        if expediente:
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(expediente)
            print(f"Expediente extracted: {expediente}")
        else:
            # Create empty file to indicate no expediente found
            output_file.touch()
            print("No expediente found")
            
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
