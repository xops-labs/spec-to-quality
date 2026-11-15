# Spec-to-Quality (S2Q) — Benchmark Application Recommendation

> **Status:** Phase 1 selection rationale (historical). The detailed application specification is now in [benchmark-spec.md](benchmark-spec.md) (v1.0.0-rc1, 2026-11-15) and the locked stack decision is in [decisions.md](decisions.md) D-001 (**.NET + PostgreSQL backend, TypeScript/React frontend**, superseding the JVM lean noted below).

## 1. Criteria (from the project brief)

The application should: (C1) represent a realistic enterprise engineering problem; (C2) be implementable within controlled time and cost limits; (C3) include frontend and backend where feasible; (C4) support persistence and business logic; (C5) have clear functional acceptance criteria; (C6) be complex enough to expose maintainability and architectural differences; (C7) be independently testable; (C8) avoid external paid dependencies; (C9) avoid requirements that make evaluation subjective.

Added by us for research validity: (C10) low *contamination* risk from public benchmarks (not a well-known benchmark task); (C11) no dependence on wall-clock time or external services in tests (determinism of the *evaluation*); (C12) room for the specification to be unambiguous on behaviour while leaving design choices free (needed for H5).

## 2. Candidates considered

Ratings are qualitative judgements by the author of this document (✔ strong, ◐ partial, ✘ weak), not measurements.

| Candidate | C1 | C2 | C3 | C4 | C5 | C6 | C7 | C8 | C9 | C10 | C11 | Comments |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Incident management** (create/triage/escalate/resolve incidents, services, teams, SLAs, timeline, post-incident actions) | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ◐ | State machine + RBAC + SLA rules + audit trail; notification channels can be simulated in-app. Time-based rules need an injectable clock in the spec. |
| Workflow/approval engine (e.g., expense or change approval) | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ◐ | ✔ | Strong too; generic workflow engines invite heavy abstraction choices (good for maintainability variance, but harder to specify unambiguously). |
| Helpdesk/ticketing | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ | ✔ | ✔ | ✘ | ✔ | Very common in training data (higher contamination); less rule complexity. |
| Inventory/order management | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ | ✔ | ✔ | ✘ | ✔ | Ubiquitous CRUD/e-commerce demo; limited architectural interest. |
| HR leave management | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ | ✔ | ◐ | ◐ | ◐ | Calendar/holiday rules cause ambiguity. |
| Configuration/asset registry (CMDB-lite) | ◐ | ✔ | ◐ | ✔ | ✔ | ◐ | ✔ | ✔ | ✔ | ◐ | ✔ | Less enterprise-workflow richness. |
| Real-time collaborative tool / chat | ◐ | ✘ | ✔ | ◐ | ◐ | ✔ | ✘ | ✔ | ◐ | ◐ | ✘ | Concurrency and real-time behaviours hard to test deterministically. |

## 3. Recommendation: an incident-management application

**Recommended:** a self-contained **incident-management application** — an internal tool through which responders declare, triage, escalate, mitigate, resolve and review incidents against services and teams.

### Why it fits the criteria
- **Realistic enterprise problem (C1):** incident management is a standard operations workflow; the domain concepts (severity, ownership, escalation, SLAs, audit trail, post-incident review) are well understood by engineering leaders, which suits the target audience.
- **Business logic with persistence (C4, C6):** a lifecycle **state machine** with guarded transitions, role-based permissions, SLA/escalation rules, auditability and reporting queries produce enough non-trivial logic to expose differences in structure, error handling, duplication and testing discipline — not just CRUD.
- **Frontend + backend (C3):** list/detail/triage views, forms with validation, and a dashboard can be specified with objectively checkable behaviours.
- **Independent testing (C5, C7, C9):** transitions and invariants can be tested through a public HTTP API; UI behaviour can be tested through a stable, specified interaction contract (e.g., accessible roles/labels or a specified test-id contract). Subjectivity is avoided by *specifying* behaviours rather than look-and-feel.
- **No external paid dependencies (C8):** notifications are simulated through an in-application outbox/event feed; authentication can be local; the database is an embedded or containerised open-source engine.
- **Controlled cost (C2):** scope can be dialled by the *specification* (number of entities and rules). A "core" scope can be sized so that a full agent run fits the budget chosen after the pilot.
- **Room for implementation variation (C12):** many legitimate designs exist (layering, how SLA timers are computed, how audit events are modelled), supporting the H5 question about residual variation under a controlled spec.

### Risks and how Phase 2 should handle them
| Risk | Mitigation |
|---|---|
| **Contamination/common patterns** (C10): ticket and incident trackers are common in training corpora | Use a custom domain model and unusual-but-plausible rules; do not reuse public repositories or benchmark tasks; acknowledge residual contamination as a limitation. |
| **Time-dependent rules** (SLA timers, escalation) hurt determinism (C11) | Specify an injectable clock and a test-only time-control endpoint or configuration, defined in the spec; evaluate by advancing time, not by sleeping. Note this adds an explicit testability requirement to the spec. |
| **Scope creep / spec bloat** | Freeze a "core" scope; label extensions as optional and exclude them from the primary comparison. |
| **UI testability constraints reduce design freedom** | Specify the minimum UI contract (roles/labels or test-ids); keep other UI design free; or make the primary acceptance suite API-level with a smaller UI check. **Resolved in spec v1.0.0-rc1:** a `data-testid` contract (spec §12) plus a mostly API-level acceptance suite (126 of 154 criteria are API-level). |
| **Spec-authorship bias** | Author the spec and tests before any run; disclose any AI assistance; independent review. |
| **Cost** | Size the core scope after the pilot; keep the reserve scope out of confirmatory runs. |

### What was left open in Phase 1 and how it is resolved
Phase 1 deliberately left the technology stack, entity list, API shape, UI details, non-functional constraints, seed data and test contracts open, noting only a **lean** toward a JVM backend (Spring Boot) with a TypeScript/React frontend. **Update 2026-11-15:** the maintainer locked **.NET (ASP.NET Core Web API, EF Core, PostgreSQL) with a TypeScript/React frontend** ([decisions.md](decisions.md) D-001), and the detailed specification is [benchmark-spec.md](benchmark-spec.md). Candidate mutation tools become Stryker.NET (C#) and StrykerJS (TypeScript), coverage uses `coverlet`, and SonarQube analysis uses its C# and TypeScript support (all candidate tools, versions to be verified and pinned in Phase 3; not yet piloted). **Caveats unchanged:** results are language/stack-specific; the maintainer's familiarity with a stack could bias spec writing; model familiarity with a stack varies and cannot be controlled.

## 4. Why not pick a public benchmark task?
Public tasks are widely seen by model developers; OpenAI has stated it no longer evaluates SWE-bench Verified (see originality review B5), and METR published a note titled "Many SWE-bench-Passing PRs Would Not Be Merged into Main" (B6; we verified the title and date only). Those sources motivate using a **custom, newly written specification** for S2Q, while acknowledging that a custom task cannot eliminate every contamination path and also gives up cross-benchmark comparability.
