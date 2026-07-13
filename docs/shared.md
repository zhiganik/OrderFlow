# Shared & Contracts

Two small cross-cutting libraries referenced by every service — neither is
independently deployed.

## OrderFlow.Shared

`services/shared/OrderFlow.Shared` — Api-layer plumbing that would otherwise be
duplicated verbatim across Identity/Order/Inventory/Gateway.

| Piece | Purpose |
|---|---|
| `Middleware/GlobalExceptionHandler.cs` | `IExceptionHandler` — unhandled exceptions become an RFC7807 ProblemDetails 500, with logging |
| `Swagger/SwaggerGenOptionsExtensions.cs` | `AddBearerSecurity()` — adds the Bearer JWT scheme to Swashbuckle |
| `Auth/AuthPolicies.cs` | `Admin` policy/role name constant |
| `Auth/ForwardedHeaders.cs` | `X-User-Id` / `X-User-Email` / `X-User-Roles` header name constants |
| `Auth/HeaderAuthenticationHandler.cs` | `AuthenticationHandler` that builds a `ClaimsPrincipal` from the Gateway-forwarded headers — no token re-validation downstream |
| `Auth/HeaderAuthenticationServiceCollectionExtensions.cs` | `AddHeaderAuthentication()` — wires the handler, `ICurrentUser`, and the `Admin` authorization policy |
| `Auth/ICurrentUser.cs` / `CurrentUser.cs` | `UserId` / `Email` / `IsAdmin`, read from `HttpContext.User` claims |
| `Common/PagedResult.cs` | Generic `PagedResult<T>` (Items, Page, PageSize, TotalCount, TotalPages) |

Order and Inventory both register `AddHeaderAuthentication()` and trust
whatever `X-User-*` headers the Gateway forwards — see [gateway.md](gateway.md)
for how those headers get there in the first place.

## OrderFlow.Contracts

`services/contracts/OrderFlow.Contracts` — pure event DTOs (`sealed record`,
no behavior), the only thing Order and Inventory's `Application` layers share
across the message bus.

| Event | Fields | Published by | Consumed by |
|---|---|---|---|
| `OrderCreatedEvent` | `OrderId`, `CustomerId`, `Items: OrderCreatedItem[ProductName, Quantity]`, `CreatedAt` | Order | Inventory |
| `InventoryReservedEvent` | `OrderId`, `ReservedAt` | Inventory | Order |
| `InventoryRejectedEvent` | `OrderId`, `Reason`, `RejectedAt` | Inventory | Order |

## Reference direction

```
Api          -> Shared
Application  -> Shared (Common/ only), and -> Contracts (Order/Inventory only)
Infrastructure -> never references Shared
```

Deciding to raise an event is a business decision, so `Contracts` is referenced
by `Application`, not `Infrastructure` — even though the actual publish/consume
code lives in `Infrastructure/Messaging/`.
