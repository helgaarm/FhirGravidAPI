param(
    [Parameter(Mandatory = $true)]
    [string]$Workflow
)

$ErrorActionPreference = "Stop"
$content = Get-Content -LiteralPath $Workflow -Raw
$requiredFragments = @(
    'HELSEID_TEST_TOKEN_ORGNR_PARENT: ${{ vars.HELSEID_TEST_TOKEN_ORGNR_PARENT }}',
    'HELSEID_TEST_TOKEN_CLIENT_TENANCY_TYPE: ${{ vars.HELSEID_TEST_TOKEN_CLIENT_TENANCY_TYPE }}',
    'HELSEID_TEST_TOKEN_CLIENT_NAME: ${{ vars.HELSEID_TEST_TOKEN_CLIENT_NAME }}',
    'HELSEID_TEST_TOKEN_AUTH_KEY: ${{ secrets.HELSEID_TEST_TOKEN_AUTH_KEY }}',
    'helseIdTestTokenAuthKey="$HELSEID_TEST_TOKEN_AUTH_KEY"',
    'helseIdTestTokenOrgnrParent="$HELSEID_TEST_TOKEN_ORGNR_PARENT"',
    'helseIdTestTokenClientTenancyType="$HELSEID_TEST_TOKEN_CLIENT_TENANCY_TYPE"',
    'helseIdTestTokenClientName="$HELSEID_TEST_TOKEN_CLIENT_NAME"'
)

foreach ($fragment in $requiredFragments) {
    if ($content.IndexOf($fragment, [StringComparison]::Ordinal) -lt 0) {
        throw "Azure Test workflow is missing required HelseID TEST token wiring: $fragment"
    }
}

$forbiddenFragments = @(
    'HELSEID_CLIENT_ASSERTION_JWK_B64',
    'HELSEID_DPOP_JWK_B64',
    'clientAssertionJwk=',
    'dpopJwk='
)

foreach ($fragment in $forbiddenFragments) {
    if ($content.IndexOf($fragment, [StringComparison]::Ordinal) -ge 0) {
        throw "Azure Test workflow still contains legacy private-JWK deployment wiring: $fragment"
    }
}

Write-Host "Azure Test workflow policy passed: GitHub Environment auth key -> secure Bicep parameter."
