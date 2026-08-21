# Third-party dependencies and licenses

The original source code in this repository is licensed under the [MIT License](LICENSE). That license does not relicense third-party packages, tools, container layers, standards, or services. Each third-party component remains subject to its own terms.

This inventory was produced on 2026-08-20 from `Directory.Packages.props`, all project files, the resolved `net9.0` NuGet graph, `auth-gateway/go.mod`, the packages' embedded license metadata, both Dockerfiles, and the GitHub Actions workflow. It is a compliance aid, not legal advice. Recreate and review the inventory after every dependency or base-image update.

## License summary

The resolved NuGet graph contains 121 unique package/version pairs across the production and test projects:

| Declared license | Package/version pairs | Included license text or authoritative terms |
| --- | ---: | --- |
| MIT | 95 | [SPDX MIT](https://spdx.org/licenses/MIT.html) |
| Apache-2.0 | 19 | [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0) |
| BSD-3-Clause | 7 | [SPDX BSD-3-Clause](https://spdx.org/licenses/BSD-3-Clause.html) |

## Dependency license policy

All currently resolved NuGet packages use MIT, Apache-2.0, or BSD-3-Clause. Packages with a custom, missing, file-based, or otherwise unapproved license are rejected by `scripts/check-licenses.ps1`, which runs in CI after restore.

Adding another license requires an explicit architectural and legal review plus an update to both the allowlist and this notice. Prefer replacing a package over adding a non-standard or commercial runtime license.

## Direct NuGet dependencies

`Use` identifies whether the dependency is part of the application or only the test toolchain.

| Package | Declared version | License | Use |
| --- | --- | --- | --- |
| [Duende.AccessTokenManagement](https://www.nuget.org/packages/Duende.AccessTokenManagement/4.2.0) | 4.2.0 | Apache-2.0 | Runtime |
| [Duende.IdentityModel](https://www.nuget.org/packages/Duende.IdentityModel/8.1.0) | 8.1.0 | Apache-2.0 | Runtime |
| [Hl7.Fhir.R4](https://www.nuget.org/packages/Hl7.Fhir.R4/6.3.0) | 6.3.0 | BSD-3-Clause | Runtime |
| [Microsoft.AspNetCore.Authentication.JwtBearer](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer/9.0.19) | 9.0.19 | MIT | Runtime |
| [Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi/9.0.19) | 9.0.19 | MIT | Runtime |
| [Microsoft.IdentityModel.JsonWebTokens](https://www.nuget.org/packages/Microsoft.IdentityModel.JsonWebTokens/8.19.2) | 8.19.2 | MIT | Runtime |
| [Microsoft.IdentityModel.Protocols.OpenIdConnect](https://www.nuget.org/packages/Microsoft.IdentityModel.Protocols.OpenIdConnect/8.19.2) | 8.19.2 | MIT | Test only |
| [OpenTelemetry.Exporter.OpenTelemetryProtocol](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol/1.17.0) | 1.17.0 | Apache-2.0 | Runtime |
| [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/1.17.0) | 1.17.0 | Apache-2.0 | Runtime |
| [OpenTelemetry.Instrumentation.AspNetCore](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore/1.17.0) | 1.17.0 | Apache-2.0 | Runtime |
| [OpenTelemetry.Instrumentation.Http](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http/1.17.0) | 1.17.0 | Apache-2.0 | Runtime |
| [Swashbuckle.AspNetCore](https://www.nuget.org/packages/Swashbuckle.AspNetCore/9.0.6) | 9.0.6 | MIT | Runtime |
| [Microsoft.AspNetCore.Mvc.Testing](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing/9.0.19) | 9.0.19 | MIT | Test only |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.0.1) | 18.0.1 | MIT | Test only |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector/6.0.4) | 6.0.4 | MIT | Test only |
| [xunit.v3](https://www.nuget.org/packages/xunit.v3/3.2.2) | 3.2.2 | Apache-2.0 | Test only |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.5) | 3.1.5 | Apache-2.0 | Test only |

## Resolved transitive dependency families

Some package IDs resolve at more than one version because the production and test project graphs differ. The following list covers every transitive package family in the current restore.

| License | Packages and resolved versions |
| --- | --- |
| Apache-2.0 | `OpenTelemetry`, `OpenTelemetry.Api`, `OpenTelemetry.Api.ProviderBuilderExtensions` 1.17.0; `xunit.analyzers` 1.27.0; `xunit.v3.assert`, `xunit.v3.common`, `xunit.v3.core.mtp-v1`, `xunit.v3.extensibility.core`, `xunit.v3.mtp-v1`, `xunit.v3.runner.common`, `xunit.v3.runner.inproc.console` 3.2.2 |
| BSD-3-Clause | `Fhir.Metrics` 1.3.1; `Hl7.Fhir.Base`, `Hl7.Fhir.Conformance` 6.3.0; `Polly.Core`, `Polly.Extensions`, `Polly.RateLimiting` 8.4.2 |
| MIT | `Microsoft.ApplicationInsights` 2.23.0; `Microsoft.AspNetCore.TestHost` 9.0.19; `Microsoft.Bcl.AsyncInterfaces` 6.0.0; `Microsoft.Bcl.Cryptography` 10.0.2; `Microsoft.Bcl.Memory` 10.0.4; `Microsoft.CodeCoverage` 18.0.1; all resolved `Microsoft.Extensions.*` packages (8.0.0–8.0.2, 9.0.0, 9.0.11, 9.0.19, 10.0.0, 10.0.4); all resolved `Microsoft.IdentityModel.*` packages 8.19.2; `Microsoft.NETCore.Platforms` 5.0.0; `Microsoft.OpenApi` 1.6.25; `Microsoft.Testing.*` packages 1.9.1; `Microsoft.TestPlatform.ObjectModel`, `Microsoft.TestPlatform.TestHost` 18.0.1; `Microsoft.Win32.Registry` 5.0.0; `Newtonsoft.Json` 13.0.4; `Swashbuckle.AspNetCore.Swagger`, `Swashbuckle.AspNetCore.SwaggerGen`, `Swashbuckle.AspNetCore.SwaggerUI` 9.0.6; `System.Collections.Immutable` 8.0.0; `System.ComponentModel.Annotations` 5.0.0; `System.Diagnostics.DiagnosticSource` 10.0.4; `System.Diagnostics.EventLog` 9.0.19; `System.IdentityModel.Tokens.Jwt` 8.0.1 and 8.19.2; `System.Reflection.Emit.Lightweight` 4.7.0; `System.Reflection.Metadata` 8.0.0; `System.Security.AccessControl`, `System.Security.Principal.Windows` 5.0.0; `System.Threading.RateLimiting` 8.0.0 |

Direct dependencies can also appear transitively in other projects. Their license remains the one recorded in the direct-dependency table.

NuGet marks `Microsoft.NETCore.Platforms` 5.0.0 and `System.Security.AccessControl` 5.0.0 as legacy. They are accepted only as exact-version transitive dependencies of the test projects and are not part of the application runtime graph. `scripts/check-nuget-deprecations.ps1` rejects these packages if they become direct/runtime dependencies and rejects every other deprecation until it is upgraded or explicitly reviewed.

## Go authentication gateway dependencies

The inbound HelseID/DPoP gateway uses only standard open-source licenses. `AxisCommunications/go-dpop` is the DPoP implementation recommended by HelseID for Go APIs; the repository adds HelseID-specific access-token, proof-age, replay, scope, and deployment-boundary checks around it.

| Module | Version | License | Use |
| --- | --- | --- | --- |
| [AxisCommunications/go-dpop](https://github.com/AxisCommunications/go-dpop) | 1.1.2 | MIT | Runtime DPoP validation |
| [MicahParks/keyfunc](https://github.com/MicahParks/keyfunc) | 3.8.1 | Apache-2.0 | Runtime HelseID JWKS loading and rotation |
| [golang-jwt/jwt](https://github.com/golang-jwt/jwt) | 5.3.1 | MIT | Runtime access-token validation |
| [redis/go-redis](https://github.com/redis/go-redis) | 9.22.0 | BSD-2-Clause | Runtime atomic multi-replica replay store |

Resolved runtime transitive modules are `MicahParks/jwkset` 0.11.1 (Apache-2.0), `cespare/xxhash/v2` 2.3.0 (MIT), `go.uber.org/atomic` 1.11.0 (MIT), and `golang.org/x/sys` 0.47.0 plus `golang.org/x/time` 0.15.0 (BSD-3-Clause). `alicebob/miniredis/v2` 2.38.0 and `yuin/gopher-lua` 1.1.1 are test-only and are not linked into the gateway binary. Other test-only checksums may appear in `go.sum` because upstream modules declare their own tests.

The exact runtime module allowlist enforced by CI is:

```text
github.com/AxisCommunications/go-dpop|v1.1.2
github.com/MicahParks/jwkset|v0.11.1
github.com/MicahParks/keyfunc/v3|v3.8.1
github.com/cespare/xxhash/v2|v2.3.0
github.com/golang-jwt/jwt/v5|v5.3.1
github.com/redis/go-redis/v9|v9.22.0
go.uber.org/atomic|v1.11.0
golang.org/x/sys|v0.47.0
golang.org/x/time|v0.15.0
```

## Container images

The Docker build uses these immutable image references:

| Image | Role | Licensing note |
| --- | --- | --- |
| `mcr.microsoft.com/dotnet/sdk:9.0.317-bookworm-slim@sha256:35048e3a81e6a07c316e7bbbd80d80d2ba705fe5f23a8ed42b6638c8f4c20d30` | Build stage only | The .NET Docker repository is MIT, while the image includes .NET, PowerShell, a Linux base, and other components under their own licenses. |
| `mcr.microsoft.com/dotnet/aspnet:9.0.19-bookworm-slim@sha256:4e376dd15bbc8437d4892367ab0ea06a3ac9fea482d10f92f3c493fe1a2219ad` | Distributed runtime image | Includes `/usr/share/dotnet/LICENSE.txt` and `/usr/share/dotnet/ThirdPartyNotices.txt`; Linux packages remain under their individual licenses. |
| `golang:1.27.0-alpine@sha256:4c9fe60190a2a3350ddc51de80d0224b8a6698d12bdfc999fee45ea9d6c46dbc` | Gateway build stage only | Go is BSD-3-Clause; Alpine packages retain their individual licenses. The distributed gateway runtime is `scratch` plus the binary and CA certificate bundle. |

Microsoft's [container legal notice](https://aka.ms/mcr/osslegalnotice) explains that container contents may use multiple licenses. The [.NET image license-discovery guide](https://github.com/dotnet/dotnet-docker/blob/main/documentation/image-artifact-details.md) describes how to inspect embedded notices and Linux package metadata.

The build stages are pinned by digest. For a release, generate and retain an image SBOM/license report for those exact digests; this repository-level inventory does not enumerate all operating-system packages in the images.

## Build and deployment tooling

These components support builds or deployment and are not linked into the application binaries:

| Component | Version/reference | License |
| --- | --- | --- |
| [.NET SDK](https://github.com/dotnet/sdk) | 9.0.317 with latest patch roll-forward | MIT |
| [Bicep](https://github.com/Azure/bicep) | Version supplied by Azure CLI/GitHub runner | MIT, excluding separately licensed Azure Architecture icons not used here |
| [actions/checkout](https://github.com/actions/checkout) | `3d3c42e5aac5ba805825da76410c181273ba90b1` (`v7`) | MIT |
| [actions/setup-dotnet](https://github.com/actions/setup-dotnet) | `a98b56852c35b8e3190ac28c8c2271da59106c68` (`v6`) | MIT |
| [actions/setup-go](https://github.com/actions/setup-go) | `924ae3a1cded613372ab5595356fb5720e22ba16` (`v6`) | MIT |
| [Azure/login](https://github.com/Azure/login) | `f5d393ae46f8fde4be8b75f32e3fc50e654ad0ca` (`v3`) | MIT |

GitHub-hosted runner software, Azure platform services, Docker tooling, and externally called DHG/HelseID services are governed by their respective terms and are not redistributed by this repository.

## FHIR specification material

The application uses the Firely .NET SDK packages under BSD-3-Clause. FHIR names, definitions, code systems, and specification material can also carry HL7 terms that are separate from the SDK's software license. See the [FHIR license and legal terms](https://hl7.org/fhir/license.html) when redistributing specification content or derived artifacts.

## Refresh procedure

After changing package versions, restore with the repository SDK and inspect the complete graph:

```powershell
dotnet restore PopulationDataFacade.slnx --locked-mode
dotnet list PopulationDataFacade.slnx package --include-transitive
./scripts/check-licenses.ps1
./scripts/check-nuget-vulnerabilities.ps1
./scripts/check-nuget-deprecations.ps1
Push-Location auth-gateway
go list -m all
go mod verify
Pop-Location
```

For each unique package/version pair, verify the `<license>` or `<licenseUrl>` value in the installed package's `.nuspec`. Also re-check the runtime container by digest and update the date, counts, versions, and exceptions in this file.
