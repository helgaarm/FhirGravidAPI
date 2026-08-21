param(
    [Parameter(Mandatory = $true)]
    [string]$CompiledTemplate
)

$ErrorActionPreference = "Stop"
$template = Get-Content -LiteralPath $CompiledTemplate -Raw | ConvertFrom-Json
$application = @($template.resources) |
    Where-Object { $_.type -eq "Microsoft.App/containerApps" } |
    Select-Object -First 1
if ($null -eq $application) {
    throw "Compiled template does not contain a Microsoft.App/containerApps resource."
}

$ingress = $application.properties.configuration.ingress
if ($ingress.external -ne $true -or $ingress.allowInsecure -ne $false -or $ingress.targetPort -ne 8080) {
    throw "External ingress must require HTTPS and target only auth-gateway port 8080."
}

$containers = @($application.properties.template.containers)
$api = $containers | Where-Object { $_.name -eq "api" } | Select-Object -First 1
$gateway = $containers | Where-Object { $_.name -eq "auth-gateway" } | Select-Object -First 1
if ($null -eq $api -or $null -eq $gateway -or $containers.Count -ne 2) {
    throw "Container App must contain exactly the api and auth-gateway containers."
}

$apiProbePorts = @($api.probes | ForEach-Object { $_.httpGet.port } | Sort-Object -Unique)
$gatewayProbePorts = @($gateway.probes | ForEach-Object { $_.httpGet.port } | Sort-Object -Unique)
if ($apiProbePorts.Count -ne 1 -or $apiProbePorts[0] -ne 8081) {
    throw "Every API probe must target private port 8081."
}
if ($gatewayProbePorts.Count -ne 1 -or $gatewayProbePorts[0] -ne 8080) {
    throw "Every gateway probe must target ingress port 8080."
}

$gatewayUpstream = @($gateway.env) |
    Where-Object { $_.name -eq "AUTH_GATEWAY_UPSTREAM_URL" } |
    Select-Object -ExpandProperty value -First 1
if ($gatewayUpstream -ne "http://127.0.0.1:8081") {
    throw "Gateway upstream must remain the loopback-only API origin http://127.0.0.1:8081."
}

Write-Host "Container topology policy passed: HTTPS ingress -> gateway:8080 -> loopback API:8081."
