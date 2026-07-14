# 0001 - Production failure data model

- Status: accepted
- Date: 2026-07-14
- Decides: `docs/core/ARCHITECTURE.md` "Next architectural decisions" item 2, and the
  `docs/context/BACKLOG.md` Phase 1 closeout priority.

## Question

> Confirm whether existing status data is sufficient for failure/reprint reporting; do not add
> a separate failure schema without that design decision.

`docs/core/ARCHITECTURE.md` states the same gate as three reporting needs: **root cause**,
**reprint**, and **loss** reporting.

## Data that exists today

Verified against the current schema and code paths, not from memory:

- `PrintJob.Status` (`src/ThreeDManager.Domain/Entities/PrintJob.cs:42`), constrained to the five
  constants in `PrintJobStatus` (`Imported`, `Planned`, `Completed`, `Failed`, `Canceled`).
- `PrintJob.FailureReason` (`src/ThreeDManager.Domain/Entities/PrintJob.cs:44-45`), free text,
  `StringLength(500)`, added by migration `20260709020820_AddFailureReasonToPrintJobs`.
  It is **required** when a job is moved to `Failed`
  (`src/ThreeDManager.Web/Controllers/PrintJobsController.cs:244`), so failed jobs always carry a
  human-readable reason.
- Per-job cost and consumption inputs already on the failed row: `FilamentUsedGrams`,
  `CalculatedMaterialCost`, `ActualTimeMinutes`, `PackagingCost`, `UnitsProduced`, plus
  `ProductId` / `PrinterId` / `MaterialId` / `CreatedAt`, and `Printer.CostPerHour`.

## Decision

**No separate failure entity or failure table.** The existing status data is sufficient for two of
the three reporting needs, and the two genuine gaps are closed by **two nullable columns on
`PrintJob`** — not by a new schema. Verdict per need:

| Reporting need | Verdict | Basis |
| --- | --- | --- |
| Failure rate per product/printer | **Sufficient today** | `Status` + `ProductId`/`PrinterId` + `CreatedAt` already answer Phase 6 "products with highest failure rate". No change needed. |
| Loss (value of a failure) | **Sufficient today, with a caveat** | The failed row keeps `FilamentUsedGrams`, `CalculatedMaterialCost`, `ActualTimeMinutes`; with `Printer.CostPerHour` the wasted material and machine cost are computable. See the stock caveat below. |
| Root cause | **Insufficient** | `FailureReason` is free text. It reads well on one job but cannot be grouped or counted, so "top failure causes" is unanswerable. Needs a **nullable `FailureCategory`** (a `PrintJobFailureCategory` constants class mirroring `PrintJobStatus`), keeping `FailureReason` as the free-text detail. |
| Reprint | **Insufficient** | `PrintJob` has no self-reference; a reprint of a failed job is an unrelated new row. "How many failures were reprinted" and "true cost of a failure including its reprint" are unanswerable. Needs a **nullable self-FK `ReprintOfPrintJobId`**. |

Both additions are nullable and additive, so they satisfy the data-safety invariant: existing rows
stay valid, and history simply reports as uncategorized / not-a-reprint.

## Loss caveat: failed jobs do not move stock

`PrintJobStockService.TryApplyStockDeductionAsync` returns early unless the status is `Completed`
(`src/ThreeDManager.Infrastructure/Services/PrintJobStockService.cs:116`), and
`TryApplyProductStockCreditAsync` does the same (`:226`). A job marked `Failed` therefore records
**no `MaterialStockMovement`**, even when the operator entered the filament it burned.

Consequence: loss is computable **from the `PrintJob` rows**, but **not from the stock ledger**, and
material physically consumed by a failed print stays on the books as available stock.

This is recorded as evidence, **not fixed here, and not filed as a bug**. Whether a failed print
should deduct material is precisely the open design question this gate covers, and the answer
changes real inventory data. It needs explicit sign-off, so it is deliberately left to the operator:

- **Option A (deduct on failure)** — most physically accurate; makes the stock ledger the single
  source of loss. Requires deciding whether `FilamentUsedGrams` on a failed job means "burned before
  failing" and a new `StockMovementType` (e.g. `PrintJobFailed`).
- **Option B (keep current behavior)** — stock reflects only successful output; loss reporting reads
  the `PrintJob` rows. Cheaper, but stock drifts from physical reality by exactly the failed volume.

## Consequences

- The Phase 1 closeout gate is **resolved**: the failure model needs two nullable `PrintJob`
  columns, not a separate failure schema. Phase 1 no longer blocks on an undecided data model.
- Phase 6 "products with highest failure rate" is **already implementable** on today's data.
- Root-cause and reprint reporting are unblocked for scheduling as a normal, small, migration-backed
  batch each.
- The failed-job stock question stays open pending an explicit A/B choice by the operator.

## Follow-up work (each its own batch; not started here)

1. Choose Option A or B for failed-job stock deduction. Blocked on operator sign-off.
2. Add nullable `PrintJob.FailureCategory` + `PrintJobFailureCategory` constants + migration, and
   surface it on the failure form beside `FailureReason`.
3. Add nullable `PrintJob.ReprintOfPrintJobId` self-FK + migration, and a "reimprimir" action that
   links the new job to the failed one.
