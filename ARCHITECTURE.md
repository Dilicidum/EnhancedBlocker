# EnhancedBlocker — Architecture & Design

> Status: design exploration. See [IDEA.md](IDEA.md) for the problem statement.
> Last updated: 2026-06-01

## Decisions locked in

- **Approach: classifier-first**, not "agentic" for the blocker itself. The per-page decision is a fast, principled classifier. An LLM is an *edge component* (hard cases + reports), never the foundation.
- **Local-first.** Embeddings and the classifier run on-device. Page text does not leave the machine. Cloud (if any) is reserved for rare ambiguous cases and report generation — and is optional.
- **"Working" signal = manual toggle.** A focus session is started explicitly, and carries a short **declared intent** (e.g. "writing a Rust parser").
- **Hard rules for known-bad URLs** (~20 to start) sit in front of everything.
- **No real training data expected** (~20 labels). The design must work *few-shot / zero-shot* from day one and only graduate to a trained model once data accrues.
- **Stack:** TypeScript/Angular Chrome MV3 extension, always-on **.NET** backend, **on-demand Python** ML sidecar. See [BUILD_PLAN.md](BUILD_PLAN.md). The ML sidecar runs only during focus sessions (or briefly on demand) to save resources.

## The core: one function

Everything the blocker does is:

```
decide(page, context) -> {allow, block}
```

`page`   = URL + title + extracted text (+ for YouTube: video title, channel).
`context`= focus-mode on/off, declared intent for this session, time of day.

This is a **classification problem**. The implementation is a **cascade** of tiers, cheapest and most certain first, so the expensive/uncertain machinery only runs when needed.

## The decision cascade

```
                 ┌─────────────────────────────────────────────┐
  page+context → │ Tier 0:  Hard rules (exact/domain match)     │ → allow/block (deterministic)
                 └─────────────────────────────────────────────┘
                                   │ no match
                                   ▼
                 ┌─────────────────────────────────────────────┐
                 │ Tier 1:  Embedding-based scoring (local)     │
                 │   a) similarity to known-bad prototypes      │
                 │   b) relevance to declared focus intent      │
                 │   c) zero-shot category (news, etc.)         │
                 └─────────────────────────────────────────────┘
                                   │ produces a confidence score
                        ┌──────────┴──────────┐
                 confident│                    │uncertain (near boundary)
                          ▼                    ▼
                    allow/block        ┌──────────────────────────┐
                                       │ Tier 2: LLM oracle (rare, │
                                       │ cached) OR ask the user   │
                                       │ (active learning)         │
                                       └──────────────────────────┘
```

### Tier 0 — Hard rules (deterministic)
- Exact URL / domain match against the **known-bad list** (~20) and an optional **known-good allowlist**.
- Instant, predictable, no ML. This is where "sites I already know I hate" live.
- Also where category-level absolutes live if you want them as rules (e.g. *all* news domains).

### Tier 1 — Local embedding scoring (the heart, zero/few-shot)
A local sentence-embedding model (e.g. `all-MiniLM-L6-v2`, 384-dim, runs on CPU) turns text into vectors. Three signals, **none require a training loop**:

- **(a) Known-bad prototype similarity** — embed the ~20 bad URLs once; for a new page, compute similarity to that set (nearest-centroid or k-NN). High similarity ⇒ "smells like the stuff I block." *This is how unknown-but-similar sites get caught — the original requirement.*
- **(b) Intent relevance** — cosine similarity between the page embedding and the **declared focus intent** embedding. Low relevance during a focus session ⇒ off-topic. *This is how off-topic YouTube gets blocked without blocking all YouTube.*
- **(c) Category** — a pretrained **zero-shot classifier** (NLI-based) scores labels like `news`, `entertainment`, `educational`, `work`. Lets you say "news is always out, even off the clock."

These signals combine into a single confidence score. **Day one**, the combiner is a simple, interpretable scoring rule (sensible default weights + thresholds). It is *not* hand-tuned heuristics in the brittle sense — each input is a meaningful, calibrated quantity, and the combiner is explicitly slated to be replaced by a learned model (below).

### Tier 2 — Resolve the uncertain middle
Only pages whose score lands near the decision boundary reach here:
- **Active learning (uncertainty sampling):** ask the user precisely on the cases where a label is most informative (score ≈ boundary). Minimal interruptions, fastest learning.
- **Optional LLM oracle:** for genuinely ambiguous content, a one-off LLM judgment that can also *explain*. Result is **cached per URL / per video-ID** so it's never paid twice. (Local-first: only invoked if the user opts into any cloud use.)

## Block screen & feedback loop (label collection via friction)

A block is **not** an opaque wall. The block screen:

- **Shows what was blocked** — page/video title, a preview, and *why* (which tier/signal fired, e.g. "looks similar to your blocked list" or "off-topic for: writing a Rust parser").
- Offers two feedback actions:
  - **"Good call"** → confirms the block. Records a `block` label, dismisses.
  - **"Bad call / false positive"** → records an `allow` label.
- **The link to the real content is non-clickable by default.** It becomes clickable *only after* the user presses **"Bad call / false positive."** You cannot mindlessly click through — proceeding **requires** declaring it a false positive, which is exactly the label the model needs.

This makes the override gate double as the **primary label source**: friction and learning are the same action. (Optional: add a short cooldown/confirm even after unlock, if hard-block-with-override is chosen as the block style.)

## How learning works (principled, small-data-aware)

The system is honest about data scarcity:

1. **Start with no trained model.** Tiers 0–1 are rule/embedding based and work immediately.
2. **Collect labels** — primarily from the block screen (see above), plus passive signals:
   - **"Bad call / false positive"** on the block screen ⇒ strong `allow` label (and unlocks the link)
   - **"Good call"** on the block screen ⇒ strong `block` label
   - quickly bounce off an *allowed* page during focus ⇒ weak `block` signal
   - answers to active-learning prompts ⇒ strong labels
3. **Graduate the combiner** from the default scoring rule to a **logistic regression** over the Tier-1 signals once enough labels exist. Logistic regression is the principled, *interpretable* "weighted evidence" model — you can read off why a page was blocked.
4. **Active learning** chooses which uncertain cases to ask about, so the limited labels you do produce are maximally useful.

> Why this is not "custom heuristics": the inputs are calibrated semantic quantities (embedding similarities, classifier posteriors), and the decision rule is a standard ML model (nearest-centroid → logistic regression) with a defined path from few-shot to learned. Nothing is an arbitrary if/else.

## Where the LLM legitimately belongs

| Layer | Tool | Rationale |
|---|---|---|
| Per-page decision (hot path) | Local embeddings + classifier | Must be <~50ms, reliable, private, interpretable |
| Ambiguous edge cases | LLM oracle, cached, opt-in | Rare; adds nuance + explanations that become labels |
| Reports / weekly coach (Part 3) | LLM | Genuine strength: narrate patterns over history |

The blocker is **not** an agent. If anything becomes "agentic," it's the **Part 3 coach** that reasons over your logged history — that's where multi-step autonomy is actually useful.

## Open questions
- Block style once unlocked: immediate access, or a short cooldown/confirm after "Bad call"?
- Exact local embedding + zero-shot models (size vs. quality vs. CPU budget).
- Where the cascade runs: pure in-extension (WASM/transformers.js) vs. a small local companion service. Affects model size and latency.
- Label store + classifier retraining cadence.
- How declared intent is captured at focus-session start (free text? picked from recent tasks?).

## Build order
1. **Tracking + Tier 0 hard-rule blocker** (Chrome extension): proves the plumbing, starts logging data.
2. **Tier 1 local embedding scoring**: the real value — context-aware, generalizes to unknown URLs.
3. **Active learning + learned combiner**: gets personal over time.
4. **Reports / coach (Part 3)**: LLM-driven summaries over the collected history.
