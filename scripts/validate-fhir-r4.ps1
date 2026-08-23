param(
    [string]$ValidationDirectory = "validation"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $repositoryRoot $ValidationDirectory
$examplesRoot = Join-Path $validationRoot "examples"
$manifestPath = Join-Path $validationRoot "package.json"
$lockPath = Join-Path $validationRoot "fhirpkg.lock.json"

if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $lockPath)) {
    throw "FHIR R4 validation package manifest was not found at $validationRoot."
}

$examples = @(Get-ChildItem -LiteralPath $examplesRoot -Filter "*.json" -File | Sort-Object Name)
if ($examples.Count -eq 0) {
    throw "No FHIR R4 validation examples were found at $examplesRoot."
}

Push-Location $validationRoot
try {
    & dotnet tool run fhir -- cache use-local | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Could not configure the repository-local FHIR package cache."
    }

    $requiredPackage = "hl7.fhir.r4.core@4.0.1"
    $cacheEntries = (& dotnet tool run fhir -- cache list 2>&1) -join "`n"
    if ($cacheEntries -notmatch "(?m)^$([regex]::Escape($requiredPackage))$") {
        $manifestContent = [IO.File]::ReadAllText($manifestPath)
        $lockContent = [IO.File]::ReadAllText($lockPath)
        try {
            & dotnet tool run fhir -- install $requiredPackage 2>&1 | Out-Host
        }
        finally {
            [IO.File]::WriteAllText($manifestPath, $manifestContent)
            [IO.File]::WriteAllText($lockPath, $lockContent)
        }

        $cacheEntries = (& dotnet tool run fhir -- cache list 2>&1) -join "`n"
        if ($cacheEntries -notmatch "(?m)^$([regex]::Escape($requiredPackage))$") {
            throw "Pinned FHIR package $requiredPackage was not installed in the local cache."
        }
    }

    foreach ($example in $examples) {
        $relativeExamplePath = Join-Path "examples" $example.Name
        $validationOutput = (& dotnet tool run fhir -- validate $relativeExamplePath 2>&1) -join "`n"
        if ($LASTEXITCODE -ne 0 -or
            $validationOutput -notmatch '(?im)^Result:\s+VALID\s*$' -or
            $validationOutput -match '(?im)^\s*(error|fatal)\b|^Result:\s+INVALID\s*$') {
            Write-Error $validationOutput
            throw "FHIR R4 base validation failed for $($example.Name)."
        }

        Write-Host "FHIR R4 base validation passed for $($example.Name)."
    }
}
finally {
    Pop-Location
}
