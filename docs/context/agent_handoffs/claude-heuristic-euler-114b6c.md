# claude/heuristic-euler-114b6c

- Status: ready_for_finalizer
- Candidate commit: HEAD (the commit containing this handoff)
- Base inspected: 493d7bffc079867cdb25aa2a808e26c847f4ee3f (`origin/main`, `0 0` vs local `main` at branch creation)
- Scope: Resolve the documented Phase 1 closeout gate by deciding the production-failure data model, and record the decision plus its evidence in the canonical docs. Documentation only; no application code, schema, migration, or deployment artifact changed.
- Files changed:
  - `docs/decisions/0001-production-failure-data-model.md` (new)
  - `docs/core/ARCHITECTURE.md` (decision 2 moved from open to resolved)
  - `docs/context/BACKLOG.md` (Phase 1 closeout priority resolved; operator-gated sub-question called out)
  - `docs/core/ROADMAP.md` (Phase 1 "production failure tracking" now points at the decision)
  - `docs/INDEX.md` (indexes the new `docs/decisions/` directory)
  - `docs/context/agent_handoffs/claude-heuristic-euler-114b6c.md` (this handoff)
- Validation:
  - Environment precheck: `docker ps --filter name=threedmanager-db` → `Up 45 hours`.
  - `git diff --check -- docs` → clean, no whitespace errors (the canonical gate for a docs-only batch per `docs/AGENT_CONTEXT.md`).
  - `dotnet build ThreeDManager.slnx` → `Compilação com êxito`, 0 warnings, 0 errors.
  - `dotnet test ThreeDManager.slnx --no-build` → 167 passed, 0 failed, 0 skipped (unchanged from the part-51 baseline, as expected: this batch adds no test because it changes no behavior).
  - No runtime smoke was run, and none applies: this batch changes no route, view, controller, or persisted state. Every factual claim in the decision record was instead verified directly against the code it cites (file:line references listed in the record).
- Deployment impact: none.
- Next action for finalizer: `git diff 493d7bf..HEAD -- docs` to review the decision record and the four canonical-doc edits, then integrate. Note for the diary: this batch deliberately breaks the parts 35-51 cosmetic-sort chain and instead takes the documented Phase 1 closeout gate, per the governance checkpoint's instruction that the first worker "must be selected from the documented Phase 1 closeout / Phase 2-3 design-gated backlog, not from an undocumented cosmetic queue".

## Why this task instead of the part-51 suggestion

Part 51's "next recommended task" proposed a fifth clear-sort link (Products/Printers index). That was not taken. Two documented rules override it:

- `AGENTS.md` invariant: "Do not create a new feature when a documented bug or unfinished higher-priority task exists." The Phase 1 closeout gate is unfinished and is the earliest phase, so it outranks a Phase-6-adjacent presentation affordance.
- The governance checkpoint in `tasks_diary.md` explicitly directed the first worker away from "an undocumented cosmetic queue" and toward the documented design-gated backlog.

`docs/errors/errors.md` records no active bugs, so no bug outranked this either.

## Finding that the finalizer should not silently lose

While answering the gate, one substantive discovery came out of reading the stock path:

`PrintJobStockService.TryApplyStockDeductionAsync` returns early unless the status is `Completed` (`src/ThreeDManager.Infrastructure/Services/PrintJobStockService.cs:116`). A job marked `Failed` therefore records **no `MaterialStockMovement`**, even when the operator entered the filament it burned — so material physically consumed by a failed print stays on the books as available stock.

This was **not fixed here and not filed in `errors.md`**, deliberately. Whether a failed print should deduct material is exactly the design question this gate covers, and either answer rewrites real inventory data. It is documented as Option A / Option B in the decision record and left for explicit operator sign-off. It should not be resolved opportunistically by a later worker.
