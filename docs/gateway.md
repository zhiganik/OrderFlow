# Gateway

YARP reverse proxy — the single public entry point. Validates JWTs and forwards
identity claims to downstream services as headers instead of letting each
service do its own JWT validation.

## Routes

Configured in `services/gateway/Gateway/appsettings.json` (`ReverseProxy`
section):

| Route | Path | Cluster | Auth |
|---|---|---|---|
| `identity-route` | `/identity/{**catch-all}` | `identity-api:8080` | none (register/login can't require a token you don't have) |
| `order-swagger-route` (Order 0) | `/order/swagger/{**catch-all}` | `order-api:8080` | none |
| `order-route` (Order 10) | `/order/{**catch-all}` | `order-api:8080` | `Default` (authenticated user required) |
| `inventory-swagger-route` (Order 0) | `/inventory/swagger/{**catch-all}` | `inventory-api:8080` | none |
| `inventory-route` (Order 10) | `/inventory/{**catch-all}` | `inventory-api:8080` | `Default` (authenticated user required) |

The swagger routes must be evaluated first (`"Order": 0` vs `"Order": 10` — YARP
evaluates lower values first). Without that, the blanket `Default` auth policy on
`order-route`/`inventory-route` also catches `/order/swagger/**`, breaking the
Gateway's own aggregated Swagger UI with a 401. All routes strip their own
prefix (`PathRemovePrefix`) before proxying.

## Auth

- `AddAuthentication().AddJwtBearer(...)` validates issuer, audience, lifetime,
  and signing key (`Jwt` config section: `Issuer`, `Audience`, `SigningKey`).
  This is the only place in the system a JWT is actually validated.
- A YARP request transform (`AddReverseProxy().AddTransforms(...)`) runs only
  for proxied requests: it strips any client-supplied `X-User-Id`,
  `X-User-Email`, `X-User-Roles` headers (so a caller can't spoof identity),
  then — if the request is authenticated — re-adds them from the validated
  JWT's claims (`sub` → `X-User-Id`, `email` → `X-User-Email`, role claims
  joined by comma → `X-User-Roles`).
- Downstream services never see the JWT itself, only these headers. See
  [shared.md](shared.md) for how they're trusted (`HeaderAuthenticationHandler`).

## Middleware pipeline (`ApplicationConfig.cs`)

```
UseExceptionHandler
UseStatusCodePages
UseSerilogRequestLogging   (enriches with RouteId/ClusterId/Destination/UserId)
UseHttpsRedirection
UseAuthentication
UseAuthorization
UseSwagger / UseSwaggerUI  (dev only — aggregates identity/order/inventory specs)
MapControllers
MapReverseProxy
```
