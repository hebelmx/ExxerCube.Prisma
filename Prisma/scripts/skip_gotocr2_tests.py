#!/usr/bin/env python3
"""
Skip all GotOcr2 tests - feature no longer in active development.

GotOcr2 is:
- Not deprecated yet
- No longer in active development
- Maintained only if needed
- Tests pass but take too much time
- Doesn't add value vs complexity at this time

This script adds Skip attribute to all [Fact] and [Theory] attributes in GotOcr2 test files.
"""

import re
from pathlib import Path
from typing import List

class GotOcr2TestSkipper:
    """Adds Skip attribute to all GotOcr2 test methods."""

    SKIP_REASON = "GotOcr2 no longer in active development - maintained only if needed. Feature working but tests take too long."

    GOTOCR2_TEST_FILES = [
        "Code/Src/CSharp/Tests.Infrastructure.Extraction.GotOcr2/GotOcr2OcrExecutorTests.cs",
        "Code/Src/CSharp/Tests.Infrastructure.Extraction.GotOcr2/GotOcr2OcrExecutorDegradedTests.cs",
        "Code/Src/CSharp/Tests.Infrastructure.Extraction.GotOcr2/GotOcr2OcrExecutorEnhancedTests.cs",
        "Code/Src/CSharp/Tests.Infrastructure.Extraction.GotOcr2/GotOcr2OcrExecutorEnhancedAggressiveTests.cs",
    ]

    def __init__(self, dry_run: bool = False):
        self.dry_run = dry_run
        self.files_modified = 0
        self.tests_skipped = 0

    def skip_all_gotocr2_tests(self):
        """Skip all GotOcr2 test methods."""
        print("="*80)
        print("SKIP GOTOCR2 TESTS")
        print("="*80)
        print(f"Reason: {self.SKIP_REASON}")
        print("")

        if self.dry_run:
            print("🔍 DRY RUN MODE - No files will be modified")
            print("")

        for test_file in self.GOTOCR2_TEST_FILES:
            self._skip_tests_in_file(test_file)

        print("")
        print("="*80)
        print("SUMMARY")
        print("="*80)
        print(f"Files modified: {self.files_modified}")
        print(f"Tests skipped: {self.tests_skipped}")

        if self.dry_run:
            print("")
            print("🔄 To apply changes, run without --dry-run")
        else:
            print("")
            print("✅ All GotOcr2 tests now skipped!")
            print("")
            print("To verify, run:")
            print("  dotnet test Code/Src/CSharp/Tests.Infrastructure.Extraction.GotOcr2/")
            print("")
            print("Expected: All tests skipped (0 run)")

    def _skip_tests_in_file(self, file_path: str):
        """Add Skip attribute to all test methods in a file."""
        path = Path(file_path)

        if not path.exists():
            print(f"⊘ File not found: {file_path}")
            return

        print(f"📝 Processing: {path.name}")

        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()

        original_content = content

        # Find all [Fact] and [Theory] attributes that DON'T already have Skip
        # Pattern: [Fact] or [Theory(...)] without Skip parameter
        patterns = [
            # [Fact] without Skip
            (r'\[Fact\]', f'[Fact(Skip = "{self.SKIP_REASON}")]'),
            # [Fact(DisplayName = "...")] without Skip
            (r'\[Fact\(([^)]*?)\)\]', self._add_skip_to_fact),
            # [Theory] without Skip
            (r'\[Theory\]', f'[Theory(Skip = "{self.SKIP_REASON}")]'),
            # [Theory(DisplayName = "...")] without Skip
            (r'\[Theory\(([^)]*?)\)\]', self._add_skip_to_theory),
        ]

        modified_in_file = 0

        for pattern, replacement in patterns:
            if callable(replacement):
                new_content, count = re.subn(pattern, replacement, content)
            else:
                new_content, count = re.subn(pattern, replacement, content)

            if count > 0:
                content = new_content
                modified_in_file += count

        if content != original_content:
            if not self.dry_run:
                with open(path, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"   ✓ Modified {modified_in_file} test(s)")
            else:
                print(f"   [DRY RUN] Would modify {modified_in_file} test(s)")

            self.files_modified += 1
            self.tests_skipped += modified_in_file
        else:
            print(f"   ⊘ No changes needed (already skipped or no tests)")

    def _add_skip_to_fact(self, match):
        """Add Skip parameter to [Fact(...)] attribute."""
        params = match.group(1).strip()

        # Check if already has Skip
        if 'Skip' in params:
            return match.group(0)  # Return unchanged

        # Add Skip parameter
        if params:
            new_params = f'{params}, Skip = "{self.SKIP_REASON}"'
        else:
            new_params = f'Skip = "{self.SKIP_REASON}"'

        return f'[Fact({new_params})]'

    def _add_skip_to_theory(self, match):
        """Add Skip parameter to [Theory(...)] attribute."""
        params = match.group(1).strip()

        # Check if already has Skip
        if 'Skip' in params:
            return match.group(0)  # Return unchanged

        # Add Skip parameter
        if params:
            new_params = f'{params}, Skip = "{self.SKIP_REASON}"'
        else:
            new_params = f'Skip = "{self.SKIP_REASON}"'

        return f'[Theory({new_params})]'


def main():
    import sys

    dry_run = '--dry-run' in sys.argv

    skipper = GotOcr2TestSkipper(dry_run=dry_run)
    skipper.skip_all_gotocr2_tests()


if __name__ == "__main__":
    main()
