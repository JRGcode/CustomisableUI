<#
.SYNOPSIS
    Checks the translations in Module/ModuleData/Languages against the English template.

.DESCRIPTION
    Needs no game install and no .NET SDK: just PowerShell (Windows PowerShell 5.1 or PowerShell 7).

    For every language folder next to EN it checks that language_data.xml and the strings file are well-formed, that every
    string id exists in EN/strings.xml (a typo would otherwise be silently ignored by the game), that nothing is blank or
    duplicated, and that no text contains { or } (the game reads those as variables). It also reports how much of the
    template each language covers. Strings a language doesn't have are shown in English, so partial translations are fine.

    Exits with 1 if anything is wrong, 0 otherwise.

.EXAMPLE
    ./tools/check-translations.ps1
    ./tools/check-translations.ps1 -Language DE
#>
param(
    # Only check this language folder (for example DE). Default: all of them.
    [string]$Language
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$languages = [IO.Path]::Combine($root, 'Module', 'ModuleData', 'Languages')
$errors = 0
$warnings = 0

function Fail([string]$where, [string]$message) {
    $script:errors++
    Write-Host ("  ERROR  {0}: {1}" -f $where, $message) -ForegroundColor Red
}

function Warn([string]$where, [string]$message) {
    $script:warnings++
    Write-Host ("  warn   {0}: {1}" -f $where, $message) -ForegroundColor Yellow
}

function Read-Xml([string]$path, [string]$label) {
    try {
        $doc = New-Object System.Xml.XmlDocument
        $doc.Load($path)
        return $doc
    }
    catch {
        Fail $label ("not well-formed XML: " + $_.Exception.Message)
        return $null
    }
}

# --- the English template -------------------------------------------------------------------------------------------

$englishFile = [IO.Path]::Combine($languages, 'EN', 'strings.xml')
if (-not (Test-Path $englishFile)) {
    Write-Host "Cannot find $englishFile" -ForegroundColor Red
    exit 1
}
$english = Read-Xml $englishFile 'EN/strings.xml'
if ($null -eq $english) { exit 1 }

$template = @{}
foreach ($node in $english.SelectNodes('//strings/string')) {
    $template[$node.GetAttribute('id')] = $node.GetAttribute('text')
}
Write-Host ("English template: {0} strings" -f $template.Count)

# --- each translation -----------------------------------------------------------------------------------------------

$folders = Get-ChildItem -Path $languages -Directory | Where-Object { $_.Name -ne 'EN' }
if ($Language) {
    $folders = $folders | Where-Object { $_.Name -eq $Language }
    if (-not $folders) { Write-Host "No language folder named '$Language'." -ForegroundColor Red; exit 1 }
}

if (-not $folders) {
    Write-Host "No translations yet (only EN). Nothing more to check."
    exit 0
}

$seenIds = @{}
foreach ($folder in $folders) {
    $name = $folder.Name
    Write-Host ""
    Write-Host "== $name"

    $declaration = [IO.Path]::Combine($folder.FullName, 'language_data.xml')
    if (-not (Test-Path $declaration)) {
        Fail "$name/language_data.xml" 'missing: the game only loads a language folder that has this file'
        continue
    }
    $data = Read-Xml $declaration "$name/language_data.xml"
    if ($null -eq $data) { continue }

    $element = $data.DocumentElement
    if ($element.LocalName -ne 'LanguageData') { Fail "$name/language_data.xml" "the root element must be <LanguageData>, not <$($element.LocalName)>" ; continue }

    $languageId = $element.GetAttribute('id')
    foreach ($required in 'id', 'name', 'subtitle_extension') {
        if (-not $element.GetAttribute($required)) { Fail "$name/language_data.xml" "missing the '$required' attribute" }
    }
    if (-not $element.GetAttribute('supported_iso')) { Warn "$name/language_data.xml" "no 'supported_iso' list" }
    if ($languageId -eq 'English') { Fail "$name/language_data.xml" "id 'English' belongs to the EN folder" }
    if ($languageId) {
        if ($seenIds.ContainsKey($languageId)) { Fail "$name/language_data.xml" "language id '$languageId' is also used by $($seenIds[$languageId])" }
        $seenIds[$languageId] = $name
    }

    $files = @($element.SelectNodes('LanguageFile'))
    if ($files.Count -eq 0) { Fail "$name/language_data.xml" 'lists no <LanguageFile>' }

    $translated = @{}
    foreach ($file in $files) {
        $relative = $file.GetAttribute('xml_path')
        $path = [IO.Path]::Combine($languages, $relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path $path)) { Fail "$name/language_data.xml" "xml_path '$relative' does not exist (paths start from the Languages folder, e.g. $name/strings.xml)"; continue }

        $doc = Read-Xml $path $relative
        if ($null -eq $doc) { continue }

        $tag = $doc.SelectSingleNode('//tags/tag')
        if ($null -eq $tag) { Warn $relative 'no <tag language="..."/>' }
        elseif ($languageId -and $tag.GetAttribute('language') -ne $languageId) {
            Fail $relative ("<tag language=""{0}""/> must match the id in language_data.xml (""{1}"")" -f $tag.GetAttribute('language'), $languageId)
        }

        foreach ($node in $doc.SelectNodes('//strings/string')) {
            $id = $node.GetAttribute('id')
            $text = $node.GetAttribute('text')
            if (-not $template.ContainsKey($id)) {
                Fail $relative "unknown string id '$id' (not in EN/strings.xml: a typo, or an id from a different version?)"
                continue
            }
            if ($translated.ContainsKey($id)) { Fail $relative "string id '$id' appears twice"; continue }
            if ($text.Trim().Length -eq 0) { Fail $relative "string '$id' is blank (delete the line to fall back to English)"; continue }
            if ($text.IndexOfAny([char[]]'{}') -ge 0) { Fail $relative "string '$id' contains { or }, which the game reads as a variable" }
            $translated[$id] = $text
        }
    }

    $missing = @($template.Keys | Where-Object { -not $translated.ContainsKey($_) })
    $percent = if ($template.Count) { [math]::Round(100 * $translated.Count / $template.Count) } else { 100 }
    Write-Host ("  {0} of {1} strings translated ({2}%); {3} fall back to English" -f $translated.Count, $template.Count, $percent, $missing.Count)
    if ($missing.Count -gt 0 -and $missing.Count -le 15) {
        Write-Host ("  not yet translated: " + (($missing | Sort-Object) -join ', '))
    }
}

Write-Host ""
if ($errors -gt 0) {
    Write-Host ("{0} error(s), {1} warning(s)" -f $errors, $warnings) -ForegroundColor Red
    exit 1
}
Write-Host ("OK ({0} warning(s))" -f $warnings) -ForegroundColor Green
exit 0
