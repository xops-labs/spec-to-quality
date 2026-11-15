---
title: "Cost Per Token Is the Wrong Metric"
subtitle: "What it actually costs to take one frozen specification through several AI coding models and out the other side of a quality gate"
published: false
canonical_url: ""  # set after Hashnode publish; see article/README.md
description: "Spec-to-Quality (S2Q) is an open-source experiment that measures cost-to-quality-gate across AI coding models on one frozen spec. This draft is pre-results: every finding is a placeholder."
tags: ai, software-engineering, benchmarking, llm
author: Yasvanth
---

# Cost Per Token Is the Wrong Metric

**What it actually costs to take one frozen specification through several AI coding models and out the other side of a quality gate**

*By Yasvanth, Staff Software Engineer at [xops-labs](https://github.com/xops-labs). I work on LLM observability and FinOps.*

> **Draft status (15 November 2026, America/Detroit).** This article was written *before* any confirmatory experiment was run. There are no results yet. Every number, chart, ranking, or finding that would come from a run is marked with an explicit placeholder in the form of a RESULT, CHART, TABLE, or FINDING marker (see `article/README.md`). Please do not read those as data, and do not quote them as findings. The protocol, the locked design choices, and the dated list prices below are real; the outcomes are not.

<!--
Title options (chosen title is the H1 above):
1. Cost Per Token Is the Wrong Metric  ← chosen
2. Cheaper Tokens, Cheaper Software? A Controlled Experiment from Spec to Quality Gate
3. From Frozen Spec to Quality Gate: Comparing AI Coding Models on Engineering Cost, Not List Price
4. The Cost-to-Quality-Gate Problem: Why Engineering Leaders Shouldn't Pick Coding Models from a Pricing Page
5. Don't Trust the Pricing Page: A Protocol for Measuring AI Coding Models End to End
-->

## The pricing page is a terrible decision tool

If you are choosing an AI coding model from a pricing page, you are optimizing the wrong number.

List price per million tokens is easy to spreadsheet and easy to defend in a budget review. It is also a poor proxy for what you actually pay: the cost of turning an approved specification into software that clears an agreed engineering quality bar. That total includes the first generation, the tests that fail, the static-analysis findings, the retries, the regressions introduced while fixing, and the runs that never make it through the gate at all.

A model listed at $0.50 per million output tokens can still be the expensive choice. A model listed at $20 or $50 can still be the cheap one. The opposite is also possible: the pricing page might turn out to be a decent guide. **Which of these happens is an empirical question.** I am not assuming an answer, and as of this writing I do not have one.

This article describes Spec-to-Quality (S2Q), an open-source experiment designed to measure that question under controlled conditions. Several coding models, all running in the same agent harness, each implement one frozen engineering specification. The resulting applications are scored with the same pinned tooling — SonarQube, a hidden acceptance-test suite, coverage, and mutation testing — then given a bounded chance to remediate their own findings. The headline economic metric is **cost-to-quality-gate**, and failed runs stay in the data.

The repository is public: [github.com/xops-labs/spec-to-quality](https://github.com/xops-labs/spec-to-quality).

## Why cost per token is the wrong metric

Engineering organizations already compare coding models on token price, coding-benchmark scores, generation speed, context-window size, and functional task completion. Those numbers are not useless. They are incomplete in ways that matter once you are shipping software rather than completing a programming assignment.

**List price is not usage.** Two models with the same output price can consume very different volumes of uncached input, cached input, and output — especially once an agent loop starts growing its context. Tokenizers also differ across providers, so raw token counts are not comparable; dollars are, if you compute them from dated prices and the provider's own usage log.

**List price is not even a stable list price.** Prices I checked on official provider pages on 14 November 2026 (America/Detroit), and recorded in [`data/pricing-snapshots/2026-11-14.json`](https://github.com/xops-labs/spec-to-quality/blob/main/data/pricing-snapshots/2026-11-14.json), already illustrate the traps. `gemini-4-carbon` was on a promotional rate of $2.00 / $0.10 / $10.00 per million tokens (input / cached-read / output) through 30 November 2026, doubling to $4.00 / $0.20 / $20.00 from 1 December 2026. `deepseek-flash` has peak and off-peak rates (off-peak output $0.60, peak $1.20). Several models surcharge long prompts — `claude-haiku-5-5` changes when a prompt exceeds 100K tokens; OpenAI's GPT-6 models change above 272K input tokens; `grok-4.7` changes at 200K. An agent that looks cheap on the first call can stop being cheap on the twentieth.

**Functional completion is not engineering quality.** A generated application can pass a happy-path test suite and still be full of duplication, high complexity, thin tests, or static-analysis findings that a team would not merge. METR's March 2026 note, *Many SWE-bench-Passing PRs Would Not Be Merged into Main*, is a useful reminder that passing tests and being mergeable are different questions. Static analysis is not a complete answer either — a SonarQube issue is a *candidate* finding, not a confirmed defect — but ignoring it is how "it works" turns into a quarter of remediation later.

**The first invoice is not the last invoice.** If a cheaper model introduces more maintainability or reliability findings, or needs more remediation rounds, or reaches a quality gate less often, its initial cost advantage can shrink or disappear. If it does not, that is equally worth knowing. Either way, you cannot read the answer off a pricing page.

What leaders actually fund is closer to this chain:

```
Specification → AI model + harness → generated application
    → static analysis + independent tests → remediation → engineering cost
```

S2Q is built to measure that chain, not a single link in it.

## The question

**Does the initial cost advantage of a lower-priced AI coding model remain meaningful after accounting for functional correctness, static-analysis findings, testing, and remediation effort?**

Around that primary question sit a few supporting ones, which the experiment is also designed to answer:

- Do different coding models produce materially different engineering quality from the same specification?
- Which quality characteristics vary most — reliability findings, maintainability, duplication, complexity, test effectiveness?
- Does functional correctness travel together with maintainability and test quality, or can a run pass acceptance tests and still fail a quality gate?
- Does higher coverage mean more effective tests, or just more lines executed?
- How much static-analysis debt shows up in the first generation, and how many remediation rounds does it take to clear a common gate?
- Are those differences stable across repeated independent runs, or is a single run a coin flip?
- How much of what we see is the model, and how much is the agent harness?

I am treating all of these as questions, not as claims. The hypotheses behind them, and what would count as support or contradiction, are in a later section.

## What this is not

A few guardrails, up front, so the rest of the article is harder to over-read.

- This is a research protocol and, eventually, a dataset. It is not a vendor leaderboard, a "best model of 2026" post, or a recommendation to buy or avoid any provider.
- Results will be attributed to a **model-plus-harness configuration** — model ID, endpoint, harness and version, reasoning-effort setting, and other execution parameters — not to "model X is better" in the abstract. A coding agent is the model *and* the loop it runs in.
- SonarQube findings are candidate issues. Some will be real problems, some style or maintainability opinions, some false positives; some real defects will never be flagged. A passing quality gate is a research status, not a production-readiness certificate.
- There is no composite "quality score." Unrelated metrics will be reported separately. The gate is a transparent pass/fail over explicit conditions, not a weighted blend I could tune to change the ranking.
- The first study uses one application and one stack. Findings will not be generalized to "AI coding quality" as a whole.
- No proprietary company code, specifications, or internal data are used. The specification, prompts, and benchmark application are original or openly licensed.

## What is already known — and where a gap remains

This is not a claim that nobody has looked at AI-generated code quality. Plenty of people have. A Phase 1 review (web-based, not a systematic literature review; most arXiv papers read at abstract level only) is in [`docs/originality-review.md`](https://github.com/xops-labs/spec-to-quality/blob/main/docs/originality-review.md). A fresh search will be done before this article is published as a results piece. What I can say today is what that review found, and what it did not.

Static analysis of LLM output is an established technique. Sonar's own *Coding Personalities of Leading LLMs* report (2025, vendor-authored) and several academic studies have run SonarQube over large sets of programming assignments or in-the-wild agent pull requests. Cynthia, Muttakin and Roy (2026) reported that raw-count differences between agents largely disappeared after normalizing by churn — which is why S2Q normalizes issue counts per thousand lines of code rather than treating raw counts as quality. Iterative LLM repair of static-analysis warnings has also been studied (for example CodeCureAgent, and Gonçalves and Maia's 2025 SBES paper). Those are about the repair technique, not about the economics of choosing a cheaper generator.

Spec-driven development has tooling (GitHub Spec Kit is the well-known example) and a smaller evidence base. The closest controlled comparison I found is Sacouto, Sacouto and Lang (2026), who compared spec-driven and "vibe" coding on Python benchmark tasks: weighted test pass rate rose 9.2 percentage points, at roughly 28× the tokens and 15× the wall-clock time of a free-prompting baseline. That is important evidence on functional pass rate and token cost. I did not see static-analysis quality or a remediation stage in the portion I read, and the tasks were small.

Agentic benchmarks such as SWE-bench and its successors measure whether an agent resolves an issue, usually via tests. They are not designed to measure maintainability, duplication, or cost-to-gate. OpenAI has published a rationale for no longer evaluating SWE-bench Verified, citing contamination and test-quality concerns — one reason S2Q uses a newly written specification rather than a public benchmark task.

On the harness itself, a 2026 cluster of papers argues that the agent scaffold can matter as much as the model (Zhang et al.; Vats and Golev; Lewis; Zhao, Poskitt and Lo). S2Q takes that seriously: one common harness in the primary arm, results reported as configurations, and no claim that model effects have been isolated unless a second-harness sensitivity arm is actually run.

**The gap I am targeting, hedged on purpose:** in that review I did not find a study that follows one frozen engineering specification through multiple price-tiered coding models, a fully built application, a pinned static-analysis pipeline, a bounded remediation stage against a predefined gate, and a cost-to-gate analysis that keeps failed runs in the data — nor one that sets Sonar's *estimated* remediation effort against *measured* effort. "I did not find it" is not proof that it does not exist. If a closer study turns up before publication, this section will be revised and the novelty claim narrowed.

## The setup

The design is a controlled comparison. Every configuration implements the same specification, on the same stack, in the same harness, under the same budgets and the same evaluation tools. The unit of observation is one independent *run*: a clean workspace, a complete Stage A generation, and Stage B remediation if the gate is not already passed. Runs share no memory, no cached workspace, and no scratch.

A few design choices are already locked. Others — including the API budget cap, exact quality-gate thresholds, and SonarQube edition — are still open and will be frozen before confirmatory runs. Locked choices are marked as such below.

### One frozen specification

The benchmark application is a self-contained **incident-management** system: an internal tool through which responders declare, triage, escalate, mitigate, resolve, and review incidents against services and teams, with role-based access, SLA rules, an audit trail, and a dashboard.

I chose this over helpdesk, inventory, or generic workflow apps because it is a realistic enterprise problem with a genuine state machine and business rules, not just CRUD, while still being independently testable through a public API and a specified UI contract. Notifications are simulated in-application, so there are no paid external dependencies. Time-based SLA rules will be evaluated with an injectable clock — advance time in tests, do not sleep — so the evaluation stays deterministic.

The technology stack is locked: a **.NET backend** and a **TypeScript/React frontend**. That is a change from an earlier lean recommendation of a JVM backend; the comparison is specific to this stack, and I will not generalize results across languages.

The detailed specification — requirement IDs, API contracts, UI behaviour at the level acceptance tests can check, validation and error rules, seed data — is being written as a version-controlled document and will be frozen before any confirmatory run. Agents never see the hidden acceptance-test suite. Residual training-data contamination is still a risk (incident trackers are common in public code) and is listed as a limitation, not something I can design away completely.

### Models across price tiers

Tiers are a pre-registered stratification by **list output price**, not a capability ranking. "Premium" does not mean "best." The point of the tiers is to ask whether list price predicts cost-to-gate.

The core set proposed in Phase 1 spans three tiers, five providers, and one open-weight option. Exact IDs will be pinned, and each run will record the model ID the API actually returns. Prices below are **list prices in USD per 1,000,000 tokens**, read from official provider pages on **14 November 2026** and stored in the snapshot cited above. They are a snapshot, not a durable fact, and they were not invoice-verified. Phase 4 will take a fresh snapshot before each execution batch. The final membership of the set may shrink if the still-open budget cap requires it, or if a model cannot reliably use the harness's tool-calling format.

| Tier | Model ID | Input / cached-read / output | Notes (from the 14 Nov 2026 snapshot) |
|---|---|---|---|
| Low | `claude-haiku-5-5` | $0.10 / $0.01 / $0.50 | Prompts over 100K tokens: $0.50 / $0.05 / $2.50 |
| Low | `gpt-6-luna` | $0.10 / $0.01 / $0.50 | Prompts over 272K input tokens: 2× input/cache and 1.5× output on the request |
| Low (open-weight) | `deepseek-flash` | Off-peak $0.15 / $0.003 / $0.60; peak $0.30 / $0.006 / $1.20 | Peak hours defined by DeepSeek; MIT-licensed weights per Hugging Face metadata on the check date |
| Mid | `claude-sonnet-5-5` | $2.00 / $0.10 / $10.00 | |
| Mid | `gpt-6.1-sol` | $2.00 / $0.10 / $10.00 | Same long-prompt surcharge rule as Luna |
| Mid | `gemini-4-hydrogen` | $1.50 / $0.075 / $7.50 | Output price includes thinking tokens |
| Mid | `grok-4.7` | $2.00 / $0.50 / $6.00 | Prompts ≥ 200K: $4.00 / $1.00 / $12.00 |
| Premium | `claude-opus-5-5` | $4.00 / $0.20 / $20.00 | |
| Premium | `gpt-6-astra` | $10.00 / $1.00 / $50.00 | Same long-prompt surcharge rule as Luna |
| Premium | `gemini-4-carbon` | Promo $2.00 / $0.10 / $10.00 through 30 Nov 2026; $4.00 / $0.20 / $20.00 from 1 Dec 2026 | Primary analysis will include a normalised, post-promotion price view |

A reserve list exists (`claude-fable-5-2`, `gemini-4-argon`, `gemini-3.5-flash-lite`, `grok-build-0.1`, `deepseek-v4-pro`, and `mistral-large-4` once its API ID is verified) if a core model is unusable or budget allows a substitution.

Notice that promotional prices blur the mid/premium boundary: on the check date, Carbon's promo output price equals Sonnet's. The pre-registration will state a tier rule that uses the post-promotion list price for Gemini 4 models, so a discount cannot quietly reclassify a model after the fact.

I am deliberately *not* converting these list prices into a predicted cost per run. Token volume is an outcome of the experiment, not an input I get to assume.

### A common harness, and each model's default reasoning

The primary harness is locked: **[mini-swe-agent](https://github.com/SWE-agent/mini-swe-agent)**. It is a small, bash-only agent loop with multi-provider support through LiteLLM. The trade-off is intentional. A minimal tool surface is easier to audit, pin, and keep identical across models; it may also disadvantage models that were trained for richer editors or browsers. That limitation will be stated with the results, not explained away.

**Reasoning effort** is also locked: each model's **provider default**. The actual default used will be recorded per run, even when we did not override it. Effort settings change both quality and cost; matching every model to the same named level ("medium", "high") would have been a defensible alternative, but it is not the policy this study is running.

A second-harness sensitivity arm — enough to estimate a model × harness interaction — is still optional and not yet decided. Without it, the split between "model" and "harness" is not identifiable, and I will not claim it.

### How quality is measured

After each stage, and with no manual edits, the pipeline will:

1. Build the application with a pinned toolchain.
2. Run a **hidden acceptance-test suite** derived from the specification, written against the public API and a specified UI contract, and never placed on an agent-accessible path.
3. Run the *generated* tests, collect **line and branch coverage**, and run **mutation testing** on a pre-defined scope (a JS/TS candidate is StrykerJS; the .NET mutation tool will be pinned in the harness phase — it has not been piloted yet).
4. Run **SonarQube** with a pinned edition, version, rule profile, and analyzer set. Edition and mode (Standard Experience vs Multi-Quality Rule) are still an open decision; whatever is chosen will be disclosed, because counts differ between modes.

The metric catalog covers functional quality (build success, acceptance pass rate, requirement completion), reliability, maintainability, duplication, SAST-only security findings, testing (coverage and mutation score), and economics. Two measurement rules will be repeated in the results, because they are easy to violate in a write-up:

- Static findings are candidate issues. They are reported alongside acceptance tests and mutation score, not treated as confirmed bugs.
- SonarQube's estimated remediation effort (`sqale_index` and related keys) is a model-based indicator from per-rule time constants. It will never be merged with the *measured* tokens, time, and dollars that Stage B actually consumed.

DAST, cryptographic posture, infrastructure security, performance, and accessibility beyond the acceptance contract are out of scope for this study.

### Stage A: initial generation quality

Each run starts from a clean sandbox that contains only the frozen specification and a common, model-agnostic prompt. The harness runs until the agent declares completion, the budget is exhausted, or the process errors. The workspace is then snapshotted **unchanged** — no human edits, no custom SonarQube feedback hooks, no model-specific quality instructions — and the evaluation tools run against that snapshot.

Stage A is the "what did we get on the first try?" measurement: build status, hidden-suite pass rate, coverage, mutation score, Sonar findings, tokens, wall-clock time, and inference cost computed after the fact from the dated pricing snapshot.

### Stage B: bounded remediation to a quality gate

If a run already passes the gate at Stage A, remediation cost is zero and that fact is recorded. Otherwise the same configuration, in the same harness, receives its own findings through a fixed, model-agnostic feedback template: a structured list of Sonar issues and failed gate conditions, plus the instruction to remediate **while preserving functional behaviour**.

**Stage B is capped at three remediation rounds** (locked). Other stopping rules — a per-stage budget, no net reduction in gate-relevant issues for *k* consecutive rounds, or the agent reporting that it cannot continue — will be frozen before confirmatory runs. Reaching a stop without passing the gate is a first-class result, not a row to drop.

The hidden acceptance suite is re-run after each iteration so we can measure regressions. The default plan is **not** to feed hidden-test failures back to the agent; that decision will be confirmed when the Stage B template is frozen. Visible (agent-accessible) tests and Sonar results are re-run every iteration.

Stage B is also where a model can try to game the scanner. The pipeline will treat suppressions (`// NOSONAR`, `#pragma warning disable`, scan-exclusion changes), deleted or disabled tests, lowered thresholds, and removal of files from the scan scope as **integrity violations**. Those runs fail the gate regardless of the remaining metrics, and violations are reported separately.

The quality gate itself is a research instrument, not a certification. It will be fixed in advance and identical for every configuration, with a strict and a lenient variant so conclusions are not an artefact of one threshold set. Principles already agreed: the gate must include functional integrity (so it cannot be satisfied by deleting behaviour), static-quality conditions on the overall code base, testing guardrails (coverage and test-count, optionally a mutation-score floor), and the anti-gaming checks above. Exact thresholds and the SonarQube edition are still open; they will be sanity-checked on a small pilot that is *excluded* from confirmatory data, then frozen.

### The headline metric: cost-to-quality-gate

Cost-to-gate is Stage A inference cost, plus Stage B inference cost, plus infrastructure and scanning cost. If a human-time sub-study is run, those minutes are reported as a separate line, converted to money only through an explicit range of rates, never a hidden constant. Spec-authoring time is identical across configurations by design and is not included.

Because not every run will reach the gate, cost-to-gate is **right-censored**. I will not average only the successful runs. The report will include:

- the proportion of runs that reach the gate within the Stage B budget;
- cost **conditional on** reaching it;
- cost already spent by runs that did not reach it (a lower bound on their true cost-to-gate);
- a conservative view that treats non-reaching runs as at least as expensive as the budget they consumed.

The API **budget cap for the study as a whole is still open**. Equal turn, tool-call, and wall-clock limits will apply to every configuration, with one identical high dollar ceiling as a safety stop, and cost treated as an *outcome* rather than a constraint. Those limits will be set from the pilot, not guessed here.

## Hypotheses

These are hypotheses, not findings. Each one has pre-registered support, contradiction, and inconclusive criteria in [`docs/research-methodology.md`](https://github.com/xops-labs/spec-to-quality/blob/main/docs/research-methodology.md). The short version:

**H1 — Model quality variance.** Identical specifications can produce significantly different static-analysis results across coding models. Supported if an omnibus test across configurations rejects, and at least one pre-specified pairwise contrast has a Vargha–Delaney A12 effect size outside a negligible band, with a confidence interval that excludes a tie. Issue counts are normalized per thousand lines of code.

**H2 — Cost–quality trade-off (primary).** Lower initial inference costs do not necessarily produce lower costs to reach an acceptable engineering quality gate. Supported if at least one low-tier configuration is cheaper at Stage A but is not cheaper to reach the gate, or reaches it less often, with pre-registered confidence intervals. Contradicted if low-tier configurations keep a lower cost-to-gate *and* an equal-or-better gate-reached rate, with intervals excluding parity. "Not necessarily" is a weak claim — one counter-example configuration supports it — so the write-up will also answer the stronger, practical question: is the remaining advantage *meaningful*?

**H3 — Functional–quality disconnect.** Successful functional implementation does not guarantee strong maintainability, reliability, or test quality. Supported if a non-trivial share of high-passing runs still fail the gate on static quality or mutation score, or if the rank correlation between pass rate and those quality measures is weak. The coverage-versus-mutation question is tested the same way. Correlations will be computed both pooled and within each configuration, so a between-model effect cannot masquerade as a general relationship.

**H4 — Remediation overhead.** Models differ in the additional engineering effort needed to correct quality findings. Supported by detectable differences in Stage B iterations, cost, issues resolved or introduced, regressions, or gate-reached rate. Runs that already passed at Stage A have zero remediation and are reported as such, not dropped. Estimated Sonar effort and measured Stage B effort stay separate.

**H5 — Specification consistency.** A controlled specification reduces requirement ambiguity but does not eliminate variation in implementation quality. Evidence for reduced ambiguity is high, stable fidelity on specified behaviour. Evidence for residual variation is non-trivial within-configuration spread on quality metrics — the same model, same prompt, different runs, not the same software.

## How the numbers will be read

LLM code generation is non-deterministic even at fixed settings, so each configuration will be run repeatedly and conclusions will use the *distribution* of outcomes, not a single lucky (or unlucky) trial.

The planned confirmatory minimum is **n = 10 independent runs per configuration**, with a pre-registered rule to extend to n = 20 if confidence intervals on key contrasts are still too wide. A pilot of n = 3 per configuration will validate the pipeline and estimate cost and variance; **pilot data will not be pooled into confirmatory results**.

With n = 10, only large effects are reliably detectable after multiplicity correction. That is an honest limit, not a footnote. A planning simulation in the methodology document (a calculation, not a result) puts power for a large A12 of 0.80 at roughly 0.67 at α = 0.05 and 0.24 at α = 0.005. Medium effects will usually be reported as *inconclusive* with wide intervals, not as "no difference." The confirmatory family is therefore small (tier-level and a few cross-provider contrasts), other pairs are exploratory, and the minimum detectable effect will be stated in the report.

Tests are non-parametric: Kruskal–Wallis for omnibus differences, Mann–Whitney with Holm correction for pre-specified pairs, A12 and Cliff's δ for effect size, bootstrap confidence intervals for medians and A12, and Clopper–Pearson or Wilson intervals for proportions such as gate-reached. Letter ratings (A–E) are reported as distributions, never averaged. Every attempted run is kept, including failed builds, timeouts, and budget exhaustion. A run is excluded from an analysis only by a rule written in advance, and every exclusion is listed.

Hypotheses, metrics, the gate, budgets, stopping rules, n, and the contrast family will be committed and tagged **before** confirmatory runs.

## Threats to validity

A result is only as trustworthy as its limitations. The main ones, in plain language:

- **One application, one stack.** Anything we learn is about these configurations on this incident-management app, on .NET and TypeScript/React, at the time of the runs. Language and training-data familiarity may favour some models. A second application is a possible extension, not a claim I can make now.
- **Model and harness are entangled.** Unless the optional second-harness arm is run, I can report configuration differences, not "the model" in isolation. mini-swe-agent's minimal tool surface is part of the treatment.
- **Small n and noisy generation.** Repeated trials and confidence intervals help; they do not create power that n = 10 does not have.
- **Silent model and price changes.** Hosted models can change behind an alias. Runs will be interleaved at random in a bounded window, dated model IDs and response metadata will be recorded, and cost will be computed from a snapshot taken near the run, with a sensitivity view for promotional versus post-promotion prices.
- **Gaming the gate in Stage B.** Integrity checks exist because Goodhart's law applies to scanners too. They will catch the obvious suppressions; they may not catch every subtle retreat from the specification.
- **Static findings are not defects, and estimated effort is not measured effort.** Already discussed; both will be labelled as such in every table.
- **Rule-set and edition dependence.** A different SonarQube version, edition, or mode can change counts. Results are valid for the pinned configuration.
- **Benchmark contamination.** A custom specification reduces the SWE-bench-style risk; it cannot eliminate the fact that incident-management patterns are common in training data.
- **Hidden-suite quality.** Acceptance tests can have bugs too. The suite will be reviewed and, where possible, validated before confirmatory runs; remaining test defects are a threat, not an impossibility.
- **Vendor-tool independence.** S2Q uses SonarQube and is independent of Sonar. Sonar also publishes its own LLM comparisons. That will be declared in the final report, along with any provider credits or discounts if they exist.

The full threat table is in the methodology document.

## Results

*This section is empty of data on purpose. It is the outline that will be filled from archived raw run records after confirmatory Stage A and Stage B execution. Every entry below is a placeholder.*

### Stage A — initial generation

[[TABLE: Stage A run inventory — n attempted, completed, build success, budget-exhausted, errors, by configuration — TBD]]

[[RESULT: Stage A build-success rate by configuration — TBD]]

[[RESULT: Stage A hidden acceptance-test pass rate by configuration, overall and by requirement group — TBD]]

[[RESULT: Stage A requirement-completion and specification-fidelity rates — TBD]]

[[CHART: Stage A acceptance pass rate by model, with confidence intervals]]

[[RESULT: Stage A Sonar issues per KLOC by model and by software-quality dimension — TBD]]

[[CHART: Stage A Sonar issues per KLOC by model]]

[[RESULT: Stage A reliability, maintainability, and security rating distributions (counts per A–E; not averaged) — TBD]]

[[RESULT: Stage A duplication density and duplicated-line counts — TBD]]

[[RESULT: Stage A cognitive and cyclomatic complexity, per KLOC and per-function distribution — TBD]]

[[RESULT: Stage A line and branch coverage of generated tests — TBD]]

[[RESULT: Stage A mutation score on the pre-defined scope — TBD]]

[[CHART: Stage A coverage vs mutation score]]

[[RESULT: Stage A inference tokens (by type) and inference cost by configuration, computed from the run-dated pricing snapshot — TBD]]

[[RESULT: Stage A wall-clock time and tool-call counts by configuration — TBD]]

[[CHART: Stage A inference cost vs acceptance pass rate]]

[[CHART: Stage A inference cost vs issue density]]

### Stage B — remediation

[[TABLE: Stage B start inventory — already passing at Stage A, entered Stage B, stopped by cap / budget / no improvement / error — TBD]]

[[RESULT: Stage B rounds used (max 3) by configuration — TBD]]

[[RESULT: Issues resolved, introduced, and unchanged per round and in total — TBD]]

[[RESULT: Hidden-suite and visible-test regressions during Stage B — TBD]]

[[RESULT: Integrity violations (suppression, exclusion, test deletion or disablement) by configuration — TBD]]

[[RESULT: Additional Stage B tokens, wall-clock, and inference cost — TBD]]

[[RESULT: Quality-gate pass rate after Stage B, under the primary gate — TBD]]

[[RESULT: Quality-gate pass rate under the strict and lenient gate variants — TBD]]

[[CHART: Gate-reached proportion by configuration, with confidence intervals]]

[[RESULT: Estimated Sonar remediation effort vs measured Stage B effort (reported separately, never merged) — TBD]]

[[CHART: Estimated vs measured remediation effort]]

### Cost-to-quality-gate

[[RESULT: Proportion of runs reaching the gate within the Stage B budget, by configuration and by price tier — TBD]]

[[RESULT: Cost-to-gate conditional on reaching the gate, by configuration — TBD]]

[[RESULT: Cost spent by runs that did not reach the gate (lower bound), by configuration — TBD]]

[[RESULT: Conservative cost-to-gate treating non-reaching runs as at least the budget consumed — TBD]]

[[CHART: cost-to-gate vs tier]]

[[CHART: Stage A cost rank vs cost-to-gate rank by configuration]]

[[TABLE: Cost-to-gate components — Stage A inference, Stage B inference, infrastructure — by configuration — TBD]]

[[RESULT: Sensitivity of cost-to-gate under as-run prices vs normalised post-promotion list prices — TBD]]

[[RESULT: Breakeven price-ratio sensitivity — at what price ratio a cheaper configuration's advantage disappears, given observed reach rate and remediation overhead — TBD]]

[[RESULT: Human-time sub-study minutes and cost range, if the sub-study is run; otherwise "not run" — TBD]]

### Hypotheses

[[FINDING: H1 Model quality variance — supported / contradicted / inconclusive against pre-registered criteria — TBD]]

[[FINDING: H2 Cost–quality trade-off — supported / contradicted / inconclusive — TBD]]

[[FINDING: H2 stronger question — whether any remaining low-tier advantage is meaningful under the pre-registered ratio threshold — TBD]]

[[FINDING: H3 Functional–quality disconnect — supported / contradicted / inconclusive — TBD]]

[[FINDING: H3 coverage vs mutation-score association — TBD]]

[[FINDING: H4 Remediation overhead — supported / contradicted / inconclusive — TBD]]

[[FINDING: H5a Specification consistency (ambiguity reduction) — TBD]]

[[FINDING: H5b Residual within-configuration quality variation — TBD]]

[[TABLE: Pre-specified confirmatory contrasts with A12, bootstrap CIs, and Holm-adjusted tests — TBD]]

[[RESULT: Within-configuration vs between-configuration dispersion (H5), with interval — TBD]]

[[RESULT: Model × harness interaction, if the sensitivity arm is run; otherwise "arm not run, not identifiable" — TBD]]

[[CHART: Within-run variation for a key quality metric, faceted by configuration]]

## Discussion

*Interpretation belongs here only after the placeholders above are replaced with numbers that can be reproduced from archived raw data. The paragraphs below are the questions the discussion will answer, not answers.*

[[FINDING: Did any low-tier configuration lose its Stage A cost advantage by the gate? Which comparisons drove H2? — TBD]]

[[FINDING: Which quality characteristics (reliability, maintainability, duplication, complexity, test effectiveness) varied most? — TBD]]

[[FINDING: Where functional success and static or test quality came apart, what did that look like in the generated applications? — TBD]]

[[FINDING: How often did Stage B improve the gate outcome, and at what added cost and regression risk? Did three rounds appear sufficient, tight, or rarely needed? — TBD]]

[[FINDING: How should a reader treat estimated Sonar effort in light of measured Stage B effort? — TBD]]

[[FINDING: What, if anything, changed under the strict vs lenient gate or under post-promotion prices? — TBD]]

What will not change with the data, and should be read with the results anyway:

- These findings will apply to the tested configurations, this specification, this stack, this harness, and this date. They will not license a standing "buy this model" recommendation.
- A configuration that reaches the gate more cheaply is not therefore production-ready.
- An inconclusive result is a result. With n = 10, that will happen, and I will report it as inconclusive rather than as a tie.

## What I would tell an engineering leader today

Before the experiment has been run, the honest advice is about *method*, not about model choice.

Do not pick a coding model from list price alone, and do not pick one from a functional-benchmark score alone. Decide what "acceptable engineering quality" means in your environment — a gate you would actually be willing to merge against — and measure the cost of getting there, including the attempts that never do. Keep failed runs in the denominator. If you compare models, compare them in the same harness, or be explicit that you are comparing products, not models. Pin versions, record the prices in force on the day, and be suspicious of any ranking that averages letter grades or collapses quality into one score.

After the runs, this section will add only recommendations that are tied to a specific result and labelled with the limits above:

[[FINDING: Reusable recommendations for engineering leaders, each linked to a result and its limits — TBD]]

## What's next

The protocol is drafted and several design choices are locked: the stack (.NET and TypeScript/React), the harness (mini-swe-agent), provider-default reasoning effort, and a Stage B cap of three rounds. Still to freeze before confirmatory execution: the detailed specification and hidden acceptance suite, quality-gate thresholds and SonarQube edition, the study-wide budget cap and final model set, Stage B stopping details, the UI test contract and injectable-clock design, mutation-tool pins, and whether to run a human-time sub-study or a second-harness arm.

The path from here is the one in [`docs/experiment-roadmap.md`](https://github.com/xops-labs/spec-to-quality/blob/main/docs/experiment-roadmap.md): finish and tag the specification (Phase 2), build the pinned pipeline and run a dummy-spec dry run plus an n = 3 pilot (Phase 3), execute the confirmatory Stage A and Stage B runs (Phase 4), analyse quality and cost-to-gate without dropping failures (Phases 5–6), then replace every placeholder in this article, re-check the literature, and publish. Hashnode will carry the canonical long-form piece; a shorter version will be cross-posted to dev.to with `canonical_url` set back to Hashnode; a short discussion teaser will go on LinkedIn.

If you want to follow the work, watch the repository: [https://github.com/xops-labs/spec-to-quality](https://github.com/xops-labs/spec-to-quality). Issues and review comments on the Phase 1 documents are welcome once you have read them. The suggested license is Apache-2.0 for code and CC BY 4.0 for documents and data; that will be confirmed before a tagged release, and no `LICENSE` file has been added yet.

I will update this article when there is something to update — raw data, not a vibe.

## Notes

- Pricing facts in this article come only from `data/pricing-snapshots/2026-11-14.json`, checked against official provider documentation on 14 November 2026 (America/Detroit). Sources are listed in that file and in `docs/model-selection.md`.
- Literature citations follow `docs/originality-review.md`, including its limits (non-systematic search, abstracts-only for most preprints, vendor sources labelled as such). Unverified sources named in that review are not relied on here for numerical claims, other than the Sacouto et al. (2026) figures, which come from a paper whose full text was read in Phase 1.
- Design details live in `docs/vision.md`, `docs/research-methodology.md`, `docs/quality-metrics.md`, `docs/benchmark-application.md`, and `docs/experiment-roadmap.md`.
