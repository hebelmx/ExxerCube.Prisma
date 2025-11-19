# Search for Python files related to Ollama with prompts, personality, or generation instructions
# Searches for files with keywords: generate, dummies/dumys, document, statements, requerimientos, english, spanish

$searchPath = "F:\Dynamic"
$keywords = @("generate", "dumm", "document", "statement", "requerimiento", "english", "spanish", "español", "personality", "prompt", "instruction")

Write-Host "Searching for Python files containing 'ollama' with related keywords..." -ForegroundColor Cyan
Write-Host ""

# Option 1: Search files with ollama in content AND keywords in filename
Write-Host "=== Files with 'ollama' in content AND keywords in filename ===" -ForegroundColor Yellow
Get-ChildItem -Path $searchPath -Filter "*.py" -Recurse -ErrorAction SilentlyContinue | 
    Where-Object { 
        $name = $_.Name.ToLower()
        $hasKeyword = $keywords | Where-Object { $name -like "*$_*" }
        if ($hasKeyword) {
            $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
            $content -and $content -match "ollama"
        }
    } | 
    Select-Object FullName, @{Name="Keywords";Expression={($keywords | Where-Object { $_.Name.ToLower() -like "*$_*" }) -join ", "}} |
    Format-Table -AutoSize

Write-Host ""

# Option 2: Search files with ollama AND keywords in content (prompts/personality)
Write-Host "=== Files with 'ollama' AND keywords in content (prompts/personality) ===" -ForegroundColor Yellow
Get-ChildItem -Path $searchPath -Filter "*.py" -Recurse -ErrorAction SilentlyContinue | 
    Select-String -Pattern "ollama" -List | 
    Where-Object {
        $fileContent = Get-Content $_.Path -Raw -ErrorAction SilentlyContinue
        if ($fileContent) {
            $contentLower = $fileContent.ToLower()
            $keywords | Where-Object { $contentLower -match $_ }
        }
    } |
    Select-Object -ExpandProperty Path -Unique |
    ForEach-Object {
        $file = Get-Item $_
        $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        $foundKeywords = $keywords | Where-Object { $content.ToLower() -match $_ }
        
        [PSCustomObject]@{
            File = $file.FullName
            Keywords = ($foundKeywords -join ", ")
            Lines = (Select-String -Path $_.FullName -Pattern "ollama" -Context 2,2 | Select-Object -First 3)
        }
    } |
    Format-List

Write-Host ""

# Option 3: Search for specific patterns (personality, prompt, instruction with ollama)
Write-Host "=== Files with 'ollama' AND prompt/personality/instruction patterns ===" -ForegroundColor Yellow
$promptPatterns = @(
    "personality.*ollama|ollama.*personality",
    "prompt.*ollama|ollama.*prompt",
    "instruction.*ollama|ollama.*instruction",
    "system.*ollama|ollama.*system",
    "role.*ollama|ollama.*role"
)

Get-ChildItem -Path $searchPath -Filter "*.py" -Recurse -ErrorAction SilentlyContinue | 
    Select-String -Pattern "ollama" -List | 
    Where-Object {
        $fileContent = Get-Content $_.Path -Raw -ErrorAction SilentlyContinue
        if ($fileContent) {
            $promptPatterns | Where-Object { $fileContent -match $_ }
        }
    } |
    Select-Object -ExpandProperty Path -Unique |
    ForEach-Object {
        Write-Host "`nFile: $_" -ForegroundColor Green
        Write-Host "Matching lines:" -ForegroundColor Gray
        Select-String -Path $_ -Pattern "ollama" -Context 3,3 | 
            Select-Object -First 5 | 
            ForEach-Object {
                Write-Host "  Line $($_.LineNumber): $($_.Line.Trim())" -ForegroundColor White
            }
    }

Write-Host ""

# Option 4: Comprehensive search - all Python files with ollama
Write-Host "=== All Python files containing 'ollama' ===" -ForegroundColor Yellow
Get-ChildItem -Path $searchPath -Filter "*.py" -Recurse -ErrorAction SilentlyContinue | 
    Select-String -Pattern "ollama" -List | 
    Select-Object -ExpandProperty Path -Unique |
    Sort-Object |
    ForEach-Object {
        $file = Get-Item $_
        $lineCount = (Select-String -Path $_.FullName -Pattern "ollama" | Measure-Object).Count
        Write-Host "$($file.FullName) ($lineCount matches)" -ForegroundColor Cyan
    }

