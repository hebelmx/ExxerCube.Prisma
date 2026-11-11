"""Batch generation script for generating multiple authority-specific fixtures."""

import argparse
import subprocess
import sys
from pathlib import Path


def run_generation(authority: str, count: int, output_base: str, use_llm: bool = False,
                  llm_model: str = "llama2", chaos: str = "medium"):
    """Run generator for specific authority.

    Args:
        authority: Authority code (IMSS, SAT, etc.)
        count: Number of fixtures to generate
        output_base: Base output directory
        use_llm: Whether to use LLM
        llm_model: LLM model to use
        chaos: Chaos level
    """
    output_dir = Path(output_base) / authority
    output_dir.mkdir(parents=True, exist_ok=True)

    cmd = [
        sys.executable,
        "main_generator.py",
        "--count", str(count),
        "--authority", authority,
        "--output", str(output_dir),
        "--chaos", chaos,
        "--logo", "LogoMexico.jpg"
    ]

    if use_llm:
        cmd.extend(["--llm", "--llm-model", llm_model])

    print(f"\n{'=' * 60}")
    print(f"Generating {count} fixtures for {authority}")
    print(f"{'=' * 60}")

    result = subprocess.run(cmd)

    if result.returncode != 0:
        print(f"❌ Error generating fixtures for {authority}")
        return False

    return True


def main():
    """Main entry point for batch generation."""
    parser = argparse.ArgumentParser(
        description='Batch generate CNBV fixtures by authority',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Generate fixtures for all authorities
  python batch_generate.py --all --count 50

  # Generate specific counts for specific authorities
  python batch_generate.py --authorities IMSS:100 SAT:100 UIF:40

  # Use LLM for generation
  python batch_generate.py --all --count 20 --llm --llm-model llama3

  # Custom output directory
  python batch_generate.py --authorities IMSS:50 --output my_fixtures
        """
    )

    parser.add_argument(
        '--all',
        action='store_true',
        help='Generate for all authorities'
    )

    parser.add_argument(
        '--authorities',
        nargs='+',
        help='Authorities and counts in format AUTHORITY:COUNT (e.g., IMSS:100 SAT:50)'
    )

    parser.add_argument(
        '--count',
        type=int,
        default=50,
        help='Default count per authority when using --all (default: 50)'
    )

    parser.add_argument(
        '-o', '--output',
        type=str,
        default='batch_output',
        help='Base output directory (default: batch_output)'
    )

    parser.add_argument(
        '--chaos',
        choices=['none', 'low', 'medium', 'high'],
        default='medium',
        help='Chaos level (default: medium)'
    )

    parser.add_argument(
        '--llm',
        action='store_true',
        help='Use LLM (Ollama) for text generation'
    )

    parser.add_argument(
        '--llm-model',
        type=str,
        default='llama2',
        help='Ollama model to use (default: llama2)'
    )

    args = parser.parse_args()

    # Define all authorities with default counts
    all_authorities = {
        'IMSS': 100,
        'SAT': 100,
        'UIF': 40,
        'FGR': 60,
        'SEIDO': 50,
        'PJF': 60,
        'INFONAVIT': 50,
        'SHCP': 30,
        'CONDUSEF': 20,
    }

    # Determine which authorities to generate
    if args.all:
        # Use all authorities with specified count
        authorities_to_gen = {auth: args.count for auth in all_authorities.keys()}
    elif args.authorities:
        # Parse authority:count pairs
        authorities_to_gen = {}
        for auth_spec in args.authorities:
            if ':' in auth_spec:
                auth, count_str = auth_spec.split(':', 1)
                try:
                    count = int(count_str)
                    authorities_to_gen[auth.upper()] = count
                except ValueError:
                    print(f"⚠️  Invalid count for {auth}: {count_str}")
                    continue
            else:
                # No count specified, use default
                authorities_to_gen[auth_spec.upper()] = args.count
    else:
        print("❌ Error: Must specify either --all or --authorities")
        parser.print_help()
        sys.exit(1)

    # Validate authorities
    valid_authorities = list(all_authorities.keys())
    for auth in authorities_to_gen.keys():
        if auth not in valid_authorities:
            print(f"⚠️  Warning: Unknown authority '{auth}'. Valid options: {', '.join(valid_authorities)}")

    # Summary
    print("\n" + "=" * 60)
    print("BATCH GENERATION SUMMARY")
    print("=" * 60)
    print(f"Output directory: {args.output}")
    print(f"Chaos level: {args.chaos}")
    print(f"LLM generation: {'Enabled' if args.llm else 'Disabled'}")
    if args.llm:
        print(f"LLM model: {args.llm_model}")
    print("\nAuthorities to generate:")
    total_count = 0
    for auth, count in authorities_to_gen.items():
        print(f"  - {auth}: {count} fixtures")
        total_count += count
    print(f"\nTotal fixtures: {total_count}")
    print("=" * 60)

    # Confirm
    response = input("\nProceed with generation? (y/N): ")
    if response.lower() not in ['y', 'yes']:
        print("Cancelled.")
        sys.exit(0)

    # Generate fixtures for each authority
    successes = 0
    failures = 0

    for authority, count in authorities_to_gen.items():
        success = run_generation(
            authority=authority,
            count=count,
            output_base=args.output,
            use_llm=args.llm,
            llm_model=args.llm_model,
            chaos=args.chaos
        )

        if success:
            successes += 1
        else:
            failures += 1

    # Final summary
    print("\n" + "=" * 60)
    print("BATCH GENERATION COMPLETE")
    print("=" * 60)
    print(f"✅ Successful: {successes}/{len(authorities_to_gen)}")
    if failures > 0:
        print(f"❌ Failed: {failures}/{len(authorities_to_gen)}")
    print(f"📁 Output directory: {Path(args.output).absolute()}")
    print("=" * 60)


if __name__ == '__main__':
    main()
