# FrpC Manager

FrpC Manager is a web management platform for frpc tunnels, built with Vue 3, ASP.NET Core 8, and SQLite/MySQL. It provides a browser UI for managing TCP/UDP tunnels, frpc configuration, startup setup, audit logs, health checks, Wake-on-LAN, and backup/restore.

## Features

- Manage TCP and UDP proxy tunnels from a web UI
- Enable or disable tunnels and reload frpc automatically
- Sync existing tunnels from the current frpc configuration
- First-run setup wizard, no built-in default admin password
- JWT authentication, password change, and administrator/user role control
- Resource ownership: normal users can view all tunnels and HTTPS proxy rules but manage only their own resources
- Runtime port conflict checks while allowing duplicate disabled configurations
- Audit logs for login, tunnel, config, backup, restore, and Wake-on-LAN actions
- Health check endpoint for database and frpc API status
- Backup and restore for tunnel and frpc configuration
- SQLite by default, optional MySQL through environment variables
- HTTP on port `6887`, HTTPS on port `6888`

## Quick Start

SQLite single-container deployment:

```bash
docker run -d \
  --name frpc-manager \
  -p 6887:6887 \
  -p 6888:6888 \
  -v frpc-manager-data:/app/data \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/frpcmanager.db" \
  fengzhengjin/frpc-manager:latest
```

Open:

- HTTP: `http://localhost:6887`
- HTTPS: `https://localhost:6888`

On first launch, open the web UI and create the first administrator account.

## Docker Compose

```bash
docker compose up -d
```

When frpc runs on the Docker host, the image defaults to:

```bash
Frpc__WebServerAddr=host.docker.internal
Frpc__WebServerPort=7400
```

For Linux hosts, you may need to set the host IP explicitly or use host networking depending on your Docker environment.

## MySQL Example

```bash
docker run -d \
  --name frpc-manager \
  -p 6887:6887 \
  -p 6888:6888 \
  -v frpc-manager-data:/app/data \
  -e Database__Provider=mysql \
  -e ConnectionStrings__MySql="Server=mysql-host;Port=3306;Database=frpcmanager;User=frpcmanager;Password=change-this-password;CharSet=utf8mb4;" \
  fengzhengjin/frpc-manager:latest
```

## Environment Variables

| Variable | Description |
| --- | --- |
| `Database__Provider` | `sqlite` by default, or `mysql` |
| `ConnectionStrings__DefaultConnection` | SQLite connection string or fallback database connection |
| `ConnectionStrings__MySql` | MySQL connection string |
| `Frpc__WebServerAddr` | frpc web API host |
| `Frpc__WebServerPort` | frpc web API port |
| `Frpc__ApiBaseUrl` | Full frpc API base URL, overrides addr/port |
| `Admin__Username` | Optional initial admin username |
| `Admin__Password` | Optional initial admin password |
| `Jwt__KeyFile` | JWT signing key file path |

## Notes

- Do not use the example passwords in production.
- Persist `/app/data` with a Docker volume.
- For public HTTPS, use a reverse proxy such as Caddy, Nginx, or Traefik with a trusted certificate.

## Source

GitHub: https://github.com/fzj929/frpcManager
