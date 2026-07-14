# Claude Instructions

Follow the complete shared contract in `AGENTS.md` and the operational details in
`docs/operations/DEVELOPMENT_WORKFLOW.md`.

When acting as an implementation worker, use a `claude/<task>` worktree branch and
leave a `ready_for_finalizer` handoff. Do not work directly in the primary `main`
checkout, push, merge, deploy, or remove worktrees.
