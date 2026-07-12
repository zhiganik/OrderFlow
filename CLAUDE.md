# OrderFlow — conventions for Claude Code

## Structure
Monorepo. Each service under services/{name}/ has four projects: Api, Application,
Infrastructure, Tests. Reference direction is fixed:
Infrastructure -> Application -> Contracts (Order/Inventory only)
Api -> Application and Infrastructure (both)
Tests -> Application
Never reverse this — Application must never reference Infrastructure.

## Folders
Application: Domain/, Dtos/, Interfaces/, Services/, Validators/
Infrastructure: Persistence/, Repositories/, Messaging/ (Order & Inventory only,
producer/consumer classes — Order also consumes the inventory result event back).
No hand-rolled Outbox/Inbox folders: MassTransit's `AddEntityFrameworkOutbox` covers
both, via `AddInboxStateEntity()`/`AddOutboxMessageEntity()`/`AddOutboxStateEntity()`
called in each service's `DbContext.OnModelCreating`.

## Program.cs
Two-stage Serilog init with try/catch/finally around host build and run (bootstrap
logger catches startup failures before DI exists). Program.cs itself only calls
builder.ConfigureDependencies() and app.ConfigureApplication() — no inline
services.Add... or middleware calls belong in Program.cs directly.

## DI split
DependencyConfig.cs: every services.Add... call, IOptions<T> bindings, DbContext,
repository registrations, FluentValidation, JWT auth, Swagger.
ApplicationConfig.cs: middleware pipeline only, in order — exception handling,
authentication, authorization, Swagger UI, MapControllers.

## Config
IOptions<T> everywhere in Application/Infrastructure code. IConfiguration is only
ever touched inside DependencyConfig.cs to bind a section — never injected directly
into a service or repository.

## Contracts
contracts/OrderFlow.Contracts holds only cross-service event DTOs
(OrderCreatedEvent, InventoryReservedEvent, InventoryRejectedEvent). No shared
domain logic. Referenced by Application layer (not Infrastructure) since deciding
to raise an event is a business decision.

## Shared project
services/shared/OrderFlow.Shared holds cross-service Api-layer plumbing that
would otherwise be duplicated verbatim: GlobalExceptionHandler, the Swagger
Bearer-scheme setup, and header-based auth (AuthPolicies, ForwardedHeaders,
HeaderAuthenticationHandler, ICurrentUser). Reference direction: **Api ->
Shared only** — Application and Infrastructure never reference it, and Shared
never references any service project. Gateway does real JWT bearer auth and
forwards X-User-Id/X-User-Email/X-User-Roles downstream; Order/Inventory Apis
trust those headers via Shared's HeaderAuthenticationHandler and use plain
[Authorize]/[Authorize(Policy = AuthPolicies.Admin)] instead of parsing
headers by hand.

## Repository pattern
IRepository interfaces live in Application/Interfaces/, implementations in
Infrastructure/Repositories/. Each repository exposes only the queries its service
actually needs — no generic catch-all repository.

## Data
Fluent API entity configuration in Infrastructure/Persistence/Configurations/, one
class per entity. Index every foreign key and every column used in a WHERE or
ORDER BY on a table expected to grow.

## Testing
NUnit. One Tests project per service. Application-layer logic (services,
validators) gets unit tests.

## Secrets
Never in appsettings.json. .env locally (git-ignored), .env.example committed as
the template. Key Vault + Managed Identity in Azure.

## Logging
Serilog, structured (no string concatenation), console sink. Correlation ID
enriched on every log line.
