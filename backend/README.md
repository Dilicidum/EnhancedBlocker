# EnhancedBlocker — Backend (M1)

.NET 10 onion-architecture backend for the EnhancedBlocker context-aware website
blocker + time tracker. This is **Milestone 1**: event tracking + the Tier-0
(hard-rule) decision tier. No Python/ML yet (those are M2 seams, already wired in
nullable columns and the pluggable cascade).

## Projects (onion; dependencies point inward only)

| Project | Role |
|---|---|
| `EnhancedBlocker.Domain` | Entities (`Event`, `FocusSession`, `Rule`, `CategoryDomain`, `Label`, `DecisionLog`) as classes with **private setters** + static `Create`/`Update` factories returning `OneOf<T, ValidationError>`. Enums. No framework deps except `OneOf`. |
| `EnhancedBlocker.Application` | CQRS commands/queries + handlers, port interfaces, the decision cascade (`DecideQueryHandler`), and a thin in-house dispatcher (`ISender`). Depends only on Domain. |
| `EnhancedBlocker.Infrastructure` | EF Core (`AppDbContext`) on **PostgreSQL/Npgsql**, entity configs, repository adapters, and `Tier0RuleTier`. The initial migration lives here. Depends on Application + Domain. |
| `EnhancedBlocker.Api` | ASP.NET Core **Minimal API** host (composition root). Loopback-only Kestrel, token middleware, CORS, thin endpoints. |
| `EnhancedBlocker.Tests` | xUnit: domain factories, the cascade handler, and Tier-0 matching. |

## Dispatcher: in-house, not MediatR

We use a ~30-line reflection-based dispatcher (`Application/Messaging/Sender.cs`)
instead of MediatR. **Reason:** recent MediatR versions ship under a commercial
license whose terms are ambiguous for this kind of personal/redistributable
project, so rather than take on that licensing risk we wrote the minimal piece we
actually need. It supports exactly one handler per request (no pipeline
behaviors/streaming). `Sender` is the single seam to revisit if behaviors are ever
required — handlers and call sites would not change.

## Configuration

`appsettings.json` / `appsettings.Development.json`:

- `ConnectionStrings:AppDb` — PostgreSQL connection. Default:
  `Host=localhost;Port=5432;Database=enhancedblocker;Username=postgres;Password=postgres`
- `EnhancedBlocker:Port` — loopback port Kestrel binds to (default **5180**).
- `EnhancedBlocker:ApiToken` — shared secret required in the `X-EB-Token` header
  on every endpoint except `GET /health`.
- `EnhancedBlocker:ExtensionId` — stable `chrome-extension://<id>` allowed by CORS
  in production.
- `EnhancedBlocker:AllowDevOrigins` — when true (dev), CORS allows any origin.

The design-time migration factory also honors the `EB_CONNECTION_STRING`
environment variable.

## Run

```bash
# 1. Ensure the database exists (any client). Example:
#    psql -h localhost -U postgres -c "CREATE DATABASE enhancedblocker;"

# 2. Build
dotnet build backend/EnhancedBlocker.sln

# 3. Apply the EF migration (install tools once: dotnet tool install --global dotnet-ef)
dotnet ef database update \
  --project backend/EnhancedBlocker.Infrastructure \
  --startup-project backend/EnhancedBlocker.Api

# 4. Run the API (Development uses appsettings.Development.json → token "dev-local-token")
dotnet run --project backend/EnhancedBlocker.Api
```

The API listens on `http://127.0.0.1:5180` (loopback only).

## Endpoints

| Method | Route | Notes |
|---|---|---|
| GET | `/health` | `{status}`. Bypasses the token guard. |
| POST | `/events` | Batch of events → 202. |
| POST | `/decision` | `DecisionContext` → `{outcome, tier, reason, score?}`. |
| POST | `/feedback` | Writes a `Label` → 201. |
| GET | `/rules` | List rules. |
| POST | `/rules` | Add a rule → 201. |
| DELETE | `/rules/{id}` | 204 / 404. |
| POST | `/focus/start` | `{intent}` → `{focusSessionId}`. |
| POST | `/focus/stop` | optional `{focusSessionId}` (defaults to active session) → 200. |

All non-`/health` endpoints require header `X-EB-Token: <ApiToken>`.

### Example

```bash
TOKEN=dev-local-token
curl http://127.0.0.1:5180/health
curl -X POST http://127.0.0.1:5180/rules -H "X-EB-Token: $TOKEN" -H "Content-Type: application/json" \
  -d '{"pattern":"youtube.com","match":"Domain","kind":"Block","source":"Manual"}'
curl http://127.0.0.1:5180/rules -H "X-EB-Token: $TOKEN"
curl -X POST http://127.0.0.1:5180/decision -H "X-EB-Token: $TOKEN" -H "Content-Type: application/json" \
  -d '{"url":"https://youtube.com/watch?v=1"}'    # → outcome Block
curl -X POST http://127.0.0.1:5180/decision -H "X-EB-Token: $TOKEN" -H "Content-Type: application/json" \
  -d '{"url":"https://github.com"}'               # → outcome Allow (default)
```

## Tests

```bash
dotnet test backend/EnhancedBlocker.sln
```

Covers domain `Create`/`Update` factories (valid + invalid), `DecideQueryHandler`
(block match, allow default, defer fall-through), and `Tier0RuleTier` (exact,
domain, subdomain, lookalike-suffix, allow precedence, category cache).

## M2 seams (already in place)

- `FocusSession.IntentEmbedding` — nullable PostgreSQL `bytea` (stays null in M1).
- `Label.FeaturesJson` — nullable PostgreSQL `jsonb` (stays null in M1).
- `IDecisionTier` cascade — M1 registers only `Tier0RuleTier`; adding the ML tier
  is one `AddScoped<IDecisionTier, Tier1MlTier>()` line.
- `Outcome.Pending` exists for M2's slow ("checking…") path.
