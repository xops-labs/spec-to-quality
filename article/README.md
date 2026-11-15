# S2Q article drafts

Drafts for the Spec-to-Quality long-form article (Hashnode / dev.to) and a short LinkedIn teaser. Written 15 November 2026 (America/Detroit), **before confirmatory experiments**. Do not publish the results sections as-is.

| File | Purpose |
|---|---|
| [s2q-benchmark-article.md](s2q-benchmark-article.md) | Long-form article. Chosen title: **Cost Per Token Is the Wrong Metric**. Alternate titles are listed in an HTML comment under the byline. |
| [linkedin-teaser.md](linkedin-teaser.md) | ~150-word LinkedIn teaser, also pre-results, with the same placeholder convention. |

The protocol, locked design choices, and dated list prices in these drafts are taken from the Phase 1 documents and from `data/pricing-snapshots/2026-11-14.json`. `docs/benchmark-spec.md` and `docs/decisions.md` were not in the repository when this draft was written; if they land with a frozen requirement list, gate thresholds, or a budget cap, update the setup section before publication.

## What is a placeholder

Any value that would come from an experimental run is wrapped in double-bracket markers. Nothing inside those markers is data.

| Marker | Use for |
|---|---|
| `[[RESULT: … — TBD]]` | A measured number, rate, interval, or table cell that must be computed from raw run records |
| `[[CHART: …]]` | A figure to be generated from those records (replace the marker with the image and a caption that states axes, n, and the gate or price view used) |
| `[[TABLE: … — TBD]]` | A multi-row table of run-derived values |
| `[[FINDING: … — TBD]]` | A hypothesis classification (supported / contradicted / inconclusive) or a discussion/recommendation sentence that depends on results |

Pricing figures that already appear *without* placeholders (for example $0.10 / $0.01 / $0.50 for `claude-haiku-5-5`) are **list prices from the 14 November 2026 snapshot**, not experiment results. Do not mix them with cost-to-gate numbers. Do not invent token volumes or per-run costs to "illustrate" results.

The opening status callout, the sentence "as of this writing I do not have an answer," and the discussion line "Before the experiment has been run…" must be removed or rewritten once real results are in, so the published article does not still read as a pre-results draft.

## Cross-posting

Publish the long-form article **once**, then point the other site at it so the same piece is not treated as two originals.

1. **Hashnode is the canonical long-form URL.** Publish there first (or update the existing draft). Copy the public Hashnode URL.
2. **dev.to** gets the same Markdown, with `published: true` only when you are ready, and with `canonical_url` set to the Hashnode URL in the front matter. dev.to's front matter field is `canonical_url`. That is the field this draft already has; fill it after Hashnode is live.
3. If you instead publish on dev.to first, reverse the rule: set Hashnode's canonical field to the dev.to URL and keep one canonical.
4. Tags: keep them factual (`ai`, `software-engineering`, `benchmarking`, `llm`). Avoid clickbait or vendor-bashing tags.
5. The LinkedIn teaser is not a second publication of the article. It should link to the Hashnode URL (and the repo). After results exist, fill the two placeholders and keep the post short; do not paste tables into LinkedIn.
6. Before publishing results, repeat the originality search required by Phase 7 and adjust the "What is already known" section if a closer study has appeared. Declare affiliations and any provider credits or discounts.

Suggested license for the article text follows the repo's document license (proposed CC BY 4.0, still to be confirmed). Do not publish until the maintainer has confirmed license and byline.

## Checklist: filling placeholders after the runs

Use this after confirmatory Stage A and Stage B data are frozen and the analysis scripts have been re-run from raw records. Pilot (n = 3) data is not confirmatory and must not be substituted.

### Before you touch the article

- [ ] Confirmatory runs finished or explicitly recorded as failed/aborted; raw data frozen and checksummed
- [ ] Analysis reproduced from archived raw records with committed scripts
- [ ] Each hypothesis classified against the *pre-registered* criteria (supported / contradicted / inconclusive), not against a post-hoc reading
- [ ] Failed, timed-out, and budget-exhausted runs included wherever the analysis plan requires them
- [ ] Fresh pricing snapshot taken for the execution window; cost computed from the snapshot in force at run time
- [ ] Originality re-check completed; nearest-work section updated if needed
- [ ] Affiliations, tool-vendor independence (Sonar), and any credits/discounts declared

### Front matter and framing

- [ ] Remove or rewrite the pre-results status callout at the top of `s2q-benchmark-article.md`
- [ ] Rewrite the lede so it no longer says there is no answer
- [ ] Confirm the chosen title still fits the findings (or switch to one of the listed alternates if the result is a null or a surprise)
- [ ] Set `canonical_url` after Hashnode is live; set `published` flags only when ready
- [ ] Fill `[[FINDING: …]]` and `[[RESULT: …]]` in `linkedin-teaser.md` and point the post at the Hashnode URL

### Stage A placeholders

- [ ] `[[TABLE: Stage A run inventory …]]`
- [ ] `[[RESULT: Stage A build-success rate …]]`
- [ ] `[[RESULT: Stage A hidden acceptance-test pass rate …]]`
- [ ] `[[RESULT: Stage A requirement-completion and specification-fidelity rates …]]`
- [ ] `[[CHART: Stage A acceptance pass rate by model …]]`
- [ ] `[[RESULT: Stage A Sonar issues per KLOC …]]`
- [ ] `[[CHART: Stage A Sonar issues per KLOC by model]]`
- [ ] `[[RESULT: Stage A reliability, maintainability, and security rating distributions …]]`
- [ ] `[[RESULT: Stage A duplication density …]]`
- [ ] `[[RESULT: Stage A cognitive and cyclomatic complexity …]]`
- [ ] `[[RESULT: Stage A line and branch coverage …]]`
- [ ] `[[RESULT: Stage A mutation score …]]`
- [ ] `[[CHART: Stage A coverage vs mutation score]]`
- [ ] `[[RESULT: Stage A inference tokens … and inference cost …]]`
- [ ] `[[RESULT: Stage A wall-clock time and tool-call counts …]]`
- [ ] `[[CHART: Stage A inference cost vs acceptance pass rate]]`
- [ ] `[[CHART: Stage A inference cost vs issue density]]`

### Stage B placeholders

- [ ] `[[TABLE: Stage B start inventory …]]`
- [ ] `[[RESULT: Stage B rounds used (max 3) …]]`
- [ ] `[[RESULT: Issues resolved, introduced, and unchanged …]]`
- [ ] `[[RESULT: Hidden-suite and visible-test regressions …]]`
- [ ] `[[RESULT: Integrity violations …]]`
- [ ] `[[RESULT: Additional Stage B tokens, wall-clock, and inference cost …]]`
- [ ] `[[RESULT: Quality-gate pass rate after Stage B, under the primary gate …]]`
- [ ] `[[RESULT: Quality-gate pass rate under the strict and lenient gate variants …]]`
- [ ] `[[CHART: Gate-reached proportion by configuration …]]`
- [ ] `[[RESULT: Estimated Sonar remediation effort vs measured Stage B effort …]]`
- [ ] `[[CHART: Estimated vs measured remediation effort]]`

### Cost-to-gate placeholders

- [ ] `[[RESULT: Proportion of runs reaching the gate …]]`
- [ ] `[[RESULT: Cost-to-gate conditional on reaching the gate …]]`
- [ ] `[[RESULT: Cost spent by runs that did not reach the gate …]]`
- [ ] `[[RESULT: Conservative cost-to-gate …]]`
- [ ] `[[CHART: cost-to-gate vs tier]]`
- [ ] `[[CHART: Stage A cost rank vs cost-to-gate rank …]]`
- [ ] `[[TABLE: Cost-to-gate components …]]`
- [ ] `[[RESULT: Sensitivity of cost-to-gate under as-run prices vs normalised post-promotion list prices …]]`
- [ ] `[[RESULT: Breakeven price-ratio sensitivity …]]`
- [ ] `[[RESULT: Human-time sub-study …]]`

### Hypothesis, discussion, and recommendations

- [ ] All `[[FINDING: H1 …]]` through `[[FINDING: H5b …]]` plus the H2 stronger-question finding
- [ ] `[[TABLE: Pre-specified confirmatory contrasts …]]`
- [ ] `[[RESULT: Within-configuration vs between-configuration dispersion …]]`
- [ ] `[[RESULT: Model × harness interaction …]]` (or explicitly "arm not run")
- [ ] `[[CHART: Within-run variation …]]`
- [ ] All six discussion `[[FINDING: …]]` placeholders
- [ ] `[[FINDING: Reusable recommendations for engineering leaders …]]`
- [ ] LinkedIn teaser: `[[FINDING: One-sentence headline result …]]` and `[[RESULT: Gate-reached rate and cost-to-gate range …]]`

### Final checks

- [ ] Grep the article directory for `[[RESULT:`, `[[CHART:`, `[[TABLE:`, and `[[FINDING:` — the published drafts should have zero remaining markers
- [ ] No invented rankings, no averaged A–E ratings, no composite quality score, no dropped failed runs
- [ ] Every dollar figure that is not a dated list price is traceable to a snapshot ID and provider usage logs
- [ ] Charts have captions stating n, configuration identity (model + harness + effort), gate variant, and price view
- [ ] "What's next" updated to reflect publication and any follow-on work (second app, harness arm, etc.)
