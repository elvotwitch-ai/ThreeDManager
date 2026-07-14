# Development and Integration Workflow

## Goal

Keep the live alpha host stable while Codex and Claude continue development. `main` is the only permanent branch. Short-lived worktrees make isolated implementation possible without turning the running host or its release checkout into a shared scratchpad.

## Roles

| Role | Checkout | May do | Must not do |
| --- | --- | --- | --- |
| Implementation worker (Codex or Claude) | New temporary worktree on `codex/*` or `claude/*` | One small batch, tests, a handoff file, one candidate commit | Edit/push `main`, deploy the Windows service, remove another worker's worktree |
| Finalizer | Primary `C:\Projetos\ThreeDManager` checkout on `main` | Integrate one candidate, validate, append diary, push, remove the merged worktree | Start a new product feature, deploy automatically, force-push, discard unknown work |
| Release operator | Primary checkout, elevated PowerShell only when needed | Publish and update the service after a selected integrated commit | Put credentials or service artifacts in Git |

## Implementation-worker protocol

1. Start from current `origin/main` in a fresh worktree. Name the branch `codex/<small-task>` or `claude/<small-task>`.
2. Read the canonical docs and choose exactly one pending task. If another active worktree or uncommitted `main` work owns that area, stop with a checkpoint.
3. Implement and validate the batch. Never use the primary checkout for edits.
4. Add a handoff file under `docs/context/agent_handoffs/` named `<branch-slug>.md`, containing `HEAD (the commit containing this handoff)`, files changed, validation evidence, deployment impact, and exact next step. Do not append `tasks_diary.md` from a feature worktree: the finalizer records the authoritative integrated checkpoint.
5. Commit only the batch and its handoff. Do not push or deploy. Mark it ready for finalization in the handoff file.

## Finalizer protocol

Run only when `main` is clean and no other finalizer is active.

1. Fetch/prune `origin`, inspect `git status -sb`, `git worktree list --porcelain`, candidate handoffs, and the actual candidate diff.
2. Refuse ambiguous, dirty, unvalidated, stale, or overlapping candidates. Integrate at most one candidate per run.
3. Rebase or cherry-pick the candidate onto current `main` without force operations. Resolve only the candidate's documented conflicts; otherwise stop and preserve it.
4. Run `dotnet build ThreeDManager.slnx`, `dotnet test ThreeDManager.slnx`, and the required focused runtime smoke for the integrated flow. Append the authoritative result to `docs/context/tasks_diary.md`, then commit the finalizer documentation only if it is not already part of the candidate commit.
5. Push `main` with a fast-forward only after the full gate passes. Confirm `origin/main...main` is `0 0` and `git status -sb` is clean.
6. Remove only the worktree/branch that is merged and pushed. Run `git worktree prune`. Do not run `git clean`, `reset --hard`, or `git gc --prune=now` as part of routine finalization.

## Git hygiene and recovery

- There is one long-lived branch: `main`. Temporary agent branches disappear only after a successful integration.
- `git worktree prune` removes stale metadata only. It is safe after confirming the registered worktree list.
- Unreachable objects and orphan pack indexes are diagnostic evidence, not proof that files may be deleted. Retain them until no pending recovery is needed; schedule a separate, explicit maintenance window before aggressive Git garbage collection.
- A pushed integration still is **not** a deployment. To release it, use the explicit publish/update sequence in `ALPHA_HOSTING.md` or `REMOTE_ACCESS.md`.

## Shared handoff template

```markdown
# <branch>

- Status: ready_for_finalizer
- Candidate commit: HEAD (the commit containing this handoff)
- Base inspected: <origin/main sha>
- Scope: <one sentence>
- Files changed: <explicit paths>
- Validation: <commands and observed result>
- Deployment impact: none | publish/update required
- Next action for finalizer: <one exact command or inspection>
```
