# syntax=docker/dockerfile:1

# Build from the repository root:
#   docker build -f docker/Dockerfile -t unattendhost .
#
# Base images are pinned by digest. To move to a newer image, pull the tag and copy the digest from
#   docker image inspect --format '{{index .RepoDigests 0}}' <image:tag>

# mcr.microsoft.com/dotnet/sdk:10.0 (SDK 10.0.402 when pinned)
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build

# The upstream commit this image must be built from. Empty means: the commit recorded for
# external/unattend-generator in the repository's HEAD.
ARG EXPECTED_UPSTREAM_SHA=

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

WORKDIR /src
COPY . .

# Stop unless the upstream submodule is present, unmodified, and at the expected commit.
RUN set -eu; \
    recorded="$(git ls-tree HEAD external/unattend-generator | awk '{print $3}')"; \
    expected="${EXPECTED_UPSTREAM_SHA:-$recorded}"; \
    actual="$(git -C external/unattend-generator rev-parse HEAD 2>/dev/null || true)"; \
    if [ -z "$actual" ]; then echo "error: external/unattend-generator is missing; clone with --recurse-submodules" >&2; exit 1; fi; \
    if [ -z "$expected" ]; then echo "error: HEAD records no upstream commit; pass --build-arg EXPECTED_UPSTREAM_SHA=<sha>" >&2; exit 1; fi; \
    if [ "$actual" != "$expected" ]; then echo "error: upstream is at $actual but $expected is expected" >&2; exit 1; fi; \
    if [ -n "$(git -C external/unattend-generator status --porcelain)" ]; then echo "error: external/unattend-generator has local changes" >&2; exit 1; fi; \
    echo "upstream commit: $actual"

RUN dotnet restore UnattendHost.slnx --locked-mode

# A failing test fails the build, so no image is produced.
RUN dotnet test --solution UnattendHost.slnx --no-restore -c Release

RUN dotnet publish src/UnattendHost.Web/UnattendHost.Web.csproj --no-restore -c Release -o /app \
    && cp -r presets /app/presets


# mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled: no shell or package manager,
# runs as the non-root user "app" (UID 1654).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:5165aeeb688df15e869a56a7584bb94bf16a14e0b3ba77b49fcdbe350f6b8020

WORKDIR /app
COPY --from=build /app .

ENV UnattendHost__PresetDirectory=/app/presets

USER $APP_UID
EXPOSE 8080

# The image has no curl; the app checks its own /healthz endpoint.
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD ["dotnet", "/app/UnattendHost.Web.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "UnattendHost.Web.dll"]
