# Documentation Index

Start here:
1. `AGENTS.md`
2. `docs/AGENT_CONTEXT.md`
3. `docs/core/ARCHITECTURE.md`
3. `docs/core/IMPLEMENTATION_POLICY.md`
4. `docs/core/ROADMAP.md`
5. `docs/context/SCOPE.md`
6. `docs/context/BACKLOG.md`
7. `docs/context/tasks_diary.md`
8. `docs/errors/errors.md`

What each doc is for:
- `docs/AGENT_CONTEXT.md`: agent entrypoint and reading order.
- `docs/core/ARCHITECTURE.md`: product and technical architecture.
- `docs/core/IMPLEMENTATION_POLICY.md`: coding and validation rules.
- `docs/core/ROADMAP.md`: technical roadmap by phase and dependency.
- `docs/context/SCOPE.md`: current scope, priorities, and out-of-scope items.
- `docs/context/BACKLOG.md`: phased backlog with current status.
- `docs/context/tasks_diary.md`: running checkpoint log.
- `docs/decisions/`: numbered records for decisions that were gated before implementation.
- `docs/errors/errors.md`: active bugs, blockers, and verification status.
- `docs/operations/ALPHA_HOSTING.md`: alpha access credentials and safe local-server startup.
- `docs/operations/DEVELOPMENT_WORKFLOW.md`: shared Codex/Claude worktree and finalizer protocol.
- `AGENTS.md` / `CLAUDE.md`: root entrypoints that direct Codex and Claude to the shared contract.

Pre-commit checklist:
1. Run `dotnet build ThreeDManager.slnx`.
2. Run `dotnet run --project src/ThreeDManager.Web`.
3. Open the app and verify the changed flow end-to-end.
4. For behavior changes, verify the persisted or observable result, not only HTTP 200.
5. Run `git status`.
6. Run `git add .`.
7. Run `git commit -m "<message>"`.
