# IncidentDesk — S2Q human-written reference implementation

> **This is the human-written reference baseline for the Spec-to-Quality (S2Q) benchmark. It is *not* a model-generated run, and nothing here is a result of the experiment.**
> It was written against [`../docs/benchmark-spec.md`](../docs/benchmark-spec.md) (**v1.0.0-rc1**) so the hidden acceptance suite can be validated against a working implementation before any model runs (Phase 2 exit criterion), and so that anyone can see what a straightforward implementation of the spec looks like. It has **not** been run through SonarQube, mutation testing or the hidden acceptance suite; no quality metrics are claimed for it. If you cite it, call it the reference baseline and state the spec version.

## What it is

An internal incident-management app (spec "IncidentDesk"): declare, triage, escalate, mitigate, resolve, close and reopen incidents against services owned by teams, with role-based permissions, SLA deadlines and two-level escalation driven by an injectable clock, an append-only audit trail, a simulated notification outbox, a dashboard, a REST API (`/api/v1`) and a React UI that follows the spec's `data-testid` contract.

| Part | Technology |
|---|---|
| Backend | ASP.NET Core Web API (minimal APIs) on **.NET 10 (LTS)**, EF Core 10, JWT bearer auth, PBKDF2 password hashing |
| Database | **PostgreSQL** (EF Core migrations applied at startup). SQLite fallback for local dev and tests: used automatically when no PostgreSQL connection string is given |
| Frontend | TypeScript + React 19 + React Router, built with Vite; served by the backend from `wwwroot` on the same origin (SPA fallback) |
| Tests | xUnit (backend), Vitest + Testing Library (frontend) |

## Layout

```
reference-app/
  backend/
    IncidentApp.sln
    src/IncidentApp.Api/
      Domain/           entities, enums, SLA calculator, state machine, permission matrix (pure, no I/O)
      Application/      use-case services, request validation, DTOs, error type
      Infrastructure/   EF Core DbContext + PostgreSQL migrations, clock, seed/reset, password hasher
      Api/              endpoint mapping, error middleware
      Program.cs        configuration, DI, pipeline
    tests/IncidentApp.Tests/   Domain/ (unit) and Api/ (HTTP, WebApplicationFactory)
  frontend/             Vite + React app; tests in src/test
  scripts/              build.sh, test.sh, start.sh (spec section 2)
  e2e/smoke.mjs         optional browser smoke test (Playwright core + Chrome)
  Dockerfile, docker-compose.yml
```

## Configuration (environment variables)

| Variable | Meaning |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string (`Host=…`). If absent, SQLite file `incidentdesk.db` in the working directory |
| `S2Q_TEST_MODE` | `true` enables `/api/v1/test/*` (clock control, reset) and disables the 30 s background SLA timer. Default `false` (the test endpoints return 404) |
| `Jwt__SigningKey` | JWT signing key (≥ 32 chars). Required unless `S2Q_TEST_MODE=true`, in which case a fixed development key is used. Startup fails if it is missing outside test mode |
| `ASPNETCORE_URLS` | Listen address, default `http://0.0.0.0:8080` |

Seed users all use the public fixture password `S2Q-Demo-2026!` (spec §4): `admin`, `cmd.platform`, `cmd.payments`, `cmd.identity`, `resp.platform1`, `resp.platform2`, `resp.payments1`, `resp.identity1`, `reporter1`, `viewer1`.

## Run it

**Docker (needs Docker with Compose):**
```bash
docker compose up --build        # app on http://localhost:8080, PostgreSQL 17 alongside
```
(The compose file sets `S2Q_TEST_MODE=true` so the test endpoints are available; set it to `"false"` for a normal run. Not verified in the authoring environment: Docker was not available there; the same app was verified against a real PostgreSQL server directly, see "What was verified".)

**Local (needs .NET 10 SDK and Node ≥ 20):**
```bash
scripts/build.sh                  # frontend -> backend wwwroot, then dotnet publish -> ./publish
export S2Q_TEST_MODE=true         # or set Jwt__SigningKey
export ConnectionStrings__Default="Host=localhost;Database=incidentdesk;Username=postgres;Password=…"   # omit for SQLite
scripts/start.sh                  # http://localhost:8080
```
For development: `cd backend/src/IncidentApp.Api && S2Q_TEST_MODE=true dotnet run` and, in another shell, `cd frontend && npm install && npm run dev` (Vite proxies `/api` to port 8080).

## Test it

```bash
scripts/test.sh                   # backend + frontend tests, coverage in ./coverage (Cobertura + LCOV)
# or separately:
dotnet test backend/IncidentApp.sln
(cd frontend && npm test)
# run the same API suite against PostgreSQL:
S2Q_TEST_CONNECTION="Host=localhost;Port=5432;Database=incidentdesk_test;Username=postgres;Password=…" dotnet test backend/IncidentApp.sln
```
The API tests start the app in-process in test mode, call `POST /api/v1/test/reset` before every test and control time through the test clock, so nothing sleeps. **Use a throw-away database for `S2Q_TEST_CONNECTION`: the suite deletes all data.**

Optional browser smoke test: start the app with `S2Q_TEST_MODE=true`, then `cd e2e && npm install && BASE_URL=http://127.0.0.1:8080 CHROME_PATH=/path/to/chrome node smoke.mjs`.

## Design notes

- **Pure domain core.** `SlaCalculator`, `IncidentRules` (state machine) and `Permissions` have no I/O and take time as a parameter, so the spec's boundary rules (inclusive deadlines, 80 % at-risk, escalation order) are unit-tested exactly.
- **One clock.** Everything uses `IClock`; test mode can freeze/advance it. Setting or advancing the clock runs `EvaluateSla` for all incidents (spec 7.3a); every state-changing request evaluates the target incident first; a 30 s background timer covers real use.
- **Normative check order (spec 5.4)** is implemented literally in each use case: role → path lookup → scope → malformed JSON → field validation → business rules → state/conflict. Request bodies are therefore parsed late and field-by-field (`Fields`), not by model binding.
- **Atomicity and concurrency.** All writes of a request (incident, notes, audit events, notifications) are saved in one transaction; a rejected request saves nothing. All state-changing work runs under one in-process write gate, which is what guarantees "exactly one of two simultaneous identical transitions wins". This is correct for a single instance only; it is not a multi-instance design. `Incident.Version` is also an EF concurrency token.
- **Dates.** UTC everywhere, serialised with millisecond precision and `Z`.

## What was verified (authoring environment, 2026-11-15)

- Backend (.NET SDK 10.0.x): `dotnet build` with 0 warnings / 0 errors; **188 xUnit tests pass** (89 domain/unit, 99 HTTP API tests) on SQLite **and** on a real PostgreSQL server (embedded PostgreSQL binaries, EF migration applied by the app at startup). Backend line coverage from the Cobertura report: ~56 % lines / ~87 % branches (the figure includes the generated EF migration files).
- Frontend: `tsc --noEmit` and `vite build` succeed; **41 Vitest component/unit tests pass** (~94 % line coverage).
- Published build run with `scripts/start.sh` against PostgreSQL in production mode (no test mode): `/` and SPA routes serve the app, unknown `/api` routes return the 404 error shape, `/api/v1/test/*` returns 404; `scripts/build.sh` and `scripts/test.sh` run clean from the repo state.
- Browser smoke (`e2e/smoke.mjs`, Playwright core driving Chrome) against the published build on PostgreSQL: **22/22 checks pass** (login, create with client validation, full lifecycle through dialogs, ACTION_ITEM_REQUIRED toast, action items, audit order, dashboard, role gating, logout, not-found).
- 500 incidents created through the API: `GET /incidents` default page answered in a few milliseconds (NFR-06 smoke).
- A spot check found no seed password or password hash in the process log (AC-150 style).

**Not done:** Docker/compose run (Docker unavailable), SonarQube analysis, mutation testing, the hidden acceptance suite (does not exist yet), cross-browser testing, a formal AC-by-AC trace (the tests were written against the spec's behaviour rules and the §15 criteria were reviewed against the code by hand).

## Known deviations and spec ambiguities

Resolved here by the choices below; each is a candidate for a spec patch :

1. The spec says PostgreSQL is the *only* database; this reference additionally supports SQLite for local dev/tests (as requested for the reference). Migrations are generated for PostgreSQL only; SQLite builds its schema from the model.
2. `version` is incremented once per request that changes the incident row (including adding a note or action item, which touch `updatedAt`), and once more when a request-time SLA evaluation escalates the incident.
3. `expectedVersion` is checked before the transition validity check; it is also accepted by the notes and action-item endpoints.
4. A reporter editing a non-OPEN incident, or someone else's, gets 403 (scope check), not 409.
5. `escalationsApplied` counts level crossings (each `ESCALATED` event), not incidents.
6. `GET /health` is served at both `/health` and `/api/v1/health`.
