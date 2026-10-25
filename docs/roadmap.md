# Roadmap

The MVP is a foundation. These are the next steps, roughly in priority order. Each should get an issue (and an ADR where noted).

## Next

1. **Deploy to Azure.** *Infrastructure and pipeline done* ([deployment guide](deployment.md), [ADR 0008](adr/0008-azure-hosting-topology.md)): Container Apps (web, internal API, migrations job), Azure SQL with Entra-only auth, Key Vault, App Configuration, Application Insights, and an OIDC deploy workflow with environment approvals. Remaining: **Front Door + WAF** and a custom domain in front of the web app; read **App Configuration** (and feature flags) from the apps; alerts and budgets; PSRule for Azure in CI.
2. **Production BFF hardening.** *Mostly done:* managed identity federated credential (or Key Vault certificate) instead of a client secret, Redis token cache (Azure Managed Redis, prod), Data Protection keys in Blob + Key Vault. Remaining: confirm the External ID tenant topology for the federated credential (see [deployment §3.2](deployment.md#32-web-app-credential-option-a-managed-identity-federated-credential-default)).
3. **Persisted audit log.** Store audit events in an append-only table (or Log Analytics with retention locks) and show recent admin actions in the admin panel.
4. **Family deletion and other cross-module events.** Introduce a transactional outbox and in-process or Service Bus integration events (`FamilyDeleted`, `MemberRemoved`) so modules can clean up their own data.

## Soon

- **Idempotency keys** on `POST` endpoints (needed before mobile clients retry over flaky networks).
- **Family time zone** (in addition to the browser's), for consistent "today" across members and for reminders.
- **Mobile app** (.NET MAUI), reusing `Harmonee.Api.Contracts`, MSAL with Entra External ID, and an app-config endpoint for minimum supported version.
- **Recurring events** and reminders (push notifications via Azure Notification Hubs).
- **Invites by email** (Azure Communication Services) and invite-code expiry.
- **Playwright E2E tests** in CI against a deployed test environment, with axe accessibility checks.
- **Caching** of family access lookups (HybridCache with tag invalidation on membership changes) if telemetry shows it's needed.

## Later

- Shopping list sharing to non-members, meal-plan → shopping-list generation, file attachments (Blob Storage + malware scanning), AI-assisted meal planning ([handbook 13](handbook/13-ai-integration.md)).
