param(
    [string]$Solution = "PopulationDataFacade.slnx",
    [string]$Notice = "THIRD-PARTY-NOTICES.md"
)

$ErrorActionPreference = "Stop"
$approvedLicenses = @("MIT", "Apache-2.0", "BSD-3-Clause")
$legacyLicenseUrls = @{
    "https://raw.githubusercontent.com/xunit/xunit/master/license.txt" = "Apache-2.0"
}

$packageJson = (& dotnet list $Solution package --include-transitive --format json) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "Could not read the resolved NuGet dependency graph. Run dotnet restore first."
}

$graph = $packageJson | ConvertFrom-Json
$packagesByKey = @{}
foreach ($project in $graph.projects) {
    foreach ($framework in $project.frameworks) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            $key = "$($package.id.ToLowerInvariant())|$($package.resolvedVersion.ToLowerInvariant())"
            $packagesByKey[$key] = [pscustomobject]@{
                Id = $package.id
                Version = $package.resolvedVersion
            }
        }
    }
}

$globalPackagesOutput = (& dotnet nuget locals global-packages --list) -join "`n"
if ($LASTEXITCODE -ne 0 -or $globalPackagesOutput -notmatch "(?m)^[^:]+:\s*(.+)$") {
    throw "Could not determine the NuGet global-packages directory."
}
$globalPackagesDirectory = $Matches[1].Trim()

$inventory = @()
$violations = @()
foreach ($package in $packagesByKey.Values | Sort-Object Id, Version) {
    $packageDirectory = Join-Path $globalPackagesDirectory $package.Id.ToLowerInvariant()
    $packageDirectory = Join-Path $packageDirectory $package.Version.ToLowerInvariant()
    $nuspec = Get-ChildItem -LiteralPath $packageDirectory -Filter "*.nuspec" | Select-Object -First 1
    if ($null -eq $nuspec) {
        $violations += "$($package.Id) $($package.Version): package metadata was not found"
        continue
    }

    [xml]$metadata = Get-Content -LiteralPath $nuspec.FullName -Raw
    $licenseNode = $metadata.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='license']")
    $licenseUrlNode = $metadata.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='licenseUrl']")
    $declaredLicense = if ($null -ne $licenseNode) {
        if ($licenseNode.GetAttribute("type") -ne "expression") {
            "unapproved-$($licenseNode.GetAttribute('type')):$($licenseNode.InnerText)"
        }
        else {
            $licenseNode.InnerText
        }
    }
    elseif ($null -ne $licenseUrlNode -and $legacyLicenseUrls.ContainsKey($licenseUrlNode.InnerText)) {
        $legacyLicenseUrls[$licenseUrlNode.InnerText]
    }
    elseif ($null -ne $licenseUrlNode) {
        "unapproved-url:$($licenseUrlNode.InnerText)"
    }
    else {
        "missing"
    }

    $inventory += [pscustomobject]@{
        Package = $package.Id
        Version = $package.Version
        License = $declaredLicense
    }
    if ($declaredLicense -notin $approvedLicenses) {
        $violations += "$($package.Id) $($package.Version): $declaredLicense"
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "Dependency license policy failed. Review the package and update THIRD-PARTY-NOTICES.md before changing the allowlist."
}

$summary = $inventory |
    Group-Object License |
    Sort-Object Name |
    ForEach-Object { "$($_.Name)=$($_.Count)" }
Write-Host "Dependency license policy passed for $($inventory.Count) package/version pairs ($($summary -join ', '))."

if (-not (Test-Path -LiteralPath $Notice)) {
    throw "Dependency notice $Notice was not found."
}
$noticeText = Get-Content -LiteralPath $Notice -Raw
$licenseCounts = $inventory | Group-Object License
if ($noticeText -notmatch [regex]::Escape("contains $($inventory.Count) unique package/version pairs")) {
    throw "$Notice does not contain the current NuGet package count ($($inventory.Count))."
}
foreach ($licenseCount in $licenseCounts) {
    $expectedRow = "| $($licenseCount.Name) | $($licenseCount.Count) |"
    if (-not $noticeText.Contains($expectedRow)) {
        throw "$Notice does not contain the current $($licenseCount.Name) count ($($licenseCount.Count))."
    }
}

[xml]$centralVersions = Get-Content -LiteralPath "Directory.Packages.props" -Raw
foreach ($package in $centralVersions.Project.ItemGroup.PackageVersion) {
    $id = $package.Include
    $version = $package.Version
    $expectedLink = "https://www.nuget.org/packages/$id/$version"
    if (-not $noticeText.Contains($expectedLink)) {
        throw "$Notice does not document direct dependency $id $version."
    }
}
Write-Host "$Notice matches the current NuGet counts and direct dependency versions."
