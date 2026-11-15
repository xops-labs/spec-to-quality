---
title: "LinkedIn teaser — Spec-to-Quality (S2Q)"
status: draft-pre-results
audience: engineering leaders and practitioners
length_target: ~150 words
---

# LinkedIn teaser (draft, pre-results)

Cost per token is a convenient number for comparing AI coding models. It is a poor proxy for what you actually pay: the cost of turning a specification into software you would be willing to merge.

I am starting Spec-to-Quality (S2Q), an open-source experiment on that gap. Several models, in one common harness (mini-swe-agent), will each implement the same frozen spec for an incident-management app (.NET + TypeScript/React). I will measure the result with SonarQube, hidden acceptance tests, coverage, and mutation testing, then give each run at most three remediation rounds toward a predefined quality gate. The headline metric is cost-to-quality-gate. Failed runs stay in the data.

There are no results yet. When there are, they will be reported as model-plus-harness configurations, not as a "best model" ranking.

[[FINDING: One-sentence headline result for H2 / cost-to-gate — TBD]]

[[RESULT: Gate-reached rate and cost-to-gate range across tiers — TBD]]

Repo and protocol: https://github.com/xops-labs/spec-to-quality
