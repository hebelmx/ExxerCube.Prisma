# Quick Reference - Dependency Analysis Scripts

## Quick Commands

### 1. Analyze Errors
```bash
cd F:/Dynamic/ExxerAi/ExxerAI/scripts
python analyze_missing_dependencies.py
```

### 2. Fix Dependencies (Dry Run)
```bash
python fix_dependencies_smart.py --dry-run
```

### 3. Fix Dependencies (Apply)
```bash
python fix_dependencies_smart.py --apply
```

## Add New Type Mappings

Edit `analyze_missing_dependencies.py` and add to `known_type_namespaces`:

```python
self.known_type_namespaces = {
    # Add your type here
    'YourType': 'Namespace.For.YourType',
}

# And add package mapping if needed
self.namespace_to_package = {
    'Namespace.For.YourType': 'NuGetPackageName',
}
```

## Error File Format

The CS0246.txt file should be tab-separated with columns:
```
Severity	Code	Description	Project	File	Line	Suppression State	Details
```

## Common Issues

1. **Script times out**: Use the fast analyzer or limit errors
2. **Can't find project**: Check project mappings in `_find_project_path()`
3. **Package not in Directory.Packages.props**: Add it there first

## File Locations

- Error file: `F:\Dynamic\ExxerAi\ExxerAI\Errors\CS0246.txt`
- Analysis output: `smart_dependency_analysis.json`
- Backups: `scripts\smart_fix_backups\`