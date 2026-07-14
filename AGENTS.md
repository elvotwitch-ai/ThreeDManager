# ThreeDManager Agent Contract

Read this file before editing. It applies to Codex and Claude.

## Repository entrypoint

1. Read `docs/AGENT_CONTEXT.md` in its canonical order.
2. Read `docs/operations/DEVELOPMENT_WORKFLOW.md` before any Git/worktree action.
3. Check `git status -sb`, the current branch, `git worktree list --porcelain`, and active handoffs under `docs/context/agent_handoffs/`.
4. Check `threedmanager-db`. A stopped database is an environment blocker, not a code defect.

## Roles

### Implementation worker

- Use only a new short-lived worktree and branch: `codex/<task>` for Codex or `claude/<task>` for Claude.
- Implement exactly one small documented task.
- Never edit, merge, push, deploy, clean, or delete anything from the primary `main` checkout.
- Validate with `dotnet build ThreeDManager.slnx`, `dotnet test ThreeDManager.slnx`, and a real focused smoke for UI/behavior changes. Run EF update when the schema changes.
- Commit the verified batch plus one handoff file under `docs/context/agent_handoffs/`.
- Do not append `docs/context/tasks_diary.md`; the finalizer writes the integrated checkpoint.

### Finalizer

- Work only in `C:\Projetos\ThreeDManager` on clean `main`.
- Integrate at most one `ready_for_finalizer` candidate per run.
- Inspect the handoff, base, diff, and validation evidence before integrating.
- Re-run the full validation gate and required smoke, append the diary, then fast-forward push `main`.
- Remove only a merged-and-pushed candidate worktree/branch; run `git worktree prune` afterward.
- Never implement a new feature, deploy automatically, force-push, use `git clean`/`reset --hard`, or delete unknown Git objects.

## Handoff template

```markdown
# <branch>

- Status: ready_for_finalizer
- Candidate commit: HEAD (the commit containing this handoff)
- Base inspected: <origin/main SHA>
- Scope: <one sentence>
- Files changed: <explicit paths>
- Validation: <commands and observed result>
- Deployment impact: none | publish/update required
- Next action for finalizer: <one exact command or inspection>
```

## Invariants

- Preserve user changes, data, migrations, secrets, and deployment boundaries.
- Do not create a new feature when a documented bug or unfinished higher-priority task exists.
- The primary `main` checkout is release source only. A Git integration is not a Windows-service deployment.
- If state is ambiguous, blocked, or overlaps another worker, stop safely and leave a useful handoff/checkpoint.
