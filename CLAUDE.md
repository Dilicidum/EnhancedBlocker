# EnhancedBlocker — Conventions for Claude

Pet project. Workflow + coding conventions below. Read before writing code or editing plan docs.

## Workflow
- **Push straight to `main`** (small pet project). No PR ceremony, no feature branches required.
- Keep commits clean; co-author trailer as usual.

## Backend (.NET 10, PostgreSQL)
- **Onion architecture.** Layers, dependencies point inward only:
  - `Domain` — entities, value objects, domain logic. No external/framework deps.
  - `Application` — use cases via **CQRS**, abstractions (repository/port interfaces), orchestration. Depends only on Domain.
  - `Infrastructure` — EF Core, PostgreSQL, external services, repository implementations. Depends on Application/Domain.
  - `Api` (Presentation) — ASP.NET Core host/endpoints. Depends on Application (+ Infrastructure via DI at composition root only).
- **EF Core with PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL`). Not SQLite.
- **CQRS in the Application layer.** Each use case is a separate **command** (writes) or **query** (reads) with its own handler — no fat service classes. Dispatched via a mediator (e.g. MediatR) or a thin custom dispatcher. API endpoints are thin: build the command/query, send it, map the result.
- **OneOf** — use the `OneOf` library for results/unions where it fits (e.g. command/query results, decision outcomes, success/error returns) instead of throwing or null-returns. Handlers return `OneOf<...>`.
- **Domain classes expose static `Create` / `Update` factory methods** for construction and mutation — no public parameterless ctors / open setters. Encapsulate invariants there.

## Frontend (Angular 21)
- **Standalone components** (no NgModules).
- **NgRx Signals** (`@ngrx/signals` — `signalStore`, `signalState`) for state management. Signal-based throughout.

## ML
- Python FastAPI sidecar, on-demand. (Unchanged — see ARCHITECTURE.md / BUILD_PLAN.md.)
