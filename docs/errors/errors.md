# Active Errors

- None currently recorded.

## Notes (not active bugs)

- Cloudflare **quick tunnel** (`cloudflared tunnel --url http://127.0.0.1:5080`) intermittently "cannot be accessed" right after startup — this is not a code/app bug. The tunnel and origin are healthy (local `127.0.0.1:5080` answers, forwarded headers rewrite redirects to the tunnel host), but a fresh `*.trycloudflare.com` hostname takes several seconds up to ~1 min to propagate ("it may take some time to be reachable" per the cloudflared log), and on a host with no working IPv6 egress a client resolving the AAAA record before the A record fails until the A record is cached. Observed 2026-07-13 while stabilizing remote access. Workaround/next step: use `scripts\Start-ThreeDManager.ps1` (waits for reachability before opening the browser) and, for a stable fixed URL, migrate to a named tunnel per `docs/ops/REMOTE_ACCESS.md`. Not an active bug.
- Dev-mode `dotnet run --project src/ThreeDManager.Web` (outside `ASPNETCORE_ENVIRONMENT=Testing`) can throw an unhandled `System.IO.FileNotFoundException` for `wwwroot/ThreeDManager.Web.styles.css` on some requests — this bundled stylesheet only exists in published output, not local `dotnet run`, so it is an environment artifact of the dev launch profile, not application code. Observed 2026-07-08 during a `/PrintJobs` smoke; did not affect the routes under test. No action needed unless it starts blocking a route being verified.
