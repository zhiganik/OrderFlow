# OrderFlow — Architecture

OrderFlow is a small order-fulfillment system split into four services: a
**Gateway** that terminates auth, an **Identity** service that issues tokens, and
**Order**/**Inventory** services that coordinate stock reservation over events.
Everything runs behind one entry point; services talk to each other only through
RabbitMQ (or Azure Service Bus in non-dev environments), never by calling each
other's HTTP APIs directly.

## Services

| Service | Responsibility | Notes |
|---|---|---|
| [Gateway](gateway.md) | Single public entry point; JWT validation; reverse-proxies to the other three | YARP |
| [Identity](identity.md) | Register/login/refresh/logout; issues JWTs | ASP.NET Core Identity, SQL Server, Redis |
| [Order](order.md) | Order lifecycle: create, list, track status | SQL Server, publishes/consumes events |
| [Inventory](inventory.md) | Stock levels; reserves/rejects stock per order | SQL Server, consumes/publishes events |
| [Shared & Contracts](shared.md) | Cross-cutting Api plumbing + event DTOs | Referenced by every service |

Full local-Docker setup and infra rationale (SQL Server, Redis, RabbitMQ,
networking, why RabbitMQ instead of the Azure Service Bus emulator locally) is in
[`DOCKER.md`](../DOCKER.md) — not duplicated here.

Deploying this to Azure (Container Apps, Terraform, CI/CD) is covered
separately in [`infrastructure-as-code.md`](infrastructure-as-code.md).

## Solution structure

Each service under `services/{name}/` has four projects with a fixed reference
direction:

```
Infrastructure -> Application -> Contracts   (Order/Inventory only)
Api -> Application and Infrastructure (both)
Tests -> Application
```

`Application` never references `Infrastructure`. Within `Application`:
`Domain/`, `Dtos/`, `Interfaces/`, `Services/`, `Validators/`. Within
`Infrastructure`: `Persistence/`, `Repositories/`, and — for Order/Inventory only
— `Messaging/` (MassTransit producer/consumer classes).

## Request flow (synchronous, client-facing)

```
Client --JWT--> Gateway --validates JWT, forwards claims as headers--> Order / Inventory / Identity
```

1. Client sends a request with `Authorization: Bearer <jwt>` to the Gateway.
2. Gateway validates the JWT (issuer/audience/signing key) and, for a proxied
   request, strips any client-supplied `X-User-Id`/`X-User-Email`/`X-User-Roles`
   headers and re-adds them from the validated token's claims.
3. Order/Inventory never see or validate the JWT — they trust the forwarded
   headers via `HeaderAuthenticationHandler` (see [shared.md](shared.md)).
4. Order/Inventory containers have no published host port (`expose`, not
   `ports`, in `docker-compose.yml`) — the Gateway is the only way in from
   outside the Docker network.

Identity's own routes (`/identity/register`, `/login`, ...) require no token —
you can't authenticate with a token you don't have yet.

## Event flow (the core business flow)

```
POST /order/orders
      |
      v
  Order.Api  --insert Order(Pending) + OrderCreatedEvent, one transaction (EF outbox)-->
      |
      v
  RabbitMQ
      |
      v
  Inventory.Api  --OrderCreatedConsumer--> StockReservationService
      |                                        |
      |                    all items in stock? -+- yes: decrement stock, publish InventoryReservedEvent
      |                                          '- no:  publish InventoryRejectedEvent (with reason)
      v
  RabbitMQ
      |
      v
  Order.Api  --InventoryReservedConsumer / InventoryRejectedConsumer-->
      Order.MarkReserved() / Order.MarkRejected(reason)
```

Order status: `Pending` → `Reserved` or `Pending` → `Rejected` (both driven by
the flow above), plus one more transition the customer/admin can trigger
directly — `Reserved` → `Canceled` via `POST orders/{id}/cancel` — which
publishes `OrderCanceledEvent` so Inventory restocks those items. See
[order.md](order.md) and [the cancel-order spec](changes/cancel-order-feature.md)
for detail.

## Reliability

- **Transactional outbox/inbox** — every service that publishes or consumes
  events uses MassTransit's EF Core outbox (`AddEntityFrameworkOutbox`,
  `UseBusOutbox()`), with the outbox/inbox tables registered in the same
  `DbContext.OnModelCreating` as the domain tables (`AddInboxStateEntity()`,
  `AddOutboxMessageEntity()`, `AddOutboxStateEntity()`). This is what makes "save
  the order row" and "publish `OrderCreatedEvent`" a single atomic SQL
  transaction — if the publish fails, the insert rolls back too, and vice versa;
  there's no window where the DB and the broker disagree.
- **Idempotency keys** — `POST /orders` requires an `Idempotency-Key` header. A
  retried request with the same key and body replays the original response
  instead of creating a second order; the same key with a different body is
  rejected (409).

## Infra at a glance

| Component | Used by | For |
|---|---|---|
| SQL Server (one instance, per-service schema: `identity`/`order`/`inventory`) | all three domain services | domain data + outbox/inbox tables |
| Redis | Identity | refresh-token storage (`IDistributedCache`) |
| RabbitMQ (dev) / Azure Service Bus (non-dev) | Order, Inventory | `OrderCreatedEvent`, `InventoryReservedEvent`, `InventoryRejectedEvent`, `OrderCanceledEvent` |

See [`DOCKER.md`](../DOCKER.md) for why RabbitMQ is used locally instead of the
Azure Service Bus emulator, networking details, and the full container manifest.

## Auth model, summarized

JWTs are issued only by Identity and validated only by the Gateway. Downstream
services trust Gateway-forwarded headers instead of re-validating tokens. Detail
in [gateway.md](gateway.md) and [shared.md](shared.md).
