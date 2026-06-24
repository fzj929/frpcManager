# Code Map

This file gives AI coding tools and maintainers a fast map of the FrpcManager codebase. Keep it short and update it when major modules move.

## Project Shape

- `backend/FrpcManager.Api/`: ASP.NET Core 8 backend API.
- `frontend/`: Vue 3 + Element Plus frontend.
- `Dockerfile`: multi-stage Docker image build for frontend + backend.
- `docker-compose.yml`: Docker Compose example.
- `docker-build-push.bat`: Windows Docker build and Docker Hub push script.
- `docker-build-push-linux.sh`: Linux/macOS Docker build and Docker Hub push script.
- `start-publish.bat`: Windows publish/start script.
- `start-publish-linux.bat`: Linux publish/start script, despite the `.bat` suffix.
- `README.md`: Chinese README.
- `README.en.md`: English README.
- `CHANGELOG.md`: important release notes.
- `SECURITY.md`: security reporting and supported versions.

## Backend Entry Points

- `backend/FrpcManager.Api/Program.cs`
  - App startup, DI registration, authentication, rate limits, forwarded headers, CORS, database provider selection, database compatibility initialization, Kestrel ports/certificate.
  - Database compatibility initialization is guarded by `DatabaseCompatibilityVersion` and the internal `__FrpcManagerSchema` table.
  - Default HTTP/HTTPS management ports are `6887` and `6888`.
  - SQLite is default; MySQL is optional via environment/config.

- `backend/FrpcManager.Api/Data/AppDbContext.cs`
  - EF Core DbContext.
  - Main tables: `Users`, `Proxies`, `AuditLogs`, `WakeLogs`, `WakeSchedules`, `WakeMacAddresses`, `HttpsProxyRules`.

## Backend Controllers

- `Controllers/AuthController.cs`
  - Login, setup wizard, current user, password change.

- `Controllers/UsersController.cs`
  - Administrator-only user management: create users, update role/disabled state, and reset passwords.

- `Controllers/ProxiesController.cs`
  - frp tunnel CRUD, enable/disable, timed enable, sync from frpc.
  - All users can view tunnels. Normal users can manage only tunnels they created; administrators can manage all tunnels.
  - Administrators can assign or clear tunnel ownership.

- `Controllers/ConfigController.cs`
  - frpc server configuration, status, reload.

- `Controllers/HttpsProxyController.cs`
  - Lightweight HTTPS reverse proxy rules.
  - Supports default site certificate, IIS `.pfx/.p12`, and Nginx-style `.pem/.crt/.cer + .key`.
  - Can optionally create a disabled frp TCP tunnel for a new HTTPS proxy.
  - All users can view HTTPS proxy rules. Normal users can manage only rules they created; administrators can manage all rules.

- `Controllers/WakeOnLanController.cs`
  - Wake-on-LAN send, MAC address book, logs, schedules, wake again.

- `Controllers/BackupController.cs`
  - Export/restore frp tunnels, HTTPS proxy rules, and frpc config.
  - Does not export uploaded certificate files, private keys, certificate passwords, or user passwords.
  - Exports user metadata and tunnel owner usernames, but not password hashes.
  - Administrator-only.

- `Controllers/AuditLogsController.cs`
  - Operation logs.
  - Administrator-only.

- `Controllers/HealthController.cs`
  - Health checks.

## Backend Services

- `Services/AuthService.cs`
  - User authentication, role claims, disabled-account checks, and first admin setup.

- `Services/UserContextService.cs`
  - Current user ID, username, role, administrator check, and owner/admin resource management helper.

- `Services/LoginAttemptLimiter.cs`
  - Login failure tracking and account/IP-related protection.

- `Services/ProxyService.cs`
  - Main frp tunnel business logic.
  - Creates tunnels disabled by default.
  - Syncs enabled tunnels to frpc config and reloads frpc.
  - Enforces tunnel ownership for update/delete/enable/disable.
  - Allows duplicate saved remote ports, but blocks enabling TCP/UDP tunnels that conflict with already enabled tunnels.

- `Services/FrpcApiService.cs`
  - HTTP client wrapper for frpc web admin API.
  - frpc web admin address can come from environment/config, useful for Docker with `host.docker.internal`.

- `Services/HttpsProxyRuntimeService.cs`
  - Starts/stops runtime HTTPS reverse proxy listeners.
  - Proxies HTTPS requests to internal HTTP targets.

- `Services/HttpsProxyStartupService.cs`
  - Starts enabled HTTPS proxy rules on app startup.

- `Services/BackupService.cs`
  - Configuration backup/restore.
  - HTTPS proxy restore uses the default certificate because uploaded certificate material is not exported.

- `Services/WakeOnLanService.cs` and `Services/WakeScheduleService.cs`
  - Wake-on-LAN magic packet and scheduled wake support.

- `Services/ChannelExpiryService.cs`
  - Automatically disables expired/timed frp tunnels.

- `Services/TomlService.cs`
  - frpc TOML parse/write helpers.

- `Services/JwtKeyProvider.cs`
  - JWT signing key loading/generation.

## Backend Models And DTOs

- `Models/Proxy.cs`: frp tunnel.
- `Models/HttpsProxyRule.cs`: lightweight HTTPS reverse proxy rule.
- `Models/User.cs`: user account, role, disabled state, and login lockout fields.
- `Models/AuditLog.cs`: operation log.
- `Models/WakeLog.cs`, `Models/WakeSchedule.cs`, `Models/WakeMacAddress.cs`: Wake-on-LAN records, schedules, and MAC address book.
- `Models/FrpcConfig.cs`: frpc config model.

- `DTOs/ProxyDtos.cs`: frp tunnel API contracts.
- `DTOs/HttpsProxyDtos.cs`: HTTPS proxy API contracts.
- `DTOs/UserDtos.cs`: user management API contracts.
- `DTOs/BackupDtos.cs`: backup/restore JSON shape.
- `DTOs/AuthDtos.cs`, `DTOs/ConfigDtos.cs`, `DTOs/WakeOnLanDtos.cs`, `DTOs/AuditDtos.cs`, `DTOs/SetupDtos.cs`: feature DTOs.

## Frontend Entry Points

- `frontend/src/main.ts`: Vue app bootstrap.
- `frontend/src/App.vue`: app root.
- `frontend/src/router/index.ts`: routes.
- `frontend/src/api/index.ts`: centralized API client.
- `frontend/src/stores/auth.ts`: auth state.
- `frontend/src/types/index.ts`: frontend TypeScript types.
- `frontend/public/favicon.svg`: browser tab icon.

## Frontend Views

- `frontend/src/views/LoginView.vue`: login.
- `frontend/src/views/SetupView.vue`: first startup admin setup.
- `frontend/src/views/DashboardView.vue`: dashboard.
- `frontend/src/views/ProxiesView.vue`: frp tunnel list and actions.
- `frontend/src/components/ProxyFormDialog.vue`: frp tunnel create/edit form.
- `frontend/src/components/TimedEnableDialog.vue`: timed tunnel enable dialog.
- `frontend/src/views/HttpsProxiesView.vue`: HTTPS reverse proxy management.
- `frontend/src/views/WakeOnLanView.vue`: Wake-on-LAN send and schedule entry.
- `frontend/src/views/WakeMacAddressesView.vue`: MAC address management and naming.
- `frontend/src/views/WakeRecordsView.vue`: wake logs and wake again.
- `frontend/src/views/AuditLogsView.vue`: operation logs.
- `frontend/src/views/SettingsView.vue`: frpc settings, health checks, backup/restore, account security, about.
- `frontend/src/views/UsersView.vue`: administrator-only user management.
- `frontend/src/components/AppLayout.vue`: navigation shell.

## Common Change Locations

- Add a new backend API:
  - Controller in `backend/FrpcManager.Api/Controllers/`
  - DTO in `backend/FrpcManager.Api/DTOs/`
  - Service in `backend/FrpcManager.Api/Services/` if business logic is non-trivial
  - Register service in `Program.cs`
  - Add frontend API wrapper in `frontend/src/api/index.ts`
  - Add types in `frontend/src/types/index.ts`

- Add a new database-backed feature:
  - Model in `Models/`
  - `DbSet` and indexes in `AppDbContext.cs`
  - Compatibility table/column initialization in `Program.cs`
  - Backup support in `BackupDtos.cs` and `BackupService.cs` if user configuration should be portable

- Add a frontend page:
  - View in `frontend/src/views/`
  - Route in `frontend/src/router/index.ts`
  - Navigation item and title in `frontend/src/components/AppLayout.vue`

## Build And Verification

- Backend build:

```powershell
dotnet build backend\FrpcManager.Api\FrpcManager.Api.csproj --no-restore
```

- Frontend build on Windows:

```powershell
npm.cmd run build
```

- Frontend build from `frontend/` on Linux/macOS:

```bash
npm run build
```

- Docker build:

```powershell
docker build -t frpc-manager:local .
```

## Expected Build Warnings

- Frontend build can show Rolldown `INVALID_ANNOTATION` warnings from `node_modules/@vueuse/core`.
- Frontend build can warn that some chunks are larger than 500 kB.
- These warnings have been seen before and do not necessarily indicate a failed build.
