#!/usr/bin/env bash
# Restore, build the frontend into the backend's static-file location (wwwroot), then publish the backend to ./publish.
set -euo pipefail
cd "$(dirname "$0")/.."

(cd frontend && npm ci && npm run build)          # writes ../backend/src/IncidentApp.Api/wwwroot
dotnet restore backend/IncidentApp.sln
dotnet publish backend/src/IncidentApp.Api -c Release -o publish --no-restore
