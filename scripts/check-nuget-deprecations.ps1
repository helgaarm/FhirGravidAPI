param(
    [string]$Solution = "PopulationDataFacade.slnx"
)

$ErrorActionPreference = "Stop"

# These legacy packages are currently reachable only through the test toolchain.
# Keep the exceptions exact and fail if they become direct or runtime dependencies.
$approvedTestTransitiveDeprecations = @(
    "microsoft.netcore.platforms|5.0.0"
    "system.security.accesscontrol|5.0.0"
)

$resultText = (& dotnet list $Solution package --deprecated --include-transitive --format json) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "Could not query NuGet deprecation metadata."
}

$result = $resultText | ConvertFrom-Json
$violations = @()
$approvedFindings = @()
foreach ($project in $result.projects) {
    $normalizedProjectPath = $project.path.Replace("\", "/")
    foreach ($framework in @($project.frameworks)) {
        foreach ($packageGroup in @(
            [pscustomobject]@{ Kind = "direct"; Packages = @($framework.topLevelPackages) },
            [pscustomobject]@{ Kind = "transitive"; Packages = @($framework.transitivePackages) }
        )) {
            foreach ($package in $packageGroup.Packages) {
                if ($null -eq $package -or [string]::IsNullOrWhiteSpace($package.id)) {
                    continue
                }

                $key = "$($package.id.ToLowerInvariant())|$($package.resolvedVersion.ToLowerInvariant())"
                $finding = "$($package.id) $($package.resolvedVersion): $(@($package.deprecationReasons) -join ', ') ($($packageGroup.Kind), project $($project.path))"
                $isApprovedTestTransitive =
                    $packageGroup.Kind -eq "transitive" -and
                    $normalizedProjectPath -match "/tests/" -and
                    $key -in $approvedTestTransitiveDeprecations

                if ($isApprovedTestTransitive) {
                    $approvedFindings += $finding
                }
                else {
                    $violations += $finding
                }
            }
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | Sort-Object -Unique | ForEach-Object { Write-Error $_ }
    throw "NuGet deprecation policy failed. Upgrade or explicitly review every deprecated package before merging."
}

$approvedSummary = $approvedFindings | Sort-Object -Unique
Write-Host "NuGet deprecation policy passed; only $($approvedSummary.Count) reviewed test-only transitive package/version findings remain."
$approvedSummary | ForEach-Object { Write-Host "  approved: $_" }
