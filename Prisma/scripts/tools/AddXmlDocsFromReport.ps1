param(
    [string]$ReportPath = "errors/CS1591.TXT"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-NameToWords {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    $result = $Value -replace '_', ' '
    $result = [System.Text.RegularExpressions.Regex]::Replace($result, '([a-z0-9])([A-Z])', '$1 $2')
    $result = [System.Text.RegularExpressions.Regex]::Replace($result, '([A-Z]+)([A-Z][a-z])', '$1 $2')
    $result = $result -replace '\s+', ' '
    return $result.Trim()
}

function Convert-NameToLowerPhrase {
    param([string]$Value)

    $words = Convert-NameToWords -Value $Value
    if ([string]::IsNullOrWhiteSpace($words)) {
        return $words
    }

    $segments = $words.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    for ($i = 0; $i -lt $segments.Length; $i++) {
        $segment = $segments[$i]
        if ($segment -cmatch '^[A-Z0-9]+$') {
            continue
        }

        $segments[$i] = $segment.ToLowerInvariant()
    }

    return [string]::Join(' ', $segments)
}

function Capitalize-First {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    $trimmed = $Value.Trim()
    if ($trimmed.Length -eq 0) {
        return $trimmed
    }

    $first = $trimmed.Substring(0, 1).ToUpperInvariant()
    if ($trimmed.Length -eq 1) {
        return $first
    }

    return "$first$($trimmed.Substring(1))"
}

function Normalize-Sentence {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "Provides documentation."
    }

    $trimmed = $Value.Trim()
    if ($trimmed.Length -eq 0) {
        return "Provides documentation."
    }

    if ($trimmed.EndsWith('.')) {
        return $trimmed
    }

    return "$trimmed."
}

function Add-ThirdPersonSuffix {
    param([string]$Word)

    if ([string]::IsNullOrWhiteSpace($Word)) {
        return $Word
    }

    if ($Word -match '^[A-Z0-9]+$') {
        return $Word
    }

    if ($Word.EndsWith('sh') -or $Word.EndsWith('ch') -or $Word.EndsWith('x') -or $Word.EndsWith('z') -or $Word.EndsWith('s')) {
        return "$Word" + 'es'
    }

    if ($Word.EndsWith('y') -and $Word.Length -gt 1) {
        $penultimate = $Word.Substring($Word.Length - 2, 1)
        if ($penultimate -notin @('a', 'e', 'i', 'o', 'u')) {
            return $Word.Substring(0, $Word.Length - 1) + 'ies'
        }
    }

    return "$Word" + 's'
}

function Convert-ToThirdPerson {
    param([string]$Phrase)

    if ([string]::IsNullOrWhiteSpace($Phrase)) {
        return $Phrase
    }

    $words = $Phrase.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($words.Length -eq 0) {
        return $Phrase
    }

    $first = $words[0]
    if ($first -match '^[A-Z0-9]+$') {
        return $Phrase
    }

    switch ($first) {
        'be' { $words[0] = 'is'; break }
        'have' { $words[0] = 'has'; break }
        'do' { $words[0] = 'does'; break }
        'process' { $words[0] = 'processes'; break }
        'deduplicate' { $words[0] = 'deduplicates'; break }
        'respect' { $words[0] = 'respects'; break }
        'return' { $words[0] = 'returns'; break }
        'handle' { $words[0] = 'handles'; break }
        'generate' { $words[0] = 'generates'; break }
        'provide' { $words[0] = 'provides'; break }
        'produce' { $words[0] = 'produces'; break }
        'calculate' { $words[0] = 'calculates'; break }
        default { $words[0] = Add-ThirdPersonSuffix -Word $first }
    }

    return [string]::Join(' ', $words)
}

function Format-WithCondition {
    param([string]$Condition)

    if ([string]::IsNullOrWhiteSpace($Condition)) {
        return $Condition
    }

    $trimmed = $Condition.Trim()
    if ($trimmed.Length -eq 0) {
        return $trimmed
    }

    $lower = $trimmed.ToLowerInvariant()
    $prefixes = @(
        'a ', 'an ', 'the ', 'any ', 'no ', 'each ', 'every ', 'this ', 'that ', 'these ', 'those ', 'multiple ',
        'several ', 'many ', 'few ', 'all ', 'existing ', 'current ', 'duplicate ', 'additional ', 'various ',
        'other '
    )

    foreach ($prefix in $prefixes) {
        if ($lower.StartsWith($prefix)) {
            return $trimmed
        }
    }

    $words = $trimmed.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($words.Length -eq 0) {
        return $trimmed
    }

    $first = $words[0]
    if ($first -match '^[A-Z0-9]+$') {
        return $trimmed
    }

    if ($first.EndsWith('s')) {
        return $trimmed
    }

    $firstChar = $first.Substring(0, 1).ToLowerInvariant()
    $article = if (@('a', 'e', 'i', 'o', 'u') -contains $firstChar) { 'an' } else { 'a' }
    return "$article $trimmed"
}

function Build-TestMethodSummary {
    param(
        [string]$Name
    )

    $parts = $Name -split '_'
    if ($parts.Length -ge 3) {
        $subject = Convert-NameToWords -Value $parts[0]
        $conditionRaw = [string]::Join(' ', $parts[1..($parts.Length - 2)])
        $condition = Convert-NameToLowerPhrase -Value $conditionRaw
        $outcomeRaw = $parts[-1] -replace '^Should', ''
        $outcome = Convert-NameToLowerPhrase -Value $outcomeRaw
        $outcome = Convert-ToThirdPerson -Phrase $outcome

        $conditionClause = ''
        if (-not [string]::IsNullOrWhiteSpace($condition)) {
            $normalizedCondition = $condition
            $fromWith = $false
            if ($normalizedCondition.StartsWith('with ')) {
                $normalizedCondition = $normalizedCondition.Substring(5)
                $fromWith = $true
            }

            if ($fromWith) {
                $conditionClause = " when provided with $(Format-WithCondition $normalizedCondition)"
            }
            else {
                $conditionClause = " when $normalizedCondition"
            }
        }

        $sentence = "Verifies that $subject $outcome$conditionClause"
        return Normalize-Sentence $sentence
    }

    $phrase = Convert-NameToWords -Value $Name
    return Normalize-Sentence "Verifies the $phrase scenario"
}

function Build-ConstructorSummary {
    param(
        [string]$Name,
        [bool]$IsTestFile
    )

    $phrase = Convert-NameToWords -Value $Name
    if ($IsTestFile) {
        return Normalize-Sentence "Initializes the $phrase test fixture"
    }

    return Normalize-Sentence "Initializes a new instance of the $phrase class"
}

function Build-GeneralMethodSummary {
    param(
        [string]$Name,
        [bool]$IsAsync,
        [string]$Signature
    )

    $phrase = Convert-NameToWords -Value $Name
    $suffix = if ($IsAsync) { " asynchronously" } else { "" }
    $sentence = "Executes the $phrase operation$suffix"
    return Normalize-Sentence (Capitalize-First $sentence)
}

function Build-TypeSummary {
    param(
        [string]$Name,
        [string]$Kind,
        [bool]$IsTestFile
    )

    if ($IsTestFile) {
        $target = $Name -replace '(Tests|Test)$'
        $targetPhrase = Convert-NameToWords -Value $target
        if ([string]::IsNullOrWhiteSpace($targetPhrase)) {
            $targetPhrase = Convert-NameToWords -Value $Name
        }

        return Normalize-Sentence "Provides unit tests for the $targetPhrase"
    }

    $phrase = Convert-NameToWords -Value $Name
    switch ($Kind) {
        'interface' { return Normalize-Sentence "Defines the $phrase contract" }
        'enum' { return Normalize-Sentence "Specifies the $phrase enumeration" }
        default { return Normalize-Sentence "Represents the $phrase" }
    }
}

function Build-PropertySummary {
    param(
        [string]$Name,
        [string]$Signature,
        [bool]$IsTestFile
    )

    $phrase = Convert-NameToLowerPhrase -Value $Name
    $sentence = ''

    if ($Signature -match '\bget;\s*set;') {
        $sentence = "Gets or sets the $phrase"
    }
    elseif ($Signature -match '\bget;') {
        $sentence = "Gets the $phrase"
    }
    elseif ($Signature -match '\bset;') {
        $sentence = "Sets the $phrase"
    }
    elseif ($Signature -match '=>') {
        $sentence = "Gets the $phrase value"
    }
    else {
        $sentence = "Represents the $phrase"
    }

    if ($IsTestFile -and $sentence -notmatch 'for testing$') {
        $sentence = "$sentence for testing"
    }

    return Normalize-Sentence (Capitalize-First $sentence)
}

function Get-MemberInfo {
    param(
        [string[]]$Lines,
        [int]$LineIndex
    )

    $builder = New-Object System.Text.StringBuilder
    for ($i = $LineIndex; $i -lt $Lines.Length; $i++) {
        $current = $Lines[$i].Trim()
        if ($current.Length -eq 0) {
            continue
        }

        [void]$builder.Append($current).Append(' ')

        if ($current.Contains('{') -or $current.Contains(';') -or $current.Contains('=')) {
            break
        }

        if ($current.Contains(')')) {
            break
        }
    }

    $signature = $builder.ToString().Trim()

    $lowerSignature = $signature.ToLowerInvariant()

    if ($lowerSignature -match '\b(class|record|interface|struct|enum)\b') {
        $match = [System.Text.RegularExpressions.Regex]::Match($signature, '\b(class|record|interface|struct|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)')
        if ($match.Success) {
            return [PSCustomObject]@{
                Kind      = $match.Groups[1].Value.ToLowerInvariant()
                Name      = $match.Groups['name'].Value
                Signature = $signature
                IsConstructor = $false
            }
        }
    }

    if ($signature.Contains('(')) {
        $matches = [System.Text.RegularExpressions.Regex]::Matches($signature, '([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>]*>)?\s*\(')
        if ($matches.Count -gt 0) {
            $name = $matches[$matches.Count - 1].Groups[1].Value
            $constructorPattern = "^\s*(?:public|protected|internal)(?:\s+(?:protected|internal))?\s+" + [System.Text.RegularExpressions.Regex]::Escape($name) + '\s*\('
            $isConstructor = [System.Text.RegularExpressions.Regex]::IsMatch($signature, $constructorPattern)
            return [PSCustomObject]@{
                Kind      = 'method'
                Name      = $name
                Signature = $signature
                IsConstructor = $isConstructor
            }
        }
    }

    $enumMatch = [System.Text.RegularExpressions.Regex]::Match($Lines[$LineIndex], '^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*=\s*[^,]+)?\s*,?\s*$')
    if ($enumMatch.Success) {
        return [PSCustomObject]@{
            Kind = 'enumMember'
            Name = $enumMatch.Groups['name'].Value
            Signature = $Lines[$LineIndex].Trim()
            IsConstructor = $false
        }
    }

    $propertyMatch = [System.Text.RegularExpressions.Regex]::Matches($signature, '\b([A-Za-z_][A-Za-z0-9_]*)\b')
    if ($propertyMatch.Count -gt 0) {
        for ($i = $propertyMatch.Count - 1; $i -ge 0; $i--) {
            $candidate = $propertyMatch[$i].Value
            if ($candidate -in @('get', 'set', 'add', 'remove', 'init', 'where', 'class', 'struct', 'enum', 'interface', 'record', 'return')) {
                continue
            }

            if ($candidate -match '^[A-Z_]') {
                return [PSCustomObject]@{
                    Kind      = 'property'
                    Name      = $candidate
                    Signature = $signature
                    IsConstructor = $false
                }
            }
        }
    }

    return [PSCustomObject]@{
        Kind      = 'member'
        Name      = ''
        Signature = $signature
        IsConstructor = $false
    }
}

function Resolve-DeclarationIndex {
    param(
        [string[]]$Lines,
        [string]$Symbol,
        [int]$FallbackIndex
    )

    $normalizedIndex = [Math]::Max([Math]::Min($FallbackIndex, $Lines.Length - 1), 0)

    if ([string]::IsNullOrWhiteSpace($Symbol)) {
        return $normalizedIndex
    }

    $symbolParts = $Symbol.Split('.', [System.StringSplitOptions]::RemoveEmptyEntries)
    $memberPart = if ($symbolParts.Length -gt 0) { $symbolParts[$symbolParts.Length - 1] } else { $Symbol }

    $pattern = ''
    if ($memberPart.EndsWith('()')) {
        $methodName = $memberPart.Substring(0, $memberPart.Length - 2)
        if ($methodName.Length -eq 0) {
            return $normalizedIndex
        }

        $pattern = "\b$([System.Text.RegularExpressions.Regex]::Escape($methodName))\s*\("
    }
    else {
        $pattern = "\b$([System.Text.RegularExpressions.Regex]::Escape($memberPart))\b"
    }

    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $current = $Lines[$i]
        if ([System.Text.RegularExpressions.Regex]::IsMatch($current, $pattern)) {
            return $i
        }
    }

    return $normalizedIndex
}

$fullReportPath = if ([System.IO.Path]::IsPathRooted($ReportPath)) {
    $ReportPath
} else {
    Join-Path -Path (Get-Location) -ChildPath $ReportPath
}

if (-not (Test-Path -LiteralPath $fullReportPath)) {
    throw "Report file not found at '$ReportPath'."
}

$reportData = Import-Csv -Delimiter "`t" -Path $fullReportPath
$entries = $reportData | Where-Object {
    $_.Code -eq 'CS1591' -and ($_.Description -like 'Missing XML comment*' -or $_.Description -like 'The /doc compiler option was specified*')
}

if ($null -eq $entries -or ($entries -is [array] -and $entries.Count -eq 0) -or ($entries -isnot [array] -and $null -eq $entries)) {
    Write-Host "No CS1591 entries found in $ReportPath."
    return
}

$grouped = $entries | Group-Object -Property File

foreach ($group in $grouped) {
    $relativePath = $group.Name
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        continue
    }

    $targetPath = if ([System.IO.Path]::IsPathRooted($relativePath)) {
        $relativePath
    } else {
        Join-Path -Path (Get-Location) -ChildPath $relativePath
    }

    if (-not (Test-Path -LiteralPath $targetPath)) {
        Write-Warning "File '$relativePath' not found."
        continue
    }

    $existingLines = [System.IO.File]::ReadAllLines($targetPath)
    $lineList = New-Object System.Collections.Generic.List[string]
    $lineList.AddRange($existingLines)
    $processedIndices = New-Object 'System.Collections.Generic.HashSet[int]'
    $processedSymbols = New-Object 'System.Collections.Generic.HashSet[string]'
    $fileModified = $false

    $groupEntries = $group.Group | Sort-Object -Property { 
        $value = 0
        if ([int]::TryParse($_.Line, [ref]$value)) { $value } else { 0 }
    } -Descending

    $isTestFile = $relativePath -match '\\Tests\\' -or $relativePath -match '\.Tests'

    foreach ($entry in $groupEntries) {
        $lineNumber = 0
        if (-not [int]::TryParse($entry.Line, [ref]$lineNumber)) {
            $lineNumber = 0
        }

        $initialIndex = $lineNumber - 1
        $initialIndex = [Math]::Max([Math]::Min($initialIndex, $lineList.Count - 1), 0)

        $symbolMatch = [System.Text.RegularExpressions.Regex]::Match($entry.Description, "'(?<symbol>[^']+)'")
        $symbol = if ($symbolMatch.Success) { $symbolMatch.Groups['symbol'].Value } else { '' }

        if (-not [string]::IsNullOrWhiteSpace($symbol) -and $processedSymbols.Contains($symbol)) {
            continue
        }

        $currentLines = $lineList.ToArray()
        $index = Resolve-DeclarationIndex -Lines $currentLines -Symbol $symbol -FallbackIndex $initialIndex

        while ($index -lt $lineList.Count -and $lineList[$index].Trim().StartsWith('///')) {
            $index++
        }

        while ($index -lt $lineList.Count -and $lineList[$index].Trim().Length -eq 0) {
            $index++
        }

        while ($index -lt $lineList.Count -and $lineList[$index].Trim().StartsWith('///')) {
            $index++
        }

        if ($index -ge $lineList.Count) {
            continue
        }

        while ($index -lt $lineList.Count -and $lineList[$index].Trim().StartsWith('[')) {
            $index++
        }

        $attributeIndex = $index
        while ($attributeIndex -gt 0 -and $lineList[$attributeIndex - 1].Trim().StartsWith('[')) {
            $attributeIndex--
        }

        $hasExistingComment = $false
        $commentIndex = $attributeIndex - 1
        while ($commentIndex -ge 0) {
            $existingLine = $lineList[$commentIndex].Trim()
            if ($existingLine.Length -eq 0) {
                $commentIndex--
                continue
            }

            if ($existingLine.StartsWith('///')) {
                $hasExistingComment = $true
            }

            break
        }

        if ($hasExistingComment) {
            continue
        }

        if ($processedIndices.Contains($index)) {
            continue
        }

        [void]$processedIndices.Add($index)
        if (-not [string]::IsNullOrWhiteSpace($symbol)) {
            [void]$processedSymbols.Add($symbol)
        }

        $insertionIndex = $attributeIndex
        $currentLines = $lineList.ToArray()
        $memberInfo = Get-MemberInfo -Lines $currentLines -LineIndex $index

        $indentSourceIndex = if ($attributeIndex -lt $lineList.Count) { $attributeIndex } else { $index }
        $indentMatch = [System.Text.RegularExpressions.Regex]::Match($lineList[$indentSourceIndex], '^\s*')
        $indent = $indentMatch.Value

        $summary = switch ($memberInfo.Kind) {
            'method' {
                $isAsync = $memberInfo.Signature -match '\basync\b' -or $memberInfo.Name.EndsWith('Async')
                if ($memberInfo.IsConstructor) {
                    Build-ConstructorSummary -Name $memberInfo.Name -IsTestFile:$isTestFile
                }
                elseif ($isTestFile) {
                    Build-TestMethodSummary -Name $memberInfo.Name
                }
                else {
                    Build-GeneralMethodSummary -Name $memberInfo.Name -IsAsync:$isAsync -Signature $memberInfo.Signature
                }
            }
            'property' { Build-PropertySummary -Name $memberInfo.Name -Signature $memberInfo.Signature -IsTestFile:$isTestFile }
            'class' { Build-TypeSummary -Name $memberInfo.Name -Kind 'class' -IsTestFile:$isTestFile }
            'record' { Build-TypeSummary -Name $memberInfo.Name -Kind 'record' -IsTestFile:$isTestFile }
            'interface' { Build-TypeSummary -Name $memberInfo.Name -Kind 'interface' -IsTestFile:$isTestFile }
            'struct' { Build-TypeSummary -Name $memberInfo.Name -Kind 'struct' -IsTestFile:$isTestFile }
            'enum' { Build-TypeSummary -Name $memberInfo.Name -Kind 'enum' -IsTestFile:$isTestFile }
            'enumMember' { Normalize-Sentence "Represents the $($memberInfo.Name) option" }
            default { Normalize-Sentence "Provides documentation for $($memberInfo.Name)" }
        }

        $docBlock = [string[]]@(
            "$indent/// <summary>",
            "$indent/// $summary",
            "$indent/// </summary>"
        )

        $lineList.InsertRange($insertionIndex, $docBlock)
        $fileModified = $true
    }

    if (-not $fileModified) {
        continue
    }

    $cleanLines = New-Object System.Collections.Generic.List[string]
    $i = 0
    while ($i -lt $lineList.Count) {
        $trimmed = $lineList[$i].Trim()
        if ($trimmed.StartsWith('/// <summary>') -and ($i + 2) -lt $lineList.Count -and $lineList[$i + 2].Trim().StartsWith('/// </summary>')) {
            $summaryLine = $lineList[$i + 1]
            $cleanLines.Add($lineList[$i])
            $cleanLines.Add($summaryLine)
            $cleanLines.Add($lineList[$i + 2])

            $j = $i + 3
            while (($j + 2) -lt $lineList.Count) {
                $nextSummary = $lineList[$j].Trim()
                if ($nextSummary.StartsWith('/// <summary>') -and
                    $lineList[$j + 1].Trim() -eq $summaryLine.Trim() -and
                    $lineList[$j + 2].Trim().StartsWith('/// </summary>')) {
                    $j += 3
                    continue
                }

                break
            }

            $i = $j
            continue
        }

        $cleanLines.Add($lineList[$i])
        $i++
    }

    $finalLines = New-Object System.Collections.Generic.List[string]
    $indexFinal = 0
    while ($indexFinal -lt $cleanLines.Count) {
        $current = $cleanLines[$indexFinal]
        $trimmedCurrent = $current.Trim()

        if ($trimmedCurrent.StartsWith('///')) {
            $blockStart = $indexFinal
            while ($indexFinal -lt $cleanLines.Count -and $cleanLines[$indexFinal].Trim().StartsWith('///')) {
                $indexFinal++
            }

            $blockEnd = $indexFinal - 1
            $lookAhead = $indexFinal
            while ($lookAhead -lt $cleanLines.Count -and $cleanLines[$lookAhead].Trim().Length -eq 0) {
                $lookAhead++
            }

            $keepBlock = $true
            if ($lookAhead -lt $cleanLines.Count) {
                $nextTrimmed = $cleanLines[$lookAhead].TrimStart()
                if ($nextTrimmed -notmatch '^(?:\[|public\b|internal\b|protected\b|private\b|sealed\b|static\b|partial\b|class\b|record\b|struct\b|interface\b|enum\b|async\b|extern\b|unsafe\b|[A-Za-z_][A-Za-z0-9_]*(?:\s*=\s*[^,]+)?\s*,?)') {
                    $keepBlock = $false
                }
            }

            if ($keepBlock) {
                for ($k = $blockStart; $k -le $blockEnd; $k++) {
                    $finalLines.Add($cleanLines[$k])
                }
            }

            while ($indexFinal -lt $cleanLines.Count -and $cleanLines[$indexFinal].Trim().Length -eq 0) {
                $finalLines.Add($cleanLines[$indexFinal])
                $indexFinal++
            }

            continue
        }

        $finalLines.Add($current)
        $indexFinal++
    }

    $finalArray = $finalLines.ToArray()
    if ($existingLines.Length -eq $finalArray.Length) {
        $isDifferent = $false
        for ($m = 0; $m -lt $existingLines.Length; $m++) {
            if ($existingLines[$m] -cne $finalArray[$m]) {
                $isDifferent = $true
                break
            }
        }

        if (-not $isDifferent) {
            continue
        }
    }

    [System.IO.File]::WriteAllLines($targetPath, $finalArray)
    Write-Host "Updated '$relativePath'"
}
