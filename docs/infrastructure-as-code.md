# Infrastructure as Code — Azure Deployment

This is the breakdown for taking OrderFlow from "runs locally via
`docker-compose`" to "runs in Azure, provisioned by Terraform, deployed by
GitHub Actions." It's written as a **learning path**, not a script to blindly
apply — each phase says what to do and why, with the manual `az`/Terraform
commands spelled out, so you're the one making the infra decisions and typing
them in.

Read [`architecture.md`](architecture.md) first if you haven't — this doc
assumes you know the four services and how they talk to each other.

## Decisions already made

| Question | Decision | Why |
|---|---|---|
| Frontend hosting | **Azure Static Web Apps** (Free tier) | Purpose-built for a Vite SPA — free, own CDN, free TLS, GitHub Actions integration generated for you. |
| Redis (refresh tokens) | **Dropped — refresh tokens move into SQL** | Removes an always-on cost with no scale-to-zero story of its own; small code change (below) is cheaper long-term than paying for Azure Cache for Redis. |
| Environments | **Single environment** | This is a personal/learning project — one environment keeps both cost and Terraform state manageable. |
| Terraform state | **`azurerm` backend (Storage Account)** | All-Azure, no third-party account, the pattern you'll see in every real Azure job. |
| Container images | **GitHub Container Registry (`ghcr.io`), public packages** | Free regardless of usage — unlike ACR's flat per-day fee. Public is fine here: images never contain secrets (Key Vault + managed identity handle those), only the app itself. |
| Messaging (pub/sub) | **CloudAMQP (managed RabbitMQ, free tier)** — no Azure Service Bus | Real pub/sub (exchanges + bindings, same model MassTransit already uses locally), free forever regardless of usage, and zero code change beyond pointing config at a different host — vs. Azure Service Bus Standard's ~$10/month just for the namespace to exist. See "Messaging" below. |
| Operating model | **Single persistent stack — `terraform apply` once, leave it running** | With CloudAMQP replacing Service Bus, nothing left in this stack costs real money just for existing (Container Apps scale to zero, SQL auto-pauses, CloudAMQP's free tier is free). No more apply/destroy ceremony needed. |

Keep these in mind as you read the rest — several sections assume them.

## Target architecture

```
GitHub Actions ──(OIDC, no stored secret)──> Azure

                         ┌─────────────────────────────────────────┐
                         │        Container Apps Environment        │
                         │           (Consumption, one CAE)          │
   Internet ──HTTPS──>   │  ┌──────────┐   external ingress          │
                         │  │ gateway  │───┐                         │
                         │  └──────────┘   │  internal ingress only  │
                         │        ┌────────┼────────┬────────┐      │
                         │  ┌─────▼───┐┌───▼─────┐┌──▼──────┐       │
                         │  │identity ││ order   ││inventory│       │
                         │  └────┬────┘└────┬────┘└────┬────┘       │
                         │  each: minReplicas=0, own managed identity│
                         └───────┼──────────┼──────────┼────────────┘
                                 │          │          │
                    ┌────────────┘          │    ┌─────┴──────┐
                    ▼                       │    ▼            ▼
             Azure SQL DB                   │  Key Vault   CloudAMQP
           (serverless,                     │  (secrets,   (RabbitMQ,
            auto-pause)                     │   pulled via  external to
                                             │   managed     Azure — see
                                             │   identity)   below)
                                             │
                          Order/Inventory ───┘  (amqps:// over the internet,
                                                  TLS, credentials from Key Vault)

   Azure Static Web App (Free) ──build-time VITE_API_BASE_URL──> Gateway's
        (React frontend)                                          public FQDN

   Log Analytics + Application Insights ← every Container App emits to this
```

Gateway keeps its current job unchanged: it's the only Container App with
external ingress; Identity/Order/Inventory get **internal ingress only**
(ACA's equivalent of docker-compose's `expose:` without `ports:` — see
[gateway.md](gateway.md)). CloudAMQP is the one box in this diagram that
isn't an Azure resource at all — Order/Inventory just reach out to it over
the internet the same way they'd reach any external API.

## Phase 0 — app changes required before any infra exists

These are code changes, not Terraform. Do these first; the infra below
assumes they're done.

1. **Remove Redis from Identity, replace with a SQL-backed refresh-token
   store.**
   - `services/identity/Identity.Infrastructure/Repositories/RedisRefreshTokenStore.cs`
     implements `IRefreshTokenStore` (from `Identity.Application/Interfaces`)
     with three methods: `StoreAsync`, `GetUserIdAsync`, `RevokeAsync`. Write
     a `SqlRefreshTokenStore` against `ApplicationIdentityDbContext` with a
     `RefreshTokens(Token PK, UserId, ExpiresAtUtc)` table — add an EF
     migration for it, and index `ExpiresAtUtc` if you add a cleanup job
     later.
   - Swap the DI registration in `Identity.Api/DependencyConfig.cs:46`
     (`AddScoped<IRefreshTokenStore, RedisRefreshTokenStore>()` →
     `SqlRefreshTokenStore`).
   - Delete the `AddStackExchangeRedisCache` call in `Identity.Api/DependencyConfig.cs:29-30`.
   - **Order and Inventory also register `AddStackExchangeRedisCache`**
     (`Order.Api/DependencyConfig.cs:38-39`, `Inventory.Api/DependencyConfig.cs`
     same lines) but neither ever injects `IDistributedCache` anywhere else in
     their code — it's dead registration. Delete those two calls and the
     `Microsoft.Extensions.Caching.StackExchangeRedis` package reference from
     both `.csproj` files. Nothing else touches these services.
   - Drop `ConnectionStrings__Redis` from both `.env`/`docker-compose.yml` once
     the above is done, and drop the `redis` service and `redis-data` volume
     from `docker-compose.yml` entirely.

2. **Drop Azure Service Bus entirely — always use RabbitMQ, pointed at
   CloudAMQP in Azure.** Today `Order.Api/DependencyConfig.cs:71-94` and
   `Inventory.Api/DependencyConfig.cs:63-86` branch on
   `builder.Environment.IsDevelopment()`: RabbitMQ locally,
   `UsingAzureServiceBus` (reading `ServiceBusOptions.ConnectionString`)
   otherwise. Since there's no more Service Bus, collapse this to **always**
   `x.UsingRabbitMq(...)` — less code than today, not more:
   - Delete `ServiceBusOptions.cs` in both `Order.Application/Dtos` and
     `Inventory.Application/Dtos`, and the
     `builder.Services.Configure<ServiceBusOptions>(...)` line in both
     `DependencyConfig.cs` files.
   - Delete the `if (builder.Environment.IsDevelopment()) { ... } else { ... }`
     branch, keeping only the `UsingRabbitMq` body.
   - Extend `RabbitMqOptions` (`Order.Application/Dtos/RabbitMqOptions.cs`,
     same in Inventory) with two new fields CloudAMQP needs that local
     RabbitMQ doesn't: `VirtualHost` (CloudAMQP's free "Lemur" plan uses a
     vhost equal to your username, not `/`) and `UseSsl` (CloudAMQP requires
     TLS — AMQPS on port 5671, not plain AMQP on 5672). Update the
     `cfg.Host(...)` call to pass the vhost from config instead of the
     hardcoded `"/"`, and enable TLS via the RabbitMQ host configurator's SSL
     option when `UseSsl` is true — check MassTransit's current RabbitMQ
     transport docs for the exact `UseSsl`/port API, since this has shifted
     across MassTransit versions.
   - Local `docker-compose.yml`/`.env` keep `RabbitMq__VirtualHost=/` and
     `RabbitMq__UseSsl=false` (or just omit them, defaulting to today's
     behavior) — nothing changes for local dev.
   - In Azure, set (from CloudAMQP's instance dashboard, via Key Vault — see
     the CI/CD section for how these get there without living in a workflow
     file):
     ```
     RabbitMq__Host=<your-instance>.rmq.cloudamqp.com
     RabbitMq__VirtualHost=<your-vhost>
     RabbitMq__Username=<...>
     RabbitMq__Password=<...>
     RabbitMq__UseSsl=true
     ```
   - MassTransit still auto-provisions the exchanges/queues/bindings it needs
     on first startup, exactly like it does against local RabbitMQ today —
     nothing extra to declare for the existing four events.

3. **YARP cluster destinations are hardcoded to docker-compose DNS names**
   (`services/gateway/Gateway/appsettings.json` — `http://identity-api:8080`,
   etc.). In Azure these become each Container App's **internal FQDN**
   (`<app-name>.internal.<environment-unique-id>.<region>.azurecontainerapps.io`).
   Don't hand-edit `appsettings.json` for this — override via environment
   variables at the Container App level (ASP.NET Core config binds
   double-underscore env vars to the same keys):
   ```
   ReverseProxy__Clusters__identity-cluster__Destinations__identity-api__Address
   ReverseProxy__Clusters__order-cluster__Destinations__order-api__Address
   ReverseProxy__Clusters__inventory-cluster__Destinations__inventory-api__Address
   ```
   In Terraform, wire these from the other Container Apps' `ingress.0.fqdn`
   outputs directly — Terraform then handles the dependency ordering
   (Gateway's env vars can't be known until Identity/Order/Inventory exist).

4. **CORS**: `Cors:FrontendOrigins` (`Gateway/appsettings.json`) currently only
   has `http://localhost:5173`. Add the Static Web App's hostname via env var
   override (`Cors__FrontendOrigins__0=https://<your-swa-hostname>`) once you
   know it (Terraform output again, or add after first apply).

5. **Migrations stay manual** — `DOCKER.md` already documents this is
   deliberate (no `Database.Migrate()` on startup, to avoid multiple replicas
   racing). The CI/CD pipeline needs an explicit migration step against Azure
   SQL before/alongside a deploy — see the CI/CD section.

## Azure resources

| Resource | SKU / tier | Billing shape | Notes |
|---|---|---|---|
| Resource Group | — | free | One RG holds everything. |
| Container images | **GitHub Container Registry**, public packages | **free** | No Azure resource at all — images live at `ghcr.io/<org>/<repo>/<service>`, pushed by CI. Container Apps can pull a public image with **no registry credential configured**. |
| Container Apps Environment | Consumption workload profile | environment itself is free | One environment, all 4 apps in it. Safe to leave provisioned permanently. |
| Container App × 4 | Consumption, `minReplicas=0`, `maxReplicas=1–2` | per-second while active, **$0 at zero replicas** | Nowhere near the monthly free grant (180k vCPU-s / 360k GiB-s / 2M requests) at 1-2 uses/month. |
| Azure SQL Database | Serverless, GP, 0.5–1 vCore, auto-pause | **billed hourly while online**; minimum auto-pause delay is 60 min | The one resource where "on" has a real minimum — see below. |
| **CloudAMQP** (RabbitMQ) | **Free "Lemur" plan** — not an Azure resource | **free** | Shared multi-tenant broker, modest connection/throughput limits (plenty for this app). Replaces Azure Service Bus entirely — real pub/sub via exchanges/bindings, same model your local RabbitMQ already uses. Verify current plan name/limits when you sign up, since third-party pricing pages change. |
| Key Vault | Standard | per-10k-operations | Pennies at this volume. |
| Log Analytics + Application Insights | Pay-as-you-go | **first 5 GB/day ingestion free, forever** | Nowhere close to 5 GB/day at this usage. |
| Static Web App | **Free** tier | free | 100 GB bandwidth/mo, free TLS. |

## Why this is cheap, without any apply/destroy ceremony

Every resource above is either genuinely free (GHCR, CloudAMQP's free tier,
Log Analytics under 5 GB/day) or has its own built-in idle-cost mechanism:
Container Apps scale to zero, and Azure SQL Serverless **auto-pauses** after
a configurable idle delay (minimum 60 minutes) — compute billing drops to
~$0 while paused, leaving only a storage charge of pennies to ~$1/month
regardless of how often you use it.

That means there's genuinely nothing left in this stack worth tearing down
between sessions — unlike the Service Bus Standard tier this replaces (a
namespace's ~$10/month base fee accrues for every hour it merely *exists*,
used or not), every remaining resource already costs next to nothing when
idle. **Provision everything once with `terraform apply` and leave it.**

**What an occasional session actually costs:**

| Resource | Why it's not exactly $0 | Cost |
|---|---|---|
| SQL Database | Billed per-second while online; minimum auto-pause delay is 60 min, so even 1 minute of use resumes ~1 hour of billed compute | ~$0.25–0.50 per session |
| CloudAMQP, Container Apps, Key Vault, Log Analytics, GHCR | Free tier / per-request pricing, nowhere near any threshold at this volume | $0.00 |

**Total for 1–2 sessions/month: roughly $0.50–$1**, essentially all of it
Azure SQL's resume cycle — nothing to remember to switch off, no ephemeral
Terraform stack, no risk of a forgotten teardown ballooning the bill.

Sanity-check any of this against the
[Azure Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)
once you've picked final SKUs and your region.

## Terraform repo layout

One root module, one state — no more reason to split "persistent" from
"ephemeral" now that nothing needs to be torn down between sessions:

```
infra/
  terraform/
    modules/
      resource-group/
      log-analytics/          # + Application Insights
      key-vault/
      sql/                    # server + one serverless database
      cloudamqp/               # CloudAMQP instance, via the cloudamqp provider
      container-apps-env/     # the shared Container Apps Environment
      container-app/          # one reusable module, instantiated 4x with different vars
      static-web-app/
    backend.tf                # azurerm backend block (bootstrapped manually, below)
    providers.tf               # azurerm provider + the cloudamqp provider
    main.tf                    # wires every module together
    variables.tf / outputs.tf
```

No `container-registry` module — GHCR isn't an Azure resource, it needs
nothing provisioned; images simply exist at `ghcr.io/...` once CI pushes them.

One reusable `container-app` module (name, image, ingress external/internal,
env vars, secrets, min/max replicas as variables) instantiated four times
beats four near-identical hand-written resources.

### Module notes

- **`sql`**: one `azurerm_mssql_server` + one `azurerm_mssql_database` with
  `sku_name = "GP_S_Gen5_1"` (serverless) and `auto_pause_delay_in_minutes`.
  Firewall: for a no-VNet setup, `azurerm_mssql_firewall_rule` with the
  "allow Azure services" special rule (`0.0.0.0`–`0.0.0.0`) is the pragmatic
  choice since Container Apps Consumption without VNet integration doesn't
  have a static outbound IP to allowlist instead. Note this as a deliberate
  simplification (see Security notes below for the hardening path).
- **`cloudamqp`**: uses the third-party
  [`cloudamqp/cloudamqp`](https://registry.terraform.io/providers/cloudamqp/cloudamqp)
  Terraform provider (not `azurerm` — this resource doesn't live in Azure).
  Provider block takes an `apikey` (from your CloudAMQP account — see
  bootstrap below); the resource is roughly `cloudamqp_instance` with
  `plan = "lemur"` (the free plan — confirm the current plan name/attributes
  against the provider's registry page, since free-tier details are set by
  CloudAMQP, not you). Free-plan instances are typically a shared node in a
  fixed region rather than a region/cloud you choose — a paid dedicated plan
  is what lets you pick "host on Azure" specifically; the free plan doesn't
  need to live "in Azure" at all, since Order/Inventory just reach it over
  the internet via a connection string. Output the instance's host/vhost/
  credentials and write them into the same Key Vault as everything else,
  rather than passing them through Terraform variables/state in plaintext
  any more than necessary.
- **`container-app`**: each app gets its own `azurerm_user_assigned_identity`;
  secrets are `azurerm_container_app` `secret { key_vault_secret_id = ...
  identity = ... }` blocks (Key Vault reference, no plaintext connection
  strings in Terraform state or `appsettings.json`); grant that identity
  `get`/`list` on Key Vault secrets via `azurerm_key_vault_access_policy` (or
  RBAC role assignment if you set the vault to RBAC mode). Gateway is the only
  one with `ingress { external_enabled = true }`. Image reference is just
  `ghcr.io/<org>/<repo>/<service>:<tag>` — with the GHCR package set to
  **public**, no `registry {}` block is needed at all in the Container App
  resource (anonymous pulls work); if you ever flip a package private, that's
  when you'd add a `registry { server = "ghcr.io", username = ...,
  password_secret_name = ... }` block with a GitHub PAT.
- **`static-web-app`**: `azurerm_static_web_app` creates the resource shell;
  the actual build/deploy is a GitHub Actions workflow using the
  `Azure/static-web-apps-deploy` action with the SWA's deployment token
  (`azurerm_static_web_app.default_host_name` / API key as Terraform outputs
  → one GitHub secret).

## One-time manual bootstrap (before any `terraform apply`)

Terraform needs somewhere to put its state, GitHub Actions needs a way to
authenticate to Azure, and now CloudAMQP needs an API key — all done once, by
hand, outside Terraform (you can't use Terraform to create the thing
Terraform's state lives in, or the credential Terraform itself authenticates
with).

**1. State storage:**
```bash
az group create -n rg-orderflow-tfstate -l eastus

az storage account create \
  -n stordflowtfstate \
  -g rg-orderflow-tfstate -l eastus \
  --sku Standard_LRS \
  --allow-blob-public-access false

az storage container create \
  -n tfstate \
  --account-name stordflowtfstate \
  --auth-mode login
```
(`stordflowtfstate` must be globally unique — adjust if taken.)

`infra/terraform/backend.tf`:
```hcl
terraform {
  backend "azurerm" {
    resource_group_name  = "rg-orderflow-tfstate"
    storage_account_name = "stordflowtfstate"
    container_name        = "tfstate"
    key                    = "orderflow.tfstate"
  }
}
```

**2. GitHub Actions → Azure via OIDC (no client secret to leak or rotate):**
```bash
az ad app create --display-name "orderflow-github-actions"
# note the returned appId

az ad sp create --id <appId>

az ad app federated-credential create --id <appId> --parameters '{
  "name": "orderflow-main-branch",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<your-org>/<your-repo>:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

az role assignment create \
  --assignee <appId> \
  --role Contributor \
  --scope /subscriptions/<subscription-id>
```
Add a second federated credential with
`"subject": "repo:<your-org>/<your-repo>:pull_request"` if you want `terraform
plan` to run on PRs too.

Store as **GitHub Actions repo secrets** (not `.env` — these never touch
`.env`/Key Vault, they're what *gets you into* Azure in the first place):
`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

**3. CloudAMQP account + API key:**
Sign up at CloudAMQP, and generate a Customer API key from your account
dashboard (separate from any individual instance's credentials — this key is
what lets Terraform *create* instances, not what the app uses to connect to
one). Store it as a GitHub Actions secret too (`CLOUDAMQP_API_KEY`), fed into
the `cloudamqp` provider block the same way the Azure OIDC values feed
`azurerm`. This key only needs to exist in CI — nothing about it goes into
`.env` or Key Vault, same reasoning as the Azure OIDC credentials above.

## CI/CD — GitHub Actions

Three independent workflows; each triggers only on the paths it owns so a
frontend change doesn't rebuild four containers.

### `infra.yml` — Terraform plan/apply
- Trigger: push to `main` touching `infra/terraform/**`; PR for plan-only.
- `permissions: { id-token: write, contents: read }`, `azure/login@v2` with
  the three Azure secrets above (no password, OIDC token exchange), plus the
  `CLOUDAMQP_API_KEY` secret passed to the `cloudamqp` provider.
- `terraform fmt -check`, `terraform init`, `terraform plan` (PR — post as a
  comment), `terraform apply -auto-approve` (only on `main`).

### `backend.yml` — build, push to GHCR, migrate, deploy the four services
- Trigger: push to `main` touching `services/**` (any change under
  `services/contracts/**` or `services/shared/**` should trigger **all four**,
  since every service references them — the other three path filters can stay
  scoped to their own `services/{name}/**`).
- `permissions: { packages: write }` — pushing to GHCR needs no separate
  secret at all; `docker/login-action` with `${{ secrets.GITHUB_TOKEN }}`
  (built into every workflow run) is enough, unlike ACR which needed either an
  admin login or a registry-pull identity wired up in Terraform.
- Per changed service: `docker build` using its existing `Dockerfile`
  (context is the repo root already — `COPY . .` — so no changes needed
  there), tag `ghcr.io/<org>/<repo>/<service>:<sha>`, push, then
  `az containerapp update --image ghcr.io/<org>/<repo>/<service>:<sha>`
  (simplest — let Terraform own everything about the Container App *except*
  the image tag, which changes every deploy).
- **Migration step**: run `dotnet ef database update` for the changed
  service's `Infrastructure` project against the Azure SQL connection string
  (pull it from Key Vault via `azure/login` + `az keyvault secret show`,
  don't hardcode it in the workflow). Since SQL is always provisioned now
  (just auto-paused between uses), this step can run on every deploy without
  the "is anything even up right now" caveat the old ephemeral-stack version
  had — it's the cloud equivalent of `make
  migrate-identity`/`migrate-order`/`migrate-inventory`, still a deliberate
  step, just automated instead of you typing `make` by hand.

### `frontend.yml` — Static Web App
- Connecting a SWA resource to a repo in the Portal auto-generates most of
  this workflow for you (`Azure/static-web-apps-deploy@v1` with `app_location:
  frontend`, `output_location: dist`) — accept the generated one and adjust
  the trigger path filter to `frontend/**`.
- **Gotcha worth knowing going in**: Vite bakes `VITE_API_BASE_URL`
  (`frontend/.env` → `VITE_API_BASE_URL=http://localhost:8080` today) into the
  bundle **at build time**, not read at runtime. Set it as a build env var in
  the workflow (`env: VITE_API_BASE_URL: ${{ vars.GATEWAY_URL }}`, a GitHub
  Actions repo/environment **variable**, not secret — it's public in the
  shipped JS either way) pointing at the Gateway Container App's external
  FQDN. Forgetting this is the classic "works on my machine, 404s in prod" SPA
  bug.

## Observability

- One `azurerm_log_analytics_workspace`, one `azurerm_application_insights`
  (workspace-based mode) wired to it.
- Each Container App gets `Microsoft.ApplicationInsights` (or just OpenTelemetry
  exporter, if you'd rather learn OTel over the ApplicationInsights SDK) with
  the connection string injected as a Key Vault-referenced secret, same
  pattern as the SQL connection string.
- Correlation ID enrichment (already a Serilog requirement per this project's
  conventions) carries straight into App Insights' `operation_Id` if you also
  keep the console sink — App Insights won't replace Serilog, it's an
  additional sink/exporter alongside it.

## Security notes (what's simplified here, and the hardening path)

- **No VNet / private endpoints** in this pass — Container Apps talk to SQL/
  Key Vault over public endpoints (SQL firewalled to "Azure services", Key
  Vault via Azure AD auth + managed identity rather than IP allowlisting).
  Real hardening path: put the Container Apps Environment on a custom VNet,
  SQL/Key Vault behind Private Endpoints, deny public network access
  entirely. Skipped here for cost and complexity — a good "phase 2" once the
  simple version works. This doesn't apply to CloudAMQP either way — it's
  external to Azure by design, reached over the internet via TLS (AMQPS)
  regardless of any VNet decisions on the Azure side.
- **Managed identity everywhere an Azure secret would otherwise be needed** —
  Container Apps pull Key Vault secrets via their user-assigned identity, not
  a connection string baked into an env var at plan time. CloudAMQP's
  credentials are the one exception that can't use managed identity (it's not
  an Azure service) — they still go through Key Vault as a plain secret,
  same as the SQL connection string, just without the extra managed-identity
  layer Azure-to-Azure calls get.
- Only the *bootstrap* secrets (federated OIDC creds, the CloudAMQP API key,
  the SWA deployment token) live as GitHub secrets — and OIDC means the Azure
  ones aren't even long-lived passwords.
- **`.env`/Key Vault boundary stays exactly as documented in `CLAUDE.md`**:
  local dev keeps using `.env` (git-ignored), Azure uses Key Vault +
  Managed Identity — this doc doesn't change that contract, it implements it.

## Suggested rollout order

1. Phase 0 app changes (Redis removal, RabbitMQ-everywhere + CloudAMQP
   fields, YARP env var overrides) — merge and confirm `docker-compose`
   still works locally (it should be untouched — CloudAMQP only matters
   once `ASPNETCORE_ENVIRONMENT` isn't `Development`... actually, once you
   collapse to always-RabbitMQ, there's no environment branch left at all;
   local dev just keeps pointing at `rabbitmq:5672` with `UseSsl=false`).
2. Manual bootstrap: state storage account, OIDC federated credential,
   CloudAMQP account + API key.
3. Terraform: resource group → Log Analytics/App Insights → Key Vault → SQL
   (server + serverless database, auto-pause on) → CloudAMQP instance →
   Container Apps Environment.
4. Terraform: four Container Apps, `minReplicas=0`, images from a placeholder
   (`mcr.microsoft.com/dotnet/samples:aspnetapp` or similar) until CI/CD
   exists — you need *something* running for the FQDN outputs to resolve
   before wiring Gateway's cluster env vars.
5. `backend.yml` — first real image build/push to GHCR/deploy, then re-run
   `terraform apply` (or `az containerapp update`) so Gateway points at real
   images with the FQDN-derived cluster addresses.
6. Run migrations against Azure SQL manually once (`dotnet ef database
   update` from your machine, same as `make migrate-identity`) to prove the
   connection before trusting the CI/CD migration step.
7. Terraform: Static Web App resource; connect it in the Portal (or via
   `azure/static-web-apps-deploy` workflow) to get `frontend.yml`.
8. Add the SWA hostname to Gateway's CORS env var override; add the Gateway
   FQDN as the frontend's build-time `VITE_API_BASE_URL`.
9. End-to-end smoke test: register → login → create order → watch it flip to
   Reserved/Rejected → cancel it, through the real Azure URLs.
10. Cost check-in after a week idle — confirm the Container Apps really did
    scale to zero and SQL shows "Paused" in the Portal.

There's no "every session" ceremony after that — everything just sits there,
mostly paused/scaled-to-zero, until you open the app again.

## Deferred / stretch ideas

### A second pub/sub consumer: notification Azure Function on `OrderCreatedEvent`

Yes, this is possible, and it's actually a nice demonstration of what
pub/sub buys you: a second, completely independent subscriber to an event
Order already publishes, with **zero changes to Order.Api**.

- **How it works**: Azure Functions has a RabbitMQ trigger extension
  (`Microsoft.Azure.WebJobs.Extensions.RabbitMQ` for the in-process model, or
  the isolated-worker equivalent — check current NuGet for the exact package
  name/version, this extension family has moved around) that fires the
  function whenever a message lands on a specific **queue**. RabbitMQ doesn't
  have "topics" the way Service Bus does — pub/sub there means: one exchange
  (already auto-created by MassTransit for `OrderCreatedEvent`), multiple
  independently-bound **queues**. Inventory's consumer already has its own
  queue bound to that exchange; the Function needs its **own separate
  queue**, bound to the same exchange, to get its own independent copy of
  every `OrderCreatedEvent` — that binding *is* the second subscription.
- **Declaring that queue+binding as infrastructure, not app code**: the
  community [`cyrilgdn/rabbitmq`](https://registry.terraform.io/providers/cyrilgdn/rabbitmq)
  Terraform provider talks to RabbitMQ's Management HTTP API (which CloudAMQP
  exposes) and can declare `rabbitmq_queue` + `rabbitmq_binding` resources
  directly — so the Function's subscription is Terraform-managed, not
  something buried in a consumer's startup code.
- **The interop gotcha**: MassTransit wraps every published message in its
  own JSON envelope (message-type URNs, headers, and the actual payload
  nested under a `message` property) — a plain Azure Functions trigger has
  no idea about that shape by default. Two ways to handle it: (a) just
  deserialize the envelope in the Function and read `.message` — simplest,
  no producer-side change; or (b) configure a dedicated raw-JSON receive
  topology on the MassTransit side (it has an interop feature for exactly
  this — non-MassTransit consumers reading plain JSON instead of the
  envelope — check current MassTransit docs for the exact API, it's shifted
  names across versions). For "just log it," option (a) is the lower-effort
  path.
- **Cost**: Azure Functions Consumption plan — 1M executions + 400,000 GB-s
  free forever. A logging function firing once or twice a month costs $0.

### Other deferred ideas

- dev + prod environments (Terraform workspaces or a second `tfvars`).
- VNet + Private Endpoints for SQL/Key Vault.
- Custom domain + managed TLS cert on both the Gateway and the SWA.
- Pre-provisioned RabbitMQ topology via the `cyrilgdn/rabbitmq` provider
  instead of MassTransit auto-provisioning, if you want the exchange/queue
  layout to be explicit, reviewable infrastructure rather than something an
  app creates on first startup.
- Azure Front Door in front of both Gateway and the SWA for one shared
  custom domain + WAF.
