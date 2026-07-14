# Scope and Priorities

## Current Scope

ThreeDManager currently focuses on the production core:
- product registration
- printer registration
- material registration
- print import ingestion
- print job registration
- dashboard reporting

Already-delivered supporting scope includes alpha authentication/hosting, material and finished-goods stock movements, manual adjustments, low-stock alerts, and read-only costing/margin projections. These are part of the operational core, not future ideas.

## Priority Levels

### P0 - Stabilize Production Data

Why it matters:
- this is the data the rest of the system depends on

Priority items:
- preserve raw import records
- keep normalized jobs consistent with imports
- normalize status values
- track failures explicitly
- expose clear details for imported jobs

### P1 - Cost Accuracy

Why it matters:
- price and margin depend on cost data

Priority items:
- cost per gram
- cost per print hour
- energy estimation
- packaging and consumables
- suggested selling price

### P2 - Inventory Visibility

Why it matters:
- the system needs to know what is available and what is committed

Priority items:
- raw material stock
- finished goods stock
- reserved stock
- stock movements

### P3 - Commercial Model

Why it matters:
- production needs to connect to customers and orders

Priority items:
- customers
- sales channels
- orders
- delivery tracking

### P4 - Finance and Intelligence

Why it matters:
- business events should produce financial and analytical signals

Priority items:
- revenue and expenses
- fees and profit
- business dashboards
- management reports

## Out of Scope for Now

- microservices
- multi-tenant SaaS architecture
- marketplace automation before the core data model is stable
- over-engineered BI before the operational model exists
- broad refactors that do not support the current phase
