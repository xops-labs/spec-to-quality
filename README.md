# Spec-to-Quality (S2Q)

**Status: Phase 1 draft awaiting review.** No experiments have been run and there are no results yet.

S2Q is an independent, open-source research project that asks:

> Does the initial cost advantage of a lower-priced AI coding model remain meaningful after accounting for functional correctness, static-analysis findings, testing, and remediation effort?

Several AI coding models, run in a common agent harness, will each implement the *same* approved engineering specification. The resulting applications are evaluated with identical, pinned tooling (SonarQube static analysis plus independent acceptance tests, coverage and mutation testing), pushed through a bounded remediation stage toward a predefined quality gate, and costed end to end.

```
Specification → AI Model → Generated Application → Static Analysis → Remediation → Engineering Cost
```

## Project overview
- **Focus:** static code analysis (SonarQube) and engineering cost. DAST, cryptographic posture, infrastructure security and other validations are out of scope for the initial research.
- **Reporting unit:** a *model-plus-harness configuration*, not a model in isolation.
- **Hypotheses:** H1–H5 are hypotheses, not facts. Each has pre-defined support/contradict criteria.
- **Not claimed:** that any model is superior, that SonarQube proves production readiness, or that static findings are confirmed defects.

## Current status
Phase 1 (vision and research methodology) is drafted and awaiting review. Phases 2–7 are planned in the roadmap. See [docs/phase1-summary.md](docs/phase1-summary.md) for the decisions that must be made before Phase 2.

## Documentation index
| Document | Purpose |
|---|---|
| [docs/phase1-summary.md](docs/phase1-summary.md) | Concise summary and open decisions |
| [docs/vision.md](docs/vision.md) | Problem, motivation, vision, research questions, scope, success criteria |
| [docs/research-methodology.md](docs/research-methodology.md) | Hypotheses, variables, design, stages, statistics, threats to validity |
| [docs/quality-metrics.md](docs/quality-metrics.md) | Metric catalog (with SonarQube keys), limitations, gate principles, cost methodology |
| [docs/experiment-roadmap.md](docs/experiment-roadmap.md) | Phases 1–7 |
| [docs/originality-review.md](docs/originality-review.md) | Existing research, gaps, hedged novelty, references |
| [docs/model-selection.md](docs/model-selection.md) | Dated pricing, proposed model set, recording scheme, harness options |
| [docs/benchmark-application.md](docs/benchmark-application.md) | Benchmark application recommendation |
| [data/pricing-snapshots/](data/pricing-snapshots/) | Dated, machine-readable pricing snapshots |

## Research standards
1. Evidence-driven and falsifiable; hypotheses are distinguished from facts.
2. No fabricated results, citations, pricing or metrics; official documentation and peer-reviewed work preferred; every source is marked verified or unverified.
3. Reproducible configurations; model versions and execution parameters are disclosed.
4. Raw results are preserved; failed runs are never selectively excluded.
5. Statistical variation across repeated trials is analysed; threats to validity are discussed.
6. No proprietary company code, specifications or internal data in this public project.
7. Pre-registration of hypotheses, metrics, gate and analysis before confirmatory runs.

## License
Suggested: **Apache-2.0** for code and **CC BY 4.0** for documents and data (to be confirmed by the maintainer before publication). No `LICENSE` file has been added yet.

## Contributing
Not yet open. Issues and review comments on the Phase 1 documents are welcome once the repository is published.
