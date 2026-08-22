# Third-party dependencies og lisenser

Original source code i dette repositoryet er lisensiert under [MIT License](LICENSE). Denne lisensen relisensierer ikke third-party packages, tools, container layers, standards eller services. Hver third-party component er fortsatt underlagt sine egne vilkår.

Denne inventory ble opprettet 2026-08-20 fra `Directory.Packages.props`, alle project files, resolved `net9.0` NuGet graph, `auth-gateway/go.mod`, innebygd license metadata i pakkene, begge Dockerfiles og GitHub Actions workflow. Den er et compliance aid, ikke juridisk rådgivning. Opprett og gjennomgå inventory på nytt etter hver oppdatering av dependency eller base image.

## License summary

Resolved NuGet graph inneholder 121 unike package/version pairs på tvers av production- og test-projects:

| Declared license | Package/version pairs | Inkludert license text eller authoritative terms |
| --- | ---: | --- |
| MIT | 95 | [SPDX MIT](https://spdx.org/licenses/MIT.html) |
| Apache-2.0 | 19 | [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0) |
| BSD-3-Clause | 7 | [SPDX BSD-3-Clause](https://spdx.org/licenses/BSD-3-Clause.html) |

## Policy for dependency licenses

Alle resolved NuGet packages bruker MIT, Apache-2.0 eller BSD-3-Clause. Packages med custom, missing, file-based eller en annen ikke-godkjent license avvises av `scripts/check-licenses.ps1`, som kjører i CI etter restore.

Tillegg av en ny license krever eksplisitt architecture- og legal review samt oppdatering av både allowlist og dette notice. Foretrekk å erstatte en package fremfor å legge til en non-standard eller commercial runtime license.

## Direkte NuGet dependencies

`Use` angir om dependency er en del av applikasjonen eller bare test toolchain.

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

Noen package IDs resolves i mer enn én version fordi production- og test-project graphs er forskjellige. Listen nedenfor dekker alle transitive package families i gjeldende restore.

| License | Packages og resolved versions |
| --- | --- |
| Apache-2.0 | `OpenTelemetry`, `OpenTelemetry.Api`, `OpenTelemetry.Api.ProviderBuilderExtensions` 1.17.0; `xunit.analyzers` 1.27.0; `xunit.v3.assert`, `xunit.v3.common`, `xunit.v3.core.mtp-v1`, `xunit.v3.extensibility.core`, `xunit.v3.mtp-v1`, `xunit.v3.runner.common`, `xunit.v3.runner.inproc.console` 3.2.2 |
| BSD-3-Clause | `Fhir.Metrics` 1.3.1; `Hl7.Fhir.Base`, `Hl7.Fhir.Conformance` 6.3.0; `Polly.Core`, `Polly.Extensions`, `Polly.RateLimiting` 8.4.2 |
| MIT | `Microsoft.ApplicationInsights` 2.23.0; `Microsoft.AspNetCore.TestHost` 9.0.19; `Microsoft.Bcl.AsyncInterfaces` 6.0.0; `Microsoft.Bcl.Cryptography` 10.0.2; `Microsoft.Bcl.Memory` 10.0.4; `Microsoft.CodeCoverage` 18.0.1; alle resolved `Microsoft.Extensions.*` packages (8.0.0–8.0.2, 9.0.0, 9.0.11, 9.0.19, 10.0.0, 10.0.4); alle resolved `Microsoft.IdentityModel.*` packages 8.19.2; `Microsoft.NETCore.Platforms` 5.0.0; `Microsoft.OpenApi` 1.6.25; `Microsoft.Testing.*` packages 1.9.1; `Microsoft.TestPlatform.ObjectModel`, `Microsoft.TestPlatform.TestHost` 18.0.1; `Microsoft.Win32.Registry` 5.0.0; `Newtonsoft.Json` 13.0.4; `Swashbuckle.AspNetCore.Swagger`, `Swashbuckle.AspNetCore.SwaggerGen`, `Swashbuckle.AspNetCore.SwaggerUI` 9.0.6; `System.Collections.Immutable` 8.0.0; `System.ComponentModel.Annotations` 5.0.0; `System.Diagnostics.DiagnosticSource` 10.0.4; `System.Diagnostics.EventLog` 9.0.19; `System.IdentityModel.Tokens.Jwt` 8.0.1 og 8.19.2; `System.Reflection.Emit.Lightweight` 4.7.0; `System.Reflection.Metadata` 8.0.0; `System.Security.AccessControl`, `System.Security.Principal.Windows` 5.0.0; `System.Threading.RateLimiting` 8.0.0 |

Direkte dependencies kan også forekomme transitively i andre projects. Deres license er fortsatt den som er registrert i tabellen over direct dependencies.

NuGet markerer `Microsoft.NETCore.Platforms` 5.0.0 og `System.Security.AccessControl` 5.0.0 som legacy. De godtas bare som exact-version transitive dependencies i test-projects og er ikke en del av application runtime graph. `scripts/check-nuget-deprecations.ps1` avviser disse pakkene hvis de blir direct/runtime dependencies, og avviser alle andre deprecations frem til de er oppgradert eller eksplisitt gjennomgått.

## FHIR profile-validation toolchain

FHIR profile validation er build/test tooling og inngår ikke i application runtime:

| Component | Pinned version | Declared license | Use |
| --- | --- | --- | --- |
| [Firely Terminal](https://www.nuget.org/packages/Firely.Terminal/3.5.0) | 3.5.0 | BSD-3-Clause | Validator CLI |
| `hl7.fhir.r4.core` | 4.0.1 | CC0-1.0 | FHIR R4 validation definitions |
| `hl7.terminology.r4` | 7.1.0 | CC0-1.0 | FHIR terminology definitions |
| `hl7.fhir.uv.extensions.r4` | 5.2.0 | CC0-1.0 | FHIR extensions dependency |
| `hl7.fhir.uv.tools.r4` | 0.9.0 | CC0-1.0 | IG tooling dependency |
| `hl7.fhir.no.domain.vitalsigns` | 0.9.74 | CC0-1.0, med separate LOINC/SNOMED IP notices | Norsk Vital Signs profile validation |
| `hl7.fhir.no.basis` | 2.2.2 | Package manifest declares no license | Required Norwegian base-profile validation dependency; legal terms must be confirmed before redistribution |

Den offisielle Vital Signs `package.tgz` lastes bare ned under validation og avvises hvis SHA-256 avviker fra den pinnede verdien i `scripts/validate-fhir-profiles.ps1`. Package-cache og den nedlastede filen distribueres ikke fra repositoryet. Bruk av LOINC og SNOMED CT følger IP-notices i den publiserte norske implementation guiden.

## Dependencies for Go authentication gateway

Inbound HelseID/DPoP gateway bruker bare standard open-source licenses. `AxisCommunications/go-dpop` er DPoP implementation som HelseID anbefaler for Go APIs. Repositoryet legger HelseID-spesifikke kontroller for access token, proof age, replay, scope og deployment boundary rundt denne.

| Module | Version | License | Use |
| --- | --- | --- | --- |
| [AxisCommunications/go-dpop](https://github.com/AxisCommunications/go-dpop) | 1.1.2 | MIT | Runtime DPoP validation |
| [MicahParks/keyfunc](https://github.com/MicahParks/keyfunc) | 3.8.1 | Apache-2.0 | Runtime HelseID JWKS loading and rotation |
| [golang-jwt/jwt](https://github.com/golang-jwt/jwt) | 5.3.1 | MIT | Runtime access-token validation |
| [redis/go-redis](https://github.com/redis/go-redis) | 9.22.0 | BSD-2-Clause | Runtime atomic multi-replica replay store |

Resolved runtime transitive modules er `MicahParks/jwkset` 0.11.1 (Apache-2.0), `cespare/xxhash/v2` 2.3.0 (MIT), `go.uber.org/atomic` 1.11.0 (MIT), samt `golang.org/x/sys` 0.47.0 og `golang.org/x/time` 0.15.0 (BSD-3-Clause). `alicebob/miniredis/v2` 2.38.0 og `yuin/gopher-lua` 1.1.1 er test-only og linkes ikke inn i gateway binary. Andre test-only checksums kan finnes i `go.sum`, fordi upstream modules deklarerer egne tests.

Eksakt runtime module allowlist som håndheves av CI er:

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

Docker build bruker disse immutable image references:

| Image | Rolle | License note |
| --- | --- | --- |
| `mcr.microsoft.com/dotnet/sdk:9.0.317-bookworm-slim@sha256:35048e3a81e6a07c316e7bbbd80d80d2ba705fe5f23a8ed42b6638c8f4c20d30` | Bare build stage | .NET Docker repository er MIT, mens image inkluderer .NET, PowerShell, en Linux base og andre components under egne licenses. |
| `mcr.microsoft.com/dotnet/aspnet:9.0.19-bookworm-slim@sha256:4e376dd15bbc8437d4892367ab0ea06a3ac9fea482d10f92f3c493fe1a2219ad` | Distribuert runtime image | Inkluderer `/usr/share/dotnet/LICENSE.txt` og `/usr/share/dotnet/ThirdPartyNotices.txt`. Linux packages er fortsatt underlagt sine individuelle licenses. |
| `golang:1.27.0-alpine@sha256:4c9fe60190a2a3350ddc51de80d0224b8a6698d12bdfc999fee45ea9d6c46dbc` | Bare gateway build stage | Go er BSD-3-Clause. Alpine packages beholder sine individuelle licenses. Distribuert gateway runtime er `scratch` med binary og CA certificate bundle. |

Microsofts [container legal notice](https://aka.ms/mcr/osslegalnotice) forklarer at container contents kan bruke flere licenses. [.NET image license-discovery guide](https://github.com/dotnet/dotnet-docker/blob/main/documentation/image-artifact-details.md) beskriver hvordan innebygde notices og Linux package metadata kan inspiseres.

Build stages er pinned by digest. For en release skal det genereres og oppbevares en image SBOM/license report for disse eksakte digestene. Denne inventory på repository-level lister ikke alle operating-system packages i images.

## Build- og CI tooling

Disse components støtter build eller CI og linkes ikke inn i application binaries:

| Component | Version/reference | License |
| --- | --- | --- |
| [.NET SDK](https://github.com/dotnet/sdk) | 9.0.317 med latest patch roll-forward | MIT |
| [actions/checkout](https://github.com/actions/checkout) | `3d3c42e5aac5ba805825da76410c181273ba90b1` (`v7`) | MIT |
| [actions/setup-dotnet](https://github.com/actions/setup-dotnet) | `a98b56852c35b8e3190ac28c8c2271da59106c68` (`v6`) | MIT |
| [actions/setup-go](https://github.com/actions/setup-go) | `924ae3a1cded613372ab5595356fb5720e22ba16` (`v6`) | MIT |

GitHub-hosted runner software, Docker tooling og eksterne DHG/HelseID-services reguleres av sine respektive vilkår og redistribueres ikke av dette repositoryet.

## FHIR specification material

Applikasjonen bruker Firely .NET SDK packages under BSD-3-Clause. FHIR names, definitions, code systems og specification material kan også være underlagt HL7-vilkår som er separate fra SDK-ens software license. Se [FHIR license and legal terms](https://hl7.org/fhir/license.html) ved redistribusjon av specification content eller derived artifacts.

## LOINC terminology content

This material contains content from LOINC (http://loinc.org). LOINC is copyright © Regenstrief Institute, Inc. and the Logical Observation Identifiers Names and Codes (LOINC) Committee and is available at no cost under the license at http://loinc.org/license. LOINC® is a registered United States trademark of Regenstrief Institute, Inc.

Repositoryet inkluderer bare et lite, eksplisitt sett LOINC identifiers og deres offisielle English display names. Se [LOINC License](https://loinc.org/license) for gjeldende terms. UCUM codes som brukes i FHIR messages er underlagt [UCUM terms](https://unitsofmeasure.org).

## SNOMED CT terminology content

This material includes SNOMED Clinical Terms® (SNOMED CT®), which is used by permission of SNOMED International. All rights reserved. SNOMED® and SNOMED CT® are registered trademarks of SNOMED International.

Repositoryet inkluderer et lite, eksplisitt sett active SNOMED CT concept identifiers og English display terms for FHIR interoperability. Bruk og distribution av SNOMED CT content krever relevant Affiliate/National License. Norge er et SNOMED International Member country, men implementer og deployer er fortsatt ansvarlig for korrekt registration og license compliance. Se [Helsedirektoratets veiledning](https://www.helsedirektoratet.no/digitalisering-og-e-helse/snomed-ct/hvordan-ta-i-bruk-snomed-ct), [SNOMED International licensing](https://www.snomed.org/get-snomed) og [SNOMED CT URI standard](https://docs.snomed.org/snomed-ct-specifications/snomed-ct-uri-standard/2-snomed-ct-uri-space).

## Prosedyre for oppdatering

Etter endring av package versions skal du restore med repository SDK og inspisere komplett graph:

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

For hvert unike package/version pair må `<license>`- eller `<licenseUrl>`-verdien verifiseres i installert packages `.nuspec`. Kontroller også runtime container på nytt etter digest, og oppdater dato, counts, versions og exceptions i denne filen.
