# Spec-to-Quality (S2Q) — Phase 1 Summary

> **Status:** Phase 1 draft (prepared 2026-11-14, America/Detroit). **Update 2026-11-15:** the maintainer locked the stack, harness, reasoning-effort policy and Stage B cap, and the frozen spec v1.0.0-rc1 was written — see [decisions.md](decisions.md) and [benchmark-spec.md](benchmark-spec.md). The text below is the Phase 1 snapshot, annotated where superseded. No experiments have been run; no results exist. Pricing and tool facts are dated snapshots.

## Vision
An independent, open-source experimental framework in which several AI coding models, run in one common agent harness, each implement the **same frozen engineering specification**. Every resulting application is analysed with identical, pinned tooling (SonarQube static analysis plus independent acceptance tests, coverage and mutation testing), put through a bounded **remediation stage** against a predefined quality gate, and costed end to end. It studies the chain *Specification → AI Model → Generated Application → Static Analysis → Remediation → Engineering Cost*, and reports results as **model-plus-harness configurations**, not "model X is best".

## Primary research question and hypothesis
**RQ0:** Does the initial cost advantage of a lower-priced AI coding model remain meaningful after accounting for functional correctness, static-analysis findings, testing and remediation effort?

**Primary hypothesis — H2 (Cost–quality trade-off):** *Lower initial inference costs do not necessarily produce lower costs to reach an acceptable engineering quality gate.* Supported if at least one low-tier configuration is cheaper at Stage A but not cheaper to reach the gate (or reaches it less often), judged by pre-registered CIs; contradicted if low-tier configurations keep a lower cost-to-gate and equal-or-better gate-reached proportion with CIs excluding parity. Supporting hypotheses: H1 (static-quality variance across models), H3 (functional/quality disconnect), H4 (remediation overhead differs by model), H5 (controlled spec reduces ambiguity but not implementation variance). All are **hypotheses, not facts**; each has support/contradict/inconclusive criteria in `research-methodology.md`.

## Recommended benchmark application
A self-contained **incident-management application** (declare, triage, escalate, resolve and review incidents against services/teams, with RBAC, SLA rules, audit trail and a dashboard). It fits the brief's criteria (realistic, frontend + backend, persistence and non-trivial state-machine/business logic, independently testable through a public API/UI contract, no paid dependencies). Main risks: some training-data contamination from common ticket/incident patterns and the need for an injectable clock for time-based rules. Stack (locked 2026-11-15): .NET backend (ASP.NET Core, EF Core, PostgreSQL) + TypeScript/React frontend; detailed spec: `benchmark-spec.md`.

## Model strategy
Stratify by **list output price** (pre-registered; not a capability ranking) across ≥ 4 providers plus an open-weight option; pin exact IDs; record snapshots, returned model IDs, effort settings and harness versions per run. **Prices checked 2026-11-14 (USD per 1M tokens; input / cached-read / output):**

| Tier | Model ID | Input / cached / output | Source |
|---|---|---|---|
| Low | `claude-haiku-5-5` | 0.10 / 0.01 / 0.50 (prompts >100K: 0.50 / 0.05 / 2.50) | https://platform.claude.com/docs/en/about-claude/pricing |
| Low | `gpt-6-luna` | 0.10 / 0.01 / 0.50 | https://developers.openai.com/api/docs/pricing |
| Low (open-weight) | `deepseek-flash` | 0.15 / 0.003 / 0.60 off-peak; 0.30 / 0.006 / 1.20 peak | https://api-docs.deepseek.com/quick_start/pricing |
| Mid | `claude-sonnet-5-5` | 2.00 / 0.10 / 10.00 | Anthropic pricing page |
| Mid | `gpt-6.1-sol` | 2.00 / 0.10 / 10.00 | OpenAI pricing page |
| Mid | `gemini-4-hydrogen` | 1.50 / 0.075 / 7.50 | https://ai.google.dev/gemini-api/docs/pricing |
| Mid | `grok-4.7` | 2.00 / 0.50 / 6.00 (≥200K prompts: 4.00 / 1.00 / 12.00) | https://docs.x.ai/developers/models |
| Premium | `claude-opus-5-5` | 4.00 / 0.20 / 20.00 | Anthropic pricing page |
| Premium | `gpt-6-astra` | 10.00 / 1.00 / 50.00 | OpenAI pricing page |
| Premium | `gemini-4-carbon` | 2.00 / 0.10 / 10.00 **promo through 2026-11-30**; 4.00 / 0.20 / 20.00 from 2026-12-01 | Google pricing page |

Reserve: `claude-fable-5-2` (8 / 0.20 / 40), `gemini-4-argon` (promo, same as Carbon), `gemini-3.5-flash-lite` (0.30 / 0.03 / 2.50), `grok-build-0.1` (1 / 0.20 / 2), `deepseek-v4-pro` (1.32 / 0.044 / 3.96 peak), `mistral-large-4` (0.70 / 0.07 / 2.10; API ID unverified). Qwen: no ID/price verified. Harness (locked 2026-11-15): **`mini-swe-agent`** as the primary arm; an optional second-harness sensitivity arm (OpenHands/OpenCode) to estimate model×harness variation remains open.

## Potential original contribution (hedged)
In a **non-systematic, web-based** search we did not find a study that follows one frozen specification through multiple price-tiered models, a pinned static-analysis pipeline, a bounded remediation stage with gate-integrity controls, and a censoring-aware cost-to-gate analysis — nor one that sets Sonar's *estimated* remediation effort against *measured* remediation effort. The nearest work: Sonar's LLM comparisons (vendor, task-level), a matched SDD-vs-vibe-coding study (functional pass rate, 28× tokens, no static quality), in-the-wild agent-PR studies, and LLM repair of static warnings. Novelty must be re-checked before publication; see `originality-review.md` for limits.

## Methodological risks (top)
1. **Statistical power:** with n = 10 runs per configuration, only large effects (≈ A12 ≥ 0.8) are reliably detectable after multiplicity correction (planning simulation under a normal-shift assumption: power ≈ 0.67 at α = 0.05 and ≈ 0.24 at α = 0.005 for A12 = 0.80). Mitigation: small pre-registered confirmatory family, n = 20 extension rule, report minimum detectable effect, treat the rest as exploratory.
2. **Model–harness confound:** results are configuration results; the variance split is only identifiable with a sensitivity arm.
3. **Goodhart/gaming in remediation** (suppression, scan exclusions, deleting tests): integrity checks and a gate with functional and test guardrails.
4. **Static findings ≠ defects;** Sonar's estimated effort ≠ measured effort; no composite score.
5. **Single application/stack and contamination:** no cross-task generalisation claimed.
6. **Volatile pricing and models:** Gemini 4 promo ends 2026-12-01; DeepSeek peak/off-peak; long-context surcharges; tokenizer differences; silent model updates.
7. **Cost-to-gate is right-censored:** failures must be reported, not dropped.
8. **Vendor-sourced evidence** (Sonar) and edition/rule-set dependence: pin and disclose.

## Decisions to finalize before Phase 2 — status as of 2026-11-15
Full records: [decisions.md](decisions.md).

| # | Decision | Status |
|---|---|---|
| 1 | Technology stack, persistence engine, test frameworks | **Locked:** .NET (ASP.NET Core Web API, EF Core, PostgreSQL) + TypeScript/React; xUnit + `coverlet`, Vitest (D-001) |
| 2 | Primary harness; sensitivity arm | Harness **locked: `mini-swe-agent`** (D-002); sensitivity arm still open (O-010) |
| 3 | Reasoning-effort policy | **Locked: provider default**, recorded per model in the run manifest (D-003); tier rule for promotional prices still open (O-007) |
| 4 | Core model set and budget ceiling; confirm *n* | **Open** — budget cap explicitly undecided (O-001); model set and *n* after the pilot (O-007) |
| 5 | Quality-gate conditions/thresholds; SonarQube edition/mode; new-code definition | **Open** (O-002) |
| 6 | Stage B design | Iteration cap **locked: max 3 rounds** (D-004); budget form, feedback template, hidden-failure feedback still open (O-008) |
| 7 | UI acceptance contract; injectable clock | **Resolved in spec v1.0.0-rc1:** `data-testid` contract and test-mode clock endpoints (spec §7, §12); UI test tooling still open (O-003) |
| 8 | Human-time sub-study | **Open** (O-004) |
| 9 | Mutation-testing tool, scope, time limit | **Open**; suggested Stryker.NET (C#) and StrykerJS (TypeScript), not yet piloted (O-005) |
| 10 | License, repository, affiliations | **Open** (O-006); repository `xops-labs/spec-to-quality` exists, no `LICENSE` file yet |
