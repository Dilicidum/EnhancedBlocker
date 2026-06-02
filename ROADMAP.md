# EnhancedBlocker — Roadmap & Work-Package Breakdown

> The **overall plan**, expressed as discrete work-packages (WPs) with dependencies.
> This is the batch-ready view: WPs with no dependency between them can later be run
> in parallel (e.g. via `/batch`, one PR each); dependent WPs must run in order.
> Detail lives in [IDEA.md](IDEA.md) · [ARCHITECTURE.md](ARCHITECTURE.md) · [BUILD_PLAN.md](BUILD_PLAN.md) · [TECH_PLAN.md](TECH_PLAN.md).
> Status: planning only — **no code yet, by design.**
> Last updated: 2026-06-01

## How to read this
- **WP** = one self-contained unit of implementation work (roughly one PR).
- **Deps** = WPs that must be merged first.
- **∥ group** = WPs that can be built simultaneously (no shared files / no ordering).
- Implementation starts only after the plan is approved.

---

## Milestone 1 — Tracking + Tier-0 blocker  *(no Python)*
Goal: full plumbing end-to-end; start collecting events + labels. Forward-compatible seams for ML.

| WP | Title | Scope | Deps |
|----|-------|-------|------|
| **WP1** | Backend: onion skeleton | Solution + Domain/Application/Infrastructure/Api (onion refs); entities w/ `Create`/`Update` factories; EF Core DbContext + PostgreSQL (Npgsql) + first migration. | — |
| **WP2** | Backend: CQRS + API + cascade | CQRS commands/queries + handlers; `DecideQuery` runs `IDecisionTier` cascade (`Tier0RuleTier`); thin Minimal API endpoints (`/events`, `/decision`, `/feedback`, `/rules`, `/focus/*`, `/health`); CORS + token middleware. | WP1 |
| **WP3** | Extension: skeleton + build | Angular 21 workspace; `build-extension.mjs` (ng build + esbuild for SW/content + asset copy); `manifest.json`; load-unpacked works. | — |
| **WP4** | Extension: runtime | Service worker (nav logging → `/events`, decision call → `/decision`); content script (document_start "checking…" overlay → reveal / block iframe). | WP2, WP3 |
| **WP5** | Extension: UI surfaces | Popup (focus toggle + intent), Options (rules CRUD), Block screen (Good/Bad call, immediate access on false positive). | WP3 *(consumes WP2 API)* |
| **WP6** | Integration + E2E | Wire all together; verify: session logged, ~20 URLs blocked, Good/Bad-call writes Labels. | WP4, WP5 |

**Parallelizable:** `∥{WP1, WP3}` first → then WP2 → then `∥{WP4, WP5}` → WP6.
*(Only the backend/extension split is truly parallel; steps within each are sequential.)*

---

## Milestone 2 — Tier-1 ML  *(on-demand Python sidecar)*
Goal: context-aware blocking that generalizes to unknown URLs. Additive — no M1 rewrite.

| WP | Title | Scope | Deps |
|----|-------|-------|------|
| **WP7** | Python ML sidecar | FastAPI: `/embed`, `/zeroshot`, `/score`, `/health`; sentence-transformers + zero-shot models; venv. | — |
| **WP8** | .NET sidecar lifecycle | Spawn/health-poll/kill Python; tie start to focus-session, idle-timeout shutdown; cold-start fail rules. | WP2 |
| **WP9** | Tier-1 in cascade | `Tier1MlTier`: known-bad prototype similarity + intent relevance + zero-shot category → score → outcome; promote discovered category-domains to Tier-0 cache. | WP7, WP8 |
| **WP10** | Decision cache | Per-URL / per-video-id cache so nothing is classified twice. | WP9 |

**Parallelizable:** `∥{WP7, WP8}` → WP9 → WP10.

---

## Milestone 3 — Learning
Goal: personalize from the labels M1+ collected.

| WP | Title | Scope | Deps |
|----|-------|-------|------|
| **WP11** | Active learning | Query the user only on near-boundary cases (uncertainty sampling). | WP9 |
| **WP12** | Learned combiner | Graduate default scoring → logistic regression (scikit-learn) once labels suffice; persist + reload. | WP9 |

**Parallelizable:** `∥{WP11, WP12}`.

---

## Milestone 4 — Reports / coach  *(Part 3)*
Goal: see where the day went; narrative insight.

| WP | Title | Scope | Deps |
|----|-------|-------|------|
| **WP13** | Tracking rollups | Aggregations/queries over Event/FocusSession; daily/weekly time breakdowns + read endpoints. | WP1 |
| **WP14** | LLM narrative reports | Summaries over history (local vs cloud LLM decided here). | WP13 |

**Parallelizable:** WP13 → WP14. *(WP13 also feeds the external **statistics app** seam.)*

---

## Ecosystem seams (kept open, built elsewhere)
- **Pomodoro app (ASP.NET Core):** drives focus sessions via `POST /focus/start|stop` instead of/alongside the manual toggle.
- **Statistics app:** consumes Event/FocusSession data + reports (WP13). Keep `Core`/`Data` reusable.

## Critical-path summary
`WP1 → WP2 → WP4/WP5 → WP6` is the M1 critical path. Everything ML (WP7–WP12) is layered on top without reworking M1, thanks to the four seams in [BUILD_PLAN.md](BUILD_PLAN.md#future-proofing-making-the-python-ml-step-additive-built-in-m1).
