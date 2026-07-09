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
| `Makefile` | Shortcuts for the commands you'll run every day (`make up`, `make logs-order`, `make sql-shell`, ...). Run `make help` to list them. |

Code changes made alongside the infra:
- `services/gateway/Gateway/Gateway.csproj` — added `Yarp.ReverseProxy`.
- `services/gateway/Gateway/appsettings.json` — added a `ReverseProxy` section (routes + clusters).
- `services/gateway/Gateway/DependencyConfig.cs` / `ApplicationConfig.cs` — wired up YARP, removed the auth TODOs (see [Gateway auth](#does-the-gateway-need-useauthenticationuseauthorization) below).
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

No — and the TODOs for that were removed from `Gateway/ApplicationConfig.cs` and
`DependencyConfig.cs`. Reasoning: `Identity.Api`, `Order.Api`, and `Inventory.Api`
*already* each reference `Microsoft.AspNetCore.Authentication.JwtBearer` — that
package reference predates this Docker work, meaning the architecture was already
implicitly decided as **decentralized validation**: each domain service validates
the JWT itself on every request, independent of how the request arrived. `Gateway`
was never given a `JwtBearer` reference, which is the other half of that same
decision.

Given that, YARP just forwards the `Authorization` header through unchanged (this
is YARP's default behavior — it proxies request headers as-is unless you configure a
transform to strip/change them), and the downstream service does the real check.
Calling `app.UseAuthentication()` in the gateway with no `AddAuthentication()`
configured wouldn't even be valid — there'd be no scheme registered for it to use.

This *is* a real architectural choice with a trade-off, worth knowing explicitly
rather than by accident:
- **Decentralized (what you have):** each service is independently secure even if
  something bypasses the gateway and hits it directly. Downside: the JWT validation
  config (signing key, issuer, audience) is duplicated in three places.
- **Centralized (gateway validates, forwards a trusted identity):** downstream
  services get simpler (no JWT logic at all, just trust an internal header), but
  they become *insecure if reachable directly* — the gateway becomes the only thing
  standing between the internet and an unauthenticated request, so you'd need strict
  network isolation (see the next section) to make that safe.

Nothing about the current code needs to change to keep decentralized validation —
that TODO removal just makes the intent explicit instead of leaving a misleading
"do this later" marker on work that already lives elsewhere.

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

To actually enforce it, in order of how far you want to take it:
1. **Locally, in `docker-compose.yml`:** delete the `ports:` block from
   `identity-api`/`order-api`/`inventory-api`. They'd still be reachable from
   `gateway` (and from each other) over the `orderflow` Docker network — compose
   networking doesn't need a published port for that — but nothing on your host
   machine could reach `localhost:8081` anymore. This is the direct equivalent of
   "only the gateway has a public port."
2. **In a real deployment:** this becomes a network-layer rule, not an app-layer
   one — a Kubernetes `NetworkPolicy`, an Azure NSG / private VNet, or simply not
   giving Identity/Order/Inventory a public IP/ingress at all, so the gateway is the
   only thing with a route in from outside.
3. **Belt-and-suspenders (optional, usually only for zero-trust requirements):** a
   shared secret header the gateway adds and downstream services require, or mTLS
   between gateway and services — worth it only if you don't fully trust your own
   network boundary.

I left the debug ports in place since you'll likely want them while building out the
DbContext/auth TODOs (much faster to `curl localhost:8082` directly than always
round-trip through the gateway while iterating on one service). Say the word if you'd
rather I remove them now and rely on `docker compose exec`/logs for local debugging
instead.
