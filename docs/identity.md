# Identity

Registers users, authenticates them, and issues JWTs + refresh tokens. It does
**not** validate tokens for other services — that happens once, at the
[Gateway](gateway.md). Identity has no messaging: it's the only service with no
MassTransit/RabbitMQ code at all.

## Domain model

- `ApplicationUser` — extends ASP.NET Core Identity's `IdentityUser` (Id,
  UserName, Email, PasswordHash, ...); no extra fields of its own.

## Endpoints (`AuthController`)

| Method | Route | Purpose |
|---|---|---|
| POST | `/register` | Create a user |
| POST | `/login` | Validate credentials, issue access + refresh token |
| POST | `/refresh` | Rotate a refresh token for a new access token |
| POST | `/logout` | Revoke a refresh token |

(Mounted under `/identity/...` by the Gateway.)

## AuthService

- Issues JWT access tokens: HMAC-SHA256, claims `sub`, `email`, `jti`, roles.
  Lifetime from `JwtOptions.AccessTokenMinutes` (15 by default).
- Issues opaque refresh tokens (32-byte random, base64), lifetime
  `JwtOptions.RefreshTokenDays` (7 by default).
- Represents expected failures (wrong password, expired refresh token) as an
  `AuthOutcome` result value, not exceptions — `AuthController` just branches on
  `outcome.Succeeded`.

## Persistence

- `ApplicationIdentityDbContext` (schema `identity`), SQL Server, connection
  string `DefaultConnection`. Standard ASP.NET Identity tables (`AspNetUsers`,
  `AspNetRoles`, `AspNetUserRoles`, ...).
- Refresh tokens live in **Redis**, not SQL: `RedisRefreshTokenStore` implements
  `IRefreshTokenStore` via `IDistributedCache`, storing refresh-token → userId
  with a TTL.

## Startup

- `RoleSeeder` (`IHostedService`) seeds the `Admin` role on startup. There is no
  role-assignment endpoint yet — granting Admin is a manual/`make` step (see
  `Makefile`'s `grant-admin` target).

## Validation

`RegisterRequestValidator` (email format, password ≥ 8 chars),
`LoginRequestValidator` (email format, non-empty password) — FluentValidation,
auto-run via `SharpGrip.FluentValidation.AutoValidation.Mvc`.
