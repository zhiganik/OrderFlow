# Order

Owns the order lifecycle: accepts order creation, persists orders, and tracks
their fulfillment status by reacting to events from Inventory.

## Domain model

- `Order` — `Id`, `CustomerId`, `Status`, `RejectionReason` (nullable),
  `CreatedAt`, `UpdatedAt`, `Items`. Created via `Order.Create(customerId,
  items)`; mutated via `MarkReserved()` / `MarkRejected(reason)`. Private
  setters — state only changes through these methods.
- `OrderItem` — `Id`, `OrderId`, `ProductName`, `Quantity`.
- `IdempotencyKey` — `Key` (PK), `RequestHash`, `ResponseStatusCode`,
  `ResponseBody`, `CreatedAt`.

### State machine

```
Pending --(InventoryReservedEvent consumed)--> Reserved
Pending --(InventoryRejectedEvent consumed)--> Rejected (RejectionReason set)
```

No transitions exist out of `Reserved` or `Rejected` — both are terminal.

## Endpoints (`OrdersController`, `[Authorize]`)

| Method | Route | Purpose |
|---|---|---|
| GET | `orders` | Paginated list (`page`, `pageSize`, clamped 1–100). Admins see all orders; customers see only their own. |
| GET | `orders/{id:guid}` | Fetch one order. 404 if missing or if a non-admin requests someone else's order. |
| POST | `orders` | Create an order. Requires `Idempotency-Key` header (`[RequireIdempotencyKey]`); replaying the same key + body returns the cached response, same key + different body returns 409. |

## OrderService

- `CreateOrderAsync` — idempotency check (SHA256 hash of normalized
  customerId+items), creates the `Order`, publishes `OrderCreatedEvent`, saves
  order + outbox message in one `SaveChanges` call.
- `GetOrdersByCustomerAsync` / `GetAllOrdersAsync` — paginated, mapped to
  `OrderResponse`.
- `GetOrderByIdAsync` — single lookup.
- `MarkReservedAsync` / `MarkRejectedAsync` — called from the two consumers
  below; logs and no-ops if the order isn't found.

## Messaging

- **Publishes** `OrderCreatedEvent(OrderId, CustomerId, Items[ProductName,
  Quantity], CreatedAt)` from `OrderService.CreateOrderAsync`, in the same
  transaction as the order insert.
- **Consumes** `InventoryReservedEvent` (`InventoryReservedConsumer` →
  `MarkReservedAsync`) and `InventoryRejectedEvent`
  (`InventoryRejectedConsumer` → `MarkRejectedAsync`).
- Dev transport: RabbitMQ (`RabbitMqOptions`, config section `RabbitMq`),
  conventional queue naming via `cfg.ConfigureEndpoints(context)`. Non-dev:
  Azure Service Bus (`ServiceBusOptions`), same endpoint convention.

## Persistence

- `OrderDbContext`, schema `order`. Tables: `Orders`, `OrderItems`,
  `IdempotencyKeys`, plus MassTransit's outbox/inbox tables — registered in
  `OnModelCreating` via `AddInboxStateEntity()`, `AddOutboxMessageEntity()`,
  `AddOutboxStateEntity()`.
- `OrderConfiguration`: `Status` stored as string; index on
  `(CustomerId, CreatedAt)` and on `CreatedAt`.
- `OrderRepository`: `GetPagedByCustomerIdAsync` / `GetAllPagedAsync`
  (`AsNoTracking`, includes items), `GetByIdAsync` (read), `FindByIdAsync`
  (tracked, used for status-transition writes).

## Auth

`[Authorize]` (header-based, via the shared `HeaderAuthenticationHandler` — see
[shared.md](shared.md)), no named policy. Admin-vs-customer scoping is done
in-code (`ICurrentUser.IsAdmin`), not via `[Authorize(Policy = ...)]`.
