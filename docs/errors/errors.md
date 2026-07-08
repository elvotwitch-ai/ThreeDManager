# Active Errors

- None currently recorded.

## Notes (not active bugs)

- Dev-mode `dotnet run --project src/ThreeDManager.Web` (outside `ASPNETCORE_ENVIRONMENT=Testing`) can throw an unhandled `System.IO.FileNotFoundException` for `wwwroot/ThreeDManager.Web.styles.css` on some requests — this bundled stylesheet only exists in published output, not local `dotnet run`, so it is an environment artifact of the dev launch profile, not application code. Observed 2026-07-08 during a `/PrintJobs` smoke; did not affect the routes under test. No action needed unless it starts blocking a route being verified.
