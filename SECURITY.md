# Security and privacy

## Public snapshot

This showcase intentionally excludes production data, credentials, personal transactions, private operational documentation, deployment secrets, logs, and database backups. Demo content is synthetic.

## Reporting a vulnerability

Please open a private GitHub security advisory for this repository. Do not include real credentials, personal data, or exploit output in a public issue.

## Security model

- Browser authentication uses ASP.NET Core Identity cookies; APIs can use JWT.
- Owner-scoped records are protected through query filters and explicit ownership checks.
- Demo data is read-only at the persistence boundary.
- Sensitive fields use authenticated encryption at rest.
- Device tokens are hashed and revocable.
- Login and token endpoints are rate-limited.
- Secrets are supplied through environment or host secret storage and must never be committed.

## Local development

Use synthetic data only. Keep PostgreSQL connections, encryption keys, JWT material, SMTP credentials, and provider tokens outside tracked files.

