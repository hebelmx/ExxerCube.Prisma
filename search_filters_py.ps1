
# Search for Python files with "ollama" in the name or content# Option 1: Search in file content (recommended)
Get-ChildItem -Path "F:\Dynamic" -Filter "*.py" -Recurse -ErrorAction SilentlyContinue |     Select-String -Pattern "ollama" -List |     Select-Object -ExpandProperty Path -Unique


Get-ChildItem -Path "F:\Dynamic" -Filter "*.py" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*ollama*" -or (Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue) -match "ollama" } | Select-Object FullName

   # Or search for files with "generate" and "dummy" in name
  Get-ChildItem -Path "F:\Dynamic" -Filter "*dummy*" -Recurse -ErrorAction SilentlyContinue | Where-Object {
  $_.Extension -eq ".py" }
