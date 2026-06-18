# Architecture

ThreeDManager is a multi-project .NET solution with clear boundaries between UI, application logic, domain, infrastructure, and worker execution.

## Solution Shape

- `src/ThreeDManager.Web`: ASP.NET Core MVC UI and primary user workflow.
- `src/ThreeDManager.Application`: use cases, DTOs, and interfaces.
- `src/ThreeDManager.Domain`: entities and business rules.
- `src/ThreeDManager.Infrastructure`: EF Core persistence, parsers, and external integrations.
- `src/ThreeDManager.Worker`: ETL/import jobs and future sync processes.

## Current Coverage

The repository currently implements the production core:
- product, printer, material, print import, and print job records
- dashboard overview
- MVC navigation and layout entrypoints

## Data Rules

- Keep raw import data separate from normalized operational data.
- Preserve original import records even when parser output is transformed into `PrintJob`.
- Keep `AppDbContext` as the source of truth for EF entity sets and table mapping.
- Keep schema changes data-safe and reversible when possible.

## Future Direction

The next layers planned for the solution are costing, inventory, commercial, finance, and analytics.
