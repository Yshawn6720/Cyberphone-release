param(
    [Parameter(Mandatory = $true)] [string] $ScowlDir,
    [Parameter(Mandatory = $true)] [string] $FrequencyDir,
    [string] $OutDir = "",
    [int] $AnswerCount = 1500
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrEmpty($OutDir)) {
    $OutDir = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\src\Aetherphone\Words"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$stoplist = @(
    "bitch", "cunts", "fucks", "fucky", "niggr", "pussy", "shits", "twats", "wanks", "whore", "cocks", "dicks", "pricks", "slut", "sluts",
    "nazis", "rapes", "raped", "penis", "vulva", "fecal", "semen", "anals", "boobs", "tits"
)

function Normalize-Word {
    param([string] $word)
    $lower = $word.ToLowerInvariant()
    $decomposed = $lower.Normalize([Text.NormalizationForm]::FormD)
    $builder = New-Object System.Text.StringBuilder
    foreach ($character in $decomposed.ToCharArray()) {
        $category = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($character)
        if ($category -eq [Globalization.UnicodeCategory]::NonSpacingMark) { continue }
        [void] $builder.Append($character)
    }
    $ascii = $builder.ToString()
    if ($ascii -match '^[a-z]{5}$') { return $ascii.ToUpperInvariant() }
    return $null
}

function Write-Bank {
    param([string] $language, [string[]] $answers, [string[]] $valid)
    $answerSet = [System.Collections.Generic.HashSet[string]]::new([string[]] $answers)
    $validSet = [System.Collections.Generic.HashSet[string]]::new([string[]] $valid)
    foreach ($answer in $answers) { [void] $validSet.Add($answer) }
    foreach ($bad in $stoplist) { [void] $answerSet.Remove($bad.ToUpperInvariant()); [void] $validSet.Remove($bad.ToUpperInvariant()) }
    $sortedAnswers = @($answerSet | Sort-Object)
    $sortedValid = @($validSet | Sort-Object)
    [IO.File]::WriteAllLines((Join-Path $OutDir "$language.answers.txt"), $sortedAnswers)
    [IO.File]::WriteAllLines((Join-Path $OutDir "$language.valid.txt"), $sortedValid)
    Write-Host ("{0}: {1} answers, {2} valid" -f $language, $sortedAnswers.Count, $sortedValid.Count)
}

function Read-Scowl {
    param([string] $prefix, [int[]] $sizes)
    $words = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($size in $sizes) {
        foreach ($name in @("english-words.$size", "american-words.$size")) {
            $path = Join-Path $ScowlDir "final\$name"
            if (-not (Test-Path $path)) { continue }
            foreach ($line in [IO.File]::ReadLines($path, [Text.Encoding]::GetEncoding("iso-8859-1"))) {
                $normalized = Normalize-Word $line.Trim()
                if ($null -ne $normalized) { [void] $words.Add($normalized) }
            }
        }
    }
    return $words
}

$properNames = [System.Collections.Generic.HashSet[string]]::new()
$nameLists = Get-ChildItem (Join-Path $ScowlDir "final") | Where-Object { $_.Name -like "*-proper-names.*" -or $_.Name -like "*-upper.*" }
foreach ($path in $nameLists) {
    foreach ($line in [IO.File]::ReadLines($path.FullName, [Text.Encoding]::GetEncoding("iso-8859-1"))) {
        $normalized = Normalize-Word $line.Trim()
        if ($null -ne $normalized) { [void] $properNames.Add($normalized) }
    }
}

$rootWords = [System.Collections.Generic.HashSet[string]]::new()
foreach ($size in @(10, 20, 35, 50)) {
    foreach ($name in @("english-words.$size", "american-words.$size")) {
        $path = Join-Path $ScowlDir "final\$name"
        if (-not (Test-Path $path)) { continue }
        foreach ($line in [IO.File]::ReadLines($path, [Text.Encoding]::GetEncoding("iso-8859-1"))) {
            $trimmed = $line.Trim().ToUpperInvariant()
            if ($trimmed -match '^[A-Z]{4}$') { [void] $rootWords.Add($trimmed) }
        }
    }
}

$englishAnswers = @()
foreach ($word in (Read-Scowl "english" @(10, 20))) {
    if ($word.EndsWith("S") -and $rootWords.Contains($word.Substring(0, 4))) { continue }
    $englishAnswers += $word
}
$englishValid = @(Read-Scowl "english" @(10, 20, 35, 40, 50, 55, 60, 70))
Write-Bank "en" $englishAnswers $englishValid

foreach ($language in @("de", "es", "fr", "pt")) {
    $path = Join-Path $FrequencyDir "$($language)_50k.txt"
    $answers = New-Object System.Collections.Generic.List[string]
    $valid = New-Object System.Collections.Generic.List[string]
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($line in [IO.File]::ReadLines($path, [Text.Encoding]::UTF8)) {
        $parts = $line.Split(" ")
        if ($parts.Length -lt 1) { continue }
        $raw = $parts[0]
        if ($language -eq "de" -and $raw -match '[äöüß]') { continue }
        $normalized = Normalize-Word $raw
        if ($null -eq $normalized) { continue }
        if (-not $seen.Add($normalized)) { continue }
        $valid.Add($normalized)
        if ($properNames.Contains($normalized)) { continue }
        if ($answers.Count -lt $AnswerCount) { $answers.Add($normalized) }
    }
    Write-Bank $language $answers.ToArray() $valid.ToArray()
}

Copy-Item (Join-Path $ScowlDir "Copyright") (Join-Path $OutDir "SCOWL-Copyright.txt") -Force
