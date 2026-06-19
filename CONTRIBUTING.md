# Contributing

Thank you for considering a contribution to FrpC Manager.

## Development Setup

Requirements:

- .NET 8 SDK
- Node.js 18+
- frpc with the Web API enabled

Common commands:

```bash
dotnet restore backend/FrpcManager.Api/FrpcManager.Api.csproj
dotnet build backend/FrpcManager.Api/FrpcManager.Api.csproj --no-restore

cd frontend
npm ci
npm run build
```

## Pull Request Checklist

- Keep changes focused and avoid unrelated refactors.
- Update README files when behavior, configuration, ports, Docker usage, or environment variables change.
- Do not commit secrets, certificates, database files, `.env` files, or production configuration.
- Run backend and frontend builds before submitting.
- Mention any security-sensitive behavior in the pull request description.

## Security Issues

Do not open public issues for vulnerabilities. Follow [SECURITY.md](SECURITY.md).
