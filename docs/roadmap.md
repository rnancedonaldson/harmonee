# Roadmap

The MVP is a foundation. These are the next steps, roughly in priority order. Each should get an issue (and an ADR where noted).

## Next

1. **Deploy to Azure.** Container Apps (API, web; migrations as a Container Apps Job), Azure SQL with Entra-only auth and managed identity, Front Door + WAF, Key Vault, App Configuration, Application Insights. Use `azd` with Aspire-generated Bicep, then own the Bicep (Azure Verified Modules). Add a deploy workflow with OIDC workload identity federation and environment approvals. *ADR: hosting topology.*
2. **Production BFF hardening.** Federated credential (managed identity) instead of the web app's client secret; Redis-backed distributed token cache (Azure Managed Redis); Data Protection keys in Blob + Key Vault.
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
