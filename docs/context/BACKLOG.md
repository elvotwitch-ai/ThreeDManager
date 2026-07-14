# Backlog by Phase

This backlog is intentionally ordered by business dependency. The rule is: build the data spine first, then attach commercial and financial layers.

## Phase 0 - Foundation

Status: complete

Work items:
- solution structure
- MVC web app
- database and migrations
- worker process
- Docker Compose

## Phase 1 - Production Core

Status: mostly delivered

Work items:
- production dashboard
- product, printer, and material CRUD
- print import upload and parsing
- print job registration
- import-to-job linking
- production details and history views
- failure tracking

Current closeout priority: confirm whether existing status data is sufficient for failure/reprint reporting; do not add a separate failure schema without that design decision.

## Phase 2 - Costing and Pricing

Status: active / partially delivered

Work items:
- cost per gram
- cost per hour
- energy estimation
- packaging cost
- margin rules
- price suggestion

Remaining design-gated item: printer energy estimation requires an explicit `Printer.PowerConsumptionWatts` schema decision and a migration plan.

## Phase 3 - Inventory

Status: active / partially delivered

Work items:
- raw material stock
- finished goods stock
- reserved quantities
- stock movement ledger
- manual adjustments

Remaining design-gated item: reservation rules must be tied to commercial orders, so do not introduce reserved-stock behavior before Phase 4 has an order model.

## Phase 4 - Commercial

Status: planned

Work items:
- customers
- orders
- order items
- sales channels
- delivery status
- after-sales notes

## Phase 5 - Finance

Status: planned

Work items:
- revenue events
- expense events
- marketplace fees
- profit by product
- cash flow summaries

## Phase 6 - Intelligence

Status: planned

Work items:
- management dashboards
- trends by period
- products with highest failure rate
- products with highest margin
- channel performance

## Phase 7 - Integrations

Status: future

Work items:
- marketplace import/export
- external order synchronization
- stock synchronization
- webhook/API integration

## Backlog Rules

- keep each phase small enough to verify independently
- do not start a later phase before the data required by earlier phases exists
- prefer preserving raw data over destructive transformations
- avoid refactors that are unrelated to the current phase
