#!/usr/bin/env bash
# Starts the published app in the foreground. Requires scripts/build.sh to have run and ConnectionStrings__Default to be set.
# Without Jwt__SigningKey you must set S2Q_TEST_MODE=true (a fixed development key is then used).
set -euo pipefail
cd "$(dirname "$0")/../publish"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:8080}"
exec dotnet IncidentApp.Api.dll
