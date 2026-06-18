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

Project validation:
- `dotnet build ThreeDManager.slnx`
- `dotnet test` when test projects exist
- `git diff --check -- docs` for docs-only changes

Critical invariants:
- Preserve existing data when changing EF Core schema.
- Keep controller, view, and entity names consistent.
- Prefer small, verified batches.
- Build the business model around product, production, inventory, commercial, finance, and intelligence.
- Do not describe the solution as a monolith; keep boundaries explicit across the solution projects.

Task routing:
- Feature work: inspect `docs/core/ARCHITECTURE.md`, `docs/core/ROADMAP.md`, `docs/context/SCOPE.md`, and `docs/context/BACKLOG.md`.
- Bug fixes: inspect `docs/errors/errors.md` first.
- Validation: use the commands above and record results in the diary.
