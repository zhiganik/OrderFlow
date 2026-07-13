# Inventory

Owns stock levels. Admins manage stock directly through the API; stock is also
reserved or rejected automatically in reaction to `OrderCreatedEvent`.

## Domain model

- `StockItem` — `Id`, `ProductName` (unique), `QuantityAvailable`, `CreatedAt`.
  `Create(productName, qty)` validates non-empty name and `qty >= 0`.
  `Reserve(qty)` throws if `qty` exceeds what's available. `SetQuantity(qty)`
  overwrites the level directly (used by the admin upsert endpoint).

## Endpoints (`StockController`, `[Authorize(Policy = AuthPolicies.Admin)]`)

| Method | Route | Purpose |
|---|---|---|
| GET | `api/stock` | Paginated search (`page`, `pageSize`, optional `id`, optional `productName` substring filter) |
| POST | `api/stock` | Upsert by product name — updates quantity if it exists, creates it otherwise |

Every route on this controller requires the `Admin` role.

## Services

- `StockService` — the admin-facing CRUD/search behind the controller
  (`page`/`pageSize` clamped to 1–100).
- `StockReservationService` — the event-driven side: given an
  `OrderCreatedEvent`, checks every line item against current stock. If any
  item is missing or short, publishes `InventoryRejectedEvent` with a reason
  naming the product; if every item is available, decrements stock for all of
  them and publishes `InventoryReservedEvent`. All-or-nothing per order — a
  short one item rejects the whole order rather than partially reserving.

## Messaging

- **Consumes** `OrderCreatedEvent` via `OrderCreatedConsumer`, which delegates
  straight to `StockReservationService.ReserveOrRejectAsync`.
- **Publishes** `InventoryReservedEvent(OrderId, ReservedAt)` or
  `InventoryRejectedEvent(OrderId, Reason, RejectedAt)`.
- Dev transport: RabbitMQ (`RabbitMqOptions`, config section `RabbitMq`). Non-dev:
  Azure Service Bus (`ServiceBusOptions`). Same `ConfigureEndpoints` convention
  as Order.

## Persistence

- `InventoryDbContext`, schema `inventory`. Single table `StockItems` (unique
  index on `ProductName`, non-unique index on `CreatedAt`), plus MassTransit's
  outbox/inbox tables registered in `OnModelCreating`
  (`AddInboxStateEntity()`/`AddOutboxMessageEntity()`/`AddOutboxStateEntity()`).
- `StockItemRepository`: `FindByProductNameAsync`, `FindByProductNamesAsync`
  (bulk lookup used by reservation), `SearchAsync` (paginated, `AsNoTracking`).

## Seeding

`scripts/seed_inventory.py` (`make seed-inventory`) registers/logs in a seed
admin through the real Gateway, verifies the Admin role, then generates ~100
random product names and quantities and `POST`s each to `/inventory/api/stock`
— exercising the real validation/upsert path rather than writing to the
database directly.
