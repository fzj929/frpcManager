# FrpC Manager

[中文](README.md) | [English](README.en.md)

FrpC Manager is a web management platform for frpc tunnels, built with **Vue 3 + ASP.NET Core 8 + SQLite/MySQL**. It is designed to run alongside frpc and provides a browser UI for managing tunnel configuration.

---

## Features

- **Tunnel overview**: list all TCP/UDP tunnels and show runtime status
- **Status monitoring**: read active tunnel status through the frpc Web API
- **Enable / disable tunnels**: update `frpc.toml` and trigger `frpc reload`
- **Tunnel management**: create, edit, delete, search, and filter tunnels
- **Sync from frpc**: import existing tunnels from the current frpc configuration
- **Server configuration**: edit frpc server address, port, auth method, and token
- **Authentication**: JWT login and password change
- **First-run setup wizard**: create the first administrator account from the web UI
- **Audit logs**: record login, tunnel, config, backup, restore, and Wake-on-LAN actions
- **Health checks**: check database and frpc Web API connectivity
- **Backup / restore**: export and restore tunnel and frpc configuration
- **Wake-on-LAN**: send a magic packet to wake a LAN computer by MAC address
- **Wake records / scheduled wake**: keep Wake-on-LAN history, wake again from history, and run daily scheduled wake tasks
- **Docker Compose**: build and run the service with one command

---

## Tech Stack

| Layer | Technology |
| --- | --- |
| Frontend | Vue 3, Vite, TypeScript, Element Plus, Pinia, Vue Router |
| Backend | ASP.NET Core 8, EF Core, JWT Bearer |
| Database | SQLite by default, optional MySQL |
| Integration | frpc built-in Web API, default `http://127.0.0.1:7400` |

---

## Project Structure

```text
frpcManager/
├── backend/
│   └── FrpcManager.Api/          # ASP.NET Core 8 Web API
│       ├── Controllers/           # Auth, Proxies, Config, AuditLogs, Backup, Health
│       ├── Services/              # Business logic, frpc API integration, TOML parsing
│       ├── Models/ & DTOs/        # Data models and DTOs
│       ├── Data/                  # EF Core DbContext
│       └── Program.cs
├── frontend/
│   └── src/
│       ├── views/                 # Login, Setup, Dashboard, Proxies, Settings, AuditLogs
│       ├── components/            # AppLayout, ProxyFormDialog
│       ├── stores/                # Pinia stores
│       ├── api/                   # Axios API wrapper
│       └── router/
├── docker-compose.yml
├── Dockerfile
├── start-dev.bat
├── start-publish.bat
└── start-publish-linux.bat
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- frpc installed and running with `webServer` enabled

Example `frpc.toml`:

```toml
serverAddr = "your.server.com"
serverPort = 7000

auth.method = "token"
auth.token  = "your_token"

webServer.addr = "127.0.0.1"
webServer.port = 7400
```

---

## Quick Start

```bash
git clone https://github.com/fzj929/frpcManager.git
cd frpcManager

# Install frontend dependencies on first run
install.bat

# Start backend and frontend in development mode
start-dev.bat
```

After startup:

| URL | Description |
| --- | --- |
| `http://localhost:5173` | Frontend development UI |
| `http://localhost:6887` | Backend API over HTTP |
| `https://localhost:6888` | Backend API over HTTPS |
| `https://localhost:6888/swagger` | Swagger API documentation |

There is no built-in default admin password. On first launch, open the web UI and create the first administrator account. You can also preconfigure an initial admin account with `Admin__Username` and `Admin__Password`.

---

## Operations

### First-Run Setup

When no user exists, the frontend redirects to `/setup`. Create the first administrator account there. For unattended deployments:

```bash
Admin__Username=admin
Admin__Password=change-this-password
```

If `Admin__Password` is not set, the application will not create a default administrator automatically.

### Audit Logs

The Audit Logs page records recent key actions:

- Login success / failure
- First administrator creation
- Tunnel create, update, delete, enable, disable, sync
- frpc config update and reload
- Backup export and restore
- Wake-on-LAN send

### Login Security

The login endpoint includes basic brute-force protection:

- Window rate limiting by `IP + username`, default 5 attempts per minute for the same pair
- Temporary account lockout for 10 minutes after 5 consecutive failures for the same username
- Login failure, rate limit, and lockout events are written to audit logs with source IP
- `X-Forwarded-For` and `X-Forwarded-Proto` are not trusted by default, which prevents clients from spoofing IP addresses to bypass rate limits
- Docker, Nginx, Caddy, and other reverse proxy deployments can explicitly enable forwarded headers with trusted proxy IPs or networks

Configuration can be overridden with environment variables:

```bash
LoginSecurity__IpUsernamePermitLimit=5
LoginSecurity__IpUsernameWindowMinutes=1
LoginSecurity__MaxFailedAttempts=5
LoginSecurity__LockoutMinutes=10
```

To trust the real client IP from a reverse proxy, explicitly enable forwarded headers and restrict trusted proxy sources:

```bash
ForwardedHeaders__Enabled=true
ForwardedHeaders__KnownProxies__0=172.18.0.1
```

Or configure a trusted network:

```bash
ForwardedHeaders__Enabled=true
ForwardedHeaders__KnownNetworks__0=172.18.0.0/16
```

### Health Check

The Settings page includes a health check card. You can also call:

```bash
GET /api/health
```

The response includes database status, frpc Web API status, and the check time.

### Wake Records and Scheduled Wake

The Wake Records page manages Wake-on-LAN history and scheduled tasks:

- Manual and scheduled wake actions record MAC address, broadcast address, port, source, result, and time
- Wake a host again from any history record
- Create daily scheduled wake tasks at a fixed time
- Enable, disable, edit, delete, or run scheduled wake tasks immediately

### Backup / Restore

The Settings page can export and restore tunnel and frpc configuration:

- Export: download a JSON backup containing tunnels and frpc config
- Restore: upload a backup JSON and optionally apply frpc config

Restore actions are recorded in audit logs. Export a backup before large configuration changes.

---

## Database Configuration

FrpC Manager supports SQLite and MySQL.

- SQLite is the default and is suitable for lightweight single-host deployment
- MySQL can be enabled with `Database__Provider=mysql`

### SQLite

```bash
Database__Provider=sqlite
ConnectionStrings__DefaultConnection="Data Source=frpcmanager.db"
```

For Docker:

```bash
ConnectionStrings__DefaultConnection="Data Source=/app/data/frpcmanager.db"
```

### MySQL

```bash
Database__Provider=mysql
ConnectionStrings__MySql="Server=127.0.0.1;Port=3306;Database=frpcmanager;User=frpcmanager;Password=change-this-password;CharSet=utf8mb4;"
```

If `ConnectionStrings__MySql` is not set, the application falls back to `ConnectionStrings__DefaultConnection`.

The default MySQL server version is `8.0.0`. Override it if needed:

```bash
Database__MySqlServerVersion=8.0.36
```

With the included Docker Compose MySQL profile:

```bash
docker compose --profile mysql up -d --build
```

---

## API Overview

Most endpoints require a JWT token, except setup status, first-run setup, and health check.

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/api/auth/login` | Login and return JWT |
| `POST` | `/api/auth/change-password` | Change password |
| `GET` | `/api/auth/setup-status` | Check whether first-run setup is required |
| `POST` | `/api/auth/setup` | Create the first administrator account |
| `GET` | `/api/proxies` | List all tunnels with runtime status |
| `POST` | `/api/proxies` | Create a tunnel |
| `PUT` | `/api/proxies/{id}` | Update a tunnel |
| `DELETE` | `/api/proxies/{id}` | Delete a tunnel |
| `PUT` | `/api/proxies/{id}/enable` | Enable a tunnel and reload frpc |
| `PUT` | `/api/proxies/{id}/disable` | Disable a tunnel and reload frpc |
| `POST` | `/api/proxies/sync` | Sync tunnels from current frpc config |
| `GET` | `/api/config` | Get frpc server config |
| `PUT` | `/api/config` | Update frpc server config and reload |
| `GET` | `/api/config/status` | Get live frpc tunnel status |
| `POST` | `/api/config/reload` | Manually trigger frpc reload |
| `POST` | `/api/wake-on-lan` | Send a Wake-on-LAN magic packet |
| `GET` | `/api/wake-on-lan/logs` | List Wake-on-LAN records |
| `POST` | `/api/wake-on-lan/logs/{id}/wake` | Wake again from a history record |
| `GET` | `/api/wake-on-lan/schedules` | List scheduled wake tasks |
| `POST` | `/api/wake-on-lan/schedules` | Create a scheduled wake task |
| `PUT` | `/api/wake-on-lan/schedules/{id}` | Update a scheduled wake task |
| `DELETE` | `/api/wake-on-lan/schedules/{id}` | Delete a scheduled wake task |
| `POST` | `/api/wake-on-lan/schedules/{id}/wake` | Run a scheduled wake task immediately |
| `GET` | `/api/audit-logs` | List audit logs |
| `GET` | `/api/backup` | Export tunnel and frpc config backup |
| `POST` | `/api/backup/restore` | Restore configuration backup |
| `GET` | `/api/health` | Health check |

---

## Docker Deployment

Build and run locally:

```bash
docker build -t frpc-manager .

docker run -d \
  --name frpc-manager \
  -p 6887:6887 \
  -p 6888:6888 \
  -v frpc-manager-data:/app/data \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/frpcmanager.db" \
  frpc-manager
```

Docker Hub image:

```bash
docker run -d \
  --name frpc-manager \
  -p 6887:6887 \
  -p 6888:6888 \
  -v frpc-manager-data:/app/data \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/frpcmanager.db" \
  fengzhengjin/frpc-manager:latest
```

When frpc runs on the Docker host, the image defaults to:

```bash
Frpc__WebServerAddr=host.docker.internal
Frpc__WebServerPort=7400
```

If your Docker environment does not support `host.docker.internal`, set the host IP manually:

```bash
-e Frpc__WebServerAddr="192.168.1.10" \
-e Frpc__WebServerPort="7400"
```

You can also override the full frpc API URL:

```bash
-e Frpc__ApiBaseUrl="http://192.168.1.10:7400"
```

### Docker Compose

```bash
docker compose up -d --build
```

Default exposed ports:

| Host Port | Service |
| --- | --- |
| `6887` | HTTP |
| `6888` | HTTPS |

Data is persisted in the `frpc-manager-data` Docker volume.

---

## Publish Scripts

### Windows

```powershell
# Pull latest code, build frontend, publish backend, and start
.\start-publish.bat

# Skip git pull
.\start-publish.bat --no-pull
```

### Linux

```bash
chmod +x start-publish-linux.bat

# Pull latest code, build frontend, publish backend, and start
./start-publish-linux.bat

# Skip git pull
./start-publish-linux.bat --no-pull
```

### Build and Push Docker Image

Windows:

```powershell
.\docker-build-push.bat
```

Linux:

```bash
chmod +x docker-build-push-linux.sh
./docker-build-push-linux.sh
```

The Docker Hub username must be a Docker ID / namespace, not an email address. For example, use `fengzhengjin/frpc-manager:latest`, not `fengzhengjin@example.com/frpc-manager:latest`.

### Update Docker Hub Description

Edit `dockerhub-description.md`, then run:

```powershell
.\update-dockerhub-description.ps1
```

or:

```bash
chmod +x update-dockerhub-description.sh
./update-dockerhub-description.sh
```

The script asks for Docker Hub username, namespace, repository name, and access token. The token is used only for the API call and is not written to project files.

---

## Security Notes

- No default admin password is built in.
- Use a strong `Admin__Password` for unattended deployments.
- Do not use example passwords or tokens in production.
- Persist `/app/data` with a Docker volume.
- Do not bake real secrets, database files, or production certificates into Docker images.
- For public HTTPS, use a reverse proxy such as Caddy, Nginx, or Traefik with a trusted certificate.
- The built-in HTTPS endpoint is mainly intended for private/self-hosted deployments.

---

## Open Source and Security

- License: this project is licensed under the [MIT License](LICENSE).
- Security issues: do not report vulnerability details in public issues. See [SECURITY.md](SECURITY.md).
- Contributing: read [CONTRIBUTING.md](CONTRIBUTING.md) before opening pull requests.
- Changelog: notable changes are tracked in [CHANGELOG.md](CHANGELOG.md).

---

## License

[MIT](LICENSE)
