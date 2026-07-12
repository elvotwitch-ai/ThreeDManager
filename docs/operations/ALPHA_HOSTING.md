# Alpha Hosting

The alpha deployment runs on the owner's Windows PC. Clients use only the ThreeDManager web interface; PostgreSQL and the repository remain local to the server.

## Access credentials

The current alpha stage supports one operator account. Configure it outside source control with environment variables:

```powershell
$env:AlphaAccess__Username = "operator"
$env:AlphaAccess__Password = "use-a-long-unique-password"
```

Do not write the real password into `appsettings.json`, scripts, Git, screenshots, or the task diary. Restart the web process after changing either value. Existing sessions expire after at most 12 hours or when the user selects `Sair`.

The application fails at startup outside the automated `Testing` environment when either credential is missing. This is intentional fail-closed behavior.

## Local validation

With `threedmanager-db` ready, run:

```powershell
dotnet run --project src/ThreeDManager.Web --launch-profile http
```

Open `http://localhost:5042`. An anonymous request must redirect to `/Account/Login`; invalid credentials must remain on the login page; valid credentials must open the Dashboard and render the operator name plus the `Sair` action.

## Remote exposure boundary

- Expose only the ThreeDManager HTTP/HTTPS endpoint through the selected private tunnel or reverse proxy.
- Never expose PostgreSQL port `5436` to the internet.
- `docker-compose.yml` binds PostgreSQL as `127.0.0.1:5436:5432`; preserve this localhost-only binding.
- Do not share Windows credentials, repository access, Docker access, or filesystem paths with clients.
- Add HTTPS before remote use; cookie transport security depends on the externally exposed endpoint.

## Release publication

From the repository root:

```powershell
.\scripts\Publish-ThreeDManager.ps1
```

This creates the ignored framework-dependent `win-x64` Release artifact under `artifacts\publish\ThreeDManager`. The server must retain the matching .NET 10 runtime.

## Windows service installation

Open PowerShell as Administrator and run:

```powershell
.\scripts\Install-ThreeDManagerService.ps1 -OperatorUsername "operator"
```

The installer prompts for the password without echoing it, requires at least 12 characters, copies the published artifact to `C:\ProgramData\ThreeDManager\app`, creates the automatic `ThreeDManager` Windows service, configures restart-on-failure, and binds Kestrel to `http://127.0.0.1:5080` by default. The username and password are written to the service's protected registry environment, not to the repository or published appsettings files.

The default localhost binding is intentional: the next checkpoint must place a private HTTPS endpoint or tunnel in front of it. Do not change the binding to `0.0.0.0` merely to make it reachable from the internet.

To remove the service while preserving installed application files:

```powershell
.\scripts\Uninstall-ThreeDManagerService.ps1
```

The service installer has not been executed on the host yet. Installation requires an elevated terminal and the real alpha operator password.

To deploy a new published build after the service already exists:

```powershell
.\scripts\Publish-ThreeDManager.ps1
.\scripts\Update-ThreeDManagerService.ps1
```

Run the update command from an elevated PowerShell window. It preserves the service credentials, stops the service, copies the new artifact, starts the service again, and verifies that `/css/site.css` is served as CSS.
