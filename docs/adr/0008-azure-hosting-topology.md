# ADR 0008: Azure hosting topology and delivery

| Status | Date |
|---|---|
| Accepted | 2026-10-25 |

## Context
Roadmap item 1 deploys Harmonee to Azure, following handbook chapters [08](../handbook/08-azure-hosting-and-infrastructure.md) and [11](../handbook/11-devops-and-delivery.md). We need a compute platform, infrastructure as code that we own, passwordless access everywhere, schema migrations that run before the API changes, and a pipeline with no stored credentials.

`azd infra gen` on the Aspire AppHost gave a starting point. It used one shared managed identity for every app, ran the migration service as a long-running container app, created SQL users with a deployment script, and (because the AppHost ran in Development) baked `Authentication__Mode=Development` and a fixed signing key into the app templates. That last one would have been refused at startup by [ADR 0006](0006-development-authentication-mode.md)'s guards, but it shows the generated output can't be used as-is.

## Decision
- **Compute:** Azure Container Apps, in a workload profiles (Consumption) environment inside a VNet. The **web** app has external ingress. The **API** has internal ingress only: today only the web app calls it, and Front Door will expose it for mobile. **Migrations** are a manual-trigger **Container Apps Job**.
- **IaC:** Bicep that we own in `infra/`, built from Azure Verified Modules, pinned by version. There's one template, plus `main.dev.bicepparam` and `main.prod.bicepparam`. Names follow CAF, and every resource carries the handbook's required tags. We don't use azd to deploy. It was used once to generate the starting point.
- **Identity:** one user-assigned managed identity per workload (api, web, migrations), each with only its own roles. Azure SQL uses **Entra-only authentication**. The **migrations identity is the server's Entra administrator**. After migrating, the migration service creates the API identity's database user (`CREATE USER … WITH SID = <client id>, TYPE = E`, so no Microsoft Graph permissions are needed) with `db_datareader` and `db_datawriter` only.
- **Web credential:** the web app authenticates to Entra External ID with `SignedAssertionFromManagedIdentity` (a federated credential), configured through `AzureAd:ClientCredentials`. Client secrets are refused outside Development. Because Entra requires the managed identity and the app registration to share a tenant, a **Key Vault certificate** credential is the supported alternative when the subscription isn't in the external tenant (`WEB_CLIENT_CREDENTIAL=KeyVaultCertificate`). See [deployment §3](../deployment.md#3-entra-external-id-setup-per-environment).
- **Secrets:** none in configuration. The App Insights connection string is a Key Vault secret referenced by the container apps. Data Protection keys are kept in Blob Storage and wrapped with a Key Vault key. The token cache uses Azure Managed Redis with Entra auth in prod, and memory (a single replica) in dev.
- **Networking:** prod uses private endpoints for SQL, Key Vault, Storage, App Configuration and Redis, with public access disabled. Dev uses public endpoints with Entra-only auth, to save cost. The registry stays public so hosted runners can push.
- **Delivery:** `deploy.yml` builds the images once per commit (`dotnet publish -t:PublishContainer`, archives as artifacts), then deploys dev and, after approval, prod. Each environment run does: push, what-if, deploy infrastructure (the job gets the new image), run migrations, deploy the new app images, smoke test. Azure access is OIDC workload identity federation, with the subject scoped to the GitHub environment, and actions are pinned by SHA.

## Consequences
- Each environment is reproducible from the repo, and nothing secret is stored in GitHub or in app settings.
- The API keeps running the old version while migrations run, so migrations must be backward compatible (expand/contract).
- The deploy identity needs Role Based Access Control Administrator, with a condition limiting it to the six roles the templates assign. Adding a new role assignment means updating that condition.
- The managed identity federated credential only works when the tenants match. Most setups will use the Key Vault certificate, which has to be renewed and re-uploaded yearly.
- Not done yet: Front Door + WAF, custom domains, App Configuration consumption in the apps, alerts and budgets, PSRule, deployment stacks, canary releases.
