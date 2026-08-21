FROM mcr.microsoft.com/dotnet/sdk:9.0.317-bookworm-slim@sha256:35048e3a81e6a07c316e7bbbd80d80d2ba705fe5f23a8ed42b6638c8f4c20d30 AS build
WORKDIR /src
COPY . .
RUN dotnet restore PopulationDataFacade.slnx --locked-mode \
    && dotnet publish src/PopulationDataFacade.Api/PopulationDataFacade.Api.csproj \
       --no-restore --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0.19-bookworm-slim@sha256:4e376dd15bbc8437d4892367ab0ea06a3ac9fea482d10f92f3c493fe1a2219ad AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY LICENSE THIRD-PARTY-NOTICES.md /licenses/
LABEL org.opencontainers.image.source="https://github.com/helgaarm/FhirGravidAPI" \
      org.opencontainers.image.licenses="MIT AND Apache-2.0 AND BSD-3-Clause"
ENV ASPNETCORE_HTTP_PORTS=8081
EXPOSE 8081
USER $APP_UID
ENTRYPOINT ["dotnet", "PopulationDataFacade.Api.dll"]
