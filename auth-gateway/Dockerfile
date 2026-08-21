FROM golang:1.27.0-alpine@sha256:4c9fe60190a2a3350ddc51de80d0224b8a6698d12bdfc999fee45ea9d6c46dbc AS build
WORKDIR /src
COPY auth-gateway/go.mod auth-gateway/go.sum ./
RUN go mod download
COPY auth-gateway/*.go ./
RUN CGO_ENABLED=0 GOOS=linux go build -trimpath -ldflags="-s -w" -o /out/auth-gateway .

FROM scratch
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
COPY --from=build /out/auth-gateway /auth-gateway
COPY LICENSE THIRD-PARTY-NOTICES.md /licenses/
LABEL org.opencontainers.image.source="https://github.com/helgaarm/FhirGravidAPI" \
      org.opencontainers.image.licenses="MIT AND Apache-2.0 AND BSD-2-Clause AND BSD-3-Clause"
EXPOSE 8080
USER 65532:65532
ENTRYPOINT ["/auth-gateway"]
