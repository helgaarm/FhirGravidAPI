param(
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Split-MarkdownTableRow {
    param([Parameter(Mandatory)][string] $Line)

    $value = $Line.Trim()
    if ($value.StartsWith('|')) { $value = $value[1..($value.Length - 1)] -join '' }
    if ($value.EndsWith('|')) { $value = $value[0..($value.Length - 2)] -join '' }

    $cells = [Collections.Generic.List[string]]::new()
    $cell = [Text.StringBuilder]::new()
    $insideCode = $false
    for ($index = 0; $index -lt $value.Length; $index++) {
        $character = $value[$index]
        if ($character -eq '`') {
            $insideCode = -not $insideCode
            [void] $cell.Append($character)
            continue
        }

        if ($character -eq '\' -and
            $index + 1 -lt $value.Length -and
            $value[$index + 1] -eq '|') {
            [void] $cell.Append('|')
            $index++
            continue
        }

        if ($character -eq '|' -and -not $insideCode) {
            $cells.Add($cell.ToString().Trim())
            [void] $cell.Clear()
            continue
        }

        [void] $cell.Append($character)
    }

    $cells.Add($cell.ToString().Trim())
    return $cells.ToArray()
}

function Convert-InlineMarkdown {
    param([Parameter(Mandatory)][string] $Value)

    $parts = $Value.Split([char] '`')
    $output = [Text.StringBuilder]::new()
    for ($index = 0; $index -lt $parts.Length; $index++) {
        $encoded = $parts[$index].Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
        if ($index % 2 -eq 0) {
            [void] $output.Append($encoded)
            continue
        }

        $encoded = $encoded.Replace('.', '.<wbr>')
        $encoded = $encoded.Replace('/', '/<wbr>')
        $encoded = $encoded.Replace(',', ',<wbr>')
        $encoded = $encoded.Replace('(', '(<wbr>')
        [void] $output.Append("<code>$encoded</code>")
    }

    return $output.ToString()
}

$documentationFiles = @(
    Join-Path $RepositoryRoot 'README.md'
    Get-ChildItem (Join-Path $RepositoryRoot 'docs'), (Join-Path $RepositoryRoot 'examples') `
        -Recurse -Filter '*.md' | ForEach-Object FullName
)

foreach ($file in $documentationFiles) {
    $content = [IO.File]::ReadAllText($file)
    $lines = [regex]::Split($content, '\r?\n')
    $output = [Collections.Generic.List[string]]::new()
    $insideFence = $false

    for ($index = 0; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        if ($line.TrimStart().StartsWith('```')) {
            $insideFence = -not $insideFence
            $output.Add($line)
            continue
        }

        $hasPossibleHeader = -not $insideFence -and
            $line -match '^\s*\|.*\|\s*$' -and
            $index + 1 -lt $lines.Length -and
            $lines[$index + 1] -match '^\s*\|.*\|\s*$'
        if (-not $hasPossibleHeader) {
            $output.Add($line)
            continue
        }

        $headers = @(Split-MarkdownTableRow $line)
        $separators = @(Split-MarkdownTableRow $lines[$index + 1])
        $isSeparator = $headers.Count -eq $separators.Count -and
            @($separators | Where-Object { $_ -notmatch '^:?-{3,}:?$' }).Count -eq 0
        if (-not $isSeparator) {
            $output.Add($line)
            continue
        }

        $width = (100 / $headers.Count).ToString('0.##', [Globalization.CultureInfo]::InvariantCulture)
        $headerCells = $headers | ForEach-Object {
            '<th width="{0}%" scope="col">{1}</th>' -f $width, (Convert-InlineMarkdown $_)
        }
        $output.Add('<table>')
        $output.Add('  <thead>')
        $output.Add("    <tr>$($headerCells -join '')</tr>")
        $output.Add('  </thead>')
        $output.Add('  <tbody>')

        $index += 2
        while ($index -lt $lines.Length -and $lines[$index] -match '^\s*\|.*\|\s*$') {
            $cells = @(Split-MarkdownTableRow $lines[$index])
            if ($cells.Count -ne $headers.Count) {
                throw "${file}:$($index + 1) has $($cells.Count) table cells; expected $($headers.Count)."
            }
            $dataCells = $cells | ForEach-Object { "<td>$(Convert-InlineMarkdown $_)</td>" }
            $output.Add("    <tr>$($dataCells -join '')</tr>")
            $index++
        }

        $output.Add('  </tbody>')
        $output.Add('</table>')
        $index--
    }

    $rewritten = $output -join [Environment]::NewLine
    $rewritten = [regex]::Replace($rewritten, '&#(\d+);', {
        param($match)
        $codePoint = [int] $match.Groups[1].Value
        if ($codePoint -le 127) { return $match.Value }
        return [char]::ConvertFromUtf32($codePoint)
    })
    if ($rewritten -ne $content) {
        [IO.File]::WriteAllText($file, $rewritten, [Text.UTF8Encoding]::new($false))
    }
}
