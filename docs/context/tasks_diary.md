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
- Summary: Added a focused `xUnit` project for `PrintJobStockService` and codified the stock-deduction contract with four tests covering completed deductions, restore-on-status-change, replacement on edit, and insufficient stock failure.
- Files changed: `tests/ThreeDManager.Tests/ThreeDManager.Tests.csproj`, `tests/ThreeDManager.Tests/PrintJobStockServiceTests.cs`, `ThreeDManager.slnx`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test ThreeDManager.slnx` passed with 4 tests; `dotnet build ThreeDManager.slnx` passed; `git diff --check` passed
- Blockers: none
- Next recommended task: use the new test project as the baseline for any future print-job or inventory rule changes.
- Summary: Added controller integration tests for the import-to-print-job and edit-to-completed flows, and taught the Web host to switch to `EF InMemory` only when running under the `Testing` environment so the test host can seed and verify state without PostgreSQL.
- Files changed: `src/ThreeDManager.Web/Program.cs`, `src/ThreeDManager.Web/ThreeDManager.Web.csproj`, `tests/ThreeDManager.Tests/ThreeDManager.Tests.csproj`, `tests/ThreeDManager.Tests/ThreeDManagerWebFactory.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test ThreeDManager.slnx` passed with 6 tests; `dotnet build ThreeDManager.slnx` passed; `git diff --check` passed
- Blockers: none
- Next recommended task: add one controller integration test for the failure path when stock is insufficient, so the MVC validation message is covered too.
- Summary: Added the controller integration test for insufficient stock on `PrintImports/CreatePrintJob`, proving the MVC pipeline returns the validation message, leaves the original stock unchanged, and does not create a `PrintJob` for the import.
- Files changed: `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test ThreeDManager.slnx` passed with 7 tests; `dotnet build ThreeDManager.slnx` passed; `git diff --check` passed
- Blockers: none
- Next recommended task: use the integration suite as the guardrail for any future changes to import, edit, or stock-validation behavior.
- Summary: Added a material stock movement ledger and rendered the latest movements in material details, so stock changes now preserve why the value changed across manual adjustments and print-job completion/restoration flows.
- Files changed: `src/ThreeDManager.Domain/Entities/MaterialStockMovement.cs`, `src/ThreeDManager.Infrastructure/Data/AppDbContext.cs`, `src/ThreeDManager.Infrastructure/Services/PrintJobStockService.cs`, `src/ThreeDManager.Web/Controllers/MaterialsController.cs`, `src/ThreeDManager.Web/Views/Materials/Details.cshtml`, `src/ThreeDManager.Infrastructure/Data/Migrations/20260619174811_AddMaterialStockMovements.cs`, `src/ThreeDManager.Infrastructure/Data/Migrations/20260619174811_AddMaterialStockMovements.Designer.cs`, `src/ThreeDManager.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs`, `tests/ThreeDManager.Tests/PrintJobStockServiceTests.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx` passed with 8 tests; `dotnet run --project src/ThreeDManager.Web --no-launch-profile` under `ASPNETCORE_ENVIRONMENT=Testing` passed a real-app smoke flow creating a material, changing stock from `1000.00` to `900.00`, and confirming `Materials/Details` showed the movement history
- Blockers: none
- Next recommended task: if you want the audit trail to be visible in more places, add the same movement summary to the material list or dashboard later; otherwise commit this batch and move to the next feature.
- Summary: Added a compact recent stock-movement section to the dashboard and made automatic stock movement notes more explicit with production names and gram amounts.
- Files changed: `src/ThreeDManager.Infrastructure/Services/PrintJobStockService.cs`, `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `src/ThreeDManager.Web/ViewModels/DashboardViewModel.cs`, `tests/ThreeDManager.Tests/PrintJobStockServiceTests.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 9 tests; real-app smoke under `ASPNETCORE_ENVIRONMENT=Testing` verified `/Dashboard` showed `Movimentações de estoque recentes` and `Ajuste manual`, and `Materials/Details` still showed the manual movement and `900,00 g`
- Blockers: none
- Next recommended task: if you want more visibility, surface the same stock-movement summary on `Materials/Index` next; otherwise commit this dashboard batch and continue to the next feature.
- Summary: Added a manual stock adjustment screen at `/Materials/AdjustStock/{id}` with add/remove/set operations, plus a compact latest-movement summary and action button on the materials list.
- Files changed: `src/ThreeDManager.Web/Controllers/MaterialsController.cs`, `src/ThreeDManager.Web/ViewModels/MaterialStockAdjustmentViewModel.cs`, `src/ThreeDManager.Web/Views/Materials/AdjustStock.cshtml`, `src/ThreeDManager.Web/Views/Materials/Details.cshtml`, `src/ThreeDManager.Web/Views/Materials/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 9 tests; real-app smoke under `ASPNETCORE_ENVIRONMENT=Testing` verified `Materials/AdjustStock` added stock, `Materials/Details` showed `1.500,00 g` and the note, and `Materials/Index` showed the compact recent movement summary
- Blockers: none
- Next recommended task: if you want to keep going, add the same compact stock-movement summary to another surface or move on to a different inventory workflow.
- Summary: Hardened the manual stock adjustment flow with an explicit negative-stock guard, and verified the rejection path does not persist a movement or change the balance.
- Files changed: `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `src/ThreeDManager.Web/Views/Materials/Index.cshtml`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 11 tests; real-app smoke under `ASPNETCORE_ENVIRONMENT=Testing` verified `/Materials/AdjustStock` rejects removals above available stock, keeps the material at `25,00 g`, and leaves no saved movement
- Blockers: none
- Next recommended task: if you want another inventory follow-up, add the same movement summary to a second page or continue with a separate domain flow.
- Summary: Added a compact stock-movement block to `PrintJobs/Details` so the production detail page now shows the latest movement linked to that print job.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `src/ThreeDManager.Web/Views/PrintJobs/Details.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`
- Validation run: `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 11 tests; a full real-app smoke was attempted but the ad-hoc multipart upload script for the `.gcode` import did not complete cleanly in this shell session, so the browser/UI-level verification for this page remains backed by automated tests rather than a fresh manual run
- Blockers: smoke automation for the file-upload path was noisy in this shell session
- Next recommended task: if you want a fully manual UI smoke for this page, redo the import/upload flow in the browser; otherwise move to the next domain flow.
- Summary: Added minimum-stock alerts for materials, surfaced low-stock visibility on the dashboard and materials pages, and kept the threshold editable with the material record.
- Files changed: `src/ThreeDManager.Domain/Entities/Material.cs`, `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/Controllers/MaterialsController.cs`, `src/ThreeDManager.Web/ViewModels/DashboardViewModel.cs`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `src/ThreeDManager.Web/Views/Materials/Create.cshtml`, `src/ThreeDManager.Web/Views/Materials/Edit.cshtml`, `src/ThreeDManager.Web/Views/Materials/Details.cshtml`, `src/ThreeDManager.Web/Views/Materials/Index.cshtml`, `src/ThreeDManager.Infrastructure/Data/Migrations/20260619185833_AddMinimumStockToMaterials.cs`, `src/ThreeDManager.Infrastructure/Data/Migrations/20260619185833_AddMinimumStockToMaterials.Designer.cs`, `src/ThreeDManager.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`
- Validation run: `dotnet test tests/ThreeDManager.Tests/ThreeDManager.Tests.csproj --filter LowStockAlert_IsShown_OnMaterialsAndDashboard_WhenBelowMinimum` passed; nullability warnings in the new Razor checks were removed and the test passed again cleanly
- Blockers: none
- Next recommended task: if you want to extend inventory visibility further, add the same compact stock summary to one more surface or move on to a separate production/inventory flow.
- Summary: Applied the full migration chain to a fresh local PostgreSQL database, fixed the legacy `RenameMaterialCreatedAt` migration so it no-ops when `CreatedAT` is already absent, and smoke-tested `/Dashboard` and `/Materials` against the real local DB.
- Files changed: `src/ThreeDManager.Infrastructure/Data/Migrations/20260529101446_RenameMaterialCreatedAt.cs`, `docs/context/tasks_diary.md`
- Validation run: `docker compose up -d db`; `dotnet ef database update --project src/ThreeDManager.Infrastructure --startup-project src/ThreeDManager.Web` passed on a clean local Postgres; browser smoke of `http://localhost:5042/Dashboard` and `http://localhost:5042/Materials` passed with the app connected to the local database
- Blockers: none
- Next recommended task: if you want to keep moving on inventory, choose the next page to surface the compact stock summary on or the next production flow to harden.
- Summary: Rehydrated the dirty migration-fix batch, revalidated the conditional `RenameMaterialCreatedAt` migration against local Postgres, and checked the app routes tied to the fresh-DB smoke.
- Files changed: `src/ThreeDManager.Infrastructure/Data/Migrations/20260529101446_RenameMaterialCreatedAt.cs`, `docs/context/tasks_diary.md`
- Validation run: `git diff --check` passed with line-ending warnings only; `dotnet build ThreeDManager.slnx` passed; `docker compose up -d db` confirmed `threedmanager-db` running; `dotnet ef database update --project src\ThreeDManager.Infrastructure --startup-project src\ThreeDManager.Web` passed; `dotnet run --project src\ThreeDManager.Web\ThreeDManager.Web.csproj --launch-profile http` started on `http://localhost:5042`; `Invoke-WebRequest` confirmed `/Dashboard` and `/Materials` returned HTTP 200 and rendered their expected page titles. In-app browser automation was unavailable in this automation sandbox, so the route smoke was HTTP-backed rather than visual.
- Blockers: none for the migration fix; visual browser smoke could be repeated manually if desired.
- Next recommended task: move to a separate production/inventory flow only after this migration-fix batch is committed.
- Summary: Normalized production status handling by adding a domain-owned `PrintJobStatus` vocabulary and rejecting unknown status values in import-to-production and edit-production POST paths before stock or job state changes.
- Files changed: `src/ThreeDManager.Domain/Entities/PrintJobStatus.cs`, `src/ThreeDManager.Domain/Entities/PrintJob.cs`, `src/ThreeDManager.Infrastructure/Services/PrintJobStockService.cs`, `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter "CreatePrintJob_FromImport_RejectsUnknownStatus|EditPrintJob_RejectsUnknownStatus_WithoutChangingStockOrJob"` passed with 2 tests; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 14 tests; app smoke under `ASPNETCORE_ENVIRONMENT=Testing` on `http://127.0.0.1:5056` confirmed `/PrintImports` and `/PrintJobs` returned HTTP 200 with expected page content.
- Blockers: none.
- Next recommended task: extend the same status vocabulary to print import statuses (`Uploaded`, `Parsed`, `Error`) so dashboard/import filtering no longer depends on raw string literals.

## 2026-06-20

- Summary: Normalized print import status handling by adding a domain-owned `PrintImportStatus` vocabulary and using case-insensitive normalization on import list/details and dashboard failure visibility.
- Files changed: `src/ThreeDManager.Domain/Entities/PrintImportStatus.cs`, `src/ThreeDManager.Domain/Entities/PrintImport.cs`, `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportStatus_NormalizesStoredStatusCasing_OnImportAndDashboardViews` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 15 tests; app smoke under `ASPNETCORE_ENVIRONMENT=Testing` on `http://127.0.0.1:5062` confirmed `/Dashboard` and `/PrintImports` returned HTTP 200 with expected page titles; `git diff --check` reported only line-ending warnings.
- Blockers: none.
- Next recommended task: continue Phase 1 by hardening import review flow actions around parsed/error imports, starting with the smallest controller/view path and an integration test.
- Summary: Hardened import review generation so only successfully parsed imports can enter the import-to-production flow, including direct GET/POST route requests with stale parsed JSON on an error import.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter CreatePrintJob_FromImport_RejectsErrorImport_EvenWithParsedData` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 16 tests; app smoke under `ASPNETCORE_ENVIRONMENT=Testing` on `http://127.0.0.1:5071` confirmed `/PrintImports` and `/Dashboard` returned HTTP 200 with expected page content.
- Blockers: none.
- Next recommended task: continue Phase 1 by adding an explicit review affordance for parsed imports that already have a linked production, so the details page points to the existing job instead of only hiding generation.

## 2026-06-21

- Summary: Added the linked-production affordance for parsed imports that already generated a print job; import details now links to the existing production and direct generation requests redirect to that job instead of reopening the generation form.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportDetails_LinksExistingPrintJob_WhenImportAlreadyGeneratedProduction` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 17 tests; app smoke under `ASPNETCORE_ENVIRONMENT=Testing` on `http://127.0.0.1:5084` confirmed `/PrintImports` and `/Dashboard` returned HTTP 200 with expected page content.
- Blockers: none.
- Next recommended task: continue Phase 1 by adding a compact import-to-production state indicator on the print imports list, so operators can see which parsed imports are still pending generation.
- Summary: Added the compact import-to-production state indicator on the print imports list, showing parsed imports as pending generation, linked imports as already connected to production, and unavailable imports separately.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportsIndex_ShowsProductionState_ForPendingAndLinkedParsedImports` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 18 tests; app smoke under `ASPNETCORE_ENVIRONMENT=Testing` on `http://127.0.0.1:5092` confirmed `/PrintImports` and `/Dashboard` returned HTTP 200 with expected imports page content.
- Blockers: none.
- Next recommended task: continue Phase 1 by making the print imports list action column context-aware, so parsed pending imports offer `Gerar produção` directly while linked imports point to the existing production.
- Summary: Made the print imports list action column context-aware: pending parsed imports now offer `Gerar produção` directly, and linked imports point to the existing production.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportsIndex_ShowsProductionState_ForPendingAndLinkedParsedImports` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 18 tests; app smoke under `ASPNETCORE_ENVIRONMENT=Testing` on `http://127.0.0.1:5099` confirmed `/PrintImports` and `/Dashboard` returned HTTP 200 with expected page content.
- Blockers: none.
- Next recommended task: continue Phase 1 by adding an operator-visible filter or quick view for imports still pending production generation.
- Summary: Added an operator-visible pending-production filter to the print imports list, including a pending count and a filtered quick view for parsed imports that are not linked to a production job.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportsIndex_ShowsProductionState_ForPendingAndLinkedParsedImports` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 18 tests; app smoke under `ASPNETCORE_ENVIRONMENT=Testing` on `http://127.0.0.1:5106` confirmed `/PrintImports?productionState=pending` returned HTTP 200 and rendered the pending filter, and `/Dashboard` returned HTTP 200 with expected page content.
- Blockers: none.
- Next recommended task: continue Phase 1 by choosing the next import review hardening step from the live backlog.
- Summary: Started a small Phase 1 dashboard visibility batch to surface parsed imports still pending production generation, linking the dashboard card to `/PrintImports?productionState=pending`.
- Files changed: `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/ViewModels/DashboardViewModel.cs`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: blocked. `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_LinksPendingProductionImports_ToFilteredImportReview` could not build because `C:\Projetos\ThreeDManager\src\ThreeDManager.Web\bin\Debug\net10.0\ThreeDManager.Web.exe` (PID 13872) holds `ThreeDmanager.Infrastructure.dll`.
- Blockers: running web app process is locking build output; this batch is not validated and was not committed.
- Next recommended task: stop or move the running `ThreeDManager.Web.exe` process, rerun the focused dashboard integration test, then run `dotnet build ThreeDManager.slnx` and app-smoke `/Dashboard` plus `/PrintImports?productionState=pending` before committing.
- Summary: Rehydrated the blocked dashboard pending-production visibility batch and confirmed the repository is still dirty with the same unvalidated changes.
- Files changed: `docs/context/tasks_diary.md`
- Validation run: blocked. `git status -sb` showed the existing dirty dashboard/test/doc batch; process inspection showed `dotnet run --project src/ThreeDManager.Web` (PID 23208) and `ThreeDManager.Web.exe` (PID 13872) still active, so no build/test/app smoke was run.
- Blockers: active web app process still risks locking build outputs; existing implementation changes remain unvalidated and uncommitted.
- Next recommended task: stop or move the running web app process, then validate the existing dashboard batch with `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_LinksPendingProductionImports_ToFilteredImportReview`, `dotnet build ThreeDManager.slnx`, and an app smoke for `/Dashboard` plus `/PrintImports?productionState=pending`.
- Summary: Validated and completed the dashboard pending-production visibility batch: the dashboard now counts parsed imports that still need production generation and links operators to the filtered import review page.
- Files changed: `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/ViewModels/DashboardViewModel.cs`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_LinksPendingProductionImports_ToFilteredImportReview` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 19 tests; Testing-environment app smoke on `http://127.0.0.1:5117` confirmed `/Dashboard` returned HTTP 200 with the pending-production card and `/PrintImports?productionState=pending` returned HTTP 200 with the pending filter.
- Blockers: none.
- Next recommended task: continue Phase 1 by adding one small import-review hardening affordance for error imports, such as making the details page show a clear recovery path or retry action only where supported.
- Summary: Hardened the import details recovery path for failed imports so retry is only offered when the raw content can actually be processed again, while blocked imports now show an explicit reimport message instead of a dead-end action.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter "PrintImportDetails_ShowsRetryGuidance_OnlyWhenErrorImportCanBeProcessedAgain|CreatePrintJob_FromImport_RejectsErrorImport_EvenWithParsedData"` passed with 2 tests; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 20 tests; Testing-environment app smoke on `http://127.0.0.1:5128` verified upload -> import details and confirmed the page still rendered `Processar arquivo` for a fresh `.gcode` import.
- Blockers: none.
- Next recommended task: continue Phase 1 by surfacing a compact error-recovery hint on `/PrintImports` list rows, so operators can distinguish retryable failures from reimport-only failures without opening each import.
- Summary: Surfaced compact error-recovery hints directly on `/PrintImports` rows and hid the unsupported `Processar` action for blocked error imports, keeping the list-page recovery rules aligned with the details page.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportsIndex_ShowsRecoveryHint_AndHidesUnsupportedProcessAction_ForErrorImports` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 21 tests; Testing-environment app smoke on `http://127.0.0.1:5136` confirmed `/PrintImports` and `/PrintImports?productionState=pending` returned HTTP 200 with the expected page content.
- Blockers: none.
- Next recommended task: continue Phase 1 by adding a failed-import quick filter on `/PrintImports`, so operators can isolate retry/reimport work before opening individual records.

## 2026-06-22

- Summary: Added a failed-import quick filter on `/PrintImports`, so operators can isolate retryable and reimport-only errors without mixing them with pending-production or uploaded imports.
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportsIndex_FiltersFailedImports_ForRecoveryReview` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 22 tests; Testing-environment app smoke on `http://127.0.0.1:5144` confirmed `/PrintImports?status=error` and `/PrintImports?productionState=pending` returned HTTP 200 with the expected filter content.
- Blockers: none.
- Next recommended task: add a dashboard shortcut to `/PrintImports?status=error`, so operators can jump from failure visibility to the filtered recovery queue in one click.
- Summary: Added the dashboard recovery shortcut for failed imports: the recent failed-imports card now links directly to `/PrintImports?status=error`, with an integration test proving the filtered recovery queue is surfaced from `/Dashboard`.
- Files changed: `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_LinksFailedImports_ToRecoveryQueue` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 23 tests; Testing-environment app smoke on `http://127.0.0.1:5152` confirmed `/Dashboard` returned HTTP 200 with the failed-imports recovery shortcut and `/PrintImports?status=error` returned HTTP 200 with the filtered queue content.
- Blockers: none.
- Next recommended task: continue Phase 1 by adding one compact recovery-state summary to the dashboard failed-imports table, distinguishing retryable imports from reimport-only failures without opening each detail page.
- Summary: Added the compact recovery-state summary to the dashboard failed-imports table, showing whether each failed import can be processed again or must be reimported.
- Files changed: `src/ThreeDManager.Web/Controllers/DashboardController.cs`, `src/ThreeDManager.Web/ViewModels/DashboardViewModel.cs`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_LinksFailedImports_ToRecoveryQueue` passed with 1 test; `dotnet build ThreeDManager.slnx` passed with 0 errors and transient test-output copy warnings while the test host was active; `dotnet test ThreeDManager.slnx --no-build` passed with 23 tests; Testing-environment app smoke on `http://127.0.0.1:5160` confirmed `/Dashboard` and `/PrintImports?status=error` returned HTTP 200 with the expected failed-import recovery content.
- Blockers: none.
- Next recommended task: continue Phase 1 with one small import-review hardening task, such as adding a direct retry action for retryable failed imports in the dashboard table while keeping reimport-only rows detail-only.
- Summary: Added a direct retry action to the dashboard failed-imports table only for retryable failed imports, while reimport-only rows remain detail-only.
- Files changed: `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_LinksFailedImports_ToRecoveryQueue` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 23 tests; Testing-environment app smoke on `http://127.0.0.1:5168` confirmed `/Dashboard` and `/PrintImports?status=error` rendered the expected recovery UI.
- Blockers: none.
- Next recommended task: continue Phase 1 with one small import-review hardening task, such as making the dashboard failed-import retry POST flow show a clear success/failure result after processing.
- Summary: Made dashboard failed-import retry POSTs return to `/Dashboard` with a visible success or error alert, so operators get immediate feedback after retrying a recoverable import from the dashboard table.
- Timestamp: 2026-06-23 15:07:02 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_LinksFailedImports_ToRecoveryQueue` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 23 tests; Testing-environment HTTP smoke on `http://127.0.0.1:5176` confirmed `/Dashboard` and `/PrintImports?status=error` returned HTTP 200 with expected dashboard/recovery content.
- Blockers: none.
- Next recommended task: continue Phase 1 with one small import-review polish step, such as showing the last retry result on the filtered `/PrintImports?status=error` queue if operators need feedback there too.
- Summary: Kept retries started from the filtered failed-import queue on `/PrintImports?status=error`, showing the existing processing success/error feedback without sending operators to an individual details page.
- Timestamp: 2026-06-23 18:04:27 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportsIndex_RetainsFailedFilterAndShowsRetryResult` passed with 1 test; `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 24 tests; in-app browser smoke on `http://127.0.0.1:5184/PrintImports?status=error` confirmed the failed filter stayed active and the queue empty state rendered correctly.
- Blockers: none.
- Next recommended task: continue Phase 1 with one small import-review consistency step, such as preserving the pending-production filter when an operator processes an uploaded import from a filtered review page.
- Summary: Preserved the originating import-review queue when operators open import details, so the failure and pending-production filters now survive details navigation and supported retry processing.
- Timestamp: 2026-06-23 21:14:34 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `PrintImportDetails_PreservesFilteredQueueForBackAndRetryActions` passed; `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 25 tests; real PostgreSQL browser smoke on `http://127.0.0.1:5192/PrintImports?productionState=pending` verified the details URL carried `returnTo=pendingQueue`, details rendered `Voltar para pendentes`, and the back action returned to the active pending filter.
- Blockers: none.
- Next recommended task: continue Phase 1 with one small import-review consistency step, such as preserving queue context when navigating from filtered imports into production generation and validation errors.

- Summary: Preserved pending-queue context through the `Gerar produção` flow, so filtered import review now survives the create-production page and validation-error round trips.
- Timestamp: 2026-06-23 23:05:01 -03:00
- Files changed: `src/ThreeDManager.Web/ViewModels/PrintJobFromImportViewModel.cs`, `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/CreatePrintJob.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter CreatePrintJob_PreservesPendingQueueContext_OnGetAndValidationError` passed; `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors after one transient file-lock retry; `dotnet test ThreeDManager.slnx --no-build` passed with 25 tests; live app smoke on `http://127.0.0.1:5204/PrintImports?productionState=pending` loaded the pending queue with the empty-state message, but the current local database had no pending imports to click through, so the create-page queue-return behavior remained verified by the focused integration test in this run.
- Blockers: none.
- Next recommended task: continue Phase 1 with one small import-review consistency step, such as preserving `returnTo` when a filtered import already has a linked production and the controller redirects straight to `PrintJobs/Details`.

- Summary: Preserved filtered import-review context when an import is already linked to production, so the short-circuit into `PrintJobs/Details` now keeps the queue-specific back path and import link.
- Timestamp: 2026-06-24 01:04:07 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `src/ThreeDManager.Web/Views/PrintJobs/Details.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter CreatePrintJob_PreservesPendingQueueContext_WhenImportAlreadyLinkedToProduction` passed; `dotnet build ThreeDManager.slnx` passed on retry after one transient `VBCSCompiler` file lock in `src\ThreeDManager.Domain\obj\Debug\net10.0`; `dotnet test ThreeDManager.slnx --no-build` passed with 27 tests; `git diff --check` passed with line-ending warnings only. No fresh browser walk covered the exact linked-import branch because the local app state did not provide a ready linked import on demand, so that queue-preservation path remained verified by the focused integration test in this run.
- Blockers: none.
- Next recommended task: continue Phase 1 with one small import-review consistency step, such as preserving queue context after a successful `Gerar produção` POST lands on `PrintJobs/Details`.

- Summary: Preserved pending-queue context after a successful `Gerar produção` POST, so the production details page keeps the queue-specific back path and import link after the create action succeeds.
- Timestamp: 2026-06-24 03:04:00 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter CreatePrintJob_PreservesPendingQueueContext_AfterSuccessfulPost` passed with 1 test; `dotnet build ThreeDManager.slnx` passed on retry after one transient `CS2012` file lock in `src\ThreeDManager.Domain\obj\Debug\net10.0\ThreeDManager.Domain.dll`; `dotnet test ThreeDManager.slnx --no-build` passed with 27 tests; `git diff --check` passed with line-ending warnings only.
- Blockers: no fresh browser/manual smoke exercised the exact POST success branch in a live app session during this batch, so the redirect contract is verified by the MVC integration host rather than a new local UI mutation.
- Next recommended task: if you want a fully manual confirmation before commit, run one targeted app-level smoke of the pending queue -> `Gerar produção` -> production details flow with seedable local data; otherwise keep moving with the next small import-review consistency slice.

- Summary: Rehydrated the existing uncommitted queue-context batch, reran focused validation, and attempted a live Testing-environment smoke of `PrintImports -> Gerar produção -> PrintJobs/Details` without making further code changes.
- Timestamp: 2026-06-24 05:16:40 -03:00
- Files changed: `docs/context/tasks_diary.md`
- Validation run: `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter CreatePrintJob_PreservesPendingQueueContext_AfterSuccessfulPost` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; an ad-hoc Testing-environment MVC smoke created a product, printer, material, imported and processed a `.gcode`, opened the pending-production queue, posted `Gerar produção`, and confirmed the import left `/PrintImports?productionState=pending`, but the PowerShell client did not yield a conclusive assertion for the final import backlink query-string on `PrintJobs/Details`.
- Blockers: no commit yet because the exact live `PrintJobs/Details -> Ver importação` backlink with `returnTo=pendingQueue` is still only integration-tested, not cleanly confirmed through a browser/manual app read.
- Next recommended task: run one browser-visible smoke on the generated production details page and verify both `Voltar para pendentes` and `Ver importação` keep `returnTo=pendingQueue`; if that passes, commit the existing three-file batch.

- Summary: Confirmed the pending-queue redirect slice in a browser-visible Testing-environment smoke, then completed the existing three-file batch for commit.
- Timestamp: 2026-06-24 07:08:42 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: browser-visible smoke on `http://127.0.0.1:5212` created a product, printer, material, and `.gcode` import, processed it, generated a production from `/PrintImports?productionState=pending`, and verified `PrintJobs/Details` rendered both `Voltar para pendentes` and `Ver importação` with `returnTo=pendingQueue`; `Ver importação` opened `/PrintImports/Details/{id}?returnTo=pendingQueue`, and that page still rendered `Voltar para pendentes`.
- Blockers: none.
- Next recommended task: continue Phase 1 with one small import-review consistency slice adjacent to queue context, or move to the next production/inventory polish item if this chain is complete.

- Summary: Preserved import-review queue context through the print-job edit flow, so productions opened from a filtered queue keep their back path on the details page, edit page, cancel action, and successful save redirect.
- Timestamp: 2026-06-24 09:08:54 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `src/ThreeDManager.Web/Views/PrintJobs/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintJobs/Edit.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter EditPrintJob_PreservesPendingQueueContext_OnGetAndSuccessfulPost` passed; `dotnet build ThreeDManager.slnx` passed after one transient `VBCSCompiler` file-lock retry on `ThreeDManager.Domain.dll`; `dotnet test ThreeDManager.slnx --no-build` passed with 29 tests; live app smoke on `http://127.0.0.1:5224` confirmed `PrintJobs/Details/{id}?returnTo=pendingQueue` rendered `Editar` with `?returnTo=pendingQueue`, and the linked edit page rendered both the hidden `returnTo` field and the cancel link back to the same queue-aware details URL.
- Blockers: none.
- Next recommended task: continue the same consistency chain with one small follow-up on the print-job delete flow, so productions opened from a filtered import-review queue can also cancel or finish removal without losing the originating queue context.

- Summary: Preserved import-review queue context through the print-job delete flow, so queue-opened productions keep `returnTo` on the remove link, delete confirmation page, cancel action, and successful removal redirect back to the linked import.
- Timestamp: 2026-06-24 11:08:37 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `src/ThreeDManager.Web/Views/PrintJobs/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintJobs/Delete.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter DeletePrintJob_PreservesPendingQueueContext_OnGetCancelAndSuccessfulPost` passed; first `dotnet build ThreeDManager.slnx` attempt hit a transient `CS2012`/`VBCSCompiler` lock on `ThreeDManager.Domain.dll`, and the immediate retry passed; `dotnet test ThreeDManager.slnx --no-build` passed with 30 tests; live Testing-environment browser smoke on `http://127.0.0.1:5236` created a pending import, generated a production from `/PrintImports?productionState=pending`, confirmed `Remover` kept `?returnTo=pendingQueue`, confirmed the delete page `Cancelar` link returned to queue-aware production details, and confirmed successful removal redirected to `/PrintImports/Details/{id}?returnTo=pendingQueue` with `Voltar para pendentes` and `Gerar produção` visible.
- Blockers: none.
- Next recommended task: continue the same consistency chain with one small follow-up on any remaining print-job actions that still drop `returnTo`, or move to the next adjacent production/inventory polish item if the queue-context path is now complete.

- Summary: Added focused integration coverage for the print-job edit stock-validation error branch, proving `returnTo=pendingQueue` survives the rerender and keeps the queue-aware cancel path when completion fails for insufficient stock.
- Timestamp: 2026-06-24 12:36:00 -03:00
- Files changed: `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter EditPrintJob_PreservesPendingQueueContext_OnStockValidationError` passed; first `dotnet build ThreeDManager.slnx` attempt hit a transient `CS2012` lock on `ThreeDManager.Domain.dll`, and the immediate retry passed with transient `MSB3026` testhost copy warnings; `dotnet test ThreeDManager.slnx --no-build` passed with 31 tests; `git diff --check` passed with line-ending warnings only.
- Blockers: none.
- Next recommended task: if the queue-context hardening chain is considered complete, move to one adjacent production/inventory polish slice outside `returnTo`, such as tightening the print-job edit/details status presentation or another small operator-facing consistency issue.

- Summary: Centralized print-job status presentation in the Web layer so `PrintJobs` list/details/delete now show localized badges and the edit form reuses the same status option source instead of inline literals.
- Timestamp: 2026-06-24 14:02:00 -03:00
- Files changed: `src/ThreeDManager.Web/Presentation/PrintJobStatusPresentation.cs`, `src/ThreeDManager.Web/Controllers/PrintJobsController.cs`, `src/ThreeDManager.Web/Views/PrintJobs/Edit.cshtml`, `src/ThreeDManager.Web/Views/PrintJobs/Index.cshtml`, `src/ThreeDManager.Web/Views/PrintJobs/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintJobs/Delete.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: first focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintJobsViews_ShowLocalizedStatusLabels_InIndexDetailsDeleteAndEdit` attempt hit a transient `CS2012` lock on `ThreeDManager.Domain.dll`; immediate `dotnet build ThreeDManager.slnx` retry passed with 0 warnings and 0 errors; rerun of the focused test passed with 1 test; `dotnet test ThreeDManager.slnx --no-build` passed with 32 tests; `git diff --check` passed with line-ending warnings only; Testing-environment app smoke on `http://127.0.0.1:5288/PrintJobs` returned HTTP 200, but the in-memory app had no seeded production rows, so the localized status rendering itself remained verified by the focused integration test in this run.
- Blockers: none.
- Next recommended task: extend the same centralized status presentation helper to the dashboard recent-productions table, which still carries its own inline status/badge mapping.

- Summary: Reused the centralized print-job status presentation helper on the dashboard recent-productions table, removing the last inline production-status badge mapping from that operator surface.
- Timestamp: 2026-06-24 19:02:36 -03:00
- Files changed: `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_ShowsLocalizedStatusLabels_InRecentPrintJobsTable` passed; first `dotnet build ThreeDManager.slnx` attempt hit a transient `CS2012` lock on `ThreeDManager.Domain.dll` from `VBCSCompiler`, and the immediate retry passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 33 tests; Testing-environment app smoke on `http://127.0.0.1:5296/Dashboard` returned HTTP 200 and rendered the `Últimas produções` section after rerunning `dotnet run` with `--no-launch-profile` so the smoke used the in-memory Testing path instead of the unavailable local PostgreSQL instance.
- Blockers: none.
- Next recommended task: reuse the same centralized production-status option source on `src/ThreeDManager.Web/Views/PrintImports/CreatePrintJob.cshtml`, which still hardcodes the localized status list separately from the `PrintJobs` edit flow.

- Summary: Reused the centralized print-job status option source on `PrintImports/CreatePrintJob`, so the import-to-production form now renders the same localized production statuses as the `PrintJobs` edit flow.
- Timestamp: 2026-06-24 22:35:00 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/ViewModels/PrintJobFromImportViewModel.cs`, `src/ThreeDManager.Web/Views/PrintImports/CreatePrintJob.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter CreatePrintJob_View_UsesLocalizedStatusOptions` passed; `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 34 tests; Testing-environment app smoke on `http://127.0.0.1:5312` imported a sample `.gcode`, processed it, opened `/PrintImports/CreatePrintJob/{id}`, and confirmed the live status dropdown rendered `Importada`, `Planejada`, `Concluída`, `Falhou`, and `Cancelada`.
- Blockers: none.
- Next recommended task: if this status-source cleanup validates cleanly, inspect whether the print-import details page still has any adjacent duplicated production-status presentation or move to the next small production/inventory polish slice.

- Summary: Surfaced the linked production's localized status on the import review list/details pages, so operators can see the current production state without leaving `/PrintImports`.
- Timestamp: 2026-06-24 23:02:28 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter "PrintImportDetails_LinksExistingPrintJob_WhenImportAlreadyGeneratedProduction|PrintImportsIndex_ShowsLocalizedLinkedProductionStatus"` passed with 2 tests; first `dotnet build ThreeDManager.slnx` attempt hit a transient `CS2012` lock on `ThreeDManager.Application.dll`, and the immediate retry passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 35 tests; `git diff --check` reported only line-ending warnings. A Testing-environment app host started successfully on `http://127.0.0.1:5336`, but the ad-hoc PowerShell smoke script could not drive the full form flow because this shell runtime lacked the `System.Net.Http` types needed for the scripted multipart/cookie session.
- Blockers: exact live app confirmation of the new linked-status badges is still pending because the shell-only smoke script could not complete the seeded import -> linked production flow under this PowerShell runtime.
- Next recommended task: rerun one short browser-visible or app-driven smoke for `/PrintImports` and `/PrintImports/Details/{id}` with a linked production in `Testing`; if the localized linked-status badges render as expected, commit this five-file batch before moving to the next production/inventory polish slice.

- Summary: Rehydrated the existing five-file linked-status batch, reran the validation ladder, and closed the remaining live-proof gap with a Testing-environment MVC smoke that created a linked production through the real app flow.
- Timestamp: 2026-06-25 01:07:01 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter "PrintImportDetails_LinksExistingPrintJob_WhenImportAlreadyGeneratedProduction|PrintImportsIndex_ShowsLocalizedLinkedProductionStatus"` passed with 2 tests; `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 35 tests; live Testing-environment smoke on `http://127.0.0.1:5348` created a product, printer, material, imported and processed `linked-status-smoke.gcode`, generated a completed production, and confirmed both `/PrintImports` and `/PrintImports/Details/{id}` rendered `Vinculada` plus the localized `Concluída` badge without exposing raw `Completed`.
- Blockers: none.
- Next recommended task: move to the next small operator-facing consistency slice on `PrintImports`, such as centralizing the raw import `Status` field presentation on details/list so those pages no longer fall back to stored status strings.

- Summary: Centralized import-status presentation for `PrintImports` list/details so operator-facing screens now render localized labels and badge classes instead of raw stored status strings.
- Timestamp: 2026-06-25 02:10:00 -03:00
- Files changed: `src/ThreeDManager.Web/Presentation/PrintImportStatusPresentation.cs`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportsViews_ShowLocalizedImportStatusLabels_InIndexAndDetails` passed with 1 test after transient `MSB3026` retry warnings from `testhost`; standalone `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors after the initial parallel attempt hit a transient `CS2012` `VBCSCompiler` lock on `ThreeDManager.Domain.dll`; `dotnet test ThreeDManager.slnx --no-build` passed with 36 tests; Testing-environment smoke on `http://127.0.0.1:5000/PrintImports` returned HTTP 200.
- Blockers: none.
- Next recommended task: rerun the focused `PrintImports` presentation test, `dotnet build ThreeDManager.slnx`, and a short `/PrintImports` smoke; if clean, inspect whether `PrintImports/Delete` should reuse the same helper in a separate tiny follow-up.

- Summary: Reused the centralized import-status presentation helper on `PrintImports/Delete`, so the delete confirmation page now shows the same localized status badge as the import list and details pages.
- Timestamp: 2026-06-25 05:03:30 -03:00
- Files changed: `src/ThreeDManager.Web/Views/PrintImports/Delete.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintImportsViews_ShowLocalizedImportStatusLabels_InIndexDetailsAndDelete` passed with 1 test; first parallel `dotnet build ThreeDManager.slnx` attempt hit a transient `CS2012` `VBCSCompiler` lock on `ThreeDManager.Domain.dll`, and the immediate retry passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 36 tests; `git diff --check` reported line-ending warnings only; live `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5388` created an uploaded import through `/PrintImports/Create` and confirmed `/PrintImports/Delete/{id}` rendered the localized `Importado` badge without exposing raw `Uploaded`.
- Blockers: none.
- Next recommended task: inspect whether the remaining `PrintImports` create/delete labels and headings should be localized from raw English field names in one separate tiny presentation pass, or move to the next small production/inventory polish slice outside import-status display.

- Summary: Preserved queue context through `PrintImports/Delete`, so filtered import-review queues keep their `returnTo` path on the remove link, delete confirmation page, cancel action, blocked-delete redirect, and successful delete redirect.
- Timestamp: 2026-06-25 07:10:38 -03:00
- Files changed: `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`, `src/ThreeDManager.Web/Views/PrintImports/Delete.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintImports/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter DeletePrintImport_PreservesFailedQueueContext_OnGetCancelAndSuccessfulPost` passed with 1 test; `dotnet build ThreeDManager.slnx` passed; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; `git diff --check` reported line-ending warnings only; live `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5409` imported and processed `pending-delete-smoke2.gcode`, opened `/PrintImports?productionState=pending`, confirmed the rendered delete link carried `returnTo=pendingQueue`, opened the delete confirmation page, and verified the successful delete redirected back to the pending queue with the import removed.
- Blockers: none.
- Next recommended task: preserve the same `returnTo` context through any remaining `PrintImports` actions that still bounce operators to the unfiltered index, or move to one separate production/inventory polish slice outside import-review navigation.

- Summary: Localized the `PrintImports/CreatePrintJob` status field label to `Status da produção`, aligning the import-to-production form with the Portuguese presentation already used across adjacent production/import screens.
- Timestamp: 2026-06-25 09:05:41 -03:00
- Files changed: `src/ThreeDManager.Web/Views/PrintImports/CreatePrintJob.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter CreatePrintJob_View_UsesLocalizedStatusOptions` passed with 1 test after transient `MSB3026` retry warnings from `testhost`; first parallel `dotnet build ThreeDManager.slnx` attempt hit a transient `CS2012` `VBCSCompiler` lock on `ThreeDManager.Domain.dll`, and the immediate sequential retry passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; `git diff --check` reported line-ending warnings only; live `ASPNETCORE_ENVIRONMENT=Testing` host smoke confirmed `/PrintImports/Create` and `/PrintImports` returned HTTP 200, while the exact `CreatePrintJob` label rendering remained verified by the focused MVC integration test because the ad-hoc PowerShell upload/process smoke hit redirect-handling noise rather than an app failure.
- Blockers: none.
- Next recommended task: inspect `PrintImports` for the next smallest operator-facing polish outside this label cleanup, such as any remaining raw English copy on import/production forms or the next adjacent production/inventory consistency slice.

- Summary: Localized the `PrintJobs/Edit` status field label to `Status da produção`, aligning the production edit form with the import-to-production form and the surrounding Portuguese production copy.
- Timestamp: 2026-06-25 11:04:10 -03:00
- Files changed: `src/ThreeDManager.Web/Views/PrintJobs/Edit.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintJobsViews_ShowLocalizedStatusLabels_InIndexDetailsDeleteAndEdit` passed with 1 test; an initial parallel `dotnet build ThreeDManager.slnx` attempt hit a transient `CS2012` `VBCSCompiler` lock on `ThreeDManager.Domain.dll`, and the immediate sequential retry passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; `git diff --check` reported line-ending warnings only; live `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5425/PrintJobs` returned HTTP 200 and rendered the productions screen, while the exact edit-form label remained verified by the focused MVC integration test because the live Testing host did not include seeded production rows for a direct `/PrintJobs/Edit/{id}` read.
- Blockers: none.
- Next recommended task: inspect the remaining `PrintJobs` form copy for the next smallest production-facing consistency polish, such as aligning any other generic labels or helper text with the `Status da produção` wording now used in both creation and edit flows.

- Summary: Aligned the `PrintJobs` status label on the details and delete screens with the existing `Status da produção` wording already used by the production forms.
- Timestamp: 2026-06-25 13:03:28 -03:00
- Files changed: `src/ThreeDManager.Web/Views/PrintJobs/Details.cshtml`, `src/ThreeDManager.Web/Views/PrintJobs/Delete.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintJobsViews_ShowLocalizedStatusLabels_InIndexDetailsDeleteAndEdit` passed with 1 test after an initial parallel attempt hit a transient `CS2012` `VBCSCompiler` lock; `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; `git diff --check` reported line-ending warnings only; live `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5441/PrintJobs` returned HTTP 200 and rendered the productions screen, while the exact details/delete label text remained verified by the focused integration test because the live Testing host had no seeded production rows.
- Blockers: none.
- Next recommended task: inspect the remaining `PrintJobs` operator copy for the next smallest consistency polish, such as aligning any generic table/detail headings with the more explicit production wording now used across create, edit, details, and delete flows.

- Summary: Aligned the remaining production-facing status table headers on `PrintJobs` and the dashboard recent-productions card with the existing `Status da produção` wording used across the rest of the production flow.
- Timestamp: 2026-06-25 15:05:19 -03:00
- Files changed: `src/ThreeDManager.Web/Views/PrintJobs/Index.cshtml`, `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter "PrintJobsViews_ShowLocalizedStatusLabels_InIndexDetailsDeleteAndEdit|Dashboard_ShowsLocalizedStatusLabels_InRecentPrintJobsTable"` passed with 2 tests; `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; `git diff --check` reported line-ending warnings only; live `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5460` confirmed `/PrintJobs` and `/Dashboard` loaded, while the exact table-header text remained verified by the focused integration tests because the live Testing host had no seeded production rows.
- Blockers: none.
- Next recommended task: inspect the next smallest production-facing copy gap outside status labels, such as clarifying the dashboard recent-productions `Tempo`/`Custo` headings or another single operator-facing wording inconsistency.

- Summary: Clarified the dashboard recent-productions table headers so the operator view now says `Tempo estimado` and `Custos da produção` instead of the generic `Tempo` and `Custo`.
- Timestamp: 2026-06-25 17:02:50 -03:00
- Files changed: `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_ShowsLocalizedStatusLabels_InRecentPrintJobsTable` passed with 1 test; `dotnet build ThreeDManager.slnx` passed with transient `MSB3026` retry warnings caused by `testhost` holding copied test assemblies; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; live `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5472/Dashboard` returned HTTP 200.
- Blockers: none.
- Next recommended task: inspect the next smallest production-facing copy gap on the dashboard or `PrintJobs`, such as renaming the recent-productions `Arquivo`/`Material` cost labels if a more explicit operator wording is warranted.

- Summary: Clarified the dashboard recent-productions cost breakdown so the operator view now says `Custo informado` and `Custo calculado do material` instead of the terse `Arquivo` and `Material` prefixes.
- Timestamp: 2026-06-25 19:05:27 -03:00
- Files changed: `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_ShowsLocalizedStatusLabels_InRecentPrintJobsTable` passed with 1 test; `dotnet build ThreeDManager.slnx` passed with transient `MSB3026` retry warnings caused by `testhost` holding copied test assemblies; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; `git diff --check` reported line-ending warnings only; isolated `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5488/Dashboard` returned HTTP 200 and rendered the dashboard recent-productions section.
- Blockers: none.
- Next recommended task: inspect the next smallest dashboard or `PrintJobs` production-copy gap, such as clarifying the recent-productions `Filamento` label if a more explicit operator wording is still needed.

- Summary: Clarified the dashboard recent-productions filament column so it now says `Filamento usado`, matching the production forms and details page wording already used elsewhere in the flow.
- Timestamp: 2026-06-25 21:01:42 -03:00
- Files changed: `src/ThreeDManager.Web/Views/Dashboard/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter Dashboard_ShowsLocalizedStatusLabels_InRecentPrintJobsTable` passed with 1 test; an initial validation attempt was blocked by local `ThreeDManager.Web` file locks on `bin\Debug`, so the session-local web hosts were stopped and `dotnet build ThreeDManager.slnx` then passed with 0 warnings and 0 errors; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; `git diff --check` reported line-ending warnings only; isolated `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5504/Dashboard` returned HTTP 200 and rendered both `Últimas produções` and `Filamento usado`.
- Blockers: none.
- Next recommended task: inspect the next smallest production-facing wording gap, such as whether `src/ThreeDManager.Web/Views/PrintJobs/Index.cshtml` should spell out `Custo mat.` in a separate tiny batch.

- Summary: Expanded the `PrintJobs` list cost header from the abbreviated `Custo mat.` to `Custo calculado do material`, matching the explicit production-cost wording already used on adjacent surfaces.
- Timestamp: 2026-06-25 22:20:00 -03:00
- Files changed: `src/ThreeDManager.Web/Views/PrintJobs/Index.cshtml`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --filter PrintJobsViews_ShowLocalizedStatusLabels_InIndexDetailsDeleteAndEdit` passed with 1 test; `dotnet build ThreeDManager.slnx` passed with transient `MSB3026` retry warnings caused by `testhost` holding copied test assemblies; `dotnet test ThreeDManager.slnx --no-build` passed with 37 tests; `git diff --check` reported line-ending warnings only; isolated `ASPNETCORE_ENVIRONMENT=Testing` smoke on `http://127.0.0.1:5520/PrintJobs` returned HTTP 200 and rendered the productions screen, while the exact cost-header text remained verified by the focused integration test because the live Testing host had no seeded production rows.
- Blockers: none.
- Next recommended task: inspect the next smallest production-facing wording gap on `PrintJobs`, such as whether the list page should clarify any remaining terse empty-state/value copy after this cost-header alignment.

## 2026-06-30

- Summary: Added server-side data-integrity validation to the `Material` entity so material create/edit now require a name and reject negative cost-per-kg, current stock, and minimum stock values, closing a gap where the bypassable client-side `min` attribute was the only guard and negative values could corrupt cost calculations and low-stock alerts.
- Timestamp: 2026-06-30
- Files changed: `src/ThreeDManager.Domain/Entities/Material.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors; focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --no-build --filter MaterialEdit_RejectsNegativeStock_WithoutPersistingChangeOrMovement` passed with 1 test; `dotnet test ThreeDManager.slnx --no-build` passed with 38 tests; `dotnet ef migrations has-pending-model-changes` reported no model changes (annotations are validation-only, no migration needed); the new MVC integration test posts a negative `CurrentStockGrams` to `/Materials/Edit/{id}` and verifies the rendered page shows "O estoque atual não pode ser negativo." while a fresh `AppDbContext` scope confirms the stock stayed at `1000` and no stock movement was persisted; live local-Postgres boot smoke on `http://localhost:5042` returned HTTP 200 for `/`, `/Materials`, and `/Materials/Create`.
- Blockers: none. Note: pre-existing uncommitted work (`src/ThreeDManager.Web/Program.cs` `EnableRetryOnFailure` and deletion of the template `Class1.cs` files) was left untouched and not committed, since it could not be attributed to this run; only this batch's three files were staged and committed by explicit path.
- Next recommended task: extend the same non-negative validation to the `Printer` and `Product` create/edit forms if they bind their entities directly, or attribute and commit the pending `Program.cs` retry-resilience change as its own small batch.

- Summary: Extended the Material-style data-integrity validation to the `Product` entity, so product create/edit now require a name and reject a negative sale price, closing the same gap (bypassable client-side `min` attribute only) for the commercial product record that feeds future pricing/finance calculations.
- Timestamp: 2026-06-30
- Files changed: `src/ThreeDManager.Domain/Entities/Product.cs`, `tests/ThreeDManager.Tests/PrintJobControllerIntegrationTests.cs`, `docs/context/tasks_diary.md`
- Validation run: `dotnet build ThreeDManager.slnx` passed with 0 warnings and 0 errors; focused `dotnet test tests\ThreeDManager.Tests\ThreeDManager.Tests.csproj --no-build --filter ProductEdit_RejectsNegativeSalePrice_WithoutPersistingChange` passed with 1 test; `dotnet test ThreeDManager.slnx --no-build` passed with 39 tests; `dotnet ef migrations has-pending-model-changes` reported no model changes (annotations are validation-only, no migration needed); the new MVC integration test posts a negative `SalePrice` to `/Products/Edit/{id}`, asserts the rendered page carries "O preço de venda não pode ser negativo." and that a fresh `AppDbContext` scope shows `SalePrice` was not persisted (still null); live local-Postgres boot smoke on `http://localhost:5042` returned HTTP 200 for `/Products` and `/Products/Create`, and the rendered Create form emitted both the `data-val-required` name message ("Informe o nome do produto.") and the `data-val-range` sale-price message.
- Blockers: none. Note: pre-existing uncommitted work (`src/ThreeDManager.Web/Program.cs` `EnableRetryOnFailure` and deletion of the template `Class1.cs` files) was again left untouched and not committed, since it could not be attributed to this run; only this batch's three files were staged and committed by explicit path.
- Next recommended task: extend the same non-negative/required validation to the `Printer` entity (`Name` required), or attribute and commit the pending `Program.cs` retry-resilience change as its own small batch.
