# Architecture

ThreeDManager is a modular ASP.NET Core MVC application for operating a small 3D-print production business. It is deliberately a modular monolith: one web application and one PostgreSQL database, with explicit project boundaries and no premature distributed-system infrastructure.

## Solution boundaries

| Project | Responsibility |
| --- | --- |
| `ThreeDManager.Domain` | Entities and domain constants: products, materials, printers, imports, productions, and stock movements. |
| `ThreeDManager.Application` | Use-case contracts and interfaces, including the production-stock service contract. |
| `ThreeDManager.Infrastructure` | EF Core `AppDbContext`, PostgreSQL persistence, G-code parsing, migrations, and stock service implementation. |
| `ThreeDManager.Web` | MVC controllers, ViewModels, Razor views, presentation helpers, authentication, and composition root. |
| `ThreeDManager.Worker` | Reserved boundary for future import/synchronization work; it must not absorb Web or persistence responsibilities. |
| `ThreeDManager.Tests` | Unit and MVC integration coverage using isolated test databases. |

Dependencies point inward: Web depends on Application/Infrastructure composition, Infrastructure implements Application contracts, and Domain has no dependency on the delivery layers. Razor renders data; it does not own business rules.

## Current operational capabilities

The current implementation already covers more than the original production-core baseline:

- products, printers, materials, G-code imports, and print jobs;
- import review and normalized production records while preserving raw import data;
- production status presentation, queue/capacity visibility, and dashboard KPIs;
- material and finished-goods stock, movement ledgers, low-stock alerts, and manual adjustments;
- production cost and price signals using persisted material, printer, packaging, sale-price, and target-margin data;
- one alpha operator account, cookie authentication, dark theme, and a local Windows-service deployment behind a Cloudflare Tunnel.

## Data and workflow spine

```text
G-code file -> PrintImport (raw source) -> reviewed PrintJob
                                          -> material stock movement
                                          -> finished-goods stock movement

Product + Material + Printer cost data -> read-only cost/margin projections
                                       -> MVC lists, details, and Dashboard
```

`AppDbContext` is the persistence source of truth. Schema changes must preserve existing records and be represented by a new EF Core migration; existing migrations are historical records and are not rewritten.

## Runtime and security boundary

- PostgreSQL runs in Docker and is bound only to `127.0.0.1:5436`.
- The `ThreeDManager` Windows service listens on `127.0.0.1:5080`.
- External users reach only the HTTP application through Cloudflare Tunnel; they do not receive repository, Windows, Docker, or database access.
- Alpha credentials are provided through service environment variables, never source control. Data-protection keys live outside the repository in `C:\ProgramData\ThreeDManager\keys`.
- Publishing source and updating the Windows service is an explicit release action; a Git integration never restarts the live application by itself.

See `docs/operations/ALPHA_HOSTING.md` and `docs/ops/REMOTE_ACCESS.md` for the operational runbooks.

## Change and integration topology

`main` is the only long-lived branch and the only checkout allowed to represent the release source. Feature work happens in short-lived `codex/*` or `claude/*` branches inside isolated worktrees. A separate finalizer integrates one verified candidate at a time onto `main`, reruns the full gate, pushes it, and only then removes the merged worktree. The detailed protocol is `docs/operations/DEVELOPMENT_WORKFLOW.md`.

## Next architectural decisions

The present data model supports the active costing/inventory work. The next changes that need an explicit design decision before implementation are:

1. `Printer.PowerConsumptionWatts` plus the energy-cost model and migration.
2. Commercial order/customer entities, only after the operational production and inventory rules are accepted as stable.

Resolved:

- The production-failure data model is decided in `docs/decisions/0001-production-failure-data-model.md`: no separate failure schema. Failure-rate and loss reporting work on today's status data; root cause and reprint each need one nullable `PrintJob` column. One sub-question stays open for the operator — whether a failed job should deduct material stock, which today it does not.
