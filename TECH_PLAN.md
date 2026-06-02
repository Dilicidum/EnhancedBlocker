# EnhancedBlocker — Technology Architecture Plan

> Status: technology plan for M1 (and seams for M2+). See [BUILD_PLAN.md](BUILD_PLAN.md) for milestones, [ARCHITECTURE.md](ARCHITECTURE.md) for the ML design.
> Last updated: 2026-06-01

## Verified local toolchain
| Tool | Version | Used for |
|---|---|---|
| .NET SDK | **10.0.300** | Backend (target `net10.0`) |
| Node | 24.14.0 | Extension build |
| npm | 11.9.0 | Extension deps |
| Angular CLI | **21.2.5** | Extension UI |
| Python | 3.12.9 | ML sidecar (M2) |
| git | 2.53.0 | VCS |

## Solution layout
```
EnhancedBlocker/
  IDEA.md  ARCHITECTURE.md  BUILD_PLAN.md  TECH_PLAN.md
  EnhancedBlocker.sln
  backend/                          # Onion architecture (deps point inward)
    EnhancedBlocker.Domain/         # entities, value objects, domain logic. Create/Update factories. No deps.
    EnhancedBlocker.Application/    # CQRS commands/queries + handlers + port interfaces (IRuleRepo, IDecisionTier...). Depends: Domain.
    EnhancedBlocker.Infrastructure/ # EF Core + PostgreSQL, repo impls, Python client. Depends: Application, Domain.
    EnhancedBlocker.Api/            # ASP.NET Core host/endpoints (Kestrel @ 127.0.0.1). Composition root.
  extension/                    # Angular 21 workspace + esbuild for SW/content
    src/
      ui/                       # Angular app: popup, options, block surfaces
      worker/background.ts      # MV3 service worker (esbuild → ESM)
      content/content.ts        # content script (esbuild → IIFE)
      manifest.json
    tools/build-extension.mjs   # orchestrates ng build + esbuild + asset copy
  ml/                           # Python FastAPI sidecar (M2)
```
`Domain`/`Application` are dependency-light libraries so the future **statistics/Pomodoro apps can reuse the same domain + use cases**.

---

## Backend (.NET 10) — Onion architecture

**Layering:** `Domain` ← `Application` ← `Infrastructure` ← `Api`. Dependencies point inward only; `Api` is the composition root that wires Infrastructure into Application ports via DI.

**Style:** ASP.NET Core **Minimal API**, Kestrel bound to `http://127.0.0.1:<port>` (loopback only). HTTP is fine for localhost; no cert hassle.

**Conventions (see CLAUDE.md):**
- **CQRS** in Application: one command/query + handler per use case, dispatched via mediator (MediatR) or thin dispatcher. Endpoints are thin (build → send → map).
- **OneOf** for results/unions where it fits (handler results, decision outcomes, success/error) — no throwing/null-returning for expected branches.
- **Domain classes use static `Create` / `Update` factory methods**; no public ctors / open setters. Invariants live in the factories.

**NuGet packages**
- `Npgsql.EntityFrameworkCore.PostgreSQL` (10.x) — PostgreSQL provider
- `Microsoft.EntityFrameworkCore.Design` (migrations)
- `OneOf`
- (M2) `System.Net.Http.Json` for calling the Python sidecar

**Hosting (M1):** console app (`dotnet run`). Later: package as a Windows **tray app** (or service) that auto-starts. PostgreSQL connection string in config (local Postgres instance / Docker).

**Security (single-user localhost):**
- CORS policy allowing the extension origin `chrome-extension://<stable-id>` (stable id via a `key` in `manifest.json`).
- Shared-secret header (`X-EB-Token`) checked by middleware; token stored in backend config + extension storage. (Good enough for localhost; note to harden later.)

### Domain entities (`EnhancedBlocker.Domain`)
Classes with **private setters** and static **`Create` / `Update`** factories holding invariants (no public ctors). Conceptual fields:

```
Event         : Id, Ts, Url, Domain, Title?, TabId, Type, FocusSessionId?, DurationMs?
FocusSession  : Id, StartedAt, EndedAt?, DeclaredIntent, IntentEmbedding?(M2)
Rule          : Id, Pattern, Match(Exact|Domain), Kind(Block|Allow), Source, Category?
CategoryDomain: Domain, Category, Confidence, AddedAt
Label         : Id, Ts, Url, Title?, Decision(Allow|Block), Source, FeaturesJson?(M2)
DecisionLog   : Id, Ts, Url, Tier, Outcome, Score?
```

Pattern, e.g.:
```csharp
public sealed class FocusSession
{
    public Guid Id { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public string DeclaredIntent { get; private set; }
    public byte[]? IntentEmbedding { get; private set; } // M2

    private FocusSession() { } // EF

    public static OneOf<FocusSession, ValidationError> Create(string intent, DateTimeOffset now)
        => string.IsNullOrWhiteSpace(intent)
            ? new ValidationError("intent required")
            : new FocusSession { Id = Guid.NewGuid(), StartedAt = now, DeclaredIntent = intent.Trim() };

    public OneOf<Success, ValidationError> Stop(DateTimeOffset now)
    {
        if (EndedAt is not null) return new ValidationError("already stopped");
        EndedAt = now; return new Success();
    }
}
```
`IntentEmbedding` and `FeaturesJson` are nullable now → no migration when M2 fills them.

### Decision cascade (the key seam) — `EnhancedBlocker.Application`
Exposed as a CQRS **query** (`DecideQuery` → `DecideQueryHandler`) that runs the cascade below. `OneOf` models a tier's verdict; a tier either decides or defers (`Defer`).
```csharp
enum Outcome { Allow, Block, Pending }              // Pending exists for M2's slow path
record DecisionContext(string Url, string Domain, string? Title, string? Text,
                       Guid? FocusSessionId, string? Intent, DateTimeOffset Now);
record TierResult(Outcome Outcome, string Tier, string Reason, double? Score);
struct Defer { }                                    // tier had no opinion

interface IDecisionTier {                            // M1 registers Tier0 only
    Task<OneOf<TierResult, Defer>> EvaluateAsync(DecisionContext ctx, CancellationToken ct);
}

class DecisionService(IEnumerable<IDecisionTier> tiers) {
    public async Task<TierResult> DecideAsync(DecisionContext ctx, CancellationToken ct) {
        foreach (var t in tiers) {
            var r = await t.EvaluateAsync(ctx, ct);
            if (r.IsT0) return r.AsT0;               // first decisive tier wins
        }
        return new(Outcome.Allow, "default", "no rule matched", null);
    }
}
```
- **M1:** `Tier0RuleTier` — checks `Rule` (exact/domain) + `CategoryDomain` cache → `TierResult`, else `Defer`.
- **M2:** append `Tier1MlTier` — HttpClient to Python; may return `Pending` while the sidecar warms.
Registered in DI in order; adding M2 = one `AddScoped<IDecisionTier, Tier1MlTier>()` line.

### API endpoints (Minimal API) — each maps to a CQRS command/query
| Method | Route | CQRS message | Body / Query | Returns |
|---|---|---|---|---|
| POST | `/events` | `LogEventsCommand` | `Event[]` (batch) | 202 |
| POST | `/decision` | `DecideQuery` | `DecisionContext` | `TierResult` |
| POST | `/feedback` | `RecordFeedbackCommand` | `{url,title,decision,source}` | 201 (writes Label) |
| GET | `/rules` | `ListRulesQuery` | — | rule list |
| POST/DELETE | `/rules` | `AddRuleCommand` / `DeleteRuleCommand` | `Rule` / id | 201 / 204 |
| POST | `/focus/start` | `StartFocusCommand` | `{intent}` | `{focusSessionId}` |
| POST | `/focus/stop` | `StopFocusCommand` | — | 200 |
| GET | `/health` | — (no handler) | — | `{status}` |
| GET | `/reports/...` | `*Query` | — | M4 |

Endpoints are thin: bind → send command/query via mediator → map the `OneOf` result to HTTP.

`/focus/start` is the shared seam for the **Pomodoro app** to drive sessions; reads of `Event`/`FocusSession` are the seam for the **statistics app**.

---

## Extension (Angular 21 + MV3)

**Manifest V3**, three code kinds with different constraints, so **two build tools**:

| Part | Tech | Why |
|---|---|---|
| Popup / Options / Block UI | **Angular 21** (one app, route per surface) | Rich UI, your stack |
| `background.ts` (service worker) | **esbuild** → ESM (`"type":"module"`) | SW is one file, ephemeral; no Angular |
| `content.ts` (content script) | **esbuild** → IIFE | Content scripts can't use runtime ESM imports |

**Build:** `tools/build-extension.mjs` runs `ng build` (UI, `outputHashing: none` for stable names), then esbuild for `background.ts` + `content.ts`, then copies `manifest.json` + icons into `dist/`. One `npm run build:ext`.

**UI surfaces:** single Angular app, **standalone components** + `provideRouter`; `popup.html`, `options.html`, `block.html` each load the bundle and route to `/popup`, `/options`, `/block`. Keeps one bundle, three entry HTMLs.

**State:** **NgRx Signals** (`@ngrx/signals`). A `signalStore` per surface/domain (e.g. focus state, rules list), backed by a thin typed HTTP client to the .NET API. Signal-based throughout (no NgModules, no classic NgRx store/effects).

### Block + "checking…" flow (implements seam #2)
1. **Content script @ `document_start`** injects a minimal, framework-free **full-page overlay** ("checking…") so nothing flashes.
2. Sends `{url, title}` to the service worker → `POST /decision` (+ logs an Event).
3. **Allow** → content script removes overlay (reveal page).
4. **Block** → overlay is replaced by an **iframe** to `chrome.runtime.getURL('block.html')?url=...&reason=...` — full Angular block screen, same extension origin.
5. **Block screen (Angular):** shows title + reason (+ YouTube thumbnail via oembed); **Good call** → `POST /feedback {decision:block}`; **Bad call (false positive)** → `POST /feedback {decision:allow}`, set an **allow-once** flag, then `top.location = originalUrl` → **immediate access** (allow-once stops a re-block loop).
6. **Pending** (M2) is already handled: overlay just stays in "checking…" until resolved — **no extension rework needed for ML.**

### Chrome APIs (M1)
- `chrome.webNavigation` / `chrome.tabs.onUpdated` — observe navigations (logging + trigger decision).
- `chrome.storage.local` — focus state, API token, allow-once flags.
- `host_permissions`: `http://127.0.0.1/*`; `permissions`: `webNavigation`, `storage`, `tabs`, `scripting`.
- `manifest.key` → stable extension id (needed for backend CORS allowlist).
- *(declarativeNetRequest deliberately not used in M1 — we route through `/decision` to keep one async-capable cascade path for M2.)*

---

## Python ML sidecar (M2 — committed tech, built later)
- **FastAPI** + **uvicorn**, loopback port, spawned/killed by .NET (lifecycle in BUILD_PLAN.md).
- `sentence-transformers` (`all-MiniLM-L6-v2`, 384-dim) — embeddings.
- `transformers` zero-shot (`facebook/bart-large-mnli`, or smaller `valhalla/distilbart-mnli-12-3` for CPU) — categories.
- `scikit-learn` — nearest-centroid (few-shot) → logistic regression (M3).
- Endpoints: `/embed`, `/zeroshot`, `/score`, `/health`. Managed via a `venv`; `.NET` launches `python -m uvicorn ml.app:app --port <p>` and polls `/health`.
- (Verify `torch` wheels for Python 3.12 at setup — fine on 3.12.)

---

## M1 implementation order
1. `dotnet new sln` + `Domain` / `Application` / `Infrastructure` / `Api` projects (onion refs); entities w/ `Create`/`Update` factories + `AppDbContext` (Npgsql/PostgreSQL) + first migration.
2. `DecisionService` + `Tier0RuleTier` (Application/Infrastructure); wire DI at the Api composition root.
3. Minimal API endpoints + CORS + token middleware; `dotnet run` smoke test against local Postgres.
4. Angular workspace + esbuild build script; manifest; load unpacked in Chrome.
5. Service worker (nav logging + decision call) ↔ content script (overlay/checking/block iframe).
6. Angular surfaces: popup (focus toggle + intent), options (rules CRUD), block screen (Good/Bad call + immediate access).
7. End-to-end: log a session, block the ~20 URLs, confirm Good/Bad-call writes Labels.
