# Implementation Policy

- Prefer explicit, fail-visible behavior.
- Avoid silent fallbacks and hidden state.
- Keep schema changes data-safe and reversible where possible.
- Validate focused changes with the smallest meaningful build or test command.
- Do not mix unrelated refactors into feature or bugfix work.
- For each feature batch, require this sequence before commit: build, run, verify the changed flow in the app, then stage and commit.
- For behavior changes, validation must prove the expected persisted or observable state change; route availability alone is not enough.
