"""
Main entry point for AI document extractors.
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Optional

from extractors import (
    SmolVLMExtractor,
    GOTOcr2Extractor,
    PaddleOCRExtractor,
    DocTRExtractor
)


EXTRACTORS = {
    'smolvlm': SmolVLMExtractor,
    'got-ocr2': GOTOcr2Extractor,
    'paddle': PaddleOCRExtractor,
    'doctr': DocTRExtractor
}


def main():
    """Main CLI entry point."""
    parser = argparse.ArgumentParser(
        description="Extract structured information from documents using AI models",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Extract using SmolVLM
  python main.py --image document.png --extractor smolvlm
  
  # Extract with custom model configuration
  python main.py --image document.pdf --extractor smolvlm --config '{"max_new_tokens": 1024}'
  
  # Batch processing
  python main.py --batch folder/ --extractor paddle --output results.json
  
  # List available extractors
  python main.py --list-extractors
        """
    )
    
    parser.add_argument(
        '--image',
        type=str,
        help='Path to input image'
    )
    
    parser.add_argument(
        '--batch',
        type=str,
        help='Path to folder for batch processing'
    )
    
    parser.add_argument(
        '--extractor',
        type=str,
        choices=list(EXTRACTORS.keys()),
        default='smolvlm',
        help='Extractor to use (default: smolvlm)'
    )
    
    parser.add_argument(
        '--config',
        type=str,
        help='JSON configuration for extractor'
    )
    
    parser.add_argument(
        '--output',
        type=str,
        help='Output file path (default: stdout)'
    )
    
    parser.add_argument(
        '--list-extractors',
        action='store_true',
        help='List available extractors and exit'
    )
    
    parser.add_argument(
        '--device-info',
        action='store_true',
        help='Show device configuration and exit'
    )
    
    parser.add_argument(
        '--verbose',
        action='store_true',
        help='Enable verbose output'
    )
    
    args = parser.parse_args()
    
    # Handle info commands
    if args.list_extractors:
        print("Available extractors:")
        for name, cls in EXTRACTORS.items():
            print(f"  - {name}: {cls.__doc__.strip() if cls.__doc__ else 'No description'}")
        return 0
    
    if args.device_info:
        from utils.device_utils import get_optimal_device_config, get_memory_info
        config = get_optimal_device_config()
        memory = get_memory_info()
        
        print("Device Configuration:")
        print(f"  Device: {config['device']}")
        print(f"  Data type: {config['dtype']}")
        print(f"  CUDA available: {config['cuda_available']}")
        if config['gpu_name']:
            print(f"  GPU: {config['gpu_name']}")
        print(f"  Attention: {config['attn_impl']}")
        
        if memory:
            print("\nMemory Info:")
            for key, value in memory.items():
                print(f"  {key}: {value:.2f}")
        return 0
    
    # Validate input
    if not args.image and not args.batch:
        parser.error("Either --image or --batch must be specified")
    
    if args.image and args.batch:
        parser.error("Cannot specify both --image and --batch")
    
    # Parse configuration
    config = {}
    if args.config:
        try:
            config = json.loads(args.config)
        except json.JSONDecodeError as e:
            print(f"Error parsing config JSON: {e}", file=sys.stderr)
            return 1
    
    # Initialize extractor
    if args.verbose:
        print(f"Initializing {args.extractor} extractor...", file=sys.stderr)
    
    try:
        extractor_class = EXTRACTORS[args.extractor]
        extractor = extractor_class(config)
    except Exception as e:
        print(f"Error initializing extractor: {e}", file=sys.stderr)
        return 1
    
    # Process image(s)
    results = []
    
    if args.image:
        # Single image processing
        if args.verbose:
            print(f"Processing {args.image}...", file=sys.stderr)
        
        result = extractor.extract(args.image)
        results = [result]
        
    else:
        # Batch processing
        batch_path = Path(args.batch)
        if not batch_path.is_dir():
            print(f"Error: {batch_path} is not a directory", file=sys.stderr)
            return 1
        
        # Find all image files
        image_files = []
        for ext in ['*.png', '*.jpg', '*.jpeg', '*.pdf', '*.tiff', '*.bmp']:
            image_files.extend(batch_path.glob(ext))
            image_files.extend(batch_path.glob(ext.upper()))
        
        if not image_files:
            print(f"No image files found in {batch_path}", file=sys.stderr)
            return 1
        
        if args.verbose:
            print(f"Processing {len(image_files)} images...", file=sys.stderr)
        
        results = extractor.batch_extract(image_files)
    
    # Format output
    output_data = []
    for result in results:
        output_data.append(result.model_dump())
    
    # Single result unwrap
    if len(output_data) == 1 and args.image:
        output_data = output_data[0]
    
    # Write output
    output_json = json.dumps(output_data, indent=2, ensure_ascii=False)
    
    if args.output:
        output_path = Path(args.output)
        output_path.write_text(output_json, encoding='utf-8')
        if args.verbose:
            print(f"Results written to {output_path}", file=sys.stderr)
    else:
        print(output_json)
    
    return 0


if __name__ == "__main__":
    sys.exit(main())