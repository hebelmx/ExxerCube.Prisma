#!/usr/bin/env python3
"""
Fix XUnit v3 duplicate package references causing CS0101 errors.
Removes xunit.v3 meta-package when xunit.v3.core is also present.
"""
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
TESTS_ROOT = REPO_ROOT / "code" / "src" / "tests"

def fix_project_file(proj_file: Path) -> bool:
    """Fix XUnit duplicate references in a single project file."""
    content = proj_file.read_text(encoding="utf-8")

    # Check if file has both xunit.v3 and xunit.v3.core
    has_meta = '<PackageReference Include="xunit.v3"' in content
    has_core = '<PackageReference Include="xunit.v3.core"' in content

    if not (has_meta and has_core):
        return False  # No duplicate

    # Remove the xunit.v3 meta-package line
    pattern = r'\s*<PackageReference Include="xunit\.v3" />\s*\n'
    new_content = re.sub(pattern, '', content)

    if new_content == content:
        return False  # Nothing changed

    # Add comment if not present
    if "avoid duplicate source generators" not in new_content:
        new_content = new_content.replace(
            '<ItemGroup Label="xUnit v3">',
            '<ItemGroup Label="xUnit v3">\n\t\t<!-- Using xunit.v3.core + individual runners (not meta-package xunit.v3) to avoid duplicate source generators -->'
        )

    proj_file.write_text(new_content, encoding="utf-8")
    return True

def main():
    fixed_count = 0

    for proj_file in TESTS_ROOT.rglob("*.csproj"):
        try:
            if fix_project_file(proj_file):
                print(f"✅ Fixed: {proj_file.relative_to(REPO_ROOT)}")
                fixed_count += 1
        except Exception as e:
            print(f"❌ Error fixing {proj_file.name}: {e}")

    print(f"\n🎉 Fixed {fixed_count} project files")

if __name__ == "__main__":
    main()
