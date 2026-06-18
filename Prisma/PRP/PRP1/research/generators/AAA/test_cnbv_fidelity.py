#!/usr/bin/env python3
"""
Test CNBV Visual Fidelity

Tests the XML→PDF generator against real CNBV samples.
Measures visual similarity and reports results.
"""

import sys
from pathlib import Path

# Add parent to path
sys.path.insert(0, str(Path(__file__).parent))

from prp1_generator.cnbv_pdf_generator import xml_to_pdf
from prp1_generator.visual_similarity import measure_similarity


def test_sample(sample_name: str, fixtures_dir: Path, output_dir: Path):
    """
    Test one sample.

    Args:
        sample_name: Base name (e.g., "222AAA-44444444442025")
        fixtures_dir: Path to Prisma/Fixtures/PRP1/
        output_dir: Where to save generated files
    """
    print(f"\n{'='*60}")
    print(f"Testing: {sample_name}")
    print(f"{'='*60}")

    # Paths
    xml_path = fixtures_dir / f"{sample_name}.xml"
    real_pdf = fixtures_dir / f"{sample_name}.pdf"
    logo_path = fixtures_dir / "LogoMexico.jpg"

    output_pdf = output_dir / f"{sample_name}.generated.pdf"
    comparison_img = output_dir / f"{sample_name}.comparison.png"

    # Check files exist
    if not xml_path.exists():
        print(f"❌ XML not found: {xml_path}")
        return None

    if not real_pdf.exists():
        print(f"⚠ Real PDF not found: {real_pdf}")
        real_pdf = None

    # Step 1: Generate PDF from XML
    print(f"\n1. Generating PDF from XML...")
    try:
        generated_pdf = xml_to_pdf(
            xml_path,
            output_pdf,
            logo_path=logo_path if logo_path.exists() else None
        )
        print(f"   ✓ Generated: {generated_pdf}")
    except Exception as e:
        print(f"   ❌ Error: {e}")
        import traceback
        traceback.print_exc()
        return None

    # Step 2: Measure similarity (if real PDF exists)
    if real_pdf and real_pdf.exists():
        print(f"\n2. Measuring visual similarity...")
        try:
            score = measure_similarity(
                generated_pdf,
                real_pdf,
                save_comparison=comparison_img
            )

            print(f"\n   Similarity Scores:")
            print(f"   ├─ Overall:  {score.overall_score:.1f}%")
            print(f"   ├─ Layout:   {score.layout_score:.1f}%")
            print(f"   ├─ Content:  {score.content_score:.1f}%")
            print(f"   └─ Color:    {score.color_score:.1f}%")

            print(f"\n   Comparison image: {comparison_img}")

            # Interpretation
            if score.overall_score >= 85:
                print(f"\n   ✅ EXCELLENT - Highly similar to real CNBV document")
                status = "EXCELLENT"
            elif score.overall_score >= 70:
                print(f"\n   ✓ GOOD - Acceptable similarity")
                status = "GOOD"
            elif score.overall_score >= 50:
                print(f"\n   ⚠ FAIR - Needs improvement")
                status = "FAIR"
            else:
                print(f"\n   ❌ POOR - Significant differences")
                status = "POOR"

            return {
                "sample": sample_name,
                "status": status,
                "score": score.overall_score,
                "details": {
                    "layout": score.layout_score,
                    "content": score.content_score,
                    "color": score.color_score,
                },
                "files": {
                    "generated": str(generated_pdf),
                    "comparison": str(comparison_img),
                }
            }

        except Exception as e:
            print(f"   ❌ Error measuring similarity: {e}")
            import traceback
            traceback.print_exc()
            return None
    else:
        print(f"\n2. ⚠ Skipping similarity measurement (no real PDF)")
        return {
            "sample": sample_name,
            "status": "GENERATED",
            "score": None,
            "files": {
                "generated": str(generated_pdf),
            }
        }


def main():
    """Main test runner."""
    # Paths
    fixtures_dir = Path("F:/Dynamic/ExxerCubeBanamex/ExxerCube.Prisma/Prisma/Fixtures/PRP1")
    output_dir = Path("generators/AAA/test_output/fidelity_tests")

    # Create output directory
    output_dir.mkdir(parents=True, exist_ok=True)

    # Samples to test
    samples = [
        "222AAA-44444444442025",  # ASEGURAMIENTO
        "333BBB-44444444442025",  # HACENDARIO
        "333ccc-6666666662025",   # JUDICIAL
        "555CCC-66666662025",     # UIF
    ]

    print("="*60)
    print("CNBV Visual Fidelity Test Suite")
    print("="*60)
    print(f"\nFixtures directory: {fixtures_dir}")
    print(f"Output directory: {output_dir}")
    print(f"\nTesting {len(samples)} samples...")

    # Test each sample
    results = []
    for sample_name in samples:
        result = test_sample(sample_name, fixtures_dir, output_dir)
        if result:
            results.append(result)

    # Summary
    print(f"\n{'='*60}")
    print("Summary")
    print(f"{'='*60}\n")

    if not results:
        print("❌ No successful tests")
        return 1

    # Calculate averages
    scores = [r["score"] for r in results if r["score"] is not None]
    if scores:
        avg_score = sum(scores) / len(scores)
        print(f"Average Similarity Score: {avg_score:.1f}%\n")

    # Print table
    print(f"{'Sample':<30} {'Status':<12} {'Score':>8}")
    print("-" * 60)
    for r in results:
        score_str = f"{r['score']:.1f}%" if r['score'] else "N/A"
        print(f"{r['sample']:<30} {r['status']:<12} {score_str:>8}")

    print(f"\n{'='*60}")
    print(f"All outputs saved to: {output_dir}")
    print(f"{'='*60}\n")

    # Exit code based on results
    if scores and avg_score >= 70:
        print("✅ SUCCESS - Visual fidelity is acceptable")
        return 0
    elif scores:
        print("⚠ WARNING - Visual fidelity needs improvement")
        return 0
    else:
        print("❌ FAILURE - Could not measure visual fidelity")
        return 1


if __name__ == "__main__":
    sys.exit(main())
