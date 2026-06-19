# Task Diary

## 2026-06-18

- Summary: Rehydrated the repository and confirmed the dashboard initial requested in the attached note is already implemented in the web app.
- Files changed: `docs/AGENT_CONTEXT.md`, `docs/core/ARCHITECTURE.md`, `docs/core/IMPLEMENTATION_POLICY.md`, `docs/context/tasks_diary.md`
- Validation run: `dotnet build ThreeDManager.slnx` passed
- Blockers: none
- Summary: Expanded `docs/core/ARCHITECTURE.md` with the full business blueprint from the provided PDF/text, framing ThreeDManager as a multi-project solution for product, production, inventory, commercial, finance, and intelligence.
- Files changed: `docs/core/ARCHITECTURE.md`, `docs/context/tasks_diary.md`
- Validation run: docs-only update; `git diff --check -- docs` remains clean
- Blockers: none
- Next recommended task: decide which next domain to implement first, likely `Product`/`Inventory` or `Finance`, and capture the chosen scope in the diary before coding.
- Summary: Added `docs/INDEX.md`, `docs/core/ROADMAP.md`, `docs/context/SCOPE.md`, and `docs/context/BACKLOG.md` to turn the architecture into an actionable roadmap and phase backlog.
- Files changed: `docs/AGENT_CONTEXT.md`, `docs/INDEX.md`, `docs/core/ROADMAP.md`, `docs/context/SCOPE.md`, `docs/context/BACKLOG.md`, `docs/context/tasks_diary.md`
- Validation run: `git diff --check -- docs` passed
- Blockers: none
- Next recommended task: review the first implementation phase (`Production Nucleus`) and move the next code change into a small verified batch.
- Summary: Enxuguei `docs/core/ARCHITECTURE.md` e revisei `docs/core/ROADMAP.md`/`docs/context/SCOPE.md` para remover linguagem herdada e deixar a documentação compatível com o harness atual.
- Files changed: `docs/core/ARCHITECTURE.md`, `docs/core/ROADMAP.md`, `docs/context/SCOPE.md`, `docs/context/BACKLOG.md`, `docs/context/tasks_diary.md`
- Validation run: `git diff --check -- docs` passed; no inherited phase terminology remains in the core docs set
- Blockers: none
- Next recommended task: move to the next code batch in Phase 1, starting with production failure tracking or import review flow hardening.
- Summary: Extended the dashboard with import failure tracking and recent failed import visibility so the production core surfaces parse/import issues more clearly.
- Files changed: `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/ViewModels/DashboardViewModel.cs`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `docs/context/tasks_diary.md`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `git diff --check` on touched dashboard files passed
- Blockers: none
- Next recommended task: continue Phase 1 with either import review flow hardening or explicit failure detail improvements on the import details page.
- Summary: Added calculated material cost to print jobs and wired it through import generation, edit flow, details, list, and dashboard; generated the EF migration `AddCalculatedMaterialCostToPrintJobs`.
- Files changed: `src/ThreeDManager.Domain/Entities/PrintJob.cs`, `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `src/ThreeDManager.Web/ViewModels/DashboardViewModel.cs`, `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/Views/PrintJobs/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintJobs/Index.cshtml`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `src/ThreeDManager.Infrastructure/Data/Migrations/20260618221410_AddCalculatedMaterialCostToPrintJobs.cs`, `src/ThreeDManager.Infrastructure/Data/Migrations/20260618221410_AddCalculatedMaterialCostToPrintJobs.Designer.cs`, `src/ThreeDManager.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet ef migrations add AddCalculatedMaterialCostToPrintJobs` passed
- Blockers: `dotnet ef database update` could not run because `localhost:5436` is not accepting connections and Docker Desktop is not available in this session
- Next recommended task: when the database is available, apply the migration and then continue with automatic stock deduction or import review hardening.
- Summary: Added an explicit agent rule to always `build`, `run`, verify the changed app flow, and only then `git status`/`git add`/`git commit`, so connection/runtime errors are caught before committing.
- Files changed: `docs/AGENT_CONTEXT.md`, `docs/core/IMPLEMENTATION_POLICY.md`, `docs/context/tasks_diary.md`
- Validation run: docs-only update; `git diff --check -- docs` should be re-run before commit if you want a fresh hygiene pass
- Blockers: none
- Next recommended task: apply the same validation sequence to the next code batch and keep the database running before end-to-end checks.
- Summary: Added the pre-commit checklist to `docs/INDEX.md` so the validation sequence is visible from the documentation entrypoint.
- Files changed: `docs/INDEX.md`, `docs/context/tasks_diary.md`
- Validation run: pending docs hygiene check
- Blockers: none
- Next recommended task: keep this checklist in sync with any future harness changes.
- Summary: Added automatic material stock deduction when print jobs are marked as completed, including deduction tracking fields to avoid duplicate stock movements and restore stock when a deducted print job is edited or removed.
- Files changed: `src/ThreeDManager.Domain/Entities/PrintJob.cs`, `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `src/ThreeDManager.Web/Views/PrintJobs/Details.cshtml`, `src/ThreeDManager.Infrastructure/Data/Migrations/20260619011241_AddStockDeductionTrackingToPrintJobs.cs`, `src/ThreeDManager.Infrastructure/Data/Migrations/20260619011241_AddStockDeductionTrackingToPrintJobs.Designer.cs`, `src/ThreeDManager.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet ef database update` passed; `dotnet build ThreeDManager.slnx` passed; `dotnet run --project src/ThreeDManager.Web --launch-profile http` started; `Dashboard`, `PrintJobs`, and `Materials` returned HTTP 200; web process was stopped after validation
- Blockers: none
- Next recommended task: exercise the full manual UI flow with a material that has stock, then complete a print job and confirm the material stock decreases by the filament grams.

## 2026-06-19

- Summary: Tightened decimal form binding so MVC edit posts accept HTML number values with decimal points, then validated the completed-print stock deduction through the real app flow.
- Files changed: `src/ThreeDManager.Web/Program.cs`, `src/ThreeDManager.Web/ModelBinding/FlexibleDecimalModelBinder.cs`, `src/ThreeDManager.Web/ModelBinding/FlexibleDecimalModelBinderProvider.cs`, `docs/AGENT_CONTEXT.md`, `docs/INDEX.md`, `docs/core/IMPLEMENTATION_POLICY.md`, `docs/context/tasks_diary.md`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet run --project src/ThreeDManager.Web/ThreeDManager.Web.csproj --launch-profile http` started; e2e MVC POST to `PrintJobs/Edit` changed stock from `100.00` to `87.55`, set status to `Completed`, stored deducted grams `12.45`, calculated material cost `1.00`, kept stock at `87.55` on a second save, and `PrintJobs/Details` showed `Baixa de estoque`.
- Blockers: none
- Next recommended task: add a focused automated test project around print job completion/stock deduction once the test harness exists, so this e2e scenario is repeatable without manual PowerShell.
- Summary: Deduplicated the initial entrypoint so the app home and dashboard are the same screen: the default route now opens `Dashboard/Index`, the layout has a single dashboard navigation entry, and the unused `Home/Index` view was removed while keeping `/Home/Index` as a legacy redirect.
- Files changed: `src/ThreeDManager.Web/Program.cs`, `src/ThreeDManager.Web/Views/Shared/_Layout.cshtml`, `src/ThreeDManager.Web/Views/Home/Privacy.cshtml`, `src/ThreeDManager.Web/Views/Home/Index.cshtml`, `docs/context/tasks_diary.md`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet run --project src/ThreeDManager.Web/ThreeDManager.Web.csproj --launch-profile http` started; `/`, `/Dashboard`, `/Home/Index`, and `/Home/Privacy` returned HTTP 200; `/`, `/Dashboard`, and `/Home/Index` rendered the dashboard; the layout rendered one `Dashboard` nav link and no `Home` nav link.
- Blockers: none
- Next recommended task: continue with the next small dashboard/production usability improvement and validate the changed flow through the app before committing.
- Summary: Extracted print job stock deduction/restoration from MVC controllers into `IPrintJobStockService`, preserving the existing tracked material/grams/timestamp fields and keeping controller responsibility limited to MVC orchestration and model-state errors.
- Files changed: `src/ThreeDManager.Application/Interfaces/IPrintJobStockService.cs`, `src/ThreeDManager.Infrastructure/Services/PrintJobStockService.cs`, `src/ThreeDManager.Web/Program.cs`, `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet run --project src/ThreeDManager.Web/ThreeDManager.Web.csproj --launch-profile http` started; e2e MVC edits verified stock `1000.00 -> 987.55` when status became `Completed` with `12.45 g`, restored to `1000.00` when status changed to `Failed`, returned to `987.55` when completed again, and ended at `980.00` when completed grams changed to `20.00`; `PrintJobs/Details` still showed `Baixa de estoque`.
- Blockers: none
- Next recommended task: add automated coverage for `PrintJobStockService` using a focused test project and an EF test database/provider.
