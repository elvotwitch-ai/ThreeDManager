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
- Do not share Windows credentials, repository access, Docker access, or filesystem paths with clients.
- Add HTTPS before remote use; cookie transport security depends on the externally exposed endpoint.

Installing the restartable Windows service and private remote endpoint is a separate deployment batch.
