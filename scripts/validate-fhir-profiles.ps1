param(
    [string]$ValidationDirectory = "validation"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $repositoryRoot $ValidationDirectory
$examplesRoot = Join-Path $validationRoot "examples"
$manifestPath = Join-Path $validationRoot "package.json"
$lockPath = Join-Path $validationRoot "fhirpkg.lock.json"
$vitalSignsPackageUri = "https://hl7.no/fhir/no-domain/vitalsigns/package.tgz"
$vitalSignsPackageHash = "56CB3F9BCC34A5AAB9BA5FFDB925A1CA882E35DC1D64ACA6C2CE9F0B3E9EADEC"

if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $lockPath)) {
    throw "FHIR validation package manifest was not found at $validationRoot."
}

$examples = @(Get-ChildItem -LiteralPath $examplesRoot -Filter "*.json" -File | Sort-Object Name)
if ($examples.Count -eq 0) {
    throw "No FHIR validation examples were found at $examplesRoot."
}

Push-Location $validationRoot
try {
    & dotnet tool run fhir -- cache use-local | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Could not configure the repository-local FHIR package cache."
    }

    $cacheEntries = (& dotnet tool run fhir -- cache list 2>&1) -join "`n"
    if ($cacheEntries -notmatch '(?m)^hl7\.fhir\.no\.domain\.vitalsigns@0\.9\.74$') {
        $manifestContent = [IO.File]::ReadAllText($manifestPath)
        $lockContent = [IO.File]::ReadAllText($lockPath)
        $downloadPath = Join-Path ([IO.Path]::GetTempPath()) "hl7.fhir.no.domain.vitalsigns-0.9.74.tgz"

        try {
            Invoke-WebRequest -Uri $vitalSignsPackageUri -OutFile $downloadPath -UseBasicParsing
            $actualHash = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash
            if (-not [string]::Equals($actualHash, $vitalSignsPackageHash, [StringComparison]::OrdinalIgnoreCase)) {
                throw "The downloaded Vital Signs package did not match the pinned SHA-256."
            }

            # The official CI package is not resolvable from the package feed even though
            # its published download is valid. Installing the verified file indexes it in
            # Firely's local cache. Firely then attempts a feed restore and may return 1;
            # the exact cache inventory below is the authoritative success check.
            & dotnet tool run fhir -- install $downloadPath --file 2>&1 | Out-Host
        }
        finally {
            [IO.File]::WriteAllText($manifestPath, $manifestContent)
            [IO.File]::WriteAllText($lockPath, $lockContent)
            if (Test-Path -LiteralPath $downloadPath) {
                Remove-Item -LiteralPath $downloadPath -Force
            }
        }
    }

    $requiredPackages = @(
        "hl7.fhir.r4.core@4.0.1",
        "hl7.terminology.r4@7.1.0",
        "hl7.fhir.uv.extensions.r4@5.2.0",
        "hl7.fhir.uv.tools.r4@0.9.0",
        "hl7.fhir.no.basis@2.2.2",
        "hl7.fhir.no.domain.vitalsigns@0.9.74"
    )
    $cacheEntries = (& dotnet tool run fhir -- cache list 2>&1) -join "`n"
    foreach ($requiredPackage in $requiredPackages) {
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
            throw "FHIR profile validation failed for $($example.Name)."
        }

        Write-Host "FHIR profile validation passed for $($example.Name)."
    }
}
finally {
    Pop-Location
}
