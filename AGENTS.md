# Agent Instructions

These instructions are for AI coding tools working in this repository. Follow them to avoid wasting context and to preserve project behavior.

## Communication

- Prefer Chinese when replying to the project owner.
- Be concise and concrete.
- Mention verification commands that were actually run.
- Do not claim changes were committed or pushed unless Git commands succeeded.

## Repository Safety

- Do not commit secrets, tokens, passwords, databases, uploaded certificates, private keys, or generated publish output.
- Do not commit:
  - `*.db`, `*.db-shm`, `*.db-wal`
  - `.env`, `.env.local`
  - `appsettings.Production.json`
  - private cert/key files such as `*.key`, `*.pem`, `*.crt`, `*.cer`, `*.p12`, `*.pfx`
- Exception: `backend/FrpcManager.Api/frpcmanager.pfx` is intentionally included as the project default certificate.
- Do not revert user changes unless explicitly asked.
- Keep edits scoped to the requested feature or fix.

## Architecture Rules

- Backend is ASP.NET Core 8.
- Frontend is Vue 3 + Element Plus.
- Default database is SQLite.
- Optional database is MySQL, selected via environment/config.
- Main management ports:
  - HTTP: `6887`
  - HTTPS: `6888`
- frpc web admin default is `127.0.0.1:7400`; Docker deployments can use `host.docker.internal`.

## Security Rules

- Login security matters. Preserve login rate limiting and account lockout behavior.
- `X-Forwarded-For` / `X-Forwarded-Proto` support must stay default-off and require explicit trusted proxies/networks.
- Do not make forwarded headers trust arbitrary clients by default.
- Do not expose certificate passwords, JWT keys, database files, or user passwords in logs, backups, README examples, or Docker images.
- Docker Hub image and GitHub repo are public, so assume every committed file is public.

## HTTPS Proxy Rules

- HTTPS proxy feature lives mainly in:
  - `backend/FrpcManager.Api/Controllers/HttpsProxyController.cs`
  - `backend/FrpcManager.Api/Services/HttpsProxyRuntimeService.cs`
  - `backend/FrpcManager.Api/Services/HttpsProxyStartupService.cs`
  - `frontend/src/views/HttpsProxiesView.vue`
- It is a lightweight HTTPS reverse proxy from local HTTPS port to an internal HTTP URL.
- Supported certificate modes:
  - default site certificate
  - IIS certificate: `.pfx/.p12`
  - Nginx certificate: `.pem/.crt/.cer + .key`
- Do not let HTTPS proxy listen on management ports `6887` or `6888`.
- If creating an frp tunnel from the HTTPS proxy page:
  - type: `tcp`
  - local IP: `127.0.0.1`
  - local port: HTTPS listen port
  - remote port: HTTPS listen port
  - description: HTTPS proxy name
  - tunnel must be created disabled
  - tell the user it must be enabled in tunnel management before external access works
- frp tunnel name validation: letters, numbers, underscore, hyphen; max length 64.

## Backup And Restore Rules

- Backup/restore feature lives mainly in:
  - `backend/FrpcManager.Api/DTOs/BackupDtos.cs`
  - `backend/FrpcManager.Api/Services/BackupService.cs`
  - `backend/FrpcManager.Api/Controllers/BackupController.cs`
  - `frontend/src/views/SettingsView.vue`
- Backups include:
  - frp tunnels
  - HTTPS proxy rules
  - Wake-on-LAN MAC address book
  - frpc config
- Backups must not include:
  - user passwords
  - uploaded certificate files
  - private keys
  - certificate passwords
- When restoring HTTPS proxy rules that originally used uploaded certificates, restore them using the default certificate and keep them disabled if needed to avoid startup failures.
- Older backup files without `httpsProxies` should continue to restore.

## Docker Rules

- Docker image builds with a multi-stage `Dockerfile`.
- Build scripts:
  - Windows: `docker-build-push.bat`
  - Linux/macOS: `docker-build-push-linux.sh`
- Docker Hub username must be a Docker ID/namespace, not an email address.
- Do not add `# syntax=docker/dockerfile:1.7` unless Docker Hub access to `docker/dockerfile` is known to work. It previously failed through a mirror.
- BuildKit is enabled in scripts with `DOCKER_BUILDKIT=1`.
- Dependency restore steps use cache mounts:
  - npm cache: `frpcmanager-npm`
  - NuGet cache: `frpcmanager-nuget`
- If `RUN --mount=type=cache` fails on an old Docker engine, change to a more compatible Dockerfile instead of removing dependency layering.

## Frontend Rules

- Use Element Plus components and existing page layout patterns.
- Keep pages usable on mobile.
- For buttons and actions, prefer existing icon style from `@element-plus/icons-vue`.
- Do not add landing pages; this is an operational management app.
- Keep UI text direct and practical.
- Settings/About currently shows version and GitHub project link.

## Wake-on-LAN Rules

- Wake-on-LAN feature lives mainly in:
  - `backend/FrpcManager.Api/Controllers/WakeOnLanController.cs`
  - `backend/FrpcManager.Api/Services/WakeOnLanService.cs`
  - `backend/FrpcManager.Api/Services/WakeScheduleService.cs`
  - `backend/FrpcManager.Api/Models/WakeMacAddress.cs`
  - `frontend/src/views/WakeOnLanView.vue`
  - `frontend/src/views/WakeMacAddressesView.vue`
  - `frontend/src/views/WakeRecordsView.vue`
- Store MAC addresses in normalized `AA:BB:CC:DD:EE:FF` form.
- Every newly used MAC address should be added to the address book automatically with default name equal to the MAC address.
- A MAC address whose name equals its MAC address is considered unnamed in the UI.
- Wake logs and schedules should display both MAC address and associated host name when available.
- Wake menu items should stay grouped under the host wake submenu.

## Backend Rules

- Prefer putting business logic in services rather than controllers when it is shared or non-trivial.
- Add audit logs for meaningful user actions.
- If adding database fields/tables, update both SQLite and MySQL compatibility initialization in `Program.cs`.
- If adding user configuration, consider whether backup/restore should include it.
- If adding hosted/background behavior, register it in `Program.cs` and consider startup/shutdown failure handling.

## Verification Commands

Run the narrowest useful checks after changes:

```powershell
dotnet build backend\FrpcManager.Api\FrpcManager.Api.csproj --no-restore
```

```powershell
npm.cmd run build
```

Known frontend warnings:

- Rolldown `INVALID_ANNOTATION` warnings from `@vueuse/core`.
- Large chunk warning over 500 kB.

These warnings are known and do not by themselves mean the build failed.

## Git

- Before committing, inspect:

```powershell
git status --short
git diff --stat
```

- Keep commit messages short and specific.
- Push only when the user asks to upload/push/submit to GitHub.
