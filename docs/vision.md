# Spec-to-Quality (S2Q) — Vision

> **Status:** Phase 1 draft awaiting review. Nothing in this document is an experimental result. No experiment has been run. Statements about the literature are cited in [originality-review.md](originality-review.md); statements about models and prices are in [model-selection.md](model-selection.md) and are dated snapshots, not durable facts.

## 1. Problem statement

AI coding models are routinely compared by what they cost per token and by how often they pass functional benchmarks. Engineering leaders, however, pay for a different quantity: the total cost of getting a *specification* implemented as software that clears an agreed engineering-quality bar. That total includes the initial generation, the testing effort, the fixing of static-analysis findings, any regressions introduced while fixing, and the human time spent supervising all of it.

A low-priced model can look attractive on the pricing page and still be expensive end to end if its output needs more remediation, or cannot reach the quality bar at all within a reasonable budget. The opposite is also possible: a premium model could cost more per token and still be cheaper (or no cheaper) once everything is counted. **Which of these happens is an empirical question. This project does not assume an answer.**

The closest existing work (see the originality review) measures static-analysis quality of LLM-generated code, or evaluates spec-driven prompting, or evaluates automated repair of static-analysis warnings — but, on the evidence gathered so far, we did not find a study that follows one controlled specification through multiple coding models, scans the generated applications with one pinned static-analysis configuration, runs a bounded remediation stage against a predefined gate, and reports the cost of the whole chain. That is the space S2Q targets. This is a statement about what a web-based review found, not a proof that no such study exists (see originality-review §5).

## 2. Motivation

- **Pricing pages are not total cost.** Per-token list prices ignore token volume (which differs by model and harness), retries, remediation, and quality outcomes.
- **Functional benchmarks are not quality evidence.** Passing tests says little about maintainability, reliability smells, security findings, duplication, or test quality. Several 2025–2026 studies scan AI-generated code with static analysis (originality review §2), but mostly on small tasks or on in-the-wild pull requests rather than whole controlled applications from one specification.
- **Specification-driven development (SDD) is spreading faster than evidence about it.** Tooling such as GitHub Spec Kit is popular; controlled evidence on downstream quality cost is thin (originality review §3).
- **Leaders need a decision framework, not a leaderboard.** Model rankings expire in weeks. A transparent, re-runnable method for measuring *spec → code → quality → cost* has a longer shelf life than any single result.

## 3. Vision

An independent, open-source experimental framework in which several coding models, each inside the same agent harness, independently implement the *same* approved engineering specification under controlled conditions. Every resulting application is evaluated with identical, pinned validation tooling. The framework measures — and reports without hiding failures — the following twelve items from the brief:

1. Specification fidelity
2. Functional correctness (independent acceptance tests)
3. Static code-analysis quality
4. Reliability findings
5. Security findings (SAST only)
6. Code duplication
7. Cognitive and cyclomatic complexity
8. Maintainability
9. Automated test coverage and test effectiveness
10. Generation time and inference cost
11. Remediation effort (estimated by the scanner *and* measured)
12. Total cost to reach a predefined engineering quality gate

The pipeline under study is:

```
Specification → AI Model (+ harness) → Generated Application → Static Analysis → Remediation → Engineering Cost
```

Results are always attributed to a **model-plus-harness configuration**, not to a model in isolation, unless a later sensitivity analysis supports a narrower claim (research-methodology §6).

## 4. Intended audience

- Engineering leaders and staff/principal engineers making model-selection and budget decisions.
- Researchers in empirical software engineering who want a reproducible protocol (including its limitations) to extend.
- Practitioners of spec-driven development who want evidence on what quality they should expect to *pay for* after generation.
- Not intended as marketing for, or against, any provider.

## 5. Research questions

**Primary (RQ0).** Does the initial cost advantage of a lower-priced AI coding model remain meaningful after accounting for functional correctness, static-analysis findings, testing, and remediation effort?

**Supporting questions** (from the brief; each maps to hypotheses in research-methodology §1):

| ID | Question | Related hypothesis |
|----|----------|--------------------|
| RQ1 | Do different coding models produce materially different engineering quality from identical specifications? | H1 |
| RQ2 | Which quality characteristics vary most between models? | H1 |
| RQ3 | Does functional correctness correlate with maintainability? | H3 |
| RQ4 | Does higher test coverage correspond to better testing effectiveness? | H3 |
| RQ5 | How much static-analysis debt is introduced during initial generation? | H1 |
| RQ6 | How many remediation iterations are needed to satisfy a common quality gate? | H4 |
| RQ7 | Can a model's apparent cost advantage disappear after remediation? | H2 |
| RQ8 | Are quality differences consistent across repeated independent runs? | H1, H5 |
| RQ9 | How much variation comes from the model versus the agent harness or environment? | confound analysis (§6 of methodology) |

## 6. Expected engineering value

*Expected* here means "what the project is designed to deliver if it is executed, not a prediction of findings."

- A **re-runnable protocol** for comparing coding models on a realistic multi-tier application, including a quality gate, a budgeted remediation stage, and an economics model.
- A **cost-to-gate dataset** (reached / not reached, tokens, wall-clock, estimated vs measured effort) with raw per-run records preserved.
- A **sensible vocabulary** for talking about static findings (candidate defects, not confirmed ones) and about estimated vs measured remediation effort.
- A **clear description of what static analysis can and cannot say**, so that results are not over-read as "production readiness".
- Reusable recommendations for engineering leaders, each tied to a specific piece of evidence and labelled with its limits.

## 7. Scope

**In scope (initial research):**
- One benchmark application (recommended: incident management; see [benchmark-application.md](benchmark-application.md)), specified in Phase 2 and frozen before any model run.
- Multiple coding models across low / mid / premium price tiers from multiple providers, run in one common harness where technically possible.
- Stage A (initial generation, no feedback hooks, no human correction) and Stage B (bounded remediation with identical feedback process).
- Static analysis with SonarQube using a pinned version and configuration, plus independent evaluation tools (acceptance tests, coverage, mutation testing) listed in quality-metrics.md.
- Cost analysis including inference, scanner/infrastructure cost, and measured human time.

**Explicitly excluded from the initial scope (may become independent future projects):**
- DAST and runtime security testing
- Cryptographic posture analysis
- Infrastructure / deployment security
- Performance, load, and scalability testing
- Accessibility and UX quality beyond what acceptance tests define
- Fine-tuning, prompt-optimisation, or model-specific prompt engineering
- Human code review of generated code as a measurement instrument (a limited human-time sub-study is a Phase 2 decision)
- Claims about production readiness of any model's output

**Also excluded:** proprietary company code, specifications, or internal data. This is a public project and nothing proprietary may be used (brief §13.12).

## 8. Success criteria

Phase-level success is defined by the *quality of the research*, not by what the results turn out to be. A null or surprising result still counts as success.

1. **Specification:** one finalized, version-controlled specification with machine-checkable acceptance criteria, approved before execution.
2. **Pre-registration:** hypotheses, metrics, statistics, gate definition, budgets, stopping rules, and model set are committed (and tagged) before the confirmatory runs. Deviations are logged.
3. **Reproducibility:** a third party can re-run the pipeline from the repository with pinned versions and obtain results consistent with ours within the reported variation (or document why not).
4. **Completeness:** every attempted run — including failed, timed-out, and budget-exhausted runs — is preserved and reported. No selective exclusion.
5. **Validity:** threats to validity are discussed per hypothesis; confounds between model and harness are measured or disclosed.
6. **Honest reporting:** each hypothesis is reported as supported, contradicted, or inconclusive against criteria fixed in advance, with effect sizes and confidence intervals.
7. **Utility:** engineering leaders can reuse the cost-to-gate method for their own model choices without relying on our specific results.

## 9. Non-goals and guardrails

- No arbitrary composite "quality score". Metrics are reported separately; any aggregation must be justified in advance (quality-metrics §6).
- No claim that SonarQube — or any single tool — proves production readiness.
- No claim that a model is superior in general. Findings apply to the tested configurations, application, language/stack, date, and settings.
- No result is written into any document until it exists as raw data in the repository.
