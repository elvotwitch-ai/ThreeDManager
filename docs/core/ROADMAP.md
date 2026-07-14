# Technical Roadmap

This roadmap is ordered by dependency, not by UI convenience. The goal is to keep the system separated by project and responsibility while growing it in small verified steps.

## Phase 0 - Foundation

Status: done

Delivered:
- ASP.NET Core MVC web app
- layered solution structure
- PostgreSQL persistence through EF Core
- Docker Compose baseline
- initial migrations and schema history

Exit criteria:
- repository builds cleanly
- schema changes are intentional and data-safe
- web, application, domain, infrastructure, and worker layers stay separated

## Phase 1 - Production Core

Status: mostly delivered; close remaining failure/review gaps before broadening the model

Current focus:
- products
- printers
- materials
- print imports
- print jobs
- dashboard

Remaining technical work:
- production failure tracking (data model decided in `docs/decisions/0001-production-failure-data-model.md`: add nullable `FailureCategory` and `ReprintOfPrintJobId` to `PrintJob`; failed-job stock deduction still needs operator sign-off)
- better import review flows
- status normalization
- capacity-oriented reporting
- tighter links between import records and normalized jobs

Exit criteria:
- production data can be imported, reviewed, corrected, and reported without losing raw history

## Phase 2 - Costing and Pricing

Status: active / partially delivered

Goals:
- cost per gram
- cost per hour
- energy cost
- packaging cost
- suggested selling price
- expected margin by product

Exit criteria:
- the system can estimate product profitability from production data alone

## Phase 3 - Inventory

Status: active / partially delivered

Goals:
- raw material stock
- finished goods stock
- reserved stock
- stock movements
- manual adjustments

Exit criteria:
- the system knows what is available, reserved, sold, or defective

## Phase 4 - Commercial

Status: planned

Goals:
- customers
- sales channels
- orders
- order items
- delivery status
- after-sales tracking

Exit criteria:
- orders can be tied back to products and production records

## Phase 5 - Finance

Status: later

Goals:
- revenue
- expenses
- fees
- profit by product
- cash flow

Exit criteria:
- business events create traceable financial impact

## Phase 6 - Intelligence

Status: later

Goals:
- operational dashboards
- management reports
- historical trends
- product and channel analysis

Exit criteria:
- decisions can be supported by aggregated operational data

## Phase 7 - Integrations

Status: future

Goals:
- marketplace sync
- external order ingestion
- stock synchronization
- future API integrations

Exit criteria:
- the platform can exchange data with external sales and fulfillment channels without breaking the core model
