# Remote Access (Cloudflare Tunnel)

The app runs locally as the Windows service `ThreeDManager`, bound to
`http://127.0.0.1:5080`. To reach it from outside the LAN it is fronted by a
Cloudflare Tunnel (`cloudflared`).

## Quick start (today)

Double-click `scripts\Start-ThreeDManager.cmd` (or run the PowerShell script):

```powershell
# just open the tunnel + browser (app is already an auto-start service)
powershell -ExecutionPolicy Bypass -File scripts\Start-ThreeDManager.ps1

# rebuild + redeploy the service first, then tunnel (self-elevates for the update)
powershell -ExecutionPolicy Bypass -File scripts\Start-ThreeDManager.ps1 -Build
#   or:  scripts\Start-ThreeDManager.cmd build
```

What the launcher does:

1. (`-Build` only) runs `Publish-ThreeDManager.ps1` + `Update-ThreeDManagerService.ps1`.
2. Ensures the `ThreeDManager` service is running and waits for `http://127.0.0.1:5080`.
3. Stops any stray `cloudflared` so exactly one tunnel exists.
4. Starts the quick tunnel, **captures the generated `*.trycloudflare.com` URL**,
   copies it to the clipboard, and opens the default browser.
5. Keeps the tunnel alive while its window stays open (close it / Ctrl+C to end
   external access).

## Known limitations of the quick tunnel (why it feels unstable)

- **The URL changes on every run** and cannot be bookmarked.
- **No uptime guarantee** — Cloudflare may drop account-less tunnels.
- **Propagation delay**: a freshly created `*.trycloudflare.com` hostname can take
  from several seconds up to ~1 minute to become reachable ("it may take some time
  to be reachable" in the cloudflared log). Trying too early looks like a failure.
- On a host without working IPv6 egress, a client that resolves the tunnel's AAAA
  record before its A record can fail to connect until the A record is cached.

## Stable setup (named tunnel — recommended next step)

A **named tunnel** gives a fixed hostname on a domain you own, and can run as its own
Windows service so it survives reboots. It requires, one time:

1. A Cloudflare account and a domain managed in Cloudflare (the operator must own this).
2. `cloudflared tunnel login` — opens a browser for the operator to authenticate and
   authorize the zone (must be done by the operator; credentials are never scripted).
3. `cloudflared tunnel create threedmanager` → produces a credentials JSON + tunnel UUID.
4. A `config.yml` with the ingress rule `http://127.0.0.1:5080` and the hostname.
5. `cloudflared tunnel route dns threedmanager app.<domain>`.
6. `cloudflared service install` to run the tunnel as a Windows service.

Once the account + domain + login (steps 1-2) are in place, the rest can be scripted
and wired into the launcher, yielding a fixed URL with no propagation lag.
