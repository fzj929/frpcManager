# Security Policy

## Supported Versions

Security fixes are provided for the latest code on the `main` branch and the latest published Docker image tag.

## Reporting a Vulnerability

Please do not report security vulnerabilities through public GitHub issues.

Report security issues by contacting the maintainer privately through GitHub repository contact channels, or by creating a private security advisory if available.

When reporting a vulnerability, include:

- Affected version, commit, or Docker image tag
- Deployment mode, such as local publish, Docker, or Docker Compose
- Steps to reproduce
- Expected and actual impact
- Any relevant logs or screenshots with secrets removed

## Handling Secrets

Do not include real passwords, JWT keys, database connection strings, frpc tokens, certificates, private keys, `.env` files, or database files in reports or issues.

## Production Guidance

- Change all example passwords before deployment.
- Persist `/app/data` with a Docker volume.
- Keep the frpc Web API private and avoid exposing it directly to the internet.
- Use a trusted reverse proxy certificate for public HTTPS.
- Rotate any secret that may have been committed, logged, or shared.
