#!/usr/bin/env python3
"""
Command-line interface for accion solicitada extraction.
This script can be called from C# to extract accion solicitada from text files.
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
    parser = argparse.ArgumentParser(description='Extract accion solicitada from text file')
    parser.add_argument('--input', required=True, help='Input text file path')
    parser.add_argument('--output', required=True, help='Output directory path')
    
    args = parser.parse_args()
    
    try:
        # Read input file
        with open(args.input, 'r', encoding='utf-8') as f:
            text = f.read()
        
        print(f"Processing text: {text[:100]}...", file=sys.stderr)
        
        # Define accion solicitada section aliases
        accion_start_aliases = [
            "ACCION SOLICITADA",
            "ACCION SOLICITADA:",
            "PETICION",
            "PETICION:",
            "SOLICITA",
            "SOLICITA:",
            "LO QUE SE SOLICITA",
            "LO QUE SE SOLICITA:",
            "ACCION",
            "ACCION:"
        ]
        
        accion_end_aliases = [
            "EXPEDIENTE",
            "EXPEDIENTE:",
            "FECHA",
            "FECHA:",
            "FIRMA",
            "FIRMA:",
            "NOMBRE",
            "NOMBRE:",
            "DIRECCION",
            "DIRECCION:",
            "TELEFONO",
            "TELEFONO:"
        ]
        
        # Extract accion solicitada
        accion = extract_section(text, accion_start_aliases, accion_end_aliases, include_header=False)
        
        print(f"Extracted accion: {accion}", file=sys.stderr)
        
        # Create output directory
        output_dir = Path(args.output)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Write result
        output_file = output_dir / "accion_solicitada.txt"
        if accion:
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(accion)
            print(f"Accion solicitada extracted: {accion}")
        else:
            # Create empty file to indicate no accion found
            output_file.touch()
            print("No accion solicitada found")
            
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
