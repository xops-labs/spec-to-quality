# S2Q Decision Log

> Short, ADR-style records. Each decision has an ID, a date (America/Detroit), a status and its rationale. Locked decisions were made by the maintainer (Yasvanth) on **2026-11-15**; rationale text is the drafting author's reasoning, not new evidence. Changing a locked decision requires a new entry that supersedes it, and invalidates earlier runs for confirmatory comparison. No results exist yet.

## Locked decisions

### D-001 — Technology stack: .NET backend, TypeScript/React frontend
- **Date / status:** 2026-11-15 · **Locked**
- **Decision:** Backend in C# on **ASP.NET Core Web API** using the **current .NET LTS at the spec freeze date (.NET 10; exact SDK patch pinned in the sandbox image in Phase 3)**, **EF Core** with migrations, **PostgreSQL**. Frontend in **TypeScript + React**. Single origin: the backend serves the built frontend.
- **Supersedes:** the Phase 1 *lean* toward a JVM backend (`benchmark-application.md`).
- **Rationale:** the maintainer's choice; a typed, mainstream enterprise stack with available tooling for coverage (`coverlet`), static analysis (SonarQube analysis of C# and TypeScript) and mutation testing (candidate tools below; none piloted yet). PostgreSQL keeps the persistence layer open-source and containerisable with no paid dependency.
- **Consequences / caveats:** results are specific to this stack and must not be generalised to other languages. The maintainer's familiarity with .NET could bias spec authoring (disclose). Model familiarity with .NET/React varies and is not controlled. The Phase 1 candidate mutation tool for JVM (PIT) is dropped.

### D-002 — Agent harness: `mini-swe-agent`
- **Date / status:** 2026-11-15 · **Locked** (primary arm)
- **Decision:** All primary-arm runs use `mini-swe-agent` (pinned version/commit and container image digest recorded per run, Phase 3). Its model access goes through an OpenAI-compatible/multi-provider layer (LiteLLM or equivalent; version recorded).
- **Rationale:** minimal, bash-centred scaffolding maximises comparability across providers (same tool surface) and keeps harness-specific behaviour small, which helps attribute differences to the model. Open-source and multi-provider.
- **Consequences / caveats:** a minimal harness may under-represent how practitioners use richer agents; results are reported as **model + `mini-swe-agent`** configurations. Models that cannot use the harness's action format reliably are flagged, not silently dropped. Whether to run a **second-harness sensitivity arm** (OpenHands/OpenCode, for RQ9) remains open (see O-010).

### D-003 — Reasoning effort: each provider's default
- **Date / status:** 2026-11-15 · **Locked**
- **Decision:** No explicit reasoning-effort override. Every run uses the provider/model default. The run manifest records, per model: `reasoning_effort_requested = "provider_default"`, the *effective* default as stated in the provider's documentation on the snapshot date (or `"not documented"`), the returned reasoning-token counts where reported, and the documentation URL and retrieval date.
- **Rationale:** reflects what a practitioner gets out of the box and avoids guessing a matched effort level that is not equivalent across providers.
- **Consequences / caveats:** defaults differ across providers and may change silently; effort then varies *with* model, so effort effects are part of the "model" effect and cannot be separated. An explicit matched-effort arm is an optional exploratory extension (not planned).

### D-004 — Stage B remediation: at most 3 rounds per run
- **Date / status:** 2026-11-15 · **Locked**
- **Decision:** Stage B allows **at most 3 remediation rounds** per run (a round = one feedback delivery plus agent attempt plus rebuild/re-scan/re-gate). Stopping also on: gate passes; per-stage budget exhausted; no net reduction in gate-relevant issues for *k* consecutive rounds (*k* = 2, still **[PROPOSED]**); agent self-reports inability. Stopping without passing = **gate not reached**, a first-class (right-censored) result.
- **Supersedes:** the Phase 1 proposal of 5 iterations (`research-methodology.md` §4.2).
- **Rationale:** the maintainer's choice; bounds cost and keeps remediation comparable.
- **Consequences / caveats:** a small cap raises the share of right-censored runs and makes the "reached gate" proportion the main economic outcome; pilot data may show the cap is binding for some configurations; sensitivity of conclusions to the cap cannot be assessed from runs capped at 3 (post-hoc "what if 5 rounds" analysis is not possible without extra runs).

## Open decisions (not yet decided)

| ID | Item | Status | Notes / current proposal |
|---|---|---|---|
| O-001 | **Total API budget cap** | **Open** — explicitly left undecided by the maintainer on 2026-11-15 | Size after the pilot (n = 3 per configuration) from measured token use; see `model-selection.md` §6 for why no estimate is given. Proposed structure: identical high per-run dollar ceiling as a safety stop plus equal turn/tool-call/wall-clock limits; cost reported as an outcome. |
| O-002 | **Quality-gate thresholds and SonarQube edition/mode** | Open | Conditions and thresholds (strict/lenient variants), Community vs. a paid edition, Standard vs. MQR mode, and the "new code" definition for a greenfield app (`quality-metrics.md` §7). The edition decides which analyzers/rules are available for C# and TypeScript; verify and pin before Phase 3. |
| O-003 | **UI test tooling** | Open | The spec fixes the `data-testid` contract (spec §12), so any browser-automation tool works. Candidate: Playwright (Chromium, one browser). To be decided and pinned in Phase 2/3; hidden UI tests are black-box. |
| O-004 | **Human-time sub-study** | Open | Whether to run it, on what sample, with what timer protocol (`research-methodology.md` §4.3). Never merged with agent-only results. |
| O-005 | **Mutation-testing tools and scope** | Open (suggestion recorded) | Suggested: **Stryker.NET** for C# and **StrykerJS** for TypeScript. Both are candidate tools **not yet piloted**; verify versions, supported test runners and runtime on the sandbox in the pilot. Scope (e.g. domain/service layer), time limit and equivalent-mutant handling to be fixed in advance. |
| O-006 | **License, repository metadata and affiliations** | Open | Suggested Apache-2.0 for code and CC BY 4.0 for documents/data (`README.md`); no `LICENSE` file yet. Affiliations, credits and provider relationships to be declared before publication. |
| O-007 | **Model set, tier rule for promotional prices, n** | Open | Core set of 10 proposed in `model-selection.md`; n = 10 with extension rule to 20 confirmed after the pilot. |
| O-008 | **Stage B details beyond the cap** | Open | Token-vs-dollar budget, feedback template, and whether hidden-test failures are fed back (default: no). |
| O-009 | **Coverage and scanner tooling versions** | Open (tools suggested) | Suggested: `coverlet` (Cobertura) for .NET, V8/Istanbul via the test runner for TypeScript, SonarScanner for .NET plus the TypeScript analysis for SonarQube. Pin versions in Phase 3. |
| O-010 | **Second-harness sensitivity arm** | Open | Optional OpenHands/OpenCode arm for a model-subset (RQ9). Without it the model-vs-harness variance split is not identifiable and will not be claimed. |

## Spec-related decisions

### D-005 — Frozen benchmark specification v1.0.0-rc1
- **Date / status:** 2026-11-15 · **Release candidate** (becomes v1.0.0 after maintainer sign-off and validation of the hidden acceptance suite against a reference implementation)
- **Decision:** `docs/benchmark-spec.md` is the single specification all configurations implement. It fixes behaviour (API, state machine, SLA rules, audit, UI test-id contract) and leaves architecture free. Time-based rules use an injectable clock with test-mode endpoints, so tests advance time instead of sleeping.
- **Rationale:** objective, black-box-testable acceptance criteria (154 in rc1) satisfy the brief's "independent evaluation tests" and "avoid subjective evaluation" requirements; unusual-but-plausible rules (duplicate-title rule, SEV1 tier rule, closure rules) reduce reliance on memorised public implementations (contamination risk is still acknowledged).
- **Caveats:** the spec was drafted with AI assistance (to be disclosed in the publication). Spec bloat and over-prescription reduce design freedom relevant to H5; the UI test-id contract constrains UI markup.
