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
- Use administrator accounts only for trusted operators. Normal users can view all tunnels and HTTPS proxy rules, but can manage only resources they created.
- Existing resources without an owner are treated as legacy resources and should be reviewed by an administrator after upgrade.
- Configuration backups can include usernames, roles, disabled states, tunnel owner usernames, and HTTPS proxy owner usernames, but must not include user passwords or password hashes.
- Sign out and sign in again after upgrading from a version without roles so the browser stores a token with role claims.
- Persist `/app/data` with a Docker volume.
- Keep the frpc Web API private and avoid exposing it directly to the internet.
- Use a trusted reverse proxy certificate for public HTTPS.
- Rotate any secret that may have been committed, logged, or shared.
