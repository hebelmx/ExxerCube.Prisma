#!/usr/bin/env python3
"""
Command-line interface for causa extraction.
This script can be called from C# to extract causa from text files.
"""
import sys
import os
import argparse
from pathlib import Path

# Add the ocr_modules directory to the Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'ocr_modules'))

try:
    from section_extractor import extract_section
except ImportError as e:
    print(f"Error importing section_extractor: {e}", file=sys.stderr)
    sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description='Extract causa from text file')
    parser.add_argument('--input', required=True, help='Input text file path')
    parser.add_argument('--output', required=True, help='Output directory path')
    
    args = parser.parse_args()
    
    try:
        # Read input file
        with open(args.input, 'r', encoding='utf-8') as f:
            text = f.read()
        
        print(f"Processing text: {text[:100]}...", file=sys.stderr)
        
        # Define causa section aliases
        causa_start_aliases = [
            "CAUSA",
            "CAUSA:",
            "CAUSA DEL JUICIO",
            "CAUSA DEL JUICIO:",
            "NATURALEZA",
            "NATURALEZA:",
            "NATURALEZA DEL JUICIO",
            "NATURALEZA DEL JUICIO:"
        ]
        
        causa_end_aliases = [
            "ACCION SOLICITADA",
            "ACCION SOLICITADA:",
            "PETICION",
            "PETICION:",
            "SOLICITA",
            "SOLICITA:",
            "EXPEDIENTE",
            "EXPEDIENTE:",
            "FECHA",
            "FECHA:"
        ]
        
        # Extract causa
        causa = extract_section(text, causa_start_aliases, causa_end_aliases, include_header=False)
        
        print(f"Extracted causa: {causa}", file=sys.stderr)
        
        # Create output directory
        output_dir = Path(args.output)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Write result
        output_file = output_dir / "causa.txt"
        if causa:
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(causa)
            print(f"Causa extracted: {causa}")
        else:
            # Create empty file to indicate no causa found
            output_file.touch()
            print("No causa found")
            
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
