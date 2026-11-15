# S2Q Benchmark Specification — Incident Management Application ("IncidentDesk")

| Field | Value |
|---|---|
| **Spec version** | **v1.0.0-rc1** (release candidate) |
| **Freeze date** | 2026-11-15 (America/Detroit) |
| **Status** | Frozen as a release candidate for review. Becomes `v1.0.0` only after (a) maintainer sign-off and (b) validation of the hidden acceptance suite against a reference implementation (see [experiment-roadmap.md](experiment-roadmap.md), Phase 2 exit criteria). |
| **Applies to** | Every model/harness configuration in the S2Q experiment (Stage A and Stage B). |
| **Derived from** | [benchmark-application.md](benchmark-application.md) (selection rationale) and [decisions.md](decisions.md) (locked stack/harness decisions). |

> **Change control.** After the `v1.0.0` tag, this document is immutable for the experiment. Any change — including a typo fix that alters meaning — creates a new version (`v1.0.1`, `v1.1.0`, …), is recorded in the changelog at the end of this file and in [decisions.md](decisions.md), and invalidates earlier runs for confirmatory comparison (earlier runs are kept, labelled with their spec version, and never silently mixed). While the version is `-rc`, changes are allowed but must be logged in the changelog. The hidden acceptance suite is versioned together with this spec; each acceptance criterion (`AC-nnn`) maps to one or more hidden tests, and AC numbering is stable within a major version. **This document contains no results, prices or measurements.**

## 0. How to read this specification

- **MUST / MUST NOT / SHOULD / MAY** have their RFC 2119 meanings. Only MUST-level statements and the acceptance criteria (§15) are tested.
- Everything not specified is a **design choice left to the implementer** (architecture, layering, library choices beyond §2, visual design, internal naming). The spec constrains *observable behaviour*: HTTP API, UI test-id contract, persisted semantics.
- The application is called **IncidentDesk**: an internal tool for declaring, triaging, escalating, mitigating, resolving and reviewing operational incidents against services owned by teams.
- All timestamps are **UTC**, serialised as ISO-8601 with millisecond precision and a `Z` suffix (e.g. `2026-03-01T12:00:00.000Z`). Durations in the API are **integer minutes** unless stated otherwise. All identifiers are **positive 64-bit integers** assigned by the server.
- Strings are UTF-8. "Trimmed" means leading/trailing Unicode whitespace removed. Length is counted in UTF-16 code units of the trimmed value.

## 1. Scope summary

Entities: **Team, User, Service, Incident, Note, ActionItem, AuditEvent, Notification**. Roles: **Viewer, Reporter, Responder, Commander, Admin**. Lifecycle: `OPEN → ACKNOWLEDGED → MITIGATED → RESOLVED → CLOSED`, plus `RESOLVED → ACKNOWLEDGED` (reopen). Time-based rules: acknowledge/resolve SLA deadlines by severity, SLA status (`RUNNING`, `AT_RISK`, `BREACHED`, `MET`) and two-level escalation, all driven by an **injectable clock**. Also: an append-only audit trail, a simulated notification outbox, a dashboard summary, a REST API (`/api/v1`) and a React UI with a fixed `data-testid` contract.

## 2. Technology and delivery contract

Locked decisions ([decisions.md](decisions.md) D-001); identical for all runs.

| Concern | Requirement |
|---|---|
| Backend | **C# / ASP.NET Core Web API** on the **current .NET LTS release at the freeze date (.NET 10)**; **Entity Framework Core** with migrations; **PostgreSQL** as the only database. The exact SDK patch version is pinned in the sandbox image (Phase 3), not here. |
| Frontend | **TypeScript + React** single-page app. Build tool and router/state libraries are the implementer's choice. |
| Serving | The backend MUST serve the built frontend as static files at `/` on the **same origin** as the API, with SPA fallback (any non-`/api` GET that does not match a static file returns `index.html`). Default listen address `http://0.0.0.0:8080` (overridable via `ASPNETCORE_URLS`). |
| Configuration | Environment variables only: `ConnectionStrings__Default` (PostgreSQL connection string, provided by the harness); `S2Q_TEST_MODE` (`true` enables the test endpoints of §7.5; default `false`); `Jwt__SigningKey` (optional; if absent in test mode a fixed development key MAY be used; outside test mode startup MUST fail if absent and a JWT is used). |
| Startup | On startup the app MUST apply EF Core migrations and, if the database has no users, insert the seed data of §4. `GET /health` MUST return 200 within **60 s** on an empty database. |
| External services | None. No outbound network calls at runtime. No paid or hosted dependencies. |
| Required scripts | `scripts/build.sh` (restore, build backend, build frontend into the backend's static-file location), `scripts/test.sh` (runs the implementer's own tests and writes coverage reports), `scripts/start.sh` (starts the built app in the foreground). Each MUST exit non-zero on failure and run from the repository root with only the pinned toolchains and a reachable PostgreSQL instance. |
| Required tests | The implementation MUST include its own automated tests (backend: xUnit with `coverlet` coverage in Cobertura format; frontend: Vitest or equivalent with LCOV/Cobertura coverage). These *generated tests* are what coverage and mutation testing measure; they are separate from the hidden acceptance suite. |
| Layout | Free, except: backend solution under `backend/`, frontend under `frontend/`, scripts under `scripts/`. |

## 3. Domain model

### 3.1 Team
`id`, `name` (unique). Read-only through the API.

### 3.2 User
`id`, `username` (unique, case-insensitive), `displayName`, `role` ∈ {`VIEWER`,`REPORTER`,`RESPONDER`,`COMMANDER`,`ADMIN`}, `teamId` (nullable; non-null for `RESPONDER` and `COMMANDER`, null for the others in the seed data), password (stored only as a salted, slow hash; MUST NOT be stored or logged in plain text). Users are **seeded only**: no registration, password change or user-management API/UI.

### 3.3 Service
`id`, `key` (unique, `^[A-Z][A-Z0-9-]{1,19}$`), `name` (unique case-insensitively, 2–80 chars), `ownerTeamId`, `tier` ∈ {1,2,3} (1 = most critical), `active` (boolean).

### 3.4 Incident
| Field | Type | Notes |
|---|---|---|
| `id` | int | |
| `number` | string | `"INC-"` + id zero-padded to ≥4 digits (`INC-0001`, `INC-12345`). Immutable. |
| `title` | string | 5–120 chars. |
| `description` | string | 0–4000 chars; empty string allowed; never null in responses. |
| `serviceId`, `serviceName` | int, string | `serviceName` denormalised in responses. |
| `teamId` | int | Owning team = `service.ownerTeamId` **at creation time**; immutable afterwards. |
| `severity` | enum | `SEV1` (most severe) … `SEV4`. |
| `status` | enum | `OPEN`, `ACKNOWLEDGED`, `MITIGATED`, `RESOLVED`, `CLOSED`. |
| `reporterId` | int | User who created it. |
| `assigneeId` | int \| null | |
| `createdAt`, `updatedAt` | timestamp | From the injectable clock. `updatedAt` changes on every successful mutation of the incident, its notes or its action items. |
| `acknowledgedAt` | timestamp \| null | Set at the **first** acknowledgement; never cleared. |
| `mitigatedAt`, `resolvedAt`, `closedAt` | timestamp \| null | `mitigatedAt` and `resolvedAt` are cleared on reopen; `closedAt` set on close. |
| `resolutionSummary` | string \| null | Set on resolve; kept after reopen (overwritten by the next resolve). |
| `escalationLevel` | int | 0, 1 or 2; never decreases. |
| `reopenCount` | int | Incremented on each reopen. |
| `version` | int | Starts at 1; incremented on every change to the incident row. |
| `sla` | object | Computed at read time (§7.2). |
| `actionItemCount`, `openActionItemCount` | int | Computed. |

### 3.5 Note
`id`, `incidentId`, `authorId`, `body` (1–2000 chars after trim), `createdAt`. Append-only.

### 3.6 ActionItem (post-incident action)
`id`, `incidentId`, `title` (3–200), `ownerId` (a user with role `RESPONDER`, `COMMANDER` or `ADMIN`), `dueDate` (`YYYY-MM-DD`), `status` ∈ {`OPEN`,`DONE`}, `createdAt`, `completedAt` (nullable).

### 3.7 AuditEvent
`id`, `incidentId` (nullable), `actorId` (nullable = system), `action` (§8.1), `createdAt`, `details` (JSON object). Append-only.

### 3.8 Notification (simulated outbox)
`id`, `recipientId`, `type` (§8.2), `incidentId`, `message` (non-empty, content free), `createdAt`. Nothing is delivered; the stored feed *is* the channel.

## 4. Seed data (inserted at first startup and on `POST /api/v1/test/reset`)

**Teams:** 1 Platform · 2 Payments · 3 Identity.

**Services**

| id | key | name | ownerTeamId | tier | active |
|---|---|---|---|---|---|
| 1 | API-GATEWAY | API Gateway | 1 | 1 | true |
| 2 | DATA-PIPELINE | Data Pipeline | 1 | 3 | true |
| 3 | PAYMENTS-CORE | Payments Core | 2 | 1 | true |
| 4 | BILLING-EXPORT | Billing Export | 2 | 3 | true |
| 5 | AUTH-SERVICE | Auth Service | 3 | 2 | true |
| 6 | LEGACY-REPORTS | Legacy Reports | 3 | 3 | false |

**Users** (all share the fixture password `S2Q-Demo-2026!`; a public test fixture, not a secret)

| id | username | displayName | role | teamId |
|---|---|---|---|---|
| 1 | admin | Ada Admin | ADMIN | null |
| 2 | cmd.platform | Casey Platform | COMMANDER | 1 |
| 3 | cmd.payments | Chris Payments | COMMANDER | 2 |
| 4 | cmd.identity | Cam Identity | COMMANDER | 3 |
| 5 | resp.platform1 | Robin Platform | RESPONDER | 1 |
| 6 | resp.platform2 | Riley Platform | RESPONDER | 1 |
| 7 | resp.payments1 | Rene Payments | RESPONDER | 2 |
| 8 | resp.identity1 | Remy Identity | RESPONDER | 3 |
| 9 | reporter1 | Rory Reporter | REPORTER | null |
| 10 | viewer1 | Val Viewer | VIEWER | null |

No incidents, notes, action items, audit events or notifications are seeded. Ids of new records start at 1 per table (first incident = `INC-0001`). Seeding writes **no** audit events.

## 5. Authentication, roles and permissions

### 5.1 Authentication
- `POST /api/v1/auth/login` exchanges `username` + `password` for a **bearer token** (JWT or opaque; format free). Clients send `Authorization: Bearer <token>`.
- Tokens remain valid for at least 8 hours of real (system) time; expiry is **not** tied to the injectable clock.
- Every endpoint except `GET /health`, `POST /api/v1/auth/login` and (test mode) `/api/v1/test/*` requires a valid token, else `401 UNAUTHENTICATED`.
- Login failure for an unknown user and for a wrong password MUST be indistinguishable (`401 INVALID_CREDENTIALS`, same message).

### 5.2 Definitions
**Own team:** `user.teamId == incident.teamId` (non-null). **Assignee:** `user.id == incident.assigneeId`. **Own incident:** `user.id == incident.reporterId`.

### 5.3 Permission matrix
`✔` allowed; `✘` → `403 FORBIDDEN`; `team` allowed only for incidents of the user's own team (else 403); `own` only own incidents; `asg` a Responder must additionally be the assignee (Commander/Admin need not).

| Action | Viewer | Reporter | Responder | Commander | Admin |
|---|---|---|---|---|---|
| Read teams, services, users, incident list/detail, notes, action items, dashboard | ✔ | ✔ | ✔ | ✔ | ✔ |
| Read incident audit trail (`GET /incidents/{id}/audit`) | ✘ | ✘ | team | team | ✔ |
| Read global audit (`GET /audit`) | ✘ | ✘ | ✘ | ✘ | ✔ |
| Read own notifications (`GET /notifications`) | ✔ | ✔ | ✔ | ✔ | ✔ |
| Read all users' notifications (`?scope=all`) | ✘ | ✘ | ✘ | ✘ | ✔ |
| Create incident | ✘ | ✔ | ✔ | ✔ | ✔ |
| Edit title/description (`PATCH`) | ✘ | own, status `OPEN` only | ✘ | team | ✔ |
| Add note | ✘ | own | team | team | ✔ |
| Acknowledge | ✘ | ✘ | team | team | ✔ |
| Mitigate | ✘ | ✘ | team+asg | team | ✔ |
| Resolve | ✘ | ✘ | team+asg | team | ✔ |
| Close | ✘ | ✘ | ✘ | team | ✔ |
| Reopen | ✘ | ✘ | ✘ | team | ✔ |
| Assign | ✘ | ✘ | ✘ | team | ✔ |
| Change severity | ✘ | ✘ | ✘ | team | ✔ |
| Create / update action item | ✘ | ✘ | team | team | ✔ |
| Create / update service | ✘ | ✘ | ✘ | ✘ | ✔ |
| Trigger SLA evaluation (`POST /sla/evaluate`) | ✘ | ✘ | ✘ | ✘ | ✔ |

### 5.4 Order of checks (normative)
For every request the first failing check wins:
1. Authentication → `401`.
2. Role-level permission (does this role *ever* have this action? — the ✘ cells) → `403`.
3. Path resource lookup → `404 NOT_FOUND`.
4. Scope permission (`team` / `own` / `asg`) → `403`.
5. Malformed JSON → `400 MALFORMED_REQUEST`.
6. Field validation → `422 VALIDATION_ERROR`.
7. Business rules returning 422 (`BUSINESS_RULE_VIOLATION`).
8. State and conflict rules → `409`.

## 6. Incident lifecycle (state machine)

```
OPEN --acknowledge--> ACKNOWLEDGED --mitigate--> MITIGATED --resolve--> RESOLVED --close--> CLOSED
                           ^                                                |
                           +------------------------reopen------------------+
```

Only the transitions below exist. Any other (action, current status) pair → `409 INVALID_TRANSITION` with `details: {"currentStatus": "...", "action": "..."}`. `CLOSED` is terminal.

| Action | From → To | Body | Effects and rules |
|---|---|---|---|
| `acknowledge` | OPEN → ACKNOWLEDGED | optional `expectedVersion` | Sets `acknowledgedAt = now`. If `assigneeId` is null and the actor is Responder/Commander, assignee := actor (an `ASSIGNED` audit event and the notification rules apply as for assign). If `assigneeId` is null and the actor is Admin → `409 ASSIGNEE_REQUIRED`. |
| `mitigate` | ACKNOWLEDGED → MITIGATED | `message` (1–2000, required) | Sets `mitigatedAt`. Appends `message` as a Note by the actor (same transaction). |
| `resolve` | MITIGATED → RESOLVED | `resolutionSummary` (10–2000, required) | Sets `resolvedAt`, `resolutionSummary`. Direct ACKNOWLEDGED → RESOLVED is **not** allowed. |
| `close` | RESOLVED → CLOSED | optional `expectedVersion` | Sets `closedAt`. Closure rules: (a) every action item MUST be `DONE`, else `409 OPEN_ACTION_ITEMS`; (b) if severity is `SEV1` or `SEV2`, at least one action item MUST exist, else `409 ACTION_ITEM_REQUIRED`. (a) is checked first. |
| `reopen` | RESOLVED → ACKNOWLEDGED | `reason` (5–500, required) | Clears `resolvedAt` and `mitigatedAt`; `reopenCount += 1`; `acknowledgedAt` unchanged; SLA deadlines **unchanged** (the resolve timer runs again, §7.2). Appends `reason` as a Note. |

Other mutations and their state constraints (violations → `409 INVALID_STATE`):

| Operation | Allowed statuses |
|---|---|
| Assign | OPEN, ACKNOWLEDGED, MITIGATED |
| Change severity | OPEN, ACKNOWLEDGED, MITIGATED |
| Edit title/description | any except CLOSED |
| Add note | any except CLOSED |
| Create/update action item | any except CLOSED |

**Assign:** body `assigneeId` (required). The assignee MUST be a `RESPONDER` or `COMMANDER` whose `teamId` equals the incident's `teamId`; else `422 VALIDATION_ERROR` (field `assigneeId`). Re-assigning to the current assignee is a no-op returning 200 that writes **no** audit event or notification.

**Change severity:** body `severity` (required), `reason` (5–500, required). Same severity as current → `422`. Recomputes SLA deadlines from `createdAt` with the new severity's targets (§7.1). Appends `reason` as a Note. Subject to the SEV1 tier rule (§9.2).

**Optimistic concurrency:** every state-changing incident endpoint accepts an optional integer `expectedVersion`; if present and different from the current `version` → `409 VERSION_CONFLICT`. Regardless of `expectedVersion`, concurrent state-changing requests on one incident MUST be serialised so that of two simultaneous identical transitions (e.g. two `acknowledge`) exactly one succeeds and the other gets `409 INVALID_TRANSITION` or `409 VERSION_CONFLICT`.

## 7. SLA rules, escalation and the injectable clock

### 7.1 Targets (minutes from `createdAt`)

| Severity | Acknowledge target | Resolve target |
|---|---|---|
| SEV1 | 5 | 60 |
| SEV2 | 15 | 240 |
| SEV3 | 60 | 1440 |
| SEV4 | 240 | 4320 |

`ackDeadline = createdAt + ackTarget`; `resolveDeadline = createdAt + resolveTarget`. Timers never pause (no business-hours calendars).

### 7.2 SLA object (computed at read time with the clock's `now`)

```json
"sla": {
  "ack":     { "targetMinutes": 5,  "deadline": "…Z", "status": "RUNNING|AT_RISK|BREACHED|MET" },
  "resolve": { "targetMinutes": 60, "deadline": "…Z", "status": "RUNNING|AT_RISK|BREACHED|MET" },
  "breached": false
}
```

Let `T` = `acknowledgedAt` for the ack timer and `resolvedAt` for the resolve timer (null if unset). Rows are evaluated in order:

| Condition | Timer status |
|---|---|
| `T` not null and `T <= deadline` | `MET` |
| `T` not null and `T > deadline` | `BREACHED` |
| `T` null and `now > deadline` | `BREACHED` |
| `T` null and `now >= createdAt + 0.8 × target` | `AT_RISK` |
| otherwise | `RUNNING` |

Boundaries are inclusive for compliance: acknowledging exactly at the deadline is `MET`; `now == deadline` is not breached. `sla.breached = (ack.status == BREACHED) or (resolve.status == BREACHED)`. After a reopen `resolvedAt` is null, so the resolve timer is evaluated against `now` again (it can become `BREACHED`). The resolve timer of a `CLOSED` incident reflects its last `resolvedAt`.

### 7.3 Escalation
`escalationLevel` only moves upward and is set by **`EvaluateSla(now)`**:

- **Level 1** when `status == OPEN` and `now > ackDeadline` and `escalationLevel < 1`.
- **Level 2** when `status ∈ {OPEN, ACKNOWLEDGED, MITIGATED}` and `now > resolveDeadline` and `escalationLevel < 2`.
- If both hold in one evaluation, level 1 is applied first, then level 2 (two audit events, two notification batches, in that order). Level 2 may be reached without level 1 (e.g. acknowledged in time, resolve target missed).
- Each level crossing writes one `ESCALATED` audit event (`actorId` null, `details: {"level": n}`) and the notifications of §8.2.
- Resolved/closed incidents are never newly escalated. Severity changes never lower the level and trigger no retroactive events; later evaluations use the new deadlines.

`EvaluateSla` is idempotent and is invoked: (a) for **all** incidents whenever the test clock is set or advanced (§7.5); (b) for all incidents by a background timer at least every 30 s of real time (production use; MUST be disabled or harmless in test mode); (c) by `POST /api/v1/sla/evaluate` (Admin); (d) for the **target incident** at the start of every state-changing request on it, before the action is applied, using the same `now`. GET requests never write.

### 7.4 Clock abstraction
All business time MUST come from one injectable clock abstraction (e.g. `IClock.UtcNow`). Normally it returns system UTC time; in test mode it additionally supports a **fixed** mode (§7.5). No business logic may call the system clock directly. Time arithmetic is exact to the millisecond.

### 7.5 Test endpoints (exist only when `S2Q_TEST_MODE=true`, otherwise MUST return `404`; no authentication)

| Endpoint | Behaviour |
|---|---|
| `GET /api/v1/test/clock` | `200 {"now": "…Z", "mode": "system" \| "fixed"}` |
| `PUT /api/v1/test/clock` | Body `{"now": "<ISO-8601 UTC>"}`. Switches to `fixed` mode at that instant (time stops advancing), runs `EvaluateSla`, returns `200` with the GET body. Invalid timestamp → `422`. |
| `POST /api/v1/test/clock/advance` | Body `{"minutes": <int ≥ 0>}` and/or `{"seconds": <int ≥ 0>}` (at least one). In `system` mode it first fixes the clock at the current system time, then advances. Runs `EvaluateSla`. `200` with the GET body. Negative or non-integer values → `422`. |
| `POST /api/v1/test/reset` | Deletes all data, re-applies the seed (§4) including id sequences, returns the clock to `system` mode. `204`. |

## 8. Audit trail and notifications

### 8.1 Audit events
Every **successful** state-changing operation writes **exactly one** audit event per logical change, in the same database transaction as the change; rejected operations write none.

| `action` | When | `details` |
|---|---|---|
| `INCIDENT_CREATED` | create | `{"severity","serviceId"}` |
| `INCIDENT_UPDATED` | PATCH with ≥1 changed field | `{"changedFields":["title"\|"description"]}` |
| `ACKNOWLEDGED`, `MITIGATED`, `RESOLVED`, `CLOSED`, `REOPENED` | transitions | `{"from":"<status>","to":"<status>"}` |
| `ASSIGNED` | assignee changed (incl. auto-assignment on acknowledge) | `{"from":<id\|null>,"to":<id>}` |
| `SEVERITY_CHANGED` | severity change | `{"from","to","reason"}` |
| `NOTE_ADDED` | explicit note via `POST …/notes` only (not the implicit notes of mitigate/reopen/severity) | `{"noteId"}` |
| `ACTION_ITEM_CREATED`, `ACTION_ITEM_UPDATED` | action items | `{"actionItemId"}` |
| `ESCALATED` | escalation | `{"level"}` |
| `SERVICE_CREATED`, `SERVICE_UPDATED` | service admin (`incidentId` null) | `{"serviceId"}` |

The trail is append-only (no endpoint or UI updates or deletes events); `id` is strictly increasing and defines order; `createdAt` comes from the injectable clock; `actorId` is the authenticated user (null only for `ESCALATED`). A request with two logical changes (acknowledge with auto-assignment) writes `ASSIGNED` first, then `ACKNOWLEDGED`.

### 8.2 Notifications
Recipients are de-duplicated per event and the **actor never notifies themself**.

| `type` | Trigger | Recipients |
|---|---|---|
| `INCIDENT_CREATED` | create | all `COMMANDER` users with `teamId == incident.teamId` |
| `ASSIGNED` | assignee changed | the new assignee |
| `ESCALATION_L1` | level 1 reached | commanders of the incident's team |
| `ESCALATION_L2` | level 2 reached | commanders of the incident's team and all `ADMIN` users |
| `RESOLVED` | resolve | the reporter |
| `CLOSED` | close | the reporter |

(Escalations have no actor, so nobody is excluded.)

## 9. Validation rules

### 9.1 General
- Strings are trimmed before validation and persisted trimmed. A required string that is missing, null, wrong-typed or blank → `422` field error. Unknown JSON properties are ignored.
- Enum values are case-sensitive upper-case strings. Unknown enum → `422`.
- A reference in a **request body** to a non-existent entity (`serviceId`, `assigneeId`, `ownerId`) → `422`, field error code `not_found`. A non-existent entity in the **URL path** → `404`. Non-integer or non-positive path ids → `404 NOT_FOUND`.
- Query parameter errors → `422` with `field` = the parameter name.

### 9.2 Incident business rules
1. **Inactive service:** creating an incident for a service with `active=false` → `422 BUSINESS_RULE_VIOLATION`, `details.rule = "SERVICE_INACTIVE"`.
2. **SEV1 tier rule:** `SEV1` may only be used with services of `tier` 1 or 2 (on create and on severity change); otherwise `422 BUSINESS_RULE_VIOLATION`, `details.rule = "SEV1_REQUIRES_TIER_1_OR_2"`.
3. **Duplicate rule:** an incident MUST NOT be created (or retitled via PATCH) if another incident with the same `serviceId` and the same title (compared after trim, case-insensitive, ordinal) has status `OPEN`, `ACKNOWLEDGED` or `MITIGATED` → `409 DUPLICATE_INCIDENT`, `details.existingIncidentId`. Resolved/closed incidents do not count.

Rules 1–2 are step 7 of §5.4; rule 3 is step 8.

### 9.3 Field summary
| Field | Rule |
|---|---|
| Incident.title | required, 5–120 |
| Incident.description | optional, ≤ 4000 |
| Incident.severity | required enum on create |
| Incident.serviceId | required, must exist |
| Note.body | required, 1–2000 |
| ActionItem.title | required, 3–200 |
| ActionItem.ownerId | required, existing user with role RESPONDER/COMMANDER/ADMIN |
| ActionItem.dueDate | required `YYYY-MM-DD`; on **create** must be ≥ the UTC date of `clock.now`; on update validated only when changed |
| Service.key | required, `^[A-Z][A-Z0-9-]{1,19}$`, unique → `409 DUPLICATE_SERVICE` |
| Service.name | required, 2–80, unique case-insensitive → `409 DUPLICATE_SERVICE` |
| Service.ownerTeamId | required, existing team |
| Service.tier | required, 1–3 |
| Login.username/password | both required, non-blank |

## 10. REST API contract

Base path `/api/v1`. JSON bodies, `Content-Type: application/json; charset=utf-8`. Property names are **camelCase**. Enums are the upper-case strings above. Timestamps as §0.

### 10.1 Error format (all non-2xx responses under `/api`)
```json
{
  "code": "VALIDATION_ERROR",
  "message": "One or more fields are invalid.",
  "status": 422,
  "traceId": "any-non-empty-string",
  "errors": [ { "field": "title", "code": "too_short", "message": "Title must be at least 5 characters." } ],
  "details": { }
}
```
`code`, `message`, `status` and `traceId` are always present. `errors` is present (possibly empty) for `VALIDATION_ERROR`; `details` only where this spec defines it. Field-error `code` values used by acceptance tests: `required`, `too_short`, `too_long`, `invalid_format`, `invalid_value`, `not_found`, `out_of_range`. Messages are free text.

| HTTP | `code` values |
|---|---|
| 400 | `MALFORMED_REQUEST` |
| 401 | `UNAUTHENTICATED`, `INVALID_CREDENTIALS` |
| 403 | `FORBIDDEN` |
| 404 | `NOT_FOUND` (unknown routes under `/api` too) |
| 405 | `METHOD_NOT_ALLOWED` |
| 409 | `INVALID_TRANSITION`, `INVALID_STATE`, `DUPLICATE_INCIDENT`, `DUPLICATE_SERVICE`, `VERSION_CONFLICT`, `ASSIGNEE_REQUIRED`, `OPEN_ACTION_ITEMS`, `ACTION_ITEM_REQUIRED` |
| 422 | `VALIDATION_ERROR`, `BUSINESS_RULE_VIOLATION` |
| 500 | `INTERNAL_ERROR` (no stack traces or internals in the body) |

### 10.2 Endpoint catalogue

**Health and auth**

| Method & path | Request | Success |
|---|---|---|
| `GET /health` (no auth) | — | `200 {"status":"ok"}` |
| `POST /auth/login` | `{"username","password"}` | `200 {"token","user":User}` |
| `GET /auth/me` | — | `200 User` |

`User` = `{"id","username","displayName","role","teamId"}`.

**Reference data**

| Method & path | Request | Success |
|---|---|---|
| `GET /teams` | — | `200 [Team]` by id; `Team` = `{"id","name"}` |
| `GET /users` | — | `200 [User]` by id |
| `GET /services` | optional `?active=true\|false` | `200 [Service]` by id; `Service` = `{"id","key","name","ownerTeamId","tier","active"}` |
| `GET /services/{id}` | — | `200 Service` |
| `POST /services` (Admin) | `{"key","name","ownerTeamId","tier","active"?}` (`active` default true) | `201 Service` + `Location` header |
| `PUT /services/{id}` (Admin) | full replacement `{"key","name","ownerTeamId","tier","active"}` | `200 Service` |

Changing a service's `ownerTeamId`/`tier`/`active` does not alter existing incidents.

**Incidents**

| Method & path | Request | Success |
|---|---|---|
| `POST /incidents` | `{"title","description"?,"serviceId","severity"}` | `201 Incident` + `Location`; `status OPEN`, `reporterId` = actor, `assigneeId` null, `escalationLevel` 0, `version` 1 |
| `GET /incidents` | query below | `200 Page<Incident>` |
| `GET /incidents/{id}` | — | `200 Incident` |
| `PATCH /incidents/{id}` | `{"title"?,"description"?,"expectedVersion"?}` (≥1 of title/description) | `200 Incident` |
| `POST /incidents/{id}/acknowledge` | `{"expectedVersion"?}` | `200 Incident` |
| `POST /incidents/{id}/mitigate` | `{"message","expectedVersion"?}` | `200 Incident` |
| `POST /incidents/{id}/resolve` | `{"resolutionSummary","expectedVersion"?}` | `200 Incident` |
| `POST /incidents/{id}/close` | `{"expectedVersion"?}` | `200 Incident` |
| `POST /incidents/{id}/reopen` | `{"reason","expectedVersion"?}` | `200 Incident` |
| `POST /incidents/{id}/assign` | `{"assigneeId","expectedVersion"?}` | `200 Incident` |
| `POST /incidents/{id}/severity` | `{"severity","reason","expectedVersion"?}` | `200 Incident` |

An empty body is accepted where every body field is optional.

`Incident` (all fields always present; nullable ones are `null`):
```json
{
  "id": 1, "number": "INC-0001", "title": "…", "description": "…",
  "serviceId": 1, "serviceName": "API Gateway", "teamId": 1,
  "severity": "SEV2", "status": "OPEN",
  "reporterId": 9, "assigneeId": null,
  "createdAt": "…Z", "updatedAt": "…Z",
  "acknowledgedAt": null, "mitigatedAt": null, "resolvedAt": null, "closedAt": null,
  "resolutionSummary": null,
  "escalationLevel": 0, "reopenCount": 0, "version": 1,
  "actionItemCount": 0, "openActionItemCount": 0,
  "sla": { "ack": {}, "resolve": {}, "breached": false }
}
```
(`ack`/`resolve` as in §7.2.)

`GET /incidents` query parameters (optional, combined with AND):

| Param | Meaning |
|---|---|
| `status`, `severity` | one or more comma-separated enum values |
| `serviceId`, `teamId`, `assigneeId`, `reporterId` | exact match (positive integers) |
| `unassigned` | `true` → only incidents with null assignee |
| `q` | case-insensitive substring match on `title` or `number` |
| `slaBreached` | `true`/`false`, matches `sla.breached` at the clock's `now` |
| `active` | `true` → status ∈ {OPEN, ACKNOWLEDGED, MITIGATED}; `false` → {RESOLVED, CLOSED} |
| `sort` | `createdAt` (default), `updatedAt`, `severity` (SEV1 first when ascending) |
| `order` | `desc` (default) \| `asc`; ties always broken by `id` in the same direction |
| `page` | integer ≥ 1, default 1 |
| `pageSize` | integer 1–100, default 20 |

`Page<T>` = `{"items":[T],"page","pageSize","totalItems","totalPages"}` with `totalPages = ceil(totalItems / pageSize)` (0 if no items). A page beyond the last returns `200` with empty `items`.

**Notes, action items, audit, notifications, SLA**

| Method & path | Request | Success |
|---|---|---|
| `GET /incidents/{id}/notes` | — | `200 [Note]` ascending id (no paging) |
| `POST /incidents/{id}/notes` | `{"body"}` | `201 Note` |
| `GET /incidents/{id}/action-items` | — | `200 [ActionItem]` ascending id |
| `POST /incidents/{id}/action-items` | `{"title","ownerId","dueDate"}` | `201 ActionItem` (status `OPEN`) |
| `PATCH /incidents/{id}/action-items/{itemId}` | any of `{"title","ownerId","dueDate","status"}` (≥1) | `200 ActionItem`; `status: DONE` sets `completedAt = now`, `OPEN` clears it; the item must belong to the incident (else 404) |
| `GET /incidents/{id}/audit` | — | `200 [AuditEvent]` ascending id |
| `GET /audit` (Admin) | `?incidentId=&action=&page=&pageSize=` | `200 Page<AuditEvent>` ascending id |
| `GET /notifications` | `?scope=all` (Admin) `&page=&pageSize=` | `200 Page<Notification>` of the **current user's** notifications, newest first (id desc); `scope=all` returns everyone's |
| `POST /sla/evaluate` (Admin) | — | `200 {"escalationsApplied": n}` |
| `GET /dashboard/summary` | — | `200` see §11 |

Shapes: `Note` = `{"id","incidentId","authorId","body","createdAt"}`; `ActionItem` = `{"id","incidentId","title","ownerId","dueDate","status","createdAt","completedAt"}`; `AuditEvent` = `{"id","incidentId","actorId","action","createdAt","details"}`; `Notification` = `{"id","recipientId","type","incidentId","message","createdAt"}`. Test endpoints: §7.5.

## 11. Dashboard

`GET /api/v1/dashboard/summary` → `200` (all roles), evaluated at the clock's `now` over **all** incidents:

```json
{
  "generatedAt": "…Z",
  "totalIncidents": 0,
  "activeCount": 0,
  "byStatus": { "OPEN": 0, "ACKNOWLEDGED": 0, "MITIGATED": 0, "RESOLVED": 0, "CLOSED": 0 },
  "activeBySeverity": { "SEV1": 0, "SEV2": 0, "SEV3": 0, "SEV4": 0 },
  "breachedActiveCount": 0,
  "atRiskActiveCount": 0,
  "escalatedActiveCount": 0,
  "mttaMinutes": null,
  "mttrMinutes": null,
  "slaCompliancePercent": null,
  "topServices": [ { "serviceId": 1, "serviceName": "API Gateway", "activeCount": 2 } ]
}
```

Definitions:
- **active** = status ∈ {OPEN, ACKNOWLEDGED, MITIGATED}. All keys of `byStatus` and `activeBySeverity` are always present (zero-filled).
- `breachedActiveCount` = active incidents with `sla.breached`. `atRiskActiveCount` = active incidents that are not breached and have at least one timer `AT_RISK`. `escalatedActiveCount` = active incidents with `escalationLevel ≥ 1`.
- `mttaMinutes` = mean of `(acknowledgedAt − createdAt)` in minutes over incidents with non-null `acknowledgedAt`; `mttrMinutes` = mean of `(resolvedAt − createdAt)` over incidents with non-null `resolvedAt`. Both rounded to **1 decimal** (half away from zero); `null` if no qualifying incident.
- `slaCompliancePercent` = 100 × (RESOLVED/CLOSED incidents with `sla.breached == false`) ÷ (RESOLVED/CLOSED incidents), 1 decimal; `null` if the denominator is 0.
- `topServices` = up to 5 services with `activeCount > 0`, ordered by `activeCount` desc then `serviceName` asc.

## 12. User interface

A React SPA served at `/`. Visual design, layout and component libraries are free. The behaviours and **`data-testid` attributes** below are mandatory; hidden UI tests locate elements only through them (and visible text where stated). Elements marked *conditional* MUST NOT be in the DOM when the condition is false.

### 12.1 Routes
| Route | Page | Access |
|---|---|---|
| `/login` | Login | public |
| `/incidents` | Incident list (landing after login; `/` redirects here) | authenticated |
| `/incidents/new` | Create incident | authenticated, VIEWER redirected to `/incidents` |
| `/incidents/:id` | Incident detail | authenticated |
| `/dashboard` | Dashboard | authenticated |

Unauthenticated access to a protected route redirects to `/login`. Any `401` from the API clears the session and redirects to `/login`. The bearer token is stored in `localStorage` under key **`s2q.token`**. An unknown route shows `not-found-page`.

### 12.2 Global (all authenticated pages)
`nav-incidents`, `nav-dashboard`, `nav-new-incident` (conditional: role ≠ VIEWER), `current-user` (text contains `displayName`), `current-role` (text = role enum), `logout-button` (clears session → `/login`), `error-banner` (unexpected API errors; text free), `toast` (transient success/info message; text free).

### 12.3 Login
`login-username`, `login-password`, `login-submit`, `login-error` (conditional: after `INVALID_CREDENTIALS`). Success navigates to `/incidents`.

### 12.4 Incident list
- `incident-list` (container), `list-empty` (conditional: no results), `list-loading` (conditional).
- One `incident-row` per incident with attribute **`data-incident-id="<id>"`**, containing `incident-number`, `incident-title`, `incident-severity` (enum text), `incident-status` (enum text), `incident-assignee` (assignee `displayName` or `Unassigned`), `incident-sla` (`BREACHED` if `sla.breached`, else `AT_RISK` if any timer is `AT_RISK`, else `OK`), `incident-escalation` (the `escalationLevel` number). Clicking `incident-title` navigates to `/incidents/:id`.
- Filters: `filter-status` (select, `""` = all), `filter-severity` (select, `""` = all), `filter-service` (select, `""` = all, option values = service ids), `filter-search` (text → `q`), `filter-sla-breached` (checkbox → `slaBreached=true`), `filter-apply` (filters take effect **only** when clicked), `filter-reset` (clear filters and reload).
- Default request: `sort=createdAt&order=desc&pageSize=10`. Pagination: `pagination-prev`, `pagination-next` (HTML `disabled` when no previous/next page), `pagination-info` (text exactly `Page {page} of {totalPages}`; with no items `Page 1 of 1`).
- `new-incident-link` (conditional: role ≠ VIEWER) → `/incidents/new`.

### 12.5 Create incident
`new-title` (input), `new-description` (textarea), `new-service` (select; **active** services only; option value = service id; first option is an empty placeholder with value `""`), `new-severity` (select; `SEV1`..`SEV4`, placeholder `""`), `new-submit`. Field errors (conditional; from the server's `errors[].field` or client-side validation with the §9 rules): `error-title`, `error-description`, `error-serviceId`, `error-severity`. `BUSINESS_RULE_VIOLATION` and `DUPLICATE_INCIDENT` show in `form-error`. Success navigates to `/incidents/:newId`.

### 12.6 Incident detail
Display (text content): `detail-number`, `detail-title`, `detail-description`, `detail-status`, `detail-severity`, `detail-service` (service name), `detail-assignee` (displayName or `Unassigned`), `detail-reporter` (displayName), `detail-escalation-level`, `detail-reopen-count`, `detail-sla-ack` and `detail-sla-resolve` (timer status enums), `detail-sla-ack-deadline` and `detail-sla-resolve-deadline` (each with attribute `data-timestamp` = ISO value; visible format free), `detail-resolution-summary` (conditional: not null), `detail-not-found` (conditional: API 404).

Action buttons (**conditional**: rendered only if the user's role/scope (§5.3) *and* the incident status permit the action; a VIEWER sees none): `action-acknowledge` (OPEN), `action-mitigate` (ACKNOWLEDGED), `action-resolve` (MITIGATED), `action-close` (RESOLVED), `action-reopen` (RESOLVED), `action-assign` (OPEN/ACK/MITIGATED), `action-severity` (OPEN/ACK/MITIGATED), `action-edit` (title/description; any status except CLOSED).
`action-acknowledge` and `action-close` act immediately. The others open a modal: `dialog`, `dialog-input` (message / resolution summary / reason / new title), `dialog-input-secondary` (conditional: description in the edit dialog), `dialog-select` (conditional: assignee in the assign dialog — option value = user id, limited to Responders/Commanders of the incident's team, placeholder `""`; severity in the severity dialog — `SEV1`..`SEV4`), `dialog-reason` (conditional: reason text in the severity dialog), `dialog-confirm`, `dialog-cancel`, `dialog-error` (conditional: server `message` for 409/422; the dialog stays open). After any successful action the page shows the new state and a `toast`. Failures of immediate actions (acknowledge/close) show their server `message` in `toast` or `error-banner`.

Notes: `note-input` and `note-submit` (conditional: user may add notes and status ≠ CLOSED), `note-item` (one per note, attribute `data-note-id`) containing `note-body` and `note-author` (displayName).

Action items: `ai-list`; `ai-item` (attribute `data-item-id`) containing `ai-title`, `ai-owner` (displayName), `ai-due`, `ai-status` (`OPEN`\|`DONE`), `ai-toggle` (conditional: user may update items and status ≠ CLOSED; toggles OPEN↔DONE). Creation form (same condition): `ai-title-input`, `ai-owner-select` (option value = user id; users with role RESPONDER/COMMANDER/ADMIN), `ai-due-input` (accepts `YYYY-MM-DD` via fill), `ai-add`, `ai-error` (conditional: server error text).

Audit: `audit-list` (conditional: user may read the incident audit trail), `audit-item` (ascending) containing `audit-action` (action enum) and `audit-actor` (displayName or `System`).

### 12.7 Dashboard
`dash-active-count`, `dash-total-count`, `dash-breached-count`, `dash-at-risk-count`, `dash-escalated-count`, `dash-mtta` (one decimal or `N/A`), `dash-mttr` (same), `dash-sla-compliance` (e.g. `87.5%` or `N/A`), `dash-status-OPEN` … `dash-status-CLOSED` (text = count), `dash-severity-SEV1` … `dash-severity-SEV4` (active counts), `dash-top-service` (one per `topServices` entry, in order; text contains service name and active count).

### 12.8 Behaviour rules
- Server `422` field errors MUST be mapped to the `error-*` elements.
- UI role gating is a convenience; the API is authoritative.

## 13. Non-functional requirements (objectively checkable)

| ID | Requirement |
|---|---|
| NFR-01 | `scripts/build.sh` succeeds from a clean checkout with only the pinned toolchains. |
| NFR-02 | `GET /health` 200 ≤ 60 s after start on an empty database. |
| NFR-03 | Passwords are never stored in plain text and never appear in API responses or logs. |
| NFR-04 | All failures under `/api` (incl. unknown routes, malformed JSON, unsupported methods, auth failures) use the §10.1 error shape. |
| NFR-05 | All writes of one request (incident change, notes, audit events, notifications) are atomic. |
| NFR-06 | `GET /incidents` with 500 incidents and default paging responds in ≤ 2 s on the harness hardware (smoke check, not a benchmark). |
| NFR-07 | Test endpoints are inert (`404`) unless `S2Q_TEST_MODE=true`. |
| NFR-08 | No secrets in the repository other than the public fixture password of §4 and (test mode only) a development signing key. |
| NFR-09 | `scripts/test.sh` exits 0 and produces coverage reports (Cobertura for backend; LCOV or Cobertura for frontend). |

## 14. Explicitly out of scope

Email/SMS/chat delivery of notifications (the outbox is the channel); SSO/OAuth/LDAP, MFA, self-registration, password change/reset, user-management UI/API; file attachments; rich-text/markdown rendering; internationalisation and time-zone selection (UI may show UTC); real-time push (WebSockets/SSE); multi-tenancy; business-hours calendars and SLA pause; on-call schedules and paging integrations; post-incident review documents beyond action items; data export/import; rate limiting; API versions beyond `/api/v1`; pagination of notes and action items; deletion or soft-deletion of any entity; infrastructure-as-code, Kubernetes, CI/CD pipelines, observability stacks; DAST, cryptographic posture analysis, performance benchmarking beyond NFR-06; mobile layouts; accessibility certification; browser support beyond the one current evergreen browser used by the hidden UI tests.

## 15. Acceptance criteria

Each criterion is independently testable by the hidden acceptance suite. `[API]` criteria are checked over HTTP against a freshly reset seeded database (`POST /api/v1/test/reset`) in test mode; `[UI]` criteria are checked through a browser using only the `data-testid` contract of §12. Time is controlled with the test clock. "As *user*" means authenticated as that seeded user (§4). Unless stated otherwise, incidents are created through the API.

### 15.1 Platform, auth and reference data
- **AC-001** [API] `GET /health` without credentials returns 200 with body `{"status":"ok"}`.
- **AC-002** [API] Login as `admin` with `S2Q-Demo-2026!` returns 200 with a non-empty `token` and `user` equal to the seeded admin (`id` 1, role `ADMIN`, `teamId` null).
- **AC-003** [API] Login with a wrong password and login with an unknown username both return 401 `INVALID_CREDENTIALS` with identical `message` text.
- **AC-004** [API] Login with a missing or blank username or password returns 422 `VALIDATION_ERROR` with a field error (`required`) for each missing field.
- **AC-005** [API] A protected endpoint (e.g. `GET /incidents`) without a token, and with a malformed token, returns 401 `UNAUTHENTICATED`.
- **AC-006** [API] `GET /auth/me` returns the authenticated user and no password-related property.
- **AC-007** [API] `GET /users` returns exactly the 10 seeded users with the roles/teams of §4 and no password-related property.
- **AC-008** [API] `GET /teams` returns the 3 seeded teams; `GET /services` returns the 6 seeded services with the fields of §4; `GET /services?active=true` returns 5.
- **AC-009** [API] `GET /services/999` and `GET /services/abc` return 404 `NOT_FOUND`.
- **AC-010** [API] As `admin`, `POST /services` with valid fields returns 201 with a `Location` header and the created service; `GET /services/{id}` then returns it.
- **AC-011** [API] As `cmd.platform`, `POST /services` returns 403 `FORBIDDEN`.
- **AC-012** [API] `POST /services` with a key already in use, or a name equal ignoring case to an existing name, returns 409 `DUPLICATE_SERVICE`.
- **AC-013** [API] `POST /services` with key `bad key`, tier 4, or a 1-character name returns 422 with field errors on `key` (`invalid_format`), `tier` (`out_of_range`) and `name` (`too_short`) respectively.
- **AC-014** [API] As `admin`, `PUT /services/{id}` replaces the service, `GET` shows the new values, and `GET /audit` contains a `SERVICE_UPDATED` event with `incidentId` null.
- **AC-015** [API] `GET /api/v1/nope` returns 404 with the error shape (`code`, `message`, `status`, `traceId` present).
- **AC-016** [API] Malformed JSON in the body of `POST /incidents` returns 400 `MALFORMED_REQUEST`.
- **AC-017** [API] When the app is started without `S2Q_TEST_MODE=true`, `GET /api/v1/test/clock` returns 404.

### 15.2 Incident creation and validation
- **AC-018** [API] As `reporter1`, `POST /incidents` `{title:"Checkout latency spike", serviceId:1, severity:"SEV2"}` returns 201 with `number = "INC-0001"`, `status = OPEN`, `reporterId = 9`, `assigneeId = null`, `teamId = 1`, `escalationLevel = 0`, `version = 1`, `description = ""` and `createdAt` equal to the test clock's now.
- **AC-019** [API] The second incident created gets `number = "INC-0002"`.
- **AC-020** [API] As `viewer1`, `POST /incidents` returns 403 `FORBIDDEN`.
- **AC-021** [API] `POST /incidents` with a 4-character title returns 422 with field error `title` / `too_short`; 121 characters → `too_long`; only spaces → `required`.
- **AC-022** [API] `POST /incidents` with a 4001-character `description` returns 422 field error `description` / `too_long`.
- **AC-023** [API] `POST /incidents` with unknown severity `SEV9` returns 422 field error on `severity`; a missing severity → `required`.
- **AC-024** [API] `POST /incidents` with a non-existent `serviceId` returns 422 field error `serviceId` / `not_found`; a missing `serviceId` → `required`.
- **AC-025** [API] `POST /incidents` for service 6 (inactive) returns 422 `BUSINESS_RULE_VIOLATION` with `details.rule = "SERVICE_INACTIVE"`.
- **AC-026** [API] `POST /incidents` with `SEV1` for service 2 (tier 3) returns 422 `BUSINESS_RULE_VIOLATION` with `details.rule = "SEV1_REQUIRES_TIER_1_OR_2"`; `SEV1` for service 1 (tier 1) and service 5 (tier 2) succeeds.
- **AC-027** [API] Leading and trailing spaces in `title` are trimmed in the stored and returned value.
- **AC-028** [API] Creating a second incident on the same service whose title equals an existing `OPEN` incident's title ignoring case and surrounding spaces returns 409 `DUPLICATE_INCIDENT` with `details.existingIncidentId` equal to the first incident's id; the same title on a different service succeeds.
- **AC-029** [API] After the first incident is `RESOLVED` or `CLOSED`, creating an incident with the same service and title succeeds.
- **AC-030** [API] Creating an incident writes exactly one `INCIDENT_CREATED` audit event (`actorId` = reporter, correct `details.severity` and `details.serviceId`) and one `INCIDENT_CREATED` notification for each Commander of the owning team (excluding the actor) and for nobody else.
- **AC-031** [API] A rejected creation (any 4xx) writes no audit event and no notification.

### 15.3 Reading and listing incidents
- **AC-032** [API] `GET /incidents/{id}` returns the full Incident shape with every field of §10.2 present; a non-existent id returns 404.
- **AC-033** [API] Every authenticated role, including `viewer1`, can read the incident list and an incident's detail.
- **AC-034** [API] `GET /incidents` defaults to `createdAt` descending (ties by id descending), `page = 1`, `pageSize = 20`, and returns `items`, `page`, `pageSize`, `totalItems`, `totalPages`.
- **AC-035** [API] With 25 incidents, `pageSize=10` returns `totalPages = 3`; page 3 has 5 items; page 4 returns 200 with empty `items`.
- **AC-036** [API] `page=0`, `pageSize=0`, `pageSize=101` and `page=abc` each return 422 with a field error naming the parameter.
- **AC-037** [API] Filtering by `status`, by `severity` (including comma-separated lists), by `serviceId`, `teamId`, `assigneeId`, `reporterId`, and with `unassigned=true` each returns exactly the matching incidents.
- **AC-038** [API] `q` matches title substrings case-insensitively and matches the incident number (e.g. `q=inc-0002`).
- **AC-039** [API] `active=true` returns only OPEN/ACKNOWLEDGED/MITIGATED incidents and `active=false` only RESOLVED/CLOSED.
- **AC-040** [API] `sort=severity&order=asc` returns SEV1 before SEV2…SEV4 with ties by id ascending; `sort=updatedAt&order=desc` orders by last update.
- **AC-041** [API] `slaBreached=true` returns only incidents whose `sla.breached` is true at the clock's current time, and the result changes after the clock is advanced past a deadline.

### 15.4 Lifecycle transitions
- **AC-042** [API] As `resp.platform1`, `acknowledge` on an OPEN incident of service 1 returns 200 with `status = ACKNOWLEDGED`, `acknowledgedAt` = clock now, `assigneeId = 5` (auto-assigned) and an increased `version`.
- **AC-043** [API] Acknowledging with auto-assignment writes audit events `ASSIGNED` then `ACKNOWLEDGED` in that order; the `ACKNOWLEDGED` details are `{"from":"OPEN","to":"ACKNOWLEDGED"}`.
- **AC-044** [API] If the incident already has an assignee, `acknowledge` by another permitted user leaves `assigneeId` unchanged and writes only the `ACKNOWLEDGED` event.
- **AC-045** [API] As `admin`, acknowledging an incident with no assignee returns 409 `ASSIGNEE_REQUIRED`; after an assign, the Admin can acknowledge it.
- **AC-046** [API] Acknowledging an already ACKNOWLEDGED incident returns 409 `INVALID_TRANSITION` with `details.currentStatus = "ACKNOWLEDGED"` and `details.action = "acknowledge"`.
- **AC-047** [API] `mitigate` requires `message` (missing/blank → 422 `required`); a valid mitigate by the assignee returns 200 with `status = MITIGATED`, `mitigatedAt` set, and the message is added as a Note.
- **AC-048** [API] `mitigate` on an OPEN incident returns 409 `INVALID_TRANSITION`.
- **AC-049** [API] `resolve` requires `resolutionSummary` of 10–2000 characters (9 → 422 `too_short`); a valid resolve from MITIGATED returns 200 with `status = RESOLVED`, `resolvedAt` and `resolutionSummary` set; `resolve` from ACKNOWLEDGED returns 409 `INVALID_TRANSITION`.
- **AC-050** [API] Resolving creates a `RESOLVED` notification for the reporter unless the reporter is the actor.
- **AC-051** [API] `close` on a RESOLVED SEV3 or SEV4 incident with no action items returns 200, `status = CLOSED`, `closedAt` set, and creates a `CLOSED` notification for the reporter.
- **AC-052** [API] `close` on a RESOLVED SEV1 or SEV2 incident with no action items returns 409 `ACTION_ITEM_REQUIRED`; after one action item is created and marked `DONE`, `close` returns 200.
- **AC-053** [API] `close` with at least one `OPEN` action item returns 409 `OPEN_ACTION_ITEMS` (checked before `ACTION_ITEM_REQUIRED`).
- **AC-054** [API] `close` on a MITIGATED or OPEN incident returns 409 `INVALID_TRANSITION`.
- **AC-055** [API] As a Commander of the owning team, `reopen` of a RESOLVED incident with a `reason` of ≥5 characters returns 200 with `status = ACKNOWLEDGED`, `resolvedAt = null`, `mitigatedAt = null`, `reopenCount = 1`, `acknowledgedAt` unchanged, and the reason added as a Note; a 4-character reason returns 422.
- **AC-056** [API] `reopen` on a non-RESOLVED incident (including CLOSED) returns 409 `INVALID_TRANSITION`.
- **AC-057** [API] Every transition attempted on a CLOSED incident returns 409 `INVALID_TRANSITION`.
- **AC-058** [API] A full lifecycle produces audit events in the order `INCIDENT_CREATED`, `ASSIGNED`, `ACKNOWLEDGED`, `MITIGATED`, `RESOLVED`, `CLOSED` with strictly increasing ids and non-decreasing `createdAt`.
- **AC-059** [API] An `expectedVersion` different from the current `version` on any state-changing endpoint returns 409 `VERSION_CONFLICT` and changes nothing; the correct version succeeds.
- **AC-060** [API] Two simultaneous `acknowledge` requests on one OPEN incident yield exactly one 200 and one 409 (`INVALID_TRANSITION` or `VERSION_CONFLICT`), exactly one `ACKNOWLEDGED` audit event, and a consistently incremented `version`.
- **AC-061** [API] A rejected transition (403/409/422) writes no audit event and leaves `version`, `updatedAt` and all lifecycle timestamps unchanged.

### 15.5 Assignment, severity change and editing
- **AC-062** [API] As `cmd.platform`, `assign` of an OPEN incident of service 1 to user 6 returns 200 with `assigneeId = 6`, writes `ASSIGNED` (`details {"from":null,"to":6}`) and notifies user 6.
- **AC-063** [API] `assign` to a user of another team (user 7), to a Reporter (user 9) or to a non-existent user returns 422 with a field error on `assigneeId`.
- **AC-064** [API] Assigning the current assignee again returns 200 with no new audit event or notification.
- **AC-065** [API] `assign` on a RESOLVED or CLOSED incident returns 409 `INVALID_STATE`.
- **AC-066** [API] As `cmd.platform`, changing severity SEV3 → SEV2 with a reason returns 200, writes `SEVERITY_CHANGED` (`details {"from":"SEV3","to":"SEV2","reason":…}`), adds a Note with the reason, and `sla` targets/deadlines reflect SEV2 computed from `createdAt`.
- **AC-067** [API] Changing severity to the current value, or with a reason shorter than 5 characters, returns 422; changing to `SEV1` on a tier-3 service returns 422 `BUSINESS_RULE_VIOLATION`.
- **AC-068** [API] Changing severity on a RESOLVED or CLOSED incident returns 409 `INVALID_STATE`.
- **AC-069** [API] A severity change to a lower severity does not lower `escalationLevel`.
- **AC-070** [API] As `reporter1`, `PATCH` of the title of their own OPEN incident returns 200 and writes `INCIDENT_UPDATED` with `changedFields = ["title"]`; after acknowledgement the same request returns 403; as `viewer1` it returns 403.
- **AC-071** [API] `PATCH` with an empty object returns 422; with a title violating the length rule returns 422; with a title duplicating another active incident of the same service returns 409 `DUPLICATE_INCIDENT`.
- **AC-072** [API] `PATCH` on a CLOSED incident returns 409 `INVALID_STATE`.

### 15.6 Permissions
- **AC-073** [API] `resp.platform1` cannot acknowledge an incident of service 3 (Payments team): 403 `FORBIDDEN`.
- **AC-074** [API] `resp.platform2` cannot mitigate or resolve an incident assigned to `resp.platform1` (403); `cmd.platform` can.
- **AC-075** [API] `cmd.payments` cannot assign, change severity, close or reopen an incident of service 1 (403); `cmd.platform` and `admin` can.
- **AC-076** [API] `resp.platform1` cannot close, reopen, assign or change severity (403 regardless of team).
- **AC-077** [API] `reporter1` and `viewer1` cannot acknowledge, mitigate, resolve, close, reopen, assign, change severity or create action items (403).
- **AC-078** [API] `reporter1` can add a note to an incident they reported and gets 403 on an incident reported by someone else; `viewer1` cannot add notes (403).
- **AC-079** [API] `admin` can perform every action on incidents of any team.
- **AC-080** [API] The order of checks of §5.4 holds: a role that never has an action gets 403 even for a non-existent incident, while a role that has it gets 404; a permitted role with the wrong scope on an existing incident gets 403 even if the body is invalid.
- **AC-081** [API] `GET /incidents/{id}/audit` returns 403 for `viewer1` and `reporter1`, 200 for `resp.platform1` and `cmd.platform` on a Platform incident, 403 for `resp.payments1` on a Platform incident, and 200 for `admin`.
- **AC-082** [API] `GET /audit` returns 200 for `admin` and 403 for every other role; the `incidentId` and `action` filters narrow the result; results are ascending by id in the paging envelope.
- **AC-083** [API] `POST /sla/evaluate` returns 200 `{"escalationsApplied": n}` for `admin` and 403 for every other role.

### 15.7 Notes and action items
- **AC-084** [API] `POST /incidents/{id}/notes` with a valid body as `resp.platform1` (Platform incident) returns 201 with `authorId = 5`; the note appears in `GET …/notes` in ascending id order; a `NOTE_ADDED` audit event with `details.noteId` is written.
- **AC-085** [API] A blank note body returns 422 `required`; 2001 characters returns 422 `too_long`; a note on a CLOSED incident returns 409 `INVALID_STATE`.
- **AC-086** [API] Notes created implicitly by `mitigate`, `reopen` and severity change do **not** produce `NOTE_ADDED` audit events.
- **AC-087** [API] As `cmd.platform`, `POST …/action-items` with a title, `ownerId = 5` and a due date on or after the clock's UTC date returns 201 with `status = OPEN`, `completedAt = null`, writes `ACTION_ITEM_CREATED`, and the incident's `actionItemCount` and `openActionItemCount` increase.
- **AC-088** [API] Action item validation: title of 2 characters → 422 `too_short`; `ownerId` of a Reporter/Viewer or non-existent user → 422; a due date before the clock's UTC date → 422; a malformed date such as `2026-13-40` → 422 `invalid_format`.
- **AC-089** [API] `PATCH …/action-items/{itemId}` with `status = DONE` returns 200 with `completedAt` = clock now and decreases `openActionItemCount`; setting it back to `OPEN` clears `completedAt`; each change writes `ACTION_ITEM_UPDATED`.
- **AC-090** [API] `PATCH` of an action item through a different incident's path returns 404; with an empty body returns 422.
- **AC-091** [API] Action items cannot be created or updated on a CLOSED incident (409 `INVALID_STATE`).
- **AC-092** [API] An action item whose due date has since passed (clock advanced) can still be marked `DONE` without error.

### 15.8 SLA, clock and escalation
- **AC-093** [API] `PUT /test/clock {"now":"2026-03-01T12:00:00.000Z"}` returns 200 `{now, mode:"fixed"}`, and the clock does not advance with real time (two reads 2 s apart return the same `now`).
- **AC-094** [API] `POST /test/clock/advance {"minutes":30}` moves `now` by exactly 30 minutes and returns the new time; `{"minutes":-1}` returns 422.
- **AC-095** [API] `POST /test/reset` returns 204, removes all incidents, restores the seed data and returns the clock to `system` mode.
- **AC-096** [API] For incidents created at T: SEV1 has `sla.ack.deadline = T+5 min` and `sla.resolve.deadline = T+60 min`; SEV2 `T+15`/`T+240`; SEV3 `T+60`/`T+1440`; SEV4 `T+240`/`T+4320`; `targetMinutes` matches.
- **AC-097** [API] A fresh SEV1 incident has `sla.ack.status = RUNNING`; at T+4 min it is `AT_RISK`; at exactly T+5 min it is still `AT_RISK`; at T+5 min + 1 s it is `BREACHED` and `sla.breached = true`.
- **AC-098** [API] Acknowledging exactly at the ack deadline gives `sla.ack.status = MET`; acknowledging after it gives `BREACHED`, which stays `BREACHED` after further clock advances.
- **AC-099** [API] The resolve timer follows the same rules against `resolvedAt` and the resolve deadline: resolving at or before the deadline yields `MET`, after it `BREACHED`.
- **AC-100** [API] After reopening an incident that was resolved in time, `sla.resolve.status` is evaluated against `now` again and becomes `BREACHED` once `now` passes the original resolve deadline.
- **AC-101** [API] Advancing the clock past the ack deadline of an OPEN incident sets `escalationLevel = 1` (visible on a subsequent GET with no other action), writes one `ESCALATED` event (`actorId = null`, `details.level = 1`) and one `ESCALATION_L1` notification per Commander of the owning team.
- **AC-102** [API] Re-evaluating (advancing again within the same level, or `POST /sla/evaluate`) creates no duplicate `ESCALATED` events or notifications.
- **AC-103** [API] An incident acknowledged in time and then advanced past its resolve deadline reaches `escalationLevel = 2` directly (no level-1 event); `ESCALATION_L2` notifications go to the owning team's Commanders and every Admin.
- **AC-104** [API] An OPEN incident advanced past both deadlines in a single clock advance gets two `ESCALATED` events (level 1, then level 2) with ascending ids.
- **AC-105** [API] RESOLVED and CLOSED incidents are not escalated by clock advances.
- **AC-106** [API] Acknowledging an OPEN incident after its ack deadline results in an audit trail where the `ESCALATED` (level 1) event precedes the `ACKNOWLEDGED` event, and the final `escalationLevel` is 1.
- **AC-107** [API] `escalationLevel` never decreases, including after a lower-severity change.
- **AC-108** [API] Timers are independent per incident: advancing the clock escalates only incidents whose deadlines have passed.
- **AC-109** [API] `POST /sla/evaluate` as `admin` with nothing newly due returns `escalationsApplied = 0` and changes nothing.

### 15.9 Notifications
- **AC-110** [API] `GET /notifications` returns only the current user's notifications, newest first (id descending), in the paging envelope, each item with the §10.2 shape.
- **AC-111** [API] `GET /notifications?scope=all` returns everyone's notifications for `admin` and 403 for any other role.
- **AC-112** [API] Actor exclusion holds: when `cmd.platform` creates an incident on service 1, `cmd.platform` receives no `INCIDENT_CREATED` notification; when `reporter1` creates it, `cmd.platform` receives exactly one.
- **AC-113** [API] A Commander assigning an incident to themself produces no `ASSIGNED` notification for that user.

### 15.10 Dashboard
- **AC-114** [API] On an empty database `GET /dashboard/summary` returns zero counts, all `byStatus` and `activeBySeverity` keys present, `mttaMinutes`, `mttrMinutes` and `slaCompliancePercent` null, and `topServices = []`.
- **AC-115** [API] `totalIncidents`, `activeCount`, `byStatus` and `activeBySeverity` match a known set of incidents in different states (RESOLVED/CLOSED incidents are excluded from `activeBySeverity`).
- **AC-116** [API] `breachedActiveCount`, `atRiskActiveCount` and `escalatedActiveCount` match a known set after clock manipulation; an incident that is breached on one timer and at risk on the other counts only as breached.
- **AC-117** [API] `mttaMinutes` equals the mean time to first acknowledgement in minutes rounded to 1 decimal (incidents acknowledged after 2 and 5 minutes → `3.5`), unchanged by reopen.
- **AC-118** [API] `mttrMinutes` equals the mean time to (latest) resolution in minutes rounded to 1 decimal; a reopened incident (no `resolvedAt`) is excluded until resolved again.
- **AC-119** [API] `slaCompliancePercent` equals the share of RESOLVED/CLOSED incidents with `sla.breached = false`, rounded to 1 decimal (1 of 3 → `33.3`).
- **AC-120** [API] `topServices` lists at most 5 services with active incidents, ordered by `activeCount` desc then `serviceName` asc, excluding services with zero active incidents.
- **AC-121** [API] The dashboard is readable by every authenticated role and returns 401 without a token.

### 15.11 User interface
- **AC-122** [UI] Opening `/incidents` unauthenticated redirects to `/login`; logging in as `reporter1` lands on `/incidents`, `current-user` contains `Rory Reporter` and `current-role` equals `REPORTER`.
- **AC-123** [UI] Logging in with a wrong password shows `login-error` and stays on `/login`.
- **AC-124** [UI] After login the token is stored in `localStorage` key `s2q.token`; `logout-button` clears it and redirects to `/login`; reloading a protected page after logout redirects to `/login`.
- **AC-125** [UI] As `viewer1`, `nav-new-incident` and `new-incident-link` are absent, `/incidents/new` redirects to `/incidents`, and an incident's detail page contains none of the `action-*` buttons and no note or action-item creation controls.
- **AC-126** [UI] As `reporter1`, creating an incident through `new-title`, `new-description`, `new-service`, `new-severity`, `new-submit` navigates to `/incidents/{id}` where `detail-title`, `detail-severity` (`SEV2`), `detail-status` (`OPEN`) and `detail-service` show the entered values.
- **AC-127** [UI] Submitting the empty create form shows `error-title`, `error-serviceId` and `error-severity` and does not navigate; a 4-character title shows `error-title`.
- **AC-128** [UI] `new-service` offers only active services (no option for service 6); choosing SEV1 with a tier-3 service and submitting shows `form-error`.
- **AC-129** [UI] Submitting a duplicate (same service and title as an active incident) shows `form-error`.
- **AC-130** [UI] The list shows one `incident-row` per incident with `data-incident-id`, correct `incident-number`, `incident-title`, `incident-severity`, `incident-status`, `incident-assignee` (`Unassigned` initially) and `incident-sla`; with no incidents `list-empty` is shown.
- **AC-131** [UI] With 12 incidents the first page shows 10 rows and `pagination-info` reads `Page 1 of 2`; `pagination-next` shows the remaining 2 rows with `Page 2 of 2`; `pagination-prev` is disabled on page 1 and `pagination-next` on the last page.
- **AC-132** [UI] Filters apply only after `filter-apply` is clicked (changing `filter-status` alone leaves the list unchanged); status, severity, service, search and SLA-breached filters narrow the list; `filter-reset` restores the full list.
- **AC-133** [UI] `incident-sla` shows `OK` for a fresh incident, `AT_RISK` once the clock is advanced to 80 % of the ack target, and `BREACHED` after the ack deadline (after reloading the page).
- **AC-134** [UI] As `resp.platform1` on an OPEN Platform incident, `action-acknowledge` is visible; clicking it makes `detail-status` show `ACKNOWLEDGED`, `detail-assignee` show `Robin Platform`, and `audit-action` entries `ASSIGNED`, `ACKNOWLEDGED` appear.
- **AC-135** [UI] As `resp.payments1`, `action-acknowledge` is absent on a Platform incident.
- **AC-136** [UI] `action-mitigate` opens `dialog`; confirming with an empty `dialog-input` keeps the dialog open and shows `dialog-error`; confirming with text moves the status to `MITIGATED` and the message appears as a `note-item`.
- **AC-137** [UI] `action-resolve` requires ≥10 characters in `dialog-input`; after success `detail-status` is `RESOLVED` and `detail-resolution-summary` shows the text.
- **AC-138** [UI] As `cmd.platform` on a RESOLVED SEV3 incident, `action-close` and `action-reopen` are visible; `action-reopen` with a reason returns the incident to `ACKNOWLEDGED` and `detail-reopen-count` shows `1`.
- **AC-139** [UI] As `cmd.platform` on a RESOLVED SEV2 incident without action items, `action-close` shows the server message in `toast` or `error-banner` and `detail-status` remains `RESOLVED`.
- **AC-140** [UI] As `cmd.platform`, `action-assign` opens a dialog whose `dialog-select` lists only Platform Responders/Commanders; assigning updates `detail-assignee`.
- **AC-141** [UI] As `cmd.platform`, `action-severity` changes the severity through `dialog-select` and `dialog-reason`; `detail-severity` and the `data-timestamp` of `detail-sla-ack-deadline` update to the new severity's deadline.
- **AC-142** [UI] Adding a note via `note-input` and `note-submit` appends a `note-item` with the text in `note-body` and the user's name in `note-author`; `note-input` is absent when the incident is CLOSED.
- **AC-143** [UI] Adding an action item via `ai-title-input`, `ai-owner-select`, `ai-due-input`, `ai-add` shows an `ai-item` with `ai-status` `OPEN`; `ai-toggle` flips it to `DONE` and back; an invalid input (past date) shows `ai-error`.
- **AC-144** [UI] `audit-list` is visible to `cmd.platform` on Platform incidents and absent for `reporter1`.
- **AC-145** [UI] `detail-escalation-level` shows `1` after the clock is advanced past the ack deadline of an OPEN incident and the page is reloaded; `incident-escalation` in the list shows the same value.
- **AC-146** [UI] Visiting `/incidents/9999` shows `detail-not-found`; visiting an unknown route shows `not-found-page`.
- **AC-147** [UI] The dashboard shows `dash-active-count`, `dash-total-count`, `dash-status-*` and `dash-severity-*` values consistent with the API summary; `dash-mtta`, `dash-mttr` and `dash-sla-compliance` show `N/A` when there is no data; `dash-top-service` entries are ordered by active count.
- **AC-148** [UI] After a 401 from the API (token removed or invalidated, then an action is attempted) the UI redirects to `/login`.

### 15.12 Non-functional
- **AC-149** [API] The application answers `GET /health` 200 within 60 s of start on an empty database and applies migrations and seed automatically (NFR-02).
- **AC-150** [API] No API response and no captured process log line contains the seed password or a password hash (NFR-03); spot-checked over login, `GET /users` and one failed login.
- **AC-151** [API] Failure responses for unknown routes, malformed JSON, unsupported methods and unauthenticated access follow the §10.1 error shape (NFR-04).
- **AC-152** [API] After any rejected request (4xx), the counts of audit events, notes and notifications are unchanged (NFR-05).
- **AC-153** [API] With 500 incidents created via the API, `GET /incidents` with default paging answers in ≤ 2 s (NFR-06).
- **AC-154** [Harness] After build, `scripts/test.sh` exits 0 and the coverage report files exist (NFR-09). Checked by the harness, not by the hidden test suite.

## 16. Traceability and the hidden acceptance suite

- Each `AC-nnn` is mapped to ≥1 hidden test in `tests/acceptance/` (kept out of agent-accessible paths). The traceability matrix (`AC-nnn` ↔ test ids) is maintained with the suite and versioned with this spec.
- The hidden suite is validated against a reference implementation before runs start so that test defects surface before model runs (Phase 2 exit criterion). Ambiguities found during validation are resolved by a spec patch (new version), never by test-only interpretation.
- Hidden-test failures are **not** fed back to agents in Stage A. Whether they are fed back in Stage B is recorded in [decisions.md](decisions.md) (default: no).
- Requirement completion (fidelity) is scored per acceptance criterion as pass/fail; a build that cannot start fails all criteria.

## 17. Changelog

| Version | Date | Change |
|---|---|---|
| v1.0.0-rc1 | 2026-11-15 | First frozen release candidate. |
