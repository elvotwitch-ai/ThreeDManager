# Documentation Index

Start here:
1. `docs/AGENT_CONTEXT.md`
2. `docs/core/ARCHITECTURE.md`
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
- `docs/errors/errors.md`: active bugs, blockers, and verification status.

Pre-commit checklist:
1. Run `dotnet build ThreeDManager.slnx`.
2. Run `dotnet run --project src/ThreeDManager.Web`.
3. Open the app and verify the changed flow end-to-end.
4. Run `git status`.
5. Run `git add .`.
6. Run `git commit -m "<message>"`.
