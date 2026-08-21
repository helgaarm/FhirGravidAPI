param(
    [string]$GatewayDirectory = "auth-gateway",
    [string]$Notice = "THIRD-PARTY-NOTICES.md"
)

$ErrorActionPreference = "Stop"

# Go modules do not expose a uniform SPDX field. Every runtime module is therefore
# pinned here only after its repository LICENSE file has been reviewed.
$approvedRuntimeModules = @{
    "github.com/AxisCommunications/go-dpop|v1.1.2" = "MIT"
    "github.com/MicahParks/jwkset|v0.11.1" = "Apache-2.0"
    "github.com/MicahParks/keyfunc/v3|v3.8.1" = "Apache-2.0"
    "github.com/cespare/xxhash/v2|v2.3.0" = "MIT"
    "github.com/golang-jwt/jwt/v5|v5.3.1" = "MIT"
    "github.com/redis/go-redis/v9|v9.22.0" = "BSD-2-Clause"
    "go.uber.org/atomic|v1.11.0" = "MIT"
    "golang.org/x/sys|v0.47.0" = "BSD-3-Clause"
    "golang.org/x/time|v0.15.0" = "BSD-3-Clause"
}

Push-Location $GatewayDirectory
try {
    $runtimeModules = @(& go list -deps -f '{{with .Module}}{{if .Version}}{{.Path}}|{{.Version}}{{end}}{{end}}' .) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
    if ($LASTEXITCODE -ne 0) {
        throw "Could not resolve the auth-gateway runtime module graph."
    }
}
finally {
    Pop-Location
}

$violations = @()
foreach ($module in $runtimeModules) {
    if (-not $approvedRuntimeModules.ContainsKey($module)) {
        $violations += "${module}: missing reviewed license allowlist entry"
    }
}
foreach ($module in $approvedRuntimeModules.Keys) {
    if ($module -notin $runtimeModules) {
        $violations += "${module}: stale reviewed license allowlist entry"
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "Go dependency license policy failed. Review module license files and update THIRD-PARTY-NOTICES.md before changing the allowlist."
}

$summary = $runtimeModules |
    ForEach-Object { $approvedRuntimeModules[$_] } |
    Group-Object |
    Sort-Object Name |
    ForEach-Object { "$($_.Name)=$($_.Count)" }
Write-Host "Go dependency license policy passed for $($runtimeModules.Count) runtime modules ($($summary -join ', '))."

if (-not (Test-Path -LiteralPath $Notice)) {
    throw "Dependency notice $Notice was not found."
}
$noticeText = Get-Content -LiteralPath $Notice -Raw
foreach ($module in $runtimeModules) {
    $path, $version = $module.Split('|', 2)
    if (-not $noticeText.Contains($path) -or -not $noticeText.Contains($version)) {
        throw "$Notice does not document runtime Go module $path $version."
    }
}
Write-Host "$Notice contains every resolved runtime Go module and version."
