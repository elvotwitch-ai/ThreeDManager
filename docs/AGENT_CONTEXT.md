# Agent Context

Read in this order:
1. `docs/AGENT_CONTEXT.md`
2. `docs/INDEX.md`
3. `docs/core/ARCHITECTURE.md`
4. `docs/core/IMPLEMENTATION_POLICY.md`
5. `docs/core/ROADMAP.md`
6. `docs/context/SCOPE.md`
7. `docs/context/BACKLOG.md`
8. `docs/context/tasks_diary.md`
9. `docs/errors/errors.md`
10. `docs/operations/DEVELOPMENT_WORKFLOW.md` when creating, integrating, or cleaning a worktree

Project validation:
- `dotnet build ThreeDManager.slnx`
- `dotnet run --project src/ThreeDManager.Web`
- access the running app and verify the changed flow end-to-end
- for behavior changes, create or modify data through the app path and verify the persisted or observable result
- `dotnet test` when test projects exist
- `git diff --check -- docs` for docs-only changes
- `git status` and explicit-path staging only after build/run/app verification passes

Critical invariants:
- Preserve existing data when changing EF Core schema.
- Keep controller, view, and entity names consistent.
- Prefer small, verified batches.
- Build the business model around product, production, inventory, commercial, finance, and intelligence.
- Do not describe the solution as a monolith; keep boundaries explicit across the solution projects.
- Do not commit a feature batch until it has been built, run, and checked in the app UI or API path it changed.
- HTTP 200 alone is not enough for behavior changes; verify the expected state change.
- `main` is a release/integration checkout. Feature agents use a short-lived worktree; only the finalizer integrates into or pushes `main`.

Task routing:
- Feature work: inspect `docs/core/ARCHITECTURE.md`, `docs/core/ROADMAP.md`, `docs/context/SCOPE.md`, and `docs/context/BACKLOG.md`.
- Bug fixes: inspect `docs/errors/errors.md` first.
- Validation: use the commands above and record results in the diary.
- Worktree/finalizer operation: read `docs/operations/DEVELOPMENT_WORKFLOW.md`.
