# EnhancedBlocker — Build Plan

> Status: planning. See [IDEA.md](IDEA.md) (vision) and [ARCHITECTURE.md](ARCHITECTURE.md) (ML design).
> Last updated: 2026-06-01

## Stack decisions

| Concern | Choice | Notes |
|---|---|---|
| Browser layer | **TypeScript, Chrome MV3** | Mandatory; Angular used for the extension's UI pages. |
| Extension UI | **Angular** (standalone + NgRx Signals) | Popup, block screen, options page. |
| Backend | **.NET (ASP.NET Core)**, localhost, **onion architecture + CQRS** | Always-on, lightweight. Storage + decision cascade + reports + Python lifecycle. CQRS handlers, OneOf, domain `Create`/`Update` factories. |
| Storage | **PostgreSQL + EF Core** (Npgsql) | Local Postgres instance. |
| ML | **Python (FastAPI) sidecar**, on-demand | Committed: ML lives in Python (sentence-transformers + zero-shot + scikit-learn). Arrives in M2, but M1 is built with the seams so adding it is **additive, not a rewrite** (see below). |
| Reports LLM (later) | TBD | Local (Ollama) vs cloud, decided at Part 3. |

Everything runs locally on the user's Windows machine. Page content never leaves the device (local-first).

## Future-proofing: making the Python ML step additive (built in M1)
ML is Python and comes in M2, but M1 is designed so M2 only *adds a provider* — no rewrite. The four seams:

1. **Pluggable cascade in .NET.** `/decision` iterates an ordered list of `IDecisionTier`. M1 ships only `Tier0Rules`. M2 appends `Tier1Ml` (which calls the Python sidecar). The endpoint contract `{outcome, tier, reason, score?}` already carries tier/score.
2. **Async, "checking…"-capable decision flow.** Even though Tier-0 is instant, build the extension + block screen in M1 to handle a decision that may be *pending* (show a brief "checking…" state, then resolve). M2's slower ML decision then needs **no** extension rework. This is the single most important seam.
3. **Schema already has the ML columns** (nullable): `FocusSession.intentEmbedding` (bytea), `Label.features` (jsonb). No migration pain when M2 starts populating them.
4. **Capture inputs ML will need, from day one.** The content script extracts page title + URL (+ YouTube video title) in M1; structure extraction so adding page-text/embedding later is a content-script tweak, not a redesign. (Defer storing full page text for privacy/size; fetch at decision time when M2 needs it.)

## Related apps / ecosystem (out of scope, but design seams now)
The user is also building (separately):
- **Pomodoro app** — backend **definitely ASP.NET Core**. A Pomodoro work block *is* a focus session. Seam: let an external app start/stop focus sessions via the same `POST /focus/start|stop` API, so the timer can be the "am I working?" signal instead of (or alongside) the manual toggle.
- **Statistics app** — stack TBD. Overlaps with Part 3 (reports) and wants the same tracking data. Seam: keep Event/FocusSession data and read endpoints clean and queryable so this app can consume them; don't bury tracking data in blocker-only internals.

Implication: the **focus-session concept and the tracking data are shared assets**, not blocker-private. Model them as a small, stable contract. (Whether these apps share one backend/DB or integrate over HTTP is a later call — just don't design anything that blocks it.)

## Process model (resource-conscious)

```
 ┌────────────────────┐     localhost HTTP      ┌──────────────────────┐
 │  Chrome extension  │ ◄────────────────────►  │  .NET backend        │  ALWAYS ON
 │  (Angular + TS)    │                         │  (ASP.NET Core)      │  (tiny idle cost)
 │  - service worker  │                         │  - REST API          │
 │  - content script  │                         │  - Postgres (EFCore) │
 │  - popup / options │                         │  - cascade Tier 0    │
 │  - block screen    │                         │  - Python lifecycle  │
 └────────────────────┘                         └──────────┬───────────┘
                                                spawn/kill  │ localhost HTTP
                                                on demand    ▼
                                               ┌──────────────────────┐
                                               │  Python ML sidecar    │  ON-DEMAND ONLY
                                               │  (FastAPI)            │  (started on focus-start,
                                               │  - /embed /zeroshot   │   killed on focus-end /
                                               │  - /score             │   idle timeout)
                                               └──────────────────────┘
```

### Python sidecar lifecycle (the on-demand contract)
- **.NET owns the process.** It launches the sidecar (`uvicorn`) as a child process and polls `/health` until ready.
- **Start triggers:** focus-session start (preferred — warms models before the first block); or lazily, the first time a Tier-1 decision is needed.
- **Stop triggers:** focus-session end, **or** an idle timeout (no Tier-1 request for N minutes and no active focus session).
- **Cold start (~few seconds to load models):** warming on focus-start hides it. While warming during a focus session, fail **closed** (block, show "checking…"); outside a session, fail **open** (allow unless Tier 0 blocks).
- **Outside focus sessions only Tier 0 runs** → Python stays off. "Always-block" categories (e.g. news) are enforced via the Tier-0 **category-domain cache**, which is populated when the sidecar identifies such sites *during* sessions.

## Communication
- **Extension ↔ .NET:** REST over `http://127.0.0.1:<port>`. Extension declares `host_permissions` for that origin. A startup handshake token guards the API (only our extension may call it).
- **.NET ↔ Python:** REST over a second localhost port; .NET routes Tier-1 calls and manages the process.

## Data model (PostgreSQL)
- **Event** — id, ts, url, domain, title, tabId, eventType (navigate/active/idle), focusSessionId?, durationMs. *(time tracking — Part 2)*
- **FocusSession** — id, startedAt, endedAt?, declaredIntent (text), intentEmbedding (bytea, later).
- **Rule** — id, pattern (exact/domain), kind (block/allow), source (manual/derived), category?. *(Tier 0; the ~20 known-bad live here)*
- **CategoryDomainCache** — domain, category, confidence, addedAt. *(promotes Python discoveries to cheap Tier-0 enforcement)*
- **Label** — id, ts, url, title, decision (allow/block), source (good-call/bad-call/bounce/active-query), features (jsonb, later). *(feeds active learning)*
- **Decision** (optional log) — url, tier, score, outcome, ts. *(debugging/auditing the cascade)*

## .NET API surface (initial)
- `POST /events` — log navigation/activity (batch-friendly).
- `POST /decision` — body: {url, title, text?, context}; returns {outcome, tier, reason, score?}.
- `POST /feedback` — body: {url, title, decision, source}; writes a Label.
- `GET/POST/DELETE /rules` — manage Tier-0 rules.
- `POST /focus/start` {intent} · `POST /focus/stop` — toggles session; start may warm Python.
- `GET /reports/...` — later (Part 3).

## Repo structure
```
EnhancedBlocker/
  IDEA.md  ARCHITECTURE.md  BUILD_PLAN.md  TECH_PLAN.md  ROADMAP.md  CLAUDE.md
  backend/        # .NET onion solution (Domain/Application/Infrastructure/Api; EF Core + PostgreSQL)
  extension/      # Angular MV3 extension (standalone + NgRx Signals)
  ml/             # Python FastAPI sidecar (on-demand)
```

---

## Milestones

### M1 — Tracking + Tier-0 blocker  ← first slice (NO Python)
Goal: prove the full plumbing end-to-end and start collecting data + labels.

**Backend (.NET — onion)**
- [ ] Solution with Domain/Application/Infrastructure/Api projects; EF Core + PostgreSQL (Npgsql); entities above with `Create`/`Update` factories; migrations.
- [ ] CQRS commands/queries + handlers for each use case (LogEvents, Decide, RecordFeedback, ListRules/AddRule/DeleteRule, StartFocus/StopFocus); endpoints `POST /events`, `POST /focus/start|stop`, `GET/POST/DELETE /rules`, `POST /feedback` are thin pass-throughs.
- [ ] `IDecisionTier` abstraction + ordered cascade; ship **only `Tier0Rules`** (exact/domain match vs Rule + CategoryDomainCache → allow/block + reason). M2 appends `Tier1Ml`.
- [ ] `POST /decision` returns `{outcome, tier, reason, score?}` and supports a **pending/checking** outcome (for M2's slow path), even though Tier-0 always resolves instantly now.
- [ ] API guard token + CORS for the extension origin.
- [ ] Run as a console app for dev (tray app later).

**Extension (Angular + MV3)**
- [ ] MV3 + Angular build setup (manifest, multiple entry points, no hashed filenames).
- [ ] Service worker: subscribe to `webNavigation`/`tabs.onUpdated`; log events to `/events`; call `/decision` per navigation.
- [ ] Content script: extract title/url (+ YouTube video title); handle a **pending/"checking…"** decision state then resolve; on `block`, render the **block screen** overlay. (Extraction structured so adding page-text later is additive.)
- [ ] Block screen (Angular): show what + why; **Good call** / **Bad call (false positive)** buttons; link non-clickable until "Bad call", then **immediate** access; post `/feedback`.
- [ ] Popup (Angular): focus toggle + declared-intent field → `/focus/*`; show current status.
- [ ] Options page (Angular): CRUD the ~20 hard rules.

**Done when:** navigations are logged, a focus session can be toggled with intent, the ~20 known URLs are blocked with the feedback block screen, and Good/Bad-call clicks land as Labels.

### M2 — Tier-1 ML (on-demand Python)
- [ ] Python FastAPI sidecar: load embedding + zero-shot models; `/embed`, `/zeroshot`, `/score`, `/health`.
- [ ] .NET Python-lifecycle manager: spawn/health-poll/kill; wire to focus start/stop + idle timeout.
- [ ] Cascade Tier 1 in `/decision`: known-bad prototype similarity (nearest-centroid), intent relevance, zero-shot category → score → outcome; cold-start fail-closed/open rules.
- [ ] Promote discovered category domains (e.g. news) into `CategoryDomainCache`.
- [ ] Per-URL / per-video-ID decision cache.

### M3 — Learning
- [ ] Active learning: ask only on near-boundary cases.
- [ ] Graduate combiner from default scoring → logistic regression (scikit-learn) once labels suffice; persist + reload model.

### M4 — Reports / coach (Part 3)
- [ ] Time-tracking rollups from Event data; daily/weekly summaries.
- [ ] LLM-generated narrative reports (model choice decided here).

## Open setup questions
- Exact embedding + zero-shot model files (size vs. CPU budget) — settle in M2.
- Idle-timeout duration for the Python sidecar.
- How the .NET backend is launched/kept alive on Windows (tray app vs. service) — fine as console for M1.
