# Search for Python files with "ollama" in the name or content

# Option 1: Search in file content (recommended)
Get-ChildItem -Path "F:\Dynamic" -Filter "*.py" -Recurse -ErrorAction SilentlyContinue | 
    Select-String -Pattern "ollama" -List | 
    Select-Object -ExpandProperty Path -Unique

# Option 2: Search in file names only
Get-ChildItem -Path "F:\Dynamic" -Filter "*ollama*.py" -Recurse -ErrorAction SilentlyContinue | 
    Select-Object -ExpandProperty FullName

# Option 3: Search in both file names and content (most comprehensive)
Get-ChildItem -Path "F:\Dynamic" -Filter "*.py" -Recurse -ErrorAction SilentlyContinue | 
    Where-Object { 
        $_.Name -like "*ollama*" -or 
        (Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue) -match "ollama"
    } | 
    Select-Object -ExpandProperty FullName

