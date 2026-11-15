#!/usr/bin/env bash
# Runs backend (xUnit + coverlet, Cobertura) and frontend (Vitest, LCOV) tests. Coverage goes to ./coverage.
# Backend tests use SQLite by default; set S2Q_TEST_CONNECTION to a PostgreSQL connection string to run them on PostgreSQL.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p coverage

dotnet test backend/IncidentApp.sln --collect:"XPlat Code Coverage" --results-directory coverage/backend-raw \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
cp "$(find coverage/backend-raw -name 'coverage.cobertura.xml' | head -n 1)" coverage/backend.cobertura.xml

(cd frontend && npm ci && npx vitest run --coverage --coverage.reportsDirectory=../coverage/frontend)
echo "Coverage reports: coverage/backend.cobertura.xml, coverage/frontend/lcov.info"
