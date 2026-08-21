param(
    [string]$Solution = "PopulationDataFacade.slnx"
)

$ErrorActionPreference = "Stop"
$resultText = (& dotnet list $Solution package --vulnerable --include-transitive --format json) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "Could not query NuGet vulnerability metadata."
}

$result = $resultText | ConvertFrom-Json
$findings = @()
foreach ($project in $result.projects) {
    foreach ($framework in @($project.frameworks)) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            if ($null -eq $package -or [string]::IsNullOrWhiteSpace($package.id)) {
                continue
            }
            foreach ($vulnerability in @($package.vulnerabilities)) {
                if ($null -eq $vulnerability -or [string]::IsNullOrWhiteSpace($vulnerability.advisoryUrl)) {
                    continue
                }
                $findings += [pscustomobject]@{
                    Project = $project.path
                    Package = $package.id
                    Version = $package.resolvedVersion
                    Severity = $vulnerability.severity
                    Advisory = $vulnerability.advisoryUrl
                }
            }
        }
    }
}

if ($findings.Count -gt 0) {
    $findings |
        Sort-Object Package, Version, Advisory -Unique |
        ForEach-Object {
            Write-Error "$($_.Package) $($_.Version): $($_.Severity) $($_.Advisory) (project $($_.Project))"
        }
    throw "NuGet vulnerability policy failed. Upgrade or explicitly review every advisory before merging."
}

Write-Host "NuGet vulnerability policy passed with no known vulnerable direct or transitive packages."
