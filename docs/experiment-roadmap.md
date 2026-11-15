# Spec-to-Quality (S2Q) — Experiment Roadmap

> **Status:** Phase 1 draft awaiting review. Durations are deliberately omitted: effort depends on the maintainer's availability and on API budget decided after the pilot. Each phase ends with a **gate for the next phase** — a review checkpoint where a human decides to proceed.

Principles across all phases: pre-register before confirmatory runs; preserve raw data; never delete failed runs; keep proprietary material out; record every deviation.

## Phase 1 — Vision (current)

- **Objective:** define the problem, research questions, hypotheses, methodology, metrics, originality position, benchmark application recommendation and model-selection strategy.
- **Inputs:** the project brief; literature and tool review; official provider pricing pages (2026-11-14).
- **Deliverables:** `README.md`, `docs/vision.md`, `docs/research-methodology.md`, `docs/quality-metrics.md`, `docs/experiment-roadmap.md`, `docs/originality-review.md`, `docs/phase1-summary.md`; supporting `docs/model-selection.md`, `docs/benchmark-application.md`, `data/pricing-snapshots/2026-11-14.json`.
- **Exit criteria:** the maintainer reviews and accepts (or amends) the open decisions listed in the Phase 1 summary; the repository is created and the documents published.
- **Risks:** novelty claims being too strong (mitigated by hedged wording); pricing and model lineups going stale (mitigated by dated snapshots).
- **Out of scope:** application code, harness code, experiments, results.

## Phase 2 — Specification

- **Objective:** produce **one finalized, version-controlled engineering specification** and the independent evaluation assets derived from it.
- **Activities:**
  1. Decide the technology stack (Phase 1 lean: a JVM backend with a TypeScript/React frontend, with caveats — see benchmark-application.md), persistence engine, build tool and test frameworks.
  2. Author the specification: domain model, functional requirements with unique IDs, non-functional constraints that are *objectively testable*, API contracts, UI behaviour defined at the level the acceptance tests can check, data-validation rules, error behaviour, seed data.
  3. Author the **hidden acceptance test suite** and traceability matrix (requirement ID ↔ tests); review for ambiguity and for leakage of implementation choices.
  4. Define the **fidelity rubric** (and inter-rater procedure).
  5. Define the **quality gate** (conditions and thresholds, strict/lenient variants), **Stage B feedback template**, iteration/budget/stopping rules, and **integrity checks**.
  6. Decide the human-time sub-study design (or omit it).
  7. Pre-register: hypotheses, outcomes, contrasts, statistics, *n*, extension rule, exclusion rules. Tag the repository.
  8. Optionally pilot-read the specification with one or two models *only to find ambiguity* — those pilot outputs are discarded from the study and the specification is frozen afterwards.
- **Deliverables:** `spec/` (versioned), `tests/acceptance/` (hidden-suite source kept out of agent-accessible paths), gate definition, pre-registration file, Phase 2 review notes.
- **Exit criteria:** specification approved and tagged; acceptance suite validated against a reference implementation (or against a known-good run) so test defects are found *before* model runs; gate and budgets frozen.
- **Risks:** spec ambiguity; spec so detailed it removes design variation (and thereby H5-relevant variance); hidden-test defects biasing results; authoring bias toward one model family if AI helped write the spec (disclose assistance).

## Phase 3 — Experiment harness

- **Objective:** build the reproducible pipeline: sandboxing, model invocation through the common harness, Stage A/B orchestration, scanning, test execution, data capture.
- **Activities:** choose and pin the harness (mini-swe-agent / OpenHands / OpenCode — decision after a feasibility pilot); build the sandbox image with pinned toolchains; set up SonarQube (pinned) and scanner automation; implement coverage and mutation pipelines; implement raw-data capture (usage logs, transcripts, run records); implement integrity checks; implement cost computation from pricing snapshots; implement run randomisation and the analysis scripts (statistics from methodology §7); build a minimal dashboard.
- **Deliverables:** harness code, container definitions, configuration files, run-record schema, analysis scripts, harness README; the power-simulation script.
- **Exit criteria:** an end-to-end **dry run with a trivial dummy spec** passes; a **pilot with n = 3 per configuration** completes; pilot yields cost/token/variance estimates used to finalise *n*, budgets and gate thresholds; pilot data is labelled and excluded from confirmatory analysis.
- **Risks:** provider integration differences (tool-calling formats, reasoning-token handling); scanner nondeterminism; harness bugs favouring some models; sandbox network policy; pipeline cost overruns.

## Phase 4 — Model execution

- **Objective:** run the confirmatory Stage A (and then Stage B) experiments exactly as pre-registered.
- **Activities:** take a fresh pricing snapshot immediately before each batch; confirm model IDs still resolve and record returned IDs; execute randomised, interleaved runs in a bounded time window; monitor for infrastructure faults using pre-defined checks; apply the pre-registered *n* = 20 extension rule if triggered; archive everything.
- **Deliverables:** raw run records, transcripts, code snapshots, provider usage logs, execution logs, final pricing snapshot, an execution report listing every run, its status and any deviation.
- **Exit criteria:** all planned runs executed or explicitly recorded as failed/aborted with reasons; raw data frozen and checksummed.
- **Risks:** model deprecation or silent updates mid-experiment; rate limits; cost overrun; promotional price changes (e.g., 2026-12-01 for some Gemini models) crossing the execution window.

## Phase 5 — Quality analysis

- **Objective:** measure Stage A outputs (and Stage B outputs) with the frozen evaluation pipeline and produce descriptive and inferential analyses.
- **Activities:** build/test/coverage/mutation/Sonar analysis on every snapshot; compute the metric catalog; compute per-run size-normalised metrics; statistics per methodology §7 (confirmatory family first, then exploratory); sensitivity analyses (gate variants, price views, MQR vs Standard); within/between-configuration variation analysis; if available, the harness-sensitivity arm.
- **Deliverables:** machine-readable results tables (CSV/Parquet/JSON), analysis notebooks/scripts, figures, a statistical report that classifies each hypothesis as supported / contradicted / inconclusive against pre-registered criteria.
- **Exit criteria:** every number in the report is reproducible from the archived raw data with committed scripts; independent re-run of analysis by a second person (or a clean-machine rerun) matches.
- **Risks:** analysis flexibility (mitigated by pre-registration); mis-matched issues across snapshots; scanner version drift.

## Phase 6 — Remediation evaluation

- **Objective:** analyse Stage B: how far each configuration can remediate its own findings under the fixed budget, at what cost, with what regression and integrity outcomes; compute cost-to-gate and sensitivity (breakeven) analyses; optionally conduct the human-time sub-study.
- **Activities:** (if Stage B was executed in Phase 4 — otherwise run it here with the same discipline) compute iterations, resolved/introduced issues, regressions, gate outcomes, unresolved findings; compute cost-to-gate with censoring methods; compare estimated vs. measured remediation effort; run the human sub-study on a pre-selected sample.
- **Deliverables:** remediation dataset, cost-to-gate analysis, breakeven analysis, integrity-violation report, sub-study report (if run).
- **Exit criteria:** H2 and H4 evaluated against pre-registered criteria; failed/unreached runs fully reported.
- **Risks:** gaming behaviours; gate calibration too easy or too hard; human-study confounds.

## Phase 7 — Research publication

- **Objective:** publish findings, data and methods with limitations stated plainly.
- **Activities:** write the research article (long form, e.g., Hashnode); a shorter LinkedIn technical discussion; publish the repository with a tagged release; publish raw and derived datasets with a data card; write reusable recommendations for engineering leaders — each recommendation linked to evidence and limits; run an originality re-check (a new literature search) immediately before publication, since the field moves quickly; declare affiliations and any provider credits.
- **Deliverables:** article, discussion post, release tag, data package, dashboard, a "limitations and what this does not show" section.
- **Exit criteria:** an outside reviewer has checked the methodology section and the reproduction instructions; claims in the article match the pre-registered classifications.
- **Risks:** over-claiming (e.g., "model X is best"); stale pricing at publication time (always date-stamp); vendor reactions to unfavourable results (keep to evidence).

## Cross-phase decision log (to be maintained as `docs/decisions/`)

Record every finalized decision: stack, harness, effort settings, budgets, gate thresholds, *n*, models, any deviation, with date and rationale.
