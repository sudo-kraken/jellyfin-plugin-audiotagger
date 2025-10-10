FROM mcr.microsoft.com/devcontainers/dotnet:9.0-noble

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    bash ca-certificates curl git jq shellcheck shfmt python3 python3-pip \
  && rm -rf /var/lib/apt/lists/*

RUN python3 -m pip install --no-cache-dir pre-commit

ENV EDITOR=code
