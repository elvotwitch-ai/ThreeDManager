# claude/admiring-sammet-692630

- Status: ready_for_finalizer
- Candidate commit: HEAD (the commit containing this handoff)
- Base inspected: 91b7a0c02ca6fccd45f734e180daa6571a40458d (`origin/main` = `main`, `0 0` at branch creation)
- Scope: Fix a Phase 1 parsing defect — `GCodePrintFileParser` never read the filament-length **unit**, so a PrusaSlicer/OrcaSlicer `; filament used [mm] = 1234.5` was recorded as **1234.5 metres** instead of 1.2345 m (1000x), and a `[cm3]` **volume** was recorded as a length. Parser-only; no schema, migration, or persisted-data change.
- Files changed:
  - `src/ThreeDManager.Infrastructure/Parsers/GCodePrintFileParser.cs` (filament-length extraction now captures and converts the unit; adds `ExtractFilamentUsedMeters` + `ConvertLengthToMeters`)
  - `tests/ThreeDManager.Tests/GCodePrintFileParserTests.cs` (new; 21 unit cases — the parser previously had **zero** test coverage)
  - `docs/context/agent_handoffs/claude-admiring-sammet-692630.md` (this handoff)
- Validation:
  - Environment precheck: `docker ps --filter name=threedmanager-db` → `Up 2 days`.
  - `dotnet build ThreeDManager.slnx` → `Compilação com êxito`, **0 warnings, 0 errors**.
  - `dotnet test ThreeDManager.slnx --no-build` → **206 passed**, 0 failed, 0 skipped (185 → 206).
  - **Red/green proof** — the 21 new tests were run against the *unmodified* parser first: **10 failed** (`Expected: 1,2345 / Actual: 1234,5`), 11 passed. The 11 that passed pre-fix (grams, `[m]`, `(m)`, `filament length [m]`, both time formats) confirm the suite is a real regression guard, not a tautology. All 21 pass after the fix.
  - Runtime smoke against the real app + real Postgres + real parser: see below.
  - `git diff --check` → clean.
- Deployment impact: none. No schema change, no migration, no `dotnet ef database update` applies; the alpha DB was not modified.
- Next action for finalizer: `git diff 91b7a0c..HEAD` to review the parser change, then integrate. **Shares zero files with every other queued candidate** (see "Collision check"), so it can be integrated in any order.

## The defect

`Parse` extracted the filament length with:

```
@"(?im)^\s*;\s*filament.*(?:used|length).*\[?m\]?.*[=:]\s*(?<value>...)"
```

`\[?m\]?` makes the brackets optional but the letter `m` **required anywhere** after `used`. It therefore
never checked the unit — it only checked that an `m` existed somewhere on the line. Verified against real
slicer footers, not from memory:

| G-code line (real slicer output) | Was parsed as | Correct |
| --- | --- | --- |
| `; filament used [mm] = 1234.5` (PrusaSlicer/Orca) | **1234.5 m** | 1.2345 m |
| `; filament used [cm3] = 2.97` (a volume) | **2.97 m** | not a length at all |
| `;Filament used: 1.2345m` (Cura) | **nothing** → false "consumo não encontrado" warning | 1.2345 m |

The `[mm]` case is the common one, not an edge case: PrusaSlicer and OrcaSlicer write the length in
millimetres, and their footer lists `[mm]` **before** `[cm3]` and `[g]`, so the first regex match always
won with the millimetre number.

This is `docs/core/ROADMAP.md` Phase 1 "print imports" / `BACKLOG.md` Phase 1 "print import upload and
parsing" — the earliest incomplete phase. `docs/errors/errors.md` records no active bug, so no filed bug
outranked it.

## Why the fix reads the unit instead of widening the regex

The unit is now captured (`[\[(](?<unit>mm|cm|m)[\])]`) and converted, so `[cm3]`/`[mm3]` cannot match a
length at all (`cm` is followed by `3`, not a closing bracket), and Cura's suffix notation is handled
separately. Guessing metres when no unit is stated is exactly what caused the defect, so an unlabelled
`; filament used = 1234.5` now yields `null` rather than a fabricated number
(`FilamentUsedMeters_IsNull_WhenNoUnitIsStated`).

## Blast radius (why this mattered beyond display)

`FilamentUsedMeters` is persisted into `PrintImport.ParsedDataJson` on `Process`, then prefills the
"Filamento usado (m)" field on the `CreatePrintJob` form, which the operator accepts into
`PrintJob.FilamentUsedMeters`. A wrong value was therefore written into real production records.

**`FilamentUsedGrams` was and remains correct** — it is the field that drives `CalculatedMaterialCost` and
`PrintJobStockService` stock deduction, so **no cost or inventory figure was wrong**, and no data repair is
needed. The damage was confined to the recorded/displayed length. Grams behaviour is now pinned by five
tests so this fix cannot have disturbed it.

## Runtime smoke (real app, throwaway database)

The alpha `threedmanager` DB holds real operator data, so the smoke ran against a throwaway
`threedmanager_smoke_parser` DB in the same Postgres container, created and dropped inside this batch.
`dotnet ef database update` applied the **full migration chain from empty** (proving the chain still
applies from scratch), then `dotnet run --project src/ThreeDManager.Web --no-build --no-launch-profile` on
`127.0.0.1:5098` (`--no-launch-profile` matters: `launchSettings.json` otherwise pins 5042 and ignores
`ASPNETCORE_URLS`; Development avoids the non-Development `UseHttpsRedirection`). Smoke-only credentials
were used — no operator credential was read or written.

Drove the true operator path — real cookie login → real `.gcode` upload (PrusaSlicer footer with
`[mm]` + `[cm3]` + `[g]`) → real `Process`:

- Postgres `print_imports.ParsedDataJson` after processing: `"FilamentUsedMeters": 1.2345`,
  `"FilamentUsedGrams": 3.68`, `"Warnings": []` — i.e. exactly the row that previously stored `1234.5`.
- `CreatePrintJob` form prefill (what the operator actually sees): **`Filamento usado (m) = 1.2345`**,
  `Filamento usado (g) = 3.68`.

Teardown: the app was stopped **by its recorded PID only** — never by process name, which would match the
live alpha `ThreeDManager` Windows service — and the service was confirmed still `Running`/`Automatic`
afterwards. `threedmanager_smoke_parser` was dropped. Post-check confirmed the alpha `threedmanager` DB
still holds its 1 print import and 0 smoke rows, and was never connected to.

## Collision check (selection trace)

`main` is at `91b7a0c` and **nine** candidates are queued unintegrated. This batch was chosen specifically
because it shares **zero files** with all of them, verified with `git diff --name-only main...<branch>` for
each:

- `charming-torvalds` (`FailureCategory`) and `infallible-bartik` (`ReprintOfPrintJobId`) — decision 0001
  follow-ups 2 and 3, both **already delivered** and both carrying a migration + `AppDbContextModelSnapshot.cs`.
  Nothing left to take there, and a third parallel migration would fork the EF chain.
- Decision 0001 follow-up 1 (failed-job stock deduction) — blocked on operator A/B sign-off.
- Decision 0002 (`Printer.PowerConsumptionWatts`) — needs a migration, and decision 0002 itself is still an
  unmerged candidate (`zealous-murdock`).
- `PrintImportsController.cs` is owned by `optimistic-hawking` **and** `sad-kalam`; `PrintJobs` views/controller
  by `charming-torvalds` + `infallible-bartik`; `Printers` by `exciting-antonelli`; `Dashboard` by
  `interesting-swanson`; `Materials`/`Products` controllers by `pensive-lumiere`; `BACKLOG.md`/`ROADMAP.md` by
  `charming-torvalds` + `zealous-murdock`.

That left `GCodePrintFileParser.cs` — a documented Phase 1 surface that **no** active worktree touches, that
had no test coverage at all, and that carried a real defect. A checkpoint was deliberately **not** filed:
`laughing-goldstine` already filed one for this exact queue state, and a second would add queue depth
without lowering integration debt.

## Decisions taken autonomously (unattended run — flagging for review, not asking)

1. **Cura per-extruder lengths are summed**, not first-wins (`;Filament used: 1.2345m, 0.5m` → 1.7345 m):
   the print consumed both. Pinned by `FilamentUsedMeters_SumsCuraPerExtruderLengths`.
2. **`FilamentUsedGrams` was left alone.** Its pattern is loose in the same way, but it is correct for every
   realistic footer tested, and `BACKLOG.md` says to avoid refactors unrelated to the current fix. It is now
   pinned by tests, so tightening it later is safe.
3. **`ROADMAP.md` / `BACKLOG.md` deliberately not edited.** This fixes a defect inside the existing Phase 1
   parsing line; it does not complete that line or move a phase, and both files are already edited by two
   queued candidates.
4. **Not filed in `docs/errors/errors.md`.** That file tracks *active* errors; this one is fixed in the same
   commit that reports it. The finalizer's diary entry is the durable record.

## Findings the finalizer should not silently lose

1. **Historical `PrintJob.FilamentUsedMeters` rows are wrong by 1000x** for every job generated from a
   PrusaSlicer/Orca import (and equal to a volume for a `[cm3]`-only footer). This fix corrects new imports
   only; it does **not** backfill. No repair migration was written on purpose — the column is display-only
   and drives no cost or stock figure (grams does), and rewriting real operator rows needs sign-off, exactly
   like the decision 0001 Option A/B question. Worth an operator decision, not an opportunistic UPDATE.
2. **`TryParseDecimal` mangles grouped numbers.** It replaces `,` with `.` then parses with
   `NumberStyles.Number`/InvariantCulture, so a pt-BR `1.234,56` is captured by the regex as `1.234` and read
   as **1.234**, not 1234.56. Not triggered by the slicer footers tested (they emit invariant format), so it
   is latent, and it is out of scope for this defect — but the `R$` handling in `TryParseDecimal` and the
   `ReportedCost` pattern show pt-BR input was anticipated somewhere.
3. **`SlicerName` swallows trailing noise**: `; generated by PrusaSlicer 2.7.1 on 2026-07-15 at 10:00:00`
   yields the whole string including the timestamp, so the same slicer produces a different `SlicerName` per
   file and cannot be grouped. Cosmetic/reporting only; its own trivial batch.
