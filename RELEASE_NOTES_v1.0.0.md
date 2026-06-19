# FrpC Manager v1.0.0

First public release of FrpC Manager, a web management platform for frpc tunnels.

## Highlights

- Web UI for managing TCP and UDP frpc tunnels
- Enable, disable, create, edit, delete, search, and filter tunnels
- Sync existing tunnel configuration from frpc
- First-run setup wizard with no built-in default admin password
- JWT authentication and password change
- Audit logs for login, tunnel, config, backup, restore, and Wake-on-LAN actions
- Health check endpoint for database and frpc Web API status
- Configuration backup and restore
- Wake-on-LAN support
- SQLite by default, optional MySQL via environment variables
- Dockerfile and Docker Compose support
- HTTP on port `6887`, HTTPS on port `6888`
- English and Chinese documentation

## Docker Image

```bash
docker pull fengzhengjin/frpc-manager:latest
```

Run with SQLite:

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

## frpc Requirement

Make sure frpc has its Web API enabled:

```toml
webServer.addr = "127.0.0.1"
webServer.port = 7400
```

When running in Docker, configure the frpc Web API host if needed:

```bash
-e Frpc__WebServerAddr="host.docker.internal"
-e Frpc__WebServerPort="7400"
```

## Security Notes

- No default admin password is included.
- Create the first administrator account during setup.
- Do not use example passwords in production.
- Persist `/app/data` with a Docker volume.
- Keep the frpc Web API private.
- For public HTTPS, use a reverse proxy such as Caddy, Nginx, or Traefik with a trusted certificate.

## Source

- GitHub: https://github.com/fzj929/frpcManager
- Docker Hub: https://hub.docker.com/r/fengzhengjin/frpc-manager
