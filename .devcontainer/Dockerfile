FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      bash ca-certificates curl git jq \
      python3 python3-pip \
      shellcheck shfmt \
 && rm -rf /var/lib/apt/lists/*

RUN python3 -m pip install --no-cache-dir pre-commit

ENV EDITOR=code
