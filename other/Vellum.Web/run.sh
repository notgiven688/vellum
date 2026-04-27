#!/usr/bin/env bash
set -euo pipefail

dotnet publish -c Release
dotnet serve \
  --mime .wasm=application/wasm \
  --mime .js=text/javascript \
  --mime .json=application/json \
  --directory ./bin/Release/net10.0/browser-wasm/AppBundle
