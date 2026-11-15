# Spec-to-Quality (S2Q) — Quality Metrics

> **Status:** Phase 1 draft awaiting review. This is a *catalog and measurement design*, not results. SonarQube metric keys were checked against the SonarQube Community Build metrics-definition page (https://docs.sonarsource.com/sonarqube-community-build/user-guide/code-metrics/metrics-definition) on 2026-11-14; they **must be re-checked against the exact pinned version** in Phase 3, because Sonar has changed metric naming and is phasing out security hotspots.

## 0. Ground rules

1. **Static-analysis findings are not confirmed defects.** A SonarQube issue is a *rule violation flagged by a static analyzer*. Some are true problems, some are style or maintainability opinions, some are false positives, and some real defects are never flagged. The prevalence of each is unknown for our application. Lenarduzzi et al. studied whether SonarQube rules relate to faults and reported limited evidence for many rules (arXiv:1907.00376 — originality review [S12]). We therefore call them **candidate issues/findings** and pair them with independent evidence (acceptance tests, mutation score).
2. **SonarQube-estimated remediation effort ≠ measured remediation effort.** `sqale_index` / remediation-effort metrics are model-based estimates computed from per-rule time constants (and, for the debt *ratio*, from a cost-per-line constant; Sonar's docs give a default of 30 minutes to develop one line of code). They are not observations of how long a developer or an agent actually took. S2Q reports both and never merges them (§5).
3. **No composite quality score** without scientific justification (§6).
4. **A passing gate is not production readiness.** SonarQube alone does not prove security, performance, or correctness (brief §13.11).
5. **Edition and mode matter.** SonarQube has a Standard Experience (issue *types*: bug / vulnerability / code smell) and a Multi-Quality Rule (MQR) mode (software *qualities*: security / reliability / maintainability, with impact severities). Counts differ between modes. Pin one mode and record it; report both key families if both are available.
6. **Raw before derived.** Raw tool outputs are archived before any metric is derived from them.

## 1. Tooling reference (to be pinned in Phase 3)

| Item | State as of 2026-11-14 (from official docs/pages) | Pin in Phase 3 |
|---|---|---|
| SonarQube Community Build | Release notes list 26.11.0.134175 as the latest at check time | exact version + Docker image digest |
| SonarQube Server | Documentation version 2026.5 exists (commercial editions) | only if Community Build lacks a required feature |
| Language analyzers | Version bundled with the pinned server; language list in the Community Build docs | plugin versions |
| Scanner | SonarScanner for Maven/Gradle/NPM or CLI | scanner version |
| Coverage | e.g., JaCoCo (Java) / Istanbul-nyc or c8 (JS/TS) | tool + version |
| Mutation testing | candidate tools: PIT (hcoles/pitest, Apache-2.0) for JVM; StrykerJS (stryker-mutator/stryker-js, Apache-2.0) for JS/TS — repositories verified to exist, **not yet piloted** | tool, operator set, scope, timeout |

Community Build vs. Server/Cloud feature differences (e.g., branch analysis, security-focused rules) can change findings. The edition used is a research variable that must be disclosed.

## 2. Metric catalog

Convention: **Source** is the system that produces the value. **Type** = *observed* (measured), *estimated* (model-based), or *candidate* (static finding needing interpretation). Sonar keys are shown in code font; Standard-Experience keys and MQR keys are given where both exist.

### 2.1 Functional quality

| Metric | Definition | Source | Type | Limitations |
|---|---|---|---|---|
| Build success | Binary: clean workspace builds with the pinned toolchain and no manual change | build tool exit status | observed | Dependency resolution/network policy can cause spurious failures; record separately from compile errors. |
| Acceptance test pass rate | passed hidden acceptance tests ÷ total, overall and per requirement group | independent hidden suite (Phase 2) | observed | Only as good as the suite; tests exercise public interface. A failed build counts as 0 passed, and the count of un-run tests is reported. |
| Requirement completion | requirements with all linked acceptance tests passing ÷ total requirements | traceability matrix | observed | Depends on traceability between requirements and tests; partial credit rules must be preset. |
| Specification fidelity | requirements whose observed behaviour matches the spec, **plus** a separate count of unspecified behaviour (scope creep) | acceptance tests + fidelity rubric (Phase 2) | observed/rated | Rubric items need inter-rater checks; behaviour not covered by any test is unverified, not "correct". |
| Confirmed functional defects | acceptance failures **confirmed by triage** to be real bugs in the implementation (not test bugs) | triage of failed hidden tests | observed (triaged) | Triage labour; bound by hidden-suite coverage. Distinct from Sonar "bugs". |

### 2.2 Reliability

| Metric | Standard key | MQR key | Notes |
|---|---|---|---|
| Detected bugs (candidate) | `bugs` | `software_quality_reliability_issues` | Static *candidate* bugs. Not equal to confirmed functional defects (above). |
| Reliability rating | `reliability_rating` | `software_quality_reliability_rating` | Ordinal A–E; report counts per rating, never a mean. |
| Reliability remediation effort (estimated) | `reliability_remediation_effort` | `software_quality_reliability_remediation_effort` | *Estimated* minutes. |
| Runtime reliability signals | n/a | n/a | Test-run failures/errors (`test_failures`, `test_errors`) when reports are imported — observed, from tests not Sonar's analysis. |

### 2.3 Maintainability

| Metric | Standard key | MQR key | Notes |
|---|---|---|---|
| Code smells (candidate) | `code_smells` | `software_quality_maintainability_issues` | Heuristic maintainability findings. Counts depend heavily on active rule profile. |
| Maintainability rating | `sqale_rating` | `software_quality_maintainability_rating` | Ordinal A–E derived from debt ratio thresholds; do not average. |
| Technical debt (estimated minutes) | `sqale_index` | `software_quality_maintainability_remediation_effort` | *Estimated* effort; per-rule constants. |
| Technical debt ratio | `sqale_debt_ratio` | `software_quality_maintainability_debt_ratio` | Estimated debt ÷ estimated development cost (cost derived from `ncloc` × default 30 min/line). Depends on the configured constant; sensitive to code size. |
| Cognitive complexity | `cognitive_complexity` (project total); per-function distribution via component measures | same | Sonar's cognitive-complexity measure. Totals scale with size, so report per KLOC *and* the per-function distribution (max, 90th percentile, count above threshold). |
| Cyclomatic complexity | `complexity` | same | Totals scale with size; same reporting as above. Comparisons across languages invalid. |
| Size | `ncloc`, `lines`, `files`, `functions` (when available) | same | Denominator for all density metrics. Generated tests count toward size unless reported separately (§2.7). |
| Architectural/structural measures | not provided by default | — | Optional extension: dependency-cycle and layering checks using a separate tool; not in the initial scope unless added in Phase 2. |

### 2.4 Duplication

| Metric | Key | Notes |
|---|---|---|
| Duplicated lines | `duplicated_lines` | Counts lines in duplicated blocks. |
| Duplication density | `duplicated_lines_density` | Percentage of `lines`. Small-code-base sensitivity: percentages jump with few lines; report absolute counts too. |
| Duplicated blocks / files | `duplicated_blocks`, `duplicated_files` | Supporting detail. |

### 2.5 Security (SAST only)

| Metric | Standard key | MQR key | Notes |
|---|---|---|---|
| SAST vulnerability findings (candidate) | `vulnerabilities` | `software_quality_security_issues` | Static candidate vulnerabilities. Not exploitability. DAST is out of scope. |
| Security rating | `security_rating` | `software_quality_security_rating` | Ordinal A–E. |
| Security remediation effort (estimated) | `security_remediation_effort` | `software_quality_security_remediation_effort` | *Estimated* minutes. |
| Security hotspots | `security_hotspots`, `security_hotspots_reviewed`, `security_review_rating` | (hotspot concept being migrated) | Hotspots are *review prompts*, not defects. Sonar documentation (check date) states hotspots are being phased out — rules that formerly raised hotspots now raise vulnerabilities (Standard) or security issues (MQR). Because "reviewed" requires a human decision, `security_hotspots_reviewed` is generally not meaningful in an unattended experiment; the project therefore reports raw hotspot/security-issue counts and treats hotspot-review conditions as inapplicable (§7). |

### 2.6 Combined issue views

| Metric | Key | Notes |
|---|---|---|
| Total open issues | `violations` | Aggregate across types. Useful for a gate; not a quality score. |
| Issues by severity/impact | via Web API (`api/issues/search` facets) | Record per-rule counts and severities; the full issue list is archived as JSON. |
| Accepted/false-positive issues | `accepted_issues` | Any accepted/false-positive marking is a *manual action* and is forbidden in unattended runs; reported if it occurs (integrity violation). |

### 2.7 Testing

| Metric | Key / source | Notes |
|---|---|---|
| Line coverage | `line_coverage` (and `coverage`) | Generated tests only; computed by the pinned coverage tool and imported into Sonar. Coverage is *execution*, not *verification*. |
| Branch coverage | `branch_coverage` | As above. |
| Test execution success | `tests`, `test_failures`, `test_errors`, `skipped_tests`, `test_success_density` | From imported test reports; skipped tests tracked to detect "disabled test" gaming. |
| Independent test effectiveness | Mutation score = killed mutants ÷ (total − equivalent/ignored), on a defined scope, using the generated test suite | PIT / StrykerJS candidates. Slow; equivalent mutants and timeouts distort the score; the scope (e.g., domain/service layer) is fixed in advance. |
| Hidden-suite vs generated-suite divergence | Cases where generated tests pass but the hidden suite fails | Direct evidence of test inadequacy; measured by comparing the two sets of results. |
| Test code share | LOC in tests ÷ total LOC | To detect tests inflating size/coverage; code and test LOC reported separately where the scanner supports test-source separation. |

### 2.8 Economics and process

| Metric | Definition | Type | Limitations |
|---|---|---|---|
| Stage A tokens | Sum over API calls of input (uncached), cache-read, cache-write, and output/reasoning tokens, from provider usage fields | observed | Providers differ in tokenization and in how reasoning tokens are reported; cross-provider token counts are not directly comparable. |
| Stage A inference cost | Σ(tokens by type × price at run time from the dated pricing snapshot) | computed | Depends on snapshot accuracy, peak/off-peak, promotions, long-context surcharges, tool/search fees; reconcile with invoices for a sample. |
| Stage A wall-clock / active time | Harness start→end; separate API latency vs. tool execution | observed | Provider latency/rate-limits vary with time of day. |
| Tool usage | Counts of tool calls by type, turns, retries, errors | observed | Harness-specific definitions. |
| Completion status | {completed, self-declared-complete-but-build-fails, budget-exhausted, timeout, crash, provider-error} | observed | Pre-defined categories; no ad-hoc relabelling. |
| Stage B iterations/tokens/time/cost | Same as above per iteration and in total | observed/computed | |
| Issues resolved / introduced / unchanged | Diff of issue sets between snapshots, matched by rule+location with a fixed fuzzy-matching rule | observed (derived) | Issue matching across code movement is imperfect; the matching rule is pre-registered; both naive and fuzzy matches reported. |
| Test regressions | Hidden-suite tests passing at Stage A snapshot but failing later | observed | |
| Integrity violations | Suppressions, scan-scope changes, test deletion/skip, gate-config changes | observed | Detected by diff checks; see methodology §4.2. |
| Infrastructure & scanning cost | Compute time × unit cost for scanner, mutation testing, build, hosting of sandbox | computed | Cloud pricing assumptions recorded; small relative to inference unless mutation testing is heavy. |
| Measured human intervention time | Minutes measured in the optional human sub-study | observed | Small sample; confounded with human skill; reported separately (§5). |
| **Total cost to quality gate** | Stage A cost + Stage B cost + infrastructure cost (+ measured human cost under stated rate assumptions, reported as a *separate* line) | computed | Right-censored: runs that never reach the gate have a *lower bound*, not a value (§5). |

## 3. Data sources and storage

- **SonarQube Web API** (project measures via `api/measures/component`, issues via `api/issues/search`, gate status via `api/qualitygates/project_status`) → raw JSON archived.
- **Test and coverage reports** (JUnit XML, JaCoCo/Istanbul/Cobertura formats) archived unmodified.
- **Mutation-testing reports** archived unmodified.
- **Provider usage logs**: raw API response usage objects per call.
- **Harness transcripts and tool logs**.
- **Pricing snapshots**: `data/pricing-snapshots/<date>.json`.
- **Run record** per run (JSON), described in research-methodology §9.

Raw data are append-only; derived tables are rebuilt from raw data by committed scripts (Phase 3+).

## 4. Limitations of the measurement approach

- **Rule-profile dependence:** different Sonar rule profiles, languages and versions produce different counts; results are valid only for the pinned configuration.
- **Language-analyzer maturity differs by language;** cross-language comparisons are not part of this study.
- **Static analysis catches a subset of problems** and is blind to many design flaws, missing requirements and wrong business logic; hence the independent acceptance suite.
- **Small-code-base sensitivity:** densities and ratings are volatile for small projects; report absolute counts too.
- **Coverage ≠ test quality;** mutation score is a proxy.
- **Size confound:** larger applications naturally produce more findings; normalise by `ncloc`/churn and also analyse size itself as an outcome.
- **Estimated effort constants** come from Sonar's rule metadata and a default cost-per-line, not from empirical timing in our setting.
- **Vendor ownership of the tool:** Sonar publishes its own LLM-quality studies (originality review [S1], [S15]); S2Q uses the tool but does not rely on vendor conclusions.

## 5. Economic measurement methodology

**Principle:** report each cost component separately; combine only where the addition is physically meaningful (currency), and show the assumptions.

1. **Inference cost** (Stage A and Stage B separately) from provider-reported token usage × the dated price snapshot. Compute under **two** pricing views where relevant: *as-run* (prices on the day) and *normalised* (e.g., post-promotion list price), since promotional prices change during the project.
2. **Infrastructure cost**: scanner/mutation/build compute time × recorded unit cost.
3. **Estimated remediation effort** (Sonar minutes) is shown as an *indicator of residual debt*, **not** added to dollar costs by default. If converted to dollars it requires an explicit hourly-rate assumption and is labelled "estimated debt cost" — never "remediation cost".
4. **Measured remediation effort** = what Stage B actually consumed (agent tokens/time/cost) and, if the sub-study is run, measured human minutes. Human minutes are converted to money only through an explicitly stated sensitivity range of rates (e.g., low/medium/high), never a single hidden constant.
5. **Total cost to gate** = Stage A + Stage B + infrastructure (+ human line if measured). Because some runs will not reach the gate, report: (a) proportion reaching the gate; (b) cost conditional on reaching; (c) cost spent by non-reaching runs (a lower bound on their true cost-to-gate); (d) *expected cost per gated outcome* using the observed reach proportion, **with the caveat** that this treats failures as retryable independent attempts (an assumption to be stated).
6. **Breakeven view:** report the price ratio at which a cheaper configuration's advantage disappears given its observed reach proportion and remediation overhead — as a *sensitivity analysis*, not a prediction.
7. **Not included (stated):** developer salary for spec authoring (identical across configurations by design), review and deployment costs, opportunity costs, downstream defect costs.

## 6. Why there is no composite quality score

Combining unrelated measures (issue density, coverage, complexity, pass rate) into one number requires arbitrary weights, hides trade-offs, and invites weight-tuning that can reverse rankings. Instead S2Q reports (a) a **profile of separately reported metrics with CIs** and (b) the **gate outcome** (a transparent pass/fail over explicit conditions). If a later phase wants a summary, acceptable options are: Pareto/dominance comparisons across a pre-registered metric set, or rank-based multi-criteria displays whose sensitivity to weights is shown. A weighted composite would require a documented validation against an external criterion before use.

## 7. Quality-gate design principles

The gate is a *research instrument*: it defines "acceptable engineering quality" so cost-to-gate is comparable. Principles:

1. **Fixed in advance and identical for all configurations;** committed before confirmatory runs.
2. **Built from separate, explicit conditions** over observed/measured metrics — not from a composite.
3. **Includes functional integrity**, so that the gate cannot be satisfied by deleting functionality: e.g., build succeeds; acceptance pass rate ≥ a pre-set threshold and **no regression** relative to the Stage A snapshot.
4. **Includes static-quality conditions** on *overall* code (the greenfield application has no meaningful "new code"): e.g., no open issues at the highest severity/impact levels; reliability/security/maintainability ratings at or above a threshold; duplication density below a threshold; threshold values **[DECIDE]**.
5. **Includes testing guardrails:** minimum coverage and no net decrease in test count/enabled tests; optionally a mutation-score floor, which also makes gaming harder.
6. **Anti-gaming:** integrity violations (suppression markers, exclusion changes, gate/profile edits, deleting tests) fail the gate regardless of the metrics.
7. **Reachability calibration (pilot only):** thresholds are sanity-checked on pilot data so the gate is neither trivially passed nor impossible; *calibration uses pilot runs that are excluded from confirmatory data*, and the gate is frozen afterward.
8. **Reference, not replacement:** the default "Sonar way" quality gate is documented by Sonar as focused on *new code* with four conditions (no new issues; new security hotspots reviewed; new-code coverage threshold; new-code duplication threshold) (docs: https://docs.sonarsource.com/sonarqube-community-build/quality-standards-administration/managing-quality-gates/introduction-to-quality-gates). Hotspot-review conditions require manual review and are not applicable to unattended runs. S2Q therefore proposes a **custom** gate informed by Sonar way, adapted for overall code. Exact thresholds are a Phase 2/3 decision.
9. **Sensitivity analysis:** report results under a *strict* and a *lenient* gate variant so conclusions are not artefacts of one threshold set.
10. **A pass is a research status, not a certification** (see §0.4).

## 8. Open metric decisions for Phase 2/3

- Exact gate thresholds and the strict/lenient variants.
- Whether to run MQR mode only or both modes.
- Mutation-testing scope, tool, and time limit.
- Issue-matching rule across Stage A→B snapshots.
- Whether Stage B feedback includes hidden-suite failures (default proposal: no).
- Design (or omission) of the human-time sub-study.
