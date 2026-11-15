# Spec-to-Quality (S2Q) — Research Methodology

> **Status:** Phase 1 draft awaiting review. This is a *plan*. Numeric choices marked **[PROPOSED]** are defaults for discussion; those marked **[DECIDE]** must be finalized (and then pre-registered) before Phase 4. Nothing here reports a result.

## 0. Conventions

- **Hypothesis** = a claim we intend to test, not a fact. **Established fact** = something cited to a source in [originality-review.md](originality-review.md) or to official documentation.
- **Configuration** = (model ID + provider endpoint + harness + harness version + reasoning-effort setting + other decoding/execution parameters).
- **Run** = one complete, independent attempt by one configuration to implement the specification (Stage A), optionally followed by Stage B.
- **Gate** = the predefined quality gate (quality-metrics.md §7).

## 1. Hypotheses

Each hypothesis states the claim, what would **support** it, what would **contradict** it, and what would leave it **inconclusive**. Thresholds marked **[PROPOSED]** are fixed in the pre-registration before confirmatory runs. Unless stated, tests are two-sided, family-wise α = 0.05 with Holm correction over the pre-registered confirmatory family (§7).

### H1 — Model quality variance
*Identical specifications can produce significantly different static-analysis results across coding models.*

- **Primary outcome:** Sonar-reported issues per 1,000 lines of code (`ncloc`), by software-quality dimension, and the maintainability/reliability/security ratings; secondary: duplication density, cognitive-complexity density.
- **Supports:** Kruskal–Wallis across configurations rejects at the corrected α **and** at least one pre-specified pairwise contrast has an effect size beyond a negligible region (Vargha–Delaney A12 outside 0.44–0.56 **[PROPOSED]**) with a 95 % bootstrap CI excluding 0.5.
- **Contradicts:** omnibus test not rejected *and* pairwise CIs for A12 lie within the negligible region with n large enough to have had power for the medium effect (§7.4). If the CIs are wide and include both, the result is *inconclusive*, not contradicting.
- **Notes:** counts must be size-normalised (per KLOC), because raw issue counts partly track code volume. Cynthia et al. report that raw-count differences between agents largely disappeared after churn normalisation (originality review ref. [S6]), which is why normalisation is built into H1 rather than added afterwards.

### H2 — Cost–quality trade-off
*Lower initial inference costs do not necessarily produce lower costs to reach an acceptable engineering quality gate.*

- **Outcome:** (i) proportion of runs that reach the gate within the Stage B budget; (ii) total cost-to-gate **conditional on reaching it**; (iii) cost rank of configurations at Stage A vs. at gate.
- **Supports:** at least one low-tier configuration whose Stage A cost is lower than a pre-specified comparator (mid or premium tier) but whose cost-to-gate is **not** lower (CI for the difference in median cost-to-gate includes zero or is positive), or whose gate-reached proportion is lower with a Clopper–Pearson/Wilson CI that separates the two.
- **Contradicts:** low-tier configurations retain lower cost-to-gate **and** equal-or-higher gate-reached proportion across all pre-specified comparisons, with CIs excluding parity.
- **Notes:** "Not necessarily" is a weak claim; a single counter-example configuration supports it. The pre-registration therefore also states the *stronger* question (RQ0): is the cost advantage still *meaningful*, defined as a ratio threshold **[DECIDE]** (e.g., low-tier cost-to-gate ≤ 0.5 × comparator).

### H3 — Functional–quality disconnect
*Successful functional implementation does not guarantee strong maintainability, reliability, or test quality.*

- **Outcome:** acceptance pass rate vs. (maintainability rating, issue density, duplication density, mutation score).
- **Supports:** among runs with acceptance pass rate above a pre-specified high threshold **[DECIDE, e.g. ≥ 95 %]**, a non-trivial share still fails the gate on static quality or has a mutation score below a pre-specified level; and/or weak rank correlation (Spearman |ρ| below 0.3 **[PROPOSED]**, with a bootstrap CI) between pass rate and quality measures.
- **Contradicts:** all (or nearly all) high-pass runs also meet the quality thresholds **and** a strong, CI-supported monotone relationship exists.
- **Notes:** correlation is computed on runs (pooled across configurations) *and* within configurations, because pooled correlations can be produced entirely by between-model differences (Simpson-style effects). The supporting question "does higher coverage correspond to better test effectiveness?" is tested the same way (coverage vs. mutation score).

### H4 — Remediation overhead
*Models differ in the amount of additional engineering effort needed to correct quality findings.*

- **Outcome:** Stage B iterations, tokens, wall-clock, inference cost, issues resolved, issues introduced, test regressions, gate reached — each reported separately; **estimated** remediation effort (Sonar) and **measured** effort (agent time/cost; human minutes in the sub-study) are never merged.
- **Supports:** Kruskal–Wallis on conditional Stage B cost (among runs that start Stage B) rejects, with pairwise effect sizes as in H1; or differences in the gate-reached proportion.
- **Contradicts:** no detectable differences with adequate power (§7.4).
- **Notes:** runs that already pass the gate at Stage A have zero remediation by definition and are reported as such, not dropped. Because Stage B begins from different Stage A states, remediation differences partly reflect Stage A differences — this is part of the phenomenon, but analyses will report Stage B cost both unconditional and per resolved issue.

### H5 — Specification consistency
*A controlled specification reduces requirement ambiguity but does not eliminate variation in implementation quality.*

- **Two sub-claims:** (a) *ambiguity reduction* — implementations are consistent on specified behaviour; (b) *residual variation* — implementation quality still varies across independent runs of the **same** configuration.
- **Evidence for (a):** acceptance-test pass rates and requirement-completion rates are high and have small between-run variance on specified behaviour; independent requirement-by-requirement fidelity audit finds few "interpretation divergences" (§5.3).
- **Evidence for (b):** within-configuration dispersion (IQR, coefficient of variation, range) of quality metrics is non-trivial — e.g., within-configuration spread comparable to a meaningful fraction of the between-configuration spread (intraclass-correlation-style ratio, reported with CI).
- **Contradicts (a):** requirement interpretation diverges widely despite the controlled spec. **Contradicts (b):** quality metrics are near-identical across repeated runs of a configuration.
- **Caveat:** H5(a) is only testable relative to some baseline of ambiguity. The baseline (e.g., an intentionally under-specified prompt) is an *optional extension*, not part of the minimal design; without it H5(a) can only be evaluated in absolute terms.

## 2. Variables

### 2.1 Independent variables (manipulated)

| Variable | Levels | Notes |
|---|---|---|
| Coding model configuration | ~9–10 core configurations across low/mid/premium price tiers and ≥4 providers (see model-selection.md) | Tier assignment is a pre-registered stratification by *list output price*, not a capability ranking. Price is also analysed as a continuous covariate. |
| Evaluation stage | A (initial), B (post-remediation) | Within-run repeated measure. |

### 2.2 Secondary manipulated factors (sensitivity arms, only if budget permits)

| Factor | Levels | Purpose |
|---|---|---|
| Agent harness | primary harness vs. a second open-source harness | Quantify model×harness interaction (RQ9). |
| Reasoning-effort setting | **provider default (locked 2026-11-15, D-003)**; an alternative effort level is an optional exploratory extension, not planned | Effort settings change both quality and cost; with provider defaults, effort differences are part of the model effect. |

### 2.3 Dependent variables (measured)

Defined fully in [quality-metrics.md](quality-metrics.md). In brief: build success; acceptance pass rate; requirement completion; issues by software-quality dimension, density per KLOC, severity; reliability/maintainability/security ratings; duplication density; cognitive and cyclomatic complexity (and per-function distribution); line and branch coverage; mutation score; inference tokens (by type), cost, wall-clock, tool calls, completion status; Stage B iterations, issues resolved/introduced, regressions, gate status; measured human minutes (sub-study); infrastructure cost.

### 2.4 Controlled variables (held constant)

1. **Specification** — one frozen, version-controlled document with acceptance criteria (Phase 2); same text for every run.
2. **Technology stack** — identical language/framework/database/test frameworks/build tool, fixed in the specification. **Locked 2026-11-15** ([decisions.md](decisions.md) D-001): .NET (ASP.NET Core Web API, EF Core, PostgreSQL) + TypeScript/React; spec: [benchmark-spec.md](benchmark-spec.md).
3. **Implementation instructions** — one prompt template; no model-specific wording.
4. **Harness** — same harness, version, tool set, permissions, and sandbox image wherever technically possible.
5. **Budgets** — equal wall-clock, step/turn, and tool-call limits for all configurations; a high *safety* cost ceiling applied equally (see §4.3 for the token-vs-dollar dilemma).
6. **Evaluation tooling** — pinned SonarQube edition/version, scanner version, analyzer plugin versions, rule profile, quality gate, coverage tool, mutation tool, and hidden acceptance-test suite.
7. **Execution environment** — same container image, resources, OS, JDK/Node versions, time limits, network policy.
8. **Stage A conditions** — no custom SonarQube feedback hooks, no model-specific quality instructions, no human correction, no manual modification before measurement (brief §5–6).
9. **Stage B feedback process** — identical format and delivery for all configurations (§4.2).

### 2.5 Uncontrolled / recorded-only variables

Provider-side nondeterminism, silent model updates behind an alias, routing/load effects, time-of-day price tiers (DeepSeek peak/off-peak) and promotional prices, tokenizer differences (same text → different token counts per provider), caching behaviour (implicit vs explicit), rate-limit-induced retries, and network conditions. These are **recorded**, not controlled, and are reported as limitations (§8).

## 3. Experimental design

- **Design:** repeated-measures comparison of configurations on a single application. Unit of observation = run. Each configuration produces *n* independent runs from a clean workspace; runs share nothing (no memory, no cached workspace, no shared scratch).
- **Randomisation:** execution order of (configuration × run-index) is randomised with a recorded seed and interleaved, so provider drift and time-of-day effects are not confounded with configuration. Scanner and test infrastructure are shared, but each run is analysed in isolation.
- **Blinding:** the analysis pipeline is automated; where any human judgement is needed (fidelity audit, sub-study), artifacts are anonymised to run IDs and the configuration label is hidden until ratings are complete **[PROPOSED]**.
- **Scope of inference:** because there is *one* application and *one* specification, inferences are about "these configurations on this task". Generalising to other tasks, languages or specifications is out of scope for the initial design and listed as a limitation; a second application is a candidate extension.

## 4. Stages

### 4.1 Stage A — initial generation

1. Provision a clean sandbox from the pinned image; copy in the frozen specification and the common prompt.
2. Launch the harness with the configuration; enforce budgets; log full transcript, tool calls, token usage per call, and timing.
3. At termination (self-declared completion, budget exhaustion, or error), snapshot the workspace **unchanged**.
4. Build; run the hidden acceptance suite; run the generated tests; collect coverage; run mutation testing on a defined scope; run SonarQube with the pinned configuration.
5. Persist raw outputs (JSON from the Sonar API, test XML, coverage reports) *before* any processing.

### 4.2 Stage B — remediation to the quality gate

1. Compute Gate status for the Stage A snapshot. If already passing, record "gate passed at Stage A" with zero remediation cost.
2. Otherwise start Stage B: the agent (same configuration, same harness) receives its own findings through a **fixed, model-agnostic feedback template** — e.g., a structured list of Sonar issues (rule key, file, line, message, severity/impact) and failed gate conditions — plus the instruction to remediate **while preserving functional behaviour**.
3. After each iteration the pipeline rebuilds, re-runs the **visible** (agent-accessible) tests and Sonar, and re-evaluates the gate. The hidden acceptance suite is run for regression measurement after each iteration but **its failures are not fed back** unless the Phase 2 decision says otherwise **[DECIDE]**.
4. **Stopping criteria (fixed in advance):** gate passes; round cap reached (**locked 2026-11-15: at most 3 rounds**, D-004); per-stage budget exhausted; no net reduction in gate-relevant issues for *k* consecutive iterations **[PROPOSED: k = 2]**; agent self-reports inability. Reaching a stop without passing = *gate not reached*, a first-class result.
5. **Anti-gaming controls:** the pipeline rejects remediation by suppression — added `// NOSONAR`, `@SuppressWarnings`, changes to Sonar configuration/exclusions, deleted or disabled tests, lowered coverage thresholds, removal of files from the scan scope — and counts them as *integrity violations* reported separately. This addresses Goodhart-style effects (§8).
6. Record per iteration: issues resolved/introduced/unchanged, regressions in acceptance and visible tests, tokens, time, cost.

### 4.3 Budgets — decision to finalize
A fixed **token** budget is fairest for comparing behaviour; a fixed **dollar** budget is fairest for comparing economics but makes cheap models run far longer. Proposal: equal iteration/turn/tool-call/wall-clock limits for all configurations (behavioural equivalence) **plus** one identical high dollar ceiling as a safety stop, and report cost as an *outcome*, not a constraint. The budget cap is **open** (O-001 in [decisions.md](decisions.md)) until the pilot reveals the token distribution.

### 4.4 Measured human intervention (sub-study, optional)
The initial experiment contains **no human correction** of code. "Measured human intervention time" in the brief's economics list is addressed as a **separate, clearly labelled sub-study**: for a small sample of stage-end artifacts, a human timer-based protocol measures the time to resolve a defined set of remaining findings. It is never mixed with agent-only results. Design is a Phase 2 decision **[DECIDE]**.

## 5. Measurement methodology (summary; details in quality-metrics.md)

### 5.1 Static analysis
SonarQube with pinned edition/version, a pinned rule profile (no ad-hoc rule changes), a pinned analyzer for each language, and an explicit definition of "new code" for a greenfield project. The scan is configured so that **generated tests, build outputs and vendored files** are handled by a documented, identical rule (not by per-run choices).

### 5.2 Independent validation
- **Hidden acceptance suite** authored in Phase 2 from the specification, **not visible** to agents and written by someone other than the model under test. Tests exercise the public interface (HTTP API and, where feasible, UI) so they work for any internal design.
- **Mutation testing** (suggested candidates: Stryker.NET for C# and StrykerJS for TypeScript — not yet piloted; decision O-005 in [decisions.md](decisions.md)) applied to a defined scope to estimate how well the *generated* tests detect injected faults. Mutation scores are an approximation of test effectiveness, not proof; the link between mutants and real faults is itself an object of research (Just et al., FSE 2014 — citation unverified, see originality review).

### 5.3 Specification fidelity
Each acceptance requirement is traceable by ID. Fidelity = proportion of requirements for which an independent check (acceptance test or defined manual rubric) confirms behaviour; unspecified-but-implemented behaviour is recorded separately (scope creep). The fidelity rubric and its inter-rater procedure are Phase 2 deliverables.

### 5.4 Cost and time
Provider-reported token usage per API call is stored raw (input, cached-read, cache-write, output/reasoning where reported). Cost is **computed after the fact** from the dated pricing snapshot applicable to the run's timestamp; invoices are reconciled for a sample of runs to check the computation. Wall-clock is recorded separately from active compute, since provider latency varies.

## 6. Model vs. harness confounds

A coding agent is "model + harness"; position and empirical papers from 2026 argue the harness can matter as much as, or more than, the model (Zhang et al. 2026; Vats & Golev 2026; Lewis 2026; Zhao, Poskitt & Lo 2026 — see originality review, *harness* section). Vendor-trained models may also behave better in the vendor's own harness. S2Q therefore:

1. Uses **one common open-source, multi-provider harness** as the primary arm so that the *tool surface and orchestration are identical*. Leading candidates: `mini-swe-agent` (minimal bash-only agent, multi-provider through LiteLLM — maximises comparability, minimal scaffolding) and OpenHands or OpenCode (richer tool surfaces, closer to what practitioners use). **Locked 2026-11-15 (D-002): `mini-swe-agent` is the primary harness**; a Phase 3 pilot still verifies that every core model can use it, and models that cannot are flagged rather than silently dropped.
2. Reports results as **configuration results**, never "model X is better".
3. Optionally runs a **sensitivity arm** with a second harness (and/or a vendor-native harness for a subset), enabling a model × harness interaction estimate. Without such an arm the variance decomposition (RQ9) is *not identifiable* and will not be claimed.
4. Records tool-call mix, turn counts, context-management behaviour, and unavoidable per-provider integration differences (tool-calling format, reasoning-token handling, caching).

## 7. Statistical approach

### 7.1 Repeated trials
LLM code generation is non-deterministic even at fixed settings (Ouyang et al. 2023, ref. [T3]); setting temperature to 0 does not remove variation reliably across providers and is often unavailable for reasoning models. Therefore each configuration is run multiple times and all conclusions use the *distribution* of outcomes.

### 7.2 Number of runs
- **Pilot:** *n* = 3 per configuration to validate the pipeline, estimate cost and token distributions, and estimate variance. Pilot data are **not** pooled into confirmatory results.
- **Confirmatory minimum:** *n* = 10 per configuration **[PROPOSED]**.
- **Pre-registered extension:** if CIs on key contrasts are wider than a pre-specified half-width, extend to *n* = 20 for the configurations involved; the extension rule is fixed in advance to avoid optional stopping.

### 7.3 Why 10, and what it cannot do
The suggestions in the software-engineering literature for randomised algorithms are to use enough runs for non-parametric tests and to report effect sizes (Arcuri & Briand 2011, ref. [T1]); they do not give a single magic *n*. We used a simulation (Mann–Whitney U, two-sided, normal-shift model mapping A12→d, 3,000 simulations per cell, seed 20261114; the simulation is a planning calculation, not a result, and its script will be committed with the harness code in Phase 3) to understand what *n* buys:

| True effect (A12) | n=5 | n=10 | n=15 | n=20 | n=30 |
|---|---|---|---|---|---|
| 0.71 (≈ d 0.78, "medium–large") — power at α=0.05 | 0.14 | 0.33 | 0.51 | 0.65 | 0.82 |
| 0.80 (≈ d 1.19, "large") — power at α=0.05 | 0.29 | 0.67 | 0.86 | 0.94 | 0.99 |
| 0.80 — power at α=0.005 (Bonferroni-like, 10 comparisons) | 0.00 | 0.24 | 0.54 | 0.73 | 0.94 |
| 0.90 (≈ d 1.81, "very large") — power at α=0.05 | 0.58 | 0.95 | 1.00 | — | — |
| 0.90 — power at α=0.005 | 0.00 | 0.70 | 0.94 | 0.99 | 1.00 |

*Assumptions: normally distributed latent outcomes with equal variance and a pure location shift; real metrics are skewed/heavy-tailed and some outcomes are bounded or zero-inflated, so true power may differ. Treat this as a rough planning guide only.*

**Implication (honest limit):** with *n* = 10 per configuration, only **large** differences are reliably detectable after multiplicity correction. Medium effects will usually be reported as *inconclusive* with wide CIs, not as "no difference". The design therefore (a) restricts the confirmatory family to a small set of pre-specified contrasts (tier-level contrasts and a few cross-provider contrasts, ≤ 6 **[PROPOSED]**), (b) treats other pairwise comparisons as exploratory, (c) pre-registers the *n* = 20 extension, and (d) states the minimum detectable effect in the report. The budget per run is the binding constraint on *n*; the pilot will report actual cost so *n* can be finalised.

### 7.4 Tests, effect sizes, intervals
| Question | Method |
|---|---|
| Omnibus difference across ≥ 3 configurations | Kruskal–Wallis (distribution-free; does not assume normality) |
| Pairwise / pre-specified contrasts | Mann–Whitney U (Wilcoxon rank-sum), **Holm** correction within the confirmatory family; exploratory pairs flagged and corrected separately |
| Effect size | Vargha–Delaney **A12** (Arcuri & Briand's recommended non-parametric effect size) and Cliff's δ; plus median differences and ratios in natural units |
| Confidence intervals | **BCa bootstrap** (10,000 resamples) for medians, median differences, A12; **Clopper–Pearson** (or Wilson) for proportions (gate-reached, build success) |
| Gate-reached comparisons | Fisher's exact test on proportions (small counts), reported with exact CIs |
| Association (H3) | Spearman ρ with bootstrap CI, pooled **and** within-configuration; Kendall's τ as a robustness check |
| Within vs. between variation (H5) | Dispersion statistics per configuration; variance-component/ICC-style ratio with bootstrap CI |
| Cost-to-gate | **Right-censored** outcome (not all runs reach the gate). Report (i) the proportion reached, (ii) cost conditional on reaching, (iii) a conservative bound treating non-reaching runs as ≥ the budget spent, and (iv) a time-to-event/Kaplan–Meier style view if the number of events allows. Never silently average only the successful runs. |
| Normalisation | Issues per KLOC (`ncloc`) and per unit churn in Stage B; raw counts also reported |
| Multi-metric reporting | Metrics reported separately with their own CIs; no composite score (quality-metrics §6) |
| Ordinal ratings (A–E) | Report distributions/counts; **never average** letter ratings |

### 7.5 Confirmatory vs. exploratory
Hypotheses, primary outcomes, the contrast family, test choices, gate definition, budgets, stopping rules, *n*, and exclusion rules are committed and tagged **before** confirmatory runs. Anything else is exploratory and labelled so. Deviations from the plan are logged with rationale.

### 7.6 Missing data and failed runs
All runs, including failed builds, crashes, timeouts and provider errors, are kept. A run is *excluded* from a given analysis only by a rule written in advance (e.g., "infrastructure fault unrelated to the model", defined by an objective check such as sandbox crash code), and every exclusion is listed. Provider outages are retried under a fixed protocol and the retries' costs are included. No run is dropped because its outcome is unfavourable (brief §13.8).

## 8. Threats to validity

| Threat | Type | Mitigation / disclosure |
|---|---|---|
| **Single application / single stack** | External | State scope; no generalisation across tasks; consider a second app as extension. Language familiarity may favour some models; stack choice recorded. |
| **Model–harness confound** | Construct/internal | Common harness; sensitivity arm; report as configuration results (§6). |
| **Nondeterminism & small *n*** | Statistical conclusion | Repeated trials, CIs, pre-registered extension; state minimum detectable effect. |
| **Silent model/provider changes** | Internal | Pin dated snapshot IDs where offered; record response metadata; run in randomised, tightly bounded time window; log provider headers. |
| **Pricing volatility** (promotional prices ending 2026-12-01; DeepSeek peak/off-peak; long-context surcharges; tokenizer differences) | Construct | Date-stamped pricing snapshot; compute cost after the fact; report sensitivity under alternative prices (e.g., promo vs. post-promo). |
| **Goodhart / gaming the scanner in Stage B** (suppression, exclusions, deleting code/tests) | Construct/internal | Integrity checks (§4.2.5); report violations separately; the gate includes coverage and test-count guardrails. |
| **Static findings ≠ defects** | Construct | Findings are *candidate* issues; false-positive rates unknown; Lenarduzzi et al. found limited fault-proneness evidence for many SonarQube rules (ref. [S12]); acceptance tests and mutation score supply independent evidence. |
| **Rule-set/edition dependence** | External | Pin version/edition; MQR vs. Standard Experience metrics differ; hotspots are being phased out; re-running on other versions may change counts. |
| **Vendor-sourced evidence** | Reporting | Sonar's own LLM studies are cited as *vendor* sources; S2Q uses Sonar's tool but is independent and discloses that tooling vendor and conclusions are separate. |
| **AI-written tests as bias** | Internal | Generated tests are measured (coverage, mutation) but *correctness* is judged by the independent hidden suite. |
| **Benchmark contamination** | External | Application is a custom spec, not a public benchmark; OpenAI states SWE-bench Verified contamination concerns (ref. [B5]) — a reason to avoid reusing public tasks. Residual risk: generic CRUD/incident patterns appear widely in training data. |
| **Hidden-test leakage / specification authorship bias** | Internal | Hidden suite never in agent-accessible paths; spec and tests authored before any run; authoring uses AI assistance only if disclosed and independently reviewed (Phase 2). |
| **Sandbox network access** | Internal | Decide network policy (offline with pre-seeded dependency mirror vs. open); record it; identical for all. |
| **Greenfield "new code" ambiguity** | Construct | Define new-code period as the entire code base in Stage A; in Stage B, new code relative to the Stage A baseline *and* overall code both reported. |
| **Mutation-score validity** | Construct | Mutation score is a proxy; scope and timeouts fixed; operator set pinned. |
| **Evaluator familiarity / researcher degrees of freedom** | Internal | Pre-registration; raw-data preservation; analysis scripts committed; blinded where human judgement is used. |
| **Researcher–industry affiliation** | Reporting | The researcher is a professional engineer; no provider sponsorship is assumed; any credits, discounts or affiliations will be declared in the final report. |

## 9. Repeatability and provenance

Every run stores an immutable **run record** (JSON, schema versioned) containing: run ID and seed; timestamps; configuration (provider, endpoint, requested model ID, *returned* model ID/system fingerprint where provided, effort/temperature/max-tokens, harness name+version+commit, tool allow-list); container image digest; spec commit hash; prompt-template hash; pricing snapshot ID; scanner and plugin versions; the raw API usage log; the raw outputs of every analysis tool; budget and stop reason; and integrity-check results. Code snapshots at stage ends are stored as git commits/tarballs with hashes. See [model-selection.md](model-selection.md) §5 for the version/pricing/config recording scheme and `data/pricing-snapshots/` for the first snapshot.

Reproducibility has limits that must be disclosed: hosted models cannot be bit-for-bit reproduced and may be deprecated; therefore the repository preserves **raw outputs and generated artifacts**, so analysis is reproducible even where generation is not. Open-weight models (e.g., DeepSeek, MIT-licensed weights per Hugging Face metadata) allow stronger reproducibility if self-hosted, but a hosted-API run and a self-hosted run are *different configurations*.

## 10. Ethics, safety and housekeeping

- Agents run in an isolated sandbox with no credentials, no access to the hidden tests, and constrained network.
- No proprietary data or code (brief §13.12). Specification, prompts and application are original or openly licensed.
- API keys, if any, are kept out of the repository and out of transcripts; transcripts are scrubbed of secrets before publication.
- A short limitations section is mandatory in every public write-up.
