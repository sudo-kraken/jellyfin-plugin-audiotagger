FROM mcr.microsoft.com/devcontainers/dotnet:9.0-noble

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip

RUN apt-get update && apt-get install -y --no-install-recommends \
    bash ca-certificates curl git jq yq zip unzip shellcheck shfmt \
  && rm -rf /var/lib/apt/lists/*
