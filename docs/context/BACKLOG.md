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

Status: active

Work items:
- production dashboard
- product, printer, and material CRUD
- print import upload and parsing
- print job registration
- import-to-job linking
- production details and history views
- failure tracking

## Phase 2 - Costing and Pricing

Status: next

Work items:
- cost per gram
- cost per hour
- energy estimation
- packaging cost
- margin rules
- price suggestion

## Phase 3 - Inventory

Status: planned

Work items:
- raw material stock
- finished goods stock
- reserved quantities
- stock movement ledger
- manual adjustments

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
