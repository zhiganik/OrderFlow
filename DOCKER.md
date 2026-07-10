# Local Docker environment — what was built and why

This documents the local Docker setup added to the repo: one container per service,
a shared SQL Server, an Azure Service Bus emulator, Redis, and a Gateway that
reverse-proxies to the three domain services.

## File-by-file manifest

| File | Purpose |
|---|---|
| `services/{identity,order,inventory}/*.Api/Dockerfile` | Builds one image per API project. |
| `services/gateway/Gateway/Dockerfile` | Builds the Gateway image. |
| `docker-compose.yml` | Orchestrates all 8 containers, the network, and the volumes. |
| `.dockerignore` | Keeps `bin/`, `obj/`, `.git/` etc. out of the build context sent to the Docker daemon. |
| `.env` (git-ignored) | Real, generated secrets used by `docker-compose.yml` at runtime. Never committed. |
| `.env.example` (committed) | Template listing which secret keys exist, with empty values. Copy to `.env` and fill in (or run `make env`). |
| `docker/servicebus/Config.json` | Defines the queues (`order-created`, `inventory-result`) the Azure Service Bus emulator creates on startup. |
| `Makefile` | Shortcuts for the commands you'll run every day (`make up`, `make logs-order`, `make sql-shell`, `make migrate-identity`, ...). Run `make help` to list them. |

Code changes made alongside the infra:
- `services/gateway/Gateway/Gateway.csproj` — added `Yarp.ReverseProxy`, `Microsoft.AspNetCore.Authentication.JwtBearer`.
- `services/gateway/Gateway/appsettings.json` — `ReverseProxy` section (routes + clusters + per-route `AuthorizationPolicy`) and a `Jwt` section.
- `services/gateway/Gateway/DependencyConfig.cs` / `ApplicationConfig.cs` — JWT validation, YARP request transform for identity forwarding, Swagger dropdown (see [Gateway auth](#does-the-gateway-need-useauthenticationuseauthorization) below).
- `services/identity/**` — full register/login/refresh/logout flow: `Identity.Application` (ASP.NET Core Identity's `ApplicationUser`, `AuthService`, `IRefreshTokenStore`), `Identity.Infrastructure` (`ApplicationIdentityDbContext`, `RedisRefreshTokenStore`, the `InitialIdentitySchema` migration), `Identity.Api` (`AuthController`, DI wiring).
- `services/order/**`, `services/inventory/**` — one placeholder `[Authorize]`-free route each (`GET /orders`, `GET /inventory-items`) reading the gateway-forwarded `X-User-Id` header.
- `services/{identity,order,inventory}/*.Api/*.csproj` + `DependencyConfig.cs` — added `Microsoft.Extensions.Caching.StackExchangeRedis` and registered `IDistributedCache` against the `Redis` connection string.

---

## How the pieces fit together

**Dockerfile vs docker-compose.yml — different jobs.** A `Dockerfile` is a recipe for
building one **image** (a filesystem + a startup command). `docker-compose.yml` takes
several images (some built from local Dockerfiles, some pulled from a registry like
`mcr.microsoft.com/...`) and runs them together as **containers** on a shared virtual
network, with the environment variables, ports, and startup order they need to talk
to each other. One Dockerfile per service is correct here — each service is a
different image with different dependencies — and one `docker-compose.yml` at the
root is what ties them into one runnable system.

**Build context.** Every service's Dockerfile is built with `context: .` (the repo
root), not the service's own folder:

```yaml
identity-api:
  build:
    context: .
    dockerfile: services/identity/Identity.Api/Dockerfile
```

This is required, not a style choice: `Identity.Api` references `Identity.Application`
and `Identity.Infrastructure` via `ProjectReference`, and `Order`/`Inventory.Application`
also reference `OrderFlow.Contracts`. `docker build` can only see files inside its
context, so the context has to be wide enough to contain every project a `dotnet
restore`/`publish` will walk into. Each Dockerfile's `build` stage does `COPY . .`
(copy the whole context) then `dotnet publish` on just its own `.csproj` — simplest
correct option, at the cost of Docker not being able to cache-skip unchanged
projects as precisely as a hand-listed multi-`COPY` Dockerfile would. Fine trade-off
for local dev; would reconsider for a CI pipeline optimizing rebuild time.

**Two-stage image (`build` → `final`).** The `build` stage uses the full SDK image
(~700MB+, has the compiler) to publish the app. The `final` stage starts fresh from
the much smaller ASP.NET **runtime** image and only `COPY --from=build`s the
published output. Net effect: the image you actually run doesn't carry the compiler,
NuGet cache, or source code — just the compiled app.

**`ENV ASPNETCORE_URLS=http://+:8080`** — this is where/why: Kestrel (the ASP.NET
Core web server baked into each app) needs to know which address and port to bind
to *inside the container*. Without it, recent ASP.NET Core defaults to `http://+:8080`
already in container images, but setting it explicitly in the `base` stage makes it
unambiguous and version-proof, and it's what `EXPOSE 8080` and every port mapping in
`docker-compose.yml` (`"8081:8080"`, `"8080:8080"`, etc.) assume on the other end —
`HOST_PORT:8080`. If this didn't match what Kestrel actually binds to, the container
would be running but nothing would answer on the mapped port. It's set once per
Dockerfile because each container is a separate process with its own network
namespace — there's no global "the app's port," each one independently binds 8080
*inside its own container*, and compose's port mapping is what makes them land on
different host ports (8080/8081/8082/8083) so they don't collide on your machine.

**Networking.** `docker-compose.yml` declares one bridge network (`orderflow`) and
every service joins it. Compose gives each container a DNS entry equal to its
service name — that's why `order-api`'s connection string can say `Server=sqlserver`
and `ServiceBus__ConnectionString` can say `Endpoint=sb://sb-emulator`: those
hostnames only resolve *inside* that Docker network, not from your host machine
(hence `Server=sqlserver` in the container env, but `localhost:1433` if you connect
from a SQL client running on Windows directly, since `1433:1433` is also published
to the host).

**`depends_on` + healthchecks.** `depends_on: sqlserver: condition: service_healthy`
means "don't even start this container until sqlserver's healthcheck passes," not
just "start sqlserver first." That's why `sqlserver` and `redis` have a `healthcheck:`
block (actually runs `sqlcmd`/`redis-cli` inside the container on an interval) — a
container can report `Up` long before the database engine inside it is actually
ready to accept connections, and API containers crash-looping on startup if they
try to connect too early is the classic failure mode this avoids.

---

## What is Azure SQL Edge, and why is it here if the app uses SQL Server?

`sqledge` isn't used by Identity/Order/Inventory — it exists purely as a dependency
of `sb-emulator`. The Azure Service Bus emulator needs somewhere to durably store
its internal state (queues, messages, delivery counts) and Microsoft's official
emulator image is built to use a SQL Server–compatible engine for that, pointed at
via the `SQL_SERVER` env var. Azure SQL Edge is Microsoft's lightweight,
multi-architecture (works on ARM too, e.g. Apple Silicon) SQL Server–compatible
engine, and it's what Microsoft's own emulator docs use as that companion
database. You will never connect to `sqledge` yourself — it's private plumbing for
`sb-emulator`, which is why it has no `ports:` mapping to the host.

## The `sa` user — does this create a new user?

No. `sa` ("system administrator") is SQL Server's **built-in** login — it exists in
every SQL Server installation/image before you touch anything. `MSSQL_SA_PASSWORD`
is not "create a user with this password," it's "set the password on the sa login
that already exists," which the official `mcr.microsoft.com/mssql/server` image does
automatically the first time the container starts with `ACCEPT_EULA=Y` set. The
connection string `User Id=sa;Password=...` then just authenticates as that account —
same idea as any username/password login, nothing container-specific about it.

Using `sa` for all three services is a reasonable local-dev shortcut (one password,
one account, works immediately) but **not** what you'd want in production: `sa` is a
full admin, so any one compromised service could touch every other service's
database. For production the standard pattern is one SQL login *per service*,
scoped to only that service's database (`CREATE LOGIN order_svc ...` +
`GRANT`/`db_owner` on `OrderDb` only) — worth doing whenever the DbContext/migration
work in `DependencyConfig.cs`'s TODOs actually lands, not something this Docker setup
needed to solve.

## How the Azure Service Bus emulator works

It's a real (scaled-down) implementation of the Service Bus protocol running
locally, not a mock — your app talks to it with the exact same
`Azure.Messaging.ServiceBus` SDK and connection-string shape it would use against
real Azure. Three moving parts:

1. **`docker/servicebus/Config.json`** — declares the namespace and its queues
   (`order-created`, `inventory-result`) up front. Unlike real Azure, you can't
   create queues via API calls at runtime against the emulator in this setup — they
   must exist in this file before the container starts (you saw this in the logs:
   `Creating queue: order-created` / `inventory-result` at boot).
2. **`sqledge`** — where the emulator persists queue/message state (see above).
3. **The well-known dev connection string** —
   `Endpoint=sb://sb-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`.
   `SAS_KEY_VALUE` is a literal placeholder string, not a real secret — the emulator
   doesn't check it, `UseDevelopmentEmulator=true` tells the SDK to skip real Azure
   auth entirely. `sb-emulator` in the endpoint is the container's DNS name on the
   `orderflow` network, exactly like `sqlserver` and `redis`.

## RabbitMQ vs Azure Service Bus

| | RabbitMQ | Azure Service Bus |
|---|---|---|
| Model | AMQP broker, you design topology (exchanges, bindings, queues) yourself | Managed PaaS with opinionated primitives: queues, topics + **subscriptions** (each subscriber gets its own durable copy), sessions, scheduled messages |
| Where it runs | Anywhere — a single lightweight container, or self-hosted in any cloud/on-prem | Azure-only in production; **locally you only get it via this emulator**, there's no "run it anywhere" story like RabbitMQ |
| Delivery guarantees | At-least-once, you build retry/dead-lettering yourself (or via plugins) | At-least-once built in: automatic retries, dead-lettering, duplicate detection, sessions — all configurable per-queue (see the `Properties` in `Config.json`) |
| SDK | `RabbitMQ.Client` (or MassTransit/NServiceBus on top) | `Azure.Messaging.ServiceBus` |
| Cost model | Free/self-hosted, or pay for hosting infra | Pay-per-operation/tier in Azure |

This repo already committed to Azure Service Bus (the `Azure.Messaging.ServiceBus`
package was referenced in `Order.Infrastructure`/`Inventory.Infrastructure` before
this Docker work started) — presumably because the target deployment is Azure, where
you'd rather use the managed service than run/operate your own RabbitMQ cluster.
The trade-off you're accepting locally is the emulator (heavier: needs its own SQL
Edge companion, config file, less flexible) vs. how trivially a single `rabbitmq:
3-management` container would have dropped in. If this project ever *doesn't* end up
deployed to Azure, swapping to RabbitMQ would mean changing the Infrastructure/Messaging
implementation, not just the Docker layer.

---

## Does the Gateway need `UseAuthentication()`/`UseAuthorization()`?

**Yes — this changed.** The section below originally argued for decentralized
validation (each service validates its own JWT). After removing the published
host ports on identity-api/order-api/inventory-api (see the last section), you
asked for auth to be centralized at the Gateway instead, specifically to avoid
repeating JWT config in three places. That's what's actually implemented now:

- **Gateway** (`Gateway/DependencyConfig.cs`): `AddAuthentication().AddJwtBearer(...)` +
  `AddAuthorization()`. This is the *only* place `Jwt__SigningKey` is validated.
- **`appsettings.json`'s `ReverseProxy.Routes`**: `order-route`/`inventory-route` carry
  `"AuthorizationPolicy": "Default"` — YARP's built-in "require an authenticated user"
  policy — so an invalid/missing token gets **401 straight from the gateway**, the
  request never reaches `order-api`/`inventory-api` at all. `identity-route` has no
  policy: register/login/refresh can't require a token you don't have yet.
- **Gotcha we hit and fixed**: that blanket policy on `/order/{**catch-all}` also
  caught `/order/swagger/**`, breaking the gateway's own Swagger dropdown (401 on the
  spec fetch). Fixed with a more specific `order-swagger-route`/`inventory-swagger-route`
  (`"Order": 0`, no auth policy) matched before the general routes (`"Order": 10`) —
  YARP evaluates lower `Order` values first.
- **Identity forwarding**: implemented as a YARP request transform
  (`AddReverseProxy().AddTransforms(...)` in `Gateway/DependencyConfig.cs`), not a custom
  ASP.NET Core middleware — this only runs for requests YARP actually proxies, rather
  than every request the gateway receives, and it's the idiomatic place to mutate the
  outgoing proxied request. It copies the validated `sub`/`email` claims onto
  `X-User-Id`/`X-User-Email` on `transformContext.ProxyRequest`, after first stripping
  any client-supplied values of those same headers (so a caller can never inject a fake
  `X-User-Id` itself). `OrdersController`/`InventoryItemsController` just read
  `Request.Headers["X-User-Id"]` — no JWT package, no `[Authorize]`, nothing auth-related
  in either service at all anymore (the `JwtBearer` package was removed from both `.csproj`s).

The trade-off is the same one described below, just now the choice actually made:
Order/Inventory are **fully unauthenticated if ever reached directly** — safe only
because nothing but the gateway can reach them (`expose:`, not `ports:`). That's a
real, common pattern (API gateway / BFF doing edge auth, internal services trusting
the network), not a shortcut — but if this ever deploys somewhere the network
guarantee is weaker (an Ingress accidentally pointed at a service directly, a shared
VPC without a `NetworkPolicy`), those services would have zero protection of their
own. Worth remembering if defense-in-depth ever becomes a requirement.

## Validation: auto-validation instead of calling validators by hand

`AuthController` used to inject `IValidator<RegisterRequest>`/`IValidator<LoginRequest>`
and call `ValidateAsync` itself before doing anything — repeated in every action, easy
to forget in a new one. Replaced with automatic validation:
`SharpGrip.FluentValidation.AutoValidation.Mvc` (the community-maintained successor to
`FluentValidation.AspNetCore`, which FluentValidation's own maintainers archived) plus
one line in `Identity.Api/DependencyConfig.cs` —
`builder.Services.AddFluentValidationAutoValidation();`. It hooks in as an MVC action
filter: any FluentValidation validator already registered via
`AddValidatorsFromAssemblyContaining<...>()` runs automatically against the matching
action parameter before the action body executes, and a validation failure short-circuits
straight to a `400 ValidationProblemDetails` — the controller action never runs at all.
Verified: `POST /identity/register` with `{"email":"not-an-email","password":"short"}`
returns 400 with per-field messages, with zero validation code in the controller.

## Error handling: a Result type instead of try/catch in controllers

`AuthService` used to throw a custom `AuthException` for expected failures (wrong
password, expired refresh token, ...), which `AuthController` then had to catch in
every single action just to turn it into the right status code — try/catch as
boilerplate, repeated for something that isn't actually exceptional. Wrong
credentials and an expired refresh token are normal, expected outcomes of calling
those endpoints, not bugs — exceptions should be reserved for the unexpected
(a downed database, a bug), which the framework already surfaces as a 500 with no
extra code needed.

Replaced with `Identity.Application/Dtos/AuthOutcome.cs`, a small `Succeeded`/`Result`/
`Error` wrapper. `AuthService.RegisterAsync`/`LoginAsync`/`RefreshAsync` now return
`Task<AuthOutcome>` and represent failure as data (`AuthOutcome.Failure("...")`)
instead of throwing. `AuthController` just branches on `outcome.Succeeded` — no
try/catch anywhere in the auth flow, and none needed: `UserManager.CreateAsync`/
`CheckPasswordAsync`, `IRefreshTokenStore.GetUserIdAsync`, etc. already report expected
failures as return values (`IdentityResult.Succeeded`, `false`, `null`) rather than
exceptions, so there was never anything to catch in the first place once the outcome
is threaded through properly.

## Migrations are never applied automatically

The first version of this called `dbContext.Database.Migrate()` at the top of
`Identity.Api/ApplicationConfig.cs`, so every container start would silently apply any
pending migration. **Removed** — auto-migrating on startup is a real production
hazard (multiple replicas racing to migrate simultaneously, an untested schema change
shipping the moment a container restarts, no review gate between "merged" and
"applied to the database"), not just a local-dev nicety to skip.

Migrations are now a deliberate, manual step: `make migrate-identity` runs
`dotnet ef database update` from your host machine against the SQL Server container's
published port (`localhost,1433`), using the same `IdentityDb` + `sa` credentials the
container itself uses. Verified: on a fresh `make down-v && make up`, `POST
/identity/register` fails with a SQL "Cannot open database" error (no `AspNetUsers`
table exists yet); running `make migrate-identity` fixes it, and register/login work
immediately after with no container restart needed.

## Is `appsettings.json` the right place for the `ReverseProxy` config?

Yes, for this shape of project. `builder.Services.AddReverseProxy().LoadFromConfig(...)`
is literally YARP's documented standard pattern — it also gets you a config-reload
watcher for free (edit `appsettings.json`, YARP picks up route/cluster changes
without a restart, in non-container dev). It's also consistent with this repo's own
rule ("IConfiguration only touched inside `DependencyConfig.cs`") — `LoadFromConfig`
is the one call that touches `IConfiguration` directly, same as any other config
binding.

Two things worth doing as this grows, not needed now:
- If the route/cluster list gets long, split it into its own `appsettings.ReverseProxy.json`
  loaded via `builder.Configuration.AddJsonFile(...)` purely for file-size/readability —
  same mechanism, just organized differently.
- The cluster destinations are hardcoded container DNS names
  (`http://identity-api:8080`). That's correct for Docker Compose. If this ever moves
  to Kubernetes or Azure Container Apps, you'd swap that for real service discovery
  (K8s DNS/Service objects, or YARP's `Microsoft.Extensions.ServiceDiscovery`
  integration) rather than hand-maintaining addresses per environment.

## How do we make sure requests actually go through the Gateway?

Right now, nothing stops you from bypassing it — `docker-compose.yml` publishes
`8081/8082/8083` straight to `identity-api`/`order-api`/`inventory-api` in addition to
the gateway's `8080`. That was a deliberate choice for this task (I used it to
`curl` each service directly and confirm the gateway was really proxying, rather than
just trusting it), but it means the "everything goes through the gateway" rule
currently only exists in your head, not in the compose file.

**Done.** `identity-api`/`order-api`/`inventory-api` no longer have a `ports:` block —
each now has `expose: ["8080"]` instead, which documents that port 8080 exists
without publishing it to the host. `docker compose ps` now shows `8080/tcp` for
those three (container-internal only) instead of `0.0.0.0:8081->8080/tcp`. They're
still fully reachable from `gateway` (and from each other) over the `orderflow`
Docker network — Compose networking never needed a published port for that, only
your host machine did. Verified: `curl localhost:8081` now fails to connect, while
`curl localhost:8080/identity/...` through the gateway still returns 200.

For a real deployment this same idea becomes a network-layer rule, not app config —
a Kubernetes `NetworkPolicy`, an Azure NSG / private VNet, or simply not giving
Identity/Order/Inventory a public IP/ingress at all, so the gateway is the only thing
with a route in from outside. Belt-and-suspenders on top of that (only worth it for
zero-trust requirements): a shared secret header the gateway adds and downstream
services require, or mTLS between gateway and services.

To debug a service directly now that its port isn't published: `docker compose exec
order-api curl localhost:8080/swagger/index.html` (curl *from inside* the container),
or `make logs-order`, or temporarily add back a `ports:` line while you're actively
working on that one service.
