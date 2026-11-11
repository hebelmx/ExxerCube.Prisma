# Using Statement Collector for ExxerAI

This tool collects all using statements from Infrastructure projects and generates GlobalUsing.cs files for test projects.

## Features

- Collects all `using` and `global using` statements from Infrastructure projects
- Saves collected statements to JSON for review
- Automatically deduplicates using statements
- Reads Directory.Build.props to avoid duplicating existing global usings
- Generates well-organized GlobalUsing.cs files
- Supports dry-run mode for safety
- Backs up existing GlobalUsing.cs files before overwriting

## Usage

### Windows (using batch script)
```bash
run_collect_usings.bat
```

### Direct Python usage
```bash
# Run all steps with dry run (recommended first)
python collect_using_statements.py --step all --dry-run

# Run all steps (actual file modifications)
python collect_using_statements.py --step all

# Run individual steps
python collect_using_statements.py --step collect
python collect_using_statements.py --step generate --dry-run
python collect_using_statements.py --step deploy --dry-run
```

### Command-line options
- `--base-path`: Base path of ExxerAI project (default: F:/Dynamic/ExxerAi/ExxerAI)
- `--json-output`: Output JSON file name (default: using_statements.json)
- `--step`: Which step to run (collect/generate/deploy/all)
- `--dry-run`: Run without modifying files
- `--no-exclude-props`: Include usings that are already in Directory.Build.props

## Process Flow

1. **Collect**: Scans all .cs files in Infrastructure projects for using statements
2. **Save to JSON**: Stores all unique using statements in a JSON file
3. **Generate**: Creates GlobalUsing.cs content, excluding duplicates from Directory.Build.props
4. **Deploy**: Copies GlobalUsing.cs to all test projects (with backup if file exists)

## Output Structure

The generated GlobalUsing.cs organizes usings into categories:
- System namespaces
- Microsoft namespaces
- ExxerAI namespaces
- Third-party namespaces

## Safety Features

- Dry-run mode shows what would happen without making changes
- Automatic backup of existing GlobalUsing.cs files
- Excludes generated files (obj/, bin/, .g.cs, .Designer.cs)
- Respects existing Directory.Build.props configuration