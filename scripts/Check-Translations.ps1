# Checks every Translations\*.txt file against the English string table in Loc.cs:
# missing keys, keys that no longer exist, and {0}-style placeholder mismatches.
# Exits 1 when any file has a problem.
$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $PSScriptRoot

$locSource = Get-Content (Join-Path $projectDir "Loc.cs") -Raw -Encoding utf8
$english = @{}
foreach ($match in [regex]::Matches($locSource, 'Add\("([a-z0-9_]+)",\s*((?:"(?:[^"\\]|\\.)*"\s*\+?\s*)+)\);')) {
    $english[$match.Groups[1].Value] = $match.Groups[2].Value
}

function Get-Placeholders([string]$text) {
    ([regex]::Matches($text, '\{\d+\}') | ForEach-Object { $_.Value } | Sort-Object -Unique) -join ","
}

$failed = $false
foreach ($file in Get-ChildItem (Join-Path $projectDir "Translations") -Filter "*.txt") {
    $problems = @()
    $seen = @{}
    foreach ($line in Get-Content $file.FullName -Encoding utf8) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#")) { continue }
        $split = $trimmed.IndexOf("=")
        if ($split -le 0) { $problems += "malformed line: $trimmed"; continue }
        $key = $trimmed.Substring(0, $split).Trim()
        $value = $trimmed.Substring($split + 1).Trim()
        $seen[$key] = $true
        if (-not $english.ContainsKey($key)) { $problems += "unknown key: $key"; continue }
        $expected = Get-Placeholders $english[$key]
        $actual = Get-Placeholders $value
        if ($expected -ne $actual) { $problems += "placeholders differ for ${key}: English {$expected}, here {$actual}" }
    }
    foreach ($key in $english.Keys) {
        if (-not $seen.ContainsKey($key)) { $problems += "missing key (falls back to English): $key" }
    }

    if ($problems.Count -eq 0) {
        Write-Host "$($file.Name): OK ($($seen.Count) keys)"
    } else {
        $failed = $true
        Write-Host "$($file.Name):"
        $problems | ForEach-Object { Write-Host "  $_" }
    }
}

if ($failed) { exit 1 }
exit 0
