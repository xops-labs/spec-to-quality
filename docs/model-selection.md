# Spec-to-Quality (S2Q) — Model Selection Strategy (Phase 1 proposal)

> **Status:** Phase 1 draft awaiting review. **Date checked: 2026-11-14 (America/Detroit).** All prices were read from the official provider pages listed below on that date and are **list prices per 1,000,000 tokens in USD**. They are a *snapshot*, not durable facts. They were not reconciled with any invoice. The machine-readable copy is `data/pricing-snapshots/2026-11-14.json`.
>
> Nothing here asserts that any model is better than another. Tiers are a **pre-registered stratification by list output price**, used to ask whether price predicts cost-to-gate (RQ0). They are not capability rankings, and "premium" does not imply "best".

## 1. Selection principles

1. Span **low / mid / premium** list-price tiers across **multiple providers**, including at least one open-weight or low-cost option.
2. Only models that can run through the **common harness** with tool calling (so the same tool surface applies).
3. **Pin exact model IDs** (dated snapshots where the provider offers them) and record returned IDs.
4. Keep the core set small enough for *n* ≥ 10 runs per configuration within a budget (§6).
5. Decide tiers **before** looking at any results.
6. Avoid choosing models because we expect a particular outcome. If a hypothesis-relevant comparison is wanted (e.g., low-price vs mid-price within one provider), include both models of that provider.

## 2. Verified model data (as of 2026-11-14)

Cached-input = prompt-cache read price. "Cond." = conditions that change the price. "Default effort" = default reasoning/effort setting stated in provider docs where we saw one.

### Anthropic — https://platform.claude.com/docs/en/about-claude/pricing and https://platform.claude.com/docs/en/about-claude/models/overview

| Model ID | Input | Cached read | Output | Context | Cond. |
|---|---|---|---|---|---|
| `claude-haiku-5-5` | 0.10 | 0.01 | 0.50 | 1M | Priced per request: prompts > 100K tokens use 0.50 / 0.05 / 2.50 |
| `claude-sonnet-5-5` | 2.00 | 0.10 | 10.00 | 1M | — |
| `claude-opus-5-5` | 4.00 | 0.20 | 20.00 | 1M | — |
| `claude-fable-5-2` | 8.00 | 0.20 | 40.00 | 1M | — |

Batch API 50 % off; cache writes (5-minute) listed separately in the JSON snapshot. Anthropic's overview lists other models (e.g., Mythos 5.2 with limited availability); they are not considered. Older models in the table (Opus 5, Opus 4.x, Sonnet 4.6) are cheaper or pricier at different points; they are out of scope for reasons of recency.

### OpenAI — https://developers.openai.com/api/docs/pricing and model pages `https://developers.openai.com/api/docs/models/<id>`

| Model ID | Input | Cached | Output | Context | Cond. |
|---|---|---|---|---|---|
| `gpt-6-luna` | 0.10 | 0.01 | 0.50 | 1.05M | Prompts > 272K input tokens: 2× input/cache and 1.5× output on the whole request |
| `gpt-6.1-sol` | 2.00 | 0.10 | 10.00 | 1.05M | same |
| `gpt-6-astra` | 10.00 | 1.00 | 50.00 | 1.05M | same |

Batch/Flex at 50 % of Standard; "Fast" modes at 2× (Sol also has Ultrafast at 6×). Not used: `gpt-6.1-terra` (page was read; not selected to keep the set small).

### Google — https://ai.google.dev/gemini-api/docs/pricing and model pages `https://ai.google.dev/gemini-api/docs/models/<id>`

| Model ID | Input | Cached | Output | Cond. |
|---|---|---|---|---|
| `gemini-3.5-flash-lite` | 0.30 | 0.03 | 2.50 | cache storage $1.00 / 1M tokens / hour |
| `gemini-4-hydrogen` | 1.50 | 0.075 | 7.50 | output includes thinking tokens; 1,048,576-token unified window |
| `gemini-4-argon` | 2.00 | 0.10 | 10.00 | **Promotional through 2026-11-30; from 2026-12-01: 4.00 / 0.20 / 20.00** |
| `gemini-4-carbon` | 2.00 | 0.10 | 10.00 | **Promotional through 2026-11-30; from 2026-12-01: 4.00 / 0.20 / 20.00**; not on the free tier |

### xAI — https://docs.x.ai/developers/models (docs host shows SpaceXAI branding)

| Model ID | Input | Cached | Output | Context | Cond. |
|---|---|---|---|---|---|
| `grok-build-0.1` (aliases listed by xAI include `grok-code-fast-1`) | 1.00 | 0.20 | 2.00 | 256K | Prompts ≥ 200K: 2.00 / 0.40 / 4.00 |
| `grok-4.7` | 2.00 | 0.50 | 6.00 | 500K | Prompts ≥ 200K: 4.00 / 1.00 / 12.00; xAI docs name Grok 4.7 as the recommended code model |

### DeepSeek (open weights) — https://api-docs.deepseek.com/quick_start/pricing

| Model ID (version label) | Input (peak / off-peak) | Cached (peak / off-peak) | Output (peak / off-peak) | Context |
|---|---|---|---|---|
| `deepseek-flash` (DeepSeek-V4.1-Flash-1017) | 0.30 / 0.15 | 0.006 / 0.003 | 1.20 / 0.60 | 1M |
| `deepseek-v4-pro` (DeepSeek-V4-Pro-0813) | 1.32 / 0.66 | 0.044 / 0.022 | 3.96 / 1.98 | 1M |

Peak hours are 01:00–04:00 and 06:00–10:00 UTC, Monday–Friday excluding Chinese public holidays; other times are off-peak (half of peak). Hugging Face weights pages: https://huggingface.co/deepseek-ai/DeepSeek-V4.1-Flash-1017 and https://huggingface.co/deepseek-ai/DeepSeek-V4-Pro-0813; repo metadata showed an MIT license (verify before relying on it).

### Mistral — https://docs.mistral.ai/inference/pricing
"Mistral Large 4": input 0.70, cached 0.07, output 2.10. **The API model-ID string was not confirmed on the pricing page.** Treated as a reserve candidate until the ID is verified.

### Qwen
Qwen was researched as a possible open-weight candidate, but **no exact model ID or official price was verified** in this phase. It is omitted from the proposal; revisit in Phase 3 if an open-weight Qwen coding model is added via an official or self-hosted route.

### Unverified/claims not taken from official pages
- Any capability or ranking statement about these models (we make none).
- Model "default effort" values beyond those shown in the JSON snapshot.

## 3. Proposed comparison set

### 3.1 Core set (10 configurations, 4 providers + 1 open-weight)

| Tier (by list output price/MTok) | Configuration | Output price | Rationale |
|---|---|---|---|
| **Low** (≤ 2.50) | `claude-haiku-5-5` | 0.50 | Anthropic low-price model (note > 100K tier) |
| Low | `gpt-6-luna` | 0.50 | OpenAI low-price model |
| Low | `deepseek-flash` | 0.60–1.20 | Open-weight, time-of-day priced; allows possible self-hosting later |
| **Mid** (5–15) | `claude-sonnet-5-5` | 10.00 | Anthropic mid |
| Mid | `gpt-6.1-sol` | 10.00 | OpenAI mid |
| Mid | `gemini-4-hydrogen` | 7.50 | Google mid |
| Mid | `grok-4.7` | 6.00 | xAI mid; named by xAI as its code model |
| **Premium** (≥ 20) | `claude-opus-5-5` | 20.00 | Anthropic premium |
| Premium | `gpt-6-astra` | 50.00 | OpenAI premium |
| Premium | `gemini-4-carbon` | 10.00 now / 20.00 from 2026-12-01 | Google premium; price changes mid-window — see §4. |

*Observation:* tier labels follow list prices only, and list prices overlap across tiers when promotions are applied (Carbon currently equals Sonnet on output price). A pre-registered rule ("tier = list output price on the snapshot date, using the *post-promotion* price for Gemini 4 models") avoids ambiguity. **[DECIDE]**.

### 3.2 Reserve set (add if budget permits or a core model proves unusable in the harness)
`claude-fable-5-2`, `gemini-4-argon`, `gemini-3.5-flash-lite`, `grok-build-0.1`, `deepseek-v4-pro`, `mistral-large-4` (ID to verify).

### 3.3 Rationale for the within-provider contrasts
Anthropic, OpenAI and Google each contribute multiple tiers, so "same vendor, different price" contrasts can be pre-registered, which partly controls for vendor-specific behaviour and harness affinity. xAI and DeepSeek add cross-vendor breadth.

## 4. Pricing volatility to plan for
- **Promotional pricing ends 2026-12-01** for `gemini-4-carbon` and `gemini-4-argon` (price doubles). If any Phase 4 run happens after that date, cost must be computed with the price in force on the run date, and the primary analysis should include a normalised view at post-promotion prices.
- **DeepSeek peak/off-peak:** record the UTC timestamp of each call so cost can be computed per price window; or schedule all DeepSeek runs off-peak and state so.
- **Long-context tiers:** Haiku 5.5 (> 100K), OpenAI (> 272K), xAI (≥ 200K). Agent loops grow their context; per-request thresholds can change effective prices sharply. Record per-call prompt sizes.
- **Tokenizers differ,** so token counts are not comparable across providers; compare **dollars** and report tokens within a provider.
- **Prices change without notice;** re-snapshot before each execution batch.

## 5. Recording scheme (versions, pricing, configuration)

**5.1 Pricing snapshots** — `data/pricing-snapshots/<YYYY-MM-DD>.json` (schema `0.1-draft`): provider, model ID, tier assignment, per-token prices (input, cached read, cache write where applicable, output), conditions, source URLs, date/time checked, checker. A run record references the **snapshot ID** active at the run time and, for time-dependent prices (DeepSeek, promos), the call timestamp.

**5.2 Model identity** — per run, store: requested model ID; the **model ID returned** in the API response; any system-fingerprint/version header; provider region/endpoint; date of provider documentation consulted.

**5.3 Execution configuration** — per run: reasoning-effort setting (`provider_default` plus the effective default documented by the provider on the snapshot date, or "not documented"; D-003), max output tokens, temperature/top-p when permitted (many reasoning models do not allow them — record "not settable"), tool allow-list, context-management settings of the harness, budgets (turns, wall-clock, tool calls, dollar ceiling), seed where supported, retry policy, prompt-template hash, specification commit hash.

**5.4 Harness identity** — harness name, version/tag and commit, container image digest, LiteLLM (or equivalent) version if used, and any provider-specific adapters.

**5.5 Raw usage logs** — the unmodified `usage` object of every API call, with timestamp, latency and HTTP status; cost is computed afterwards (methodology §5.4).

**5.6 Reconciliation** — for a sample of runs per provider, compare computed cost against the provider's usage/billing dashboard and record discrepancies.

**5.7 Change log** — any silent model update, deprecation notice or price change observed during the study is logged with its date.

## 6. Illustrative cost arithmetic (assumption-driven, NOT a measurement)

Purpose: only to show why the *pilot* must measure real token usage. The numbers below are **pure arithmetic on the 2026-11-14 list prices under an assumed token profile** of 5M uncached input + 10M cached-read input + 1M output tokens per run. This profile is an **assumption invented for illustration**; it is not an estimate of what any model will use. Cache-write charges, long-context surcharges (except the Haiku >100K line), reasoning-token differences and retries are ignored.

| Configuration | Cost per assumed-profile run (USD) |
|---|---|
| `claude-haiku-5-5` (≤100K prompts / >100K prompts) | 1.10 / 5.50 |
| `gpt-6-luna` | 1.10 |
| `deepseek-flash` (off-peak / peak) | 1.38 / 2.76 |
| `claude-sonnet-5-5` | 21.00 |
| `gpt-6.1-sol` | 21.00 |
| `gemini-4-hydrogen` | 15.75 |
| `grok-4.7` | 21.00 |
| `claude-opus-5-5` | 42.00 |
| `gpt-6-astra` | 110.00 |
| `gemini-4-carbon` (promo / from 2026-12-01) | 21.00 / 42.00 |

Under this assumption the spread across the core set is roughly two orders of magnitude, and one full pass of 10 runs over all 10 configurations would be on the order of a few thousand dollars **before** Stage B. Real usage will differ in both directions (a cheap model may take more turns; a stronger model may finish in fewer). The pilot (n = 3) will replace this section with measured numbers, and *n* and the extension rule will be finalised from them.

## 7. Harness options and confounds

| Option | Notes (verified 2026-11-14 from GitHub API/README-level info) |
|---|---|
| **mini-swe-agent** (MIT, v2.4.6) | Minimal, bash-only agent loop; multi-provider via LiteLLM. Pros: small surface, easy to audit and pin; maximises comparability. Cons: little scaffolding (no dedicated edit tools), may disadvantage models trained for richer tool use. |
| **OpenHands** (MIT, v1.40.0) | Full agent platform with browsing/editing tools and sandboxing. Pros: closer to practitioner use. Cons: larger surface; more harness-specific behaviours. |
| **OpenCode** (MIT) | Terminal coding agent supporting many providers. Pros: realistic tool set. Cons: fast-moving releases; pin carefully. |
| Vendor-native agents | Useful as a **sensitivity arm** for a subset; not comparable across vendors. |

**Decision (locked 2026-11-15, [decisions.md](decisions.md) D-002):** primary arm = `mini-swe-agent` (a feasibility pilot on a small dummy spec still checks that every core model works in it); sensitivity arm = a second harness (OpenHands/OpenCode) for a subset of configurations — optional, still open (O-010). Report all results as **model + harness + settings** configurations (methodology §6). Recent literature supports treating the harness as a first-class variable (originality review G1–G4).

## 8. Open decisions for Phase 3/4
- Final tier rule for promotional prices.
- Core vs reserve membership after harness feasibility testing (a model that cannot reliably use the harness's tool-calling format should be flagged, not silently dropped).
- Reasoning-effort policy: **locked 2026-11-15 to provider default for all models** (D-003). Effort changes both cost and quality, so effort differences are part of each model's effect; the effective default per model is recorded in the run manifest.
- Whether to self-host an open-weight model for a stronger reproducibility arm.
- Verification of Mistral/Qwen model IDs and prices if they are added.
