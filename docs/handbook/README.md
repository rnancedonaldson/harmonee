# .NET on Azure — Engineering Guidelines

> **Status:** Draft v1 · **Last reviewed:** 2026-10-17 · **Baseline platform:** .NET 10 (LTS), C# 14, Aspire 13.x, Azure

This folder is the foundational engineering handbook for building a product suite made of:

- an **HTTP API** (ASP.NET Core),
- a **web application** (Blazor or a SPA backed by ASP.NET Core),
- **mobile applications** (.NET MAUI, iOS + Android),

all hosted on **Microsoft Azure**.

The aim is to give every future project a shared, opinionated starting point — a "golden path" — so that teams spend their energy on product problems instead of re-deciding the same infrastructure questions. Everything here is a **default, not a law**: deviate when you have a good reason, and record that reason in an Architecture Decision Record (see [templates/adr-template.md](templates/adr-template.md)).

---

## How to read this

| # | Document | Read it when… |
|---|----------|---------------|
| 01 | [Architecture Overview](01-architecture-overview.md) | **Start here.** Principles, architectural styles, modern paradigms, the reference architecture. |
| 02 | [Solution Structure & Coding Standards](02-solution-structure-and-coding-standards.md) | Creating a repo, setting up a solution, writing C# day to day. |
| 03 | [API Design & Development](03-api-design-and-development.md) | Building the ASP.NET Core API. |
| 04 | [Data & Persistence](04-data-and-persistence.md) | Choosing databases, using EF Core, caching, messaging consistency. |
| 05 | [Web Application](05-web-application.md) | Building the browser front-end (Blazor or SPA). |
| 06 | [Mobile Applications](06-mobile-applications.md) | Building the .NET MAUI apps. |
| 07 | [Security & Identity](07-security-and-identity.md) | Authentication, authorization, secrets, threat modelling. Everyone should read this. |
| 08 | [Azure Hosting & Infrastructure](08-azure-hosting-and-infrastructure.md) | Choosing compute, networking, IaC, environments, cost. |
| 09 | [Observability & Operations](09-observability-and-operations.md) | Logging, tracing, metrics, alerting, SLOs, incident response. |
| 10 | [Testing Strategy](10-testing-strategy.md) | Deciding what and how to test. |
| 11 | [DevOps & Delivery](11-devops-and-delivery.md) | CI/CD, branching, releases, mobile store pipelines. |
| 12 | [Performance, Resilience & Scalability](12-performance-resilience-and-scalability.md) | Making things fast and keeping them up. |
| 13 | [AI Integration](13-ai-integration.md) | Adding LLM/agent features responsibly. |
| 14 | [Checklists](14-checklists.md) | New-project bootstrap, PR review, production readiness. |
| — | [ADR Template](templates/adr-template.md) | Recording any significant decision. |

---

## The golden path at a glance

These are the defaults the rest of the documents assume. Each links to the rationale.

| Concern | Default choice | Notes / alternatives |
|---|---|---|
| Runtime | **.NET 10 LTS** (supported to Nov 2028) | Adopt the next LTS (.NET 12, Nov 2027) within ~6 months of release. Avoid STS releases for long-lived production apps unless you commit to upgrading. |
| Language | **C# 14**, nullable enabled, warnings as errors | [02](02-solution-structure-and-coding-standards.md) |
| Architecture style | **Modular monolith** with vertical slices; extract services only when forced | [01](01-architecture-overview.md#3-choosing-an-architectural-style) |
| API | **ASP.NET Core Minimal APIs**, OpenAPI 3.1 via `Microsoft.AspNetCore.OpenApi`, RFC 9457 Problem Details | Controllers are acceptable for large teams already fluent in MVC. [03](03-api-design-and-development.md) |
| Web front-end | **Blazor Web App** (static SSR + interactive islands) | React/Angular/Vue SPA with a **BFF** if the team is JS-first. [05](05-web-application.md) |
| Mobile | **.NET MAUI** + CommunityToolkit.Mvvm | Blazor Hybrid inside MAUI for heavy code-sharing with the web. [06](06-mobile-applications.md) |
| Local orchestration | **Aspire** (AppHost + ServiceDefaults) | [08](08-azure-hosting-and-infrastructure.md#9-aspire-local-development-and-deployment) |
| Compute | **Azure Container Apps** | App Service for simple web apps; Functions (isolated, Flex Consumption) for event-driven work; AKS only with a platform team. [08](08-azure-hosting-and-infrastructure.md#2-choosing-compute) |
| Relational data | **Azure SQL Database** or **Azure Database for PostgreSQL – Flexible Server**, via **EF Core 10** | Cosmos DB for global, high-scale, schema-flexible workloads. [04](04-data-and-persistence.md) |
| Caching | **HybridCache** (L1 in-memory + L2 Azure Managed Redis) | [04](04-data-and-persistence.md#7-caching) |
| Messaging | **Azure Service Bus** (commands, workflows), **Event Grid** (reactive events) | Event Hubs for high-volume telemetry/streams. |
| Workforce identity | **Microsoft Entra ID** | [07](07-security-and-identity.md) |
| Customer identity | **Microsoft Entra External ID** (Azure AD B2C is closed to new customers since May 2025) | |
| Service-to-service auth | **Managed Identity** everywhere; no connection-string secrets | |
| Secrets & config | **Key Vault** + **App Configuration** (incl. feature flags) | |
| Edge | **Azure Front Door** (WAF, CDN, TLS) + **API Management** when you have external API consumers | |
| Observability | **OpenTelemetry** → **Azure Monitor / Application Insights** | [09](09-observability-and-operations.md) |
| IaC | **Bicep** (with Azure Verified Modules) via **azd** or pipelines | Terraform if multi-cloud or org standard. |
| CI/CD | **GitHub Actions** with OIDC workload identity federation | Azure DevOps Pipelines is equally valid. [11](11-devops-and-delivery.md) |
| Branching | **Trunk-based development**, short-lived branches, feature flags | |
| Testing | **xUnit v3** on Microsoft.Testing.Platform, **Testcontainers**, `WebApplicationFactory`, **Playwright** | [10](10-testing-strategy.md) |
| AI | **Microsoft.Extensions.AI** abstractions, **Microsoft Foundry** (formerly Azure AI Foundry) models, **Microsoft Agent Framework** for agents | [13](13-ai-integration.md) |

---

## Guiding principles (the short version)

1. **Simple first, scalable by design.** Start with the simplest architecture that can work (one deployable, one database) but draw clean module boundaries so you *can* split later.
2. **Security is a feature, not a phase.** Identity-based access, least privilege, no secrets in code or config, threat model every significant feature.
3. **Everything as code.** Infrastructure, pipelines, configuration, dashboards, alerts, and policies live in Git and are reviewed like application code.
4. **Observable by default.** If you can't see it in telemetry, you can't operate it. Every service emits traces, metrics, and structured logs from day one.
5. **Automate the path to production.** Every merge to `main` is releasable; deployments are boring, frequent, and reversible.
6. **Design for failure.** Networks fail, dependencies throttle, regions go down. Timeouts, retries, idempotency, and graceful degradation are mandatory.
7. **Contracts are products.** APIs are versioned, documented, and backward compatible — especially because mobile clients can't be force-upgraded instantly.
8. **Measure, then optimise.** Performance work is driven by profiling and SLOs, not hunches.
9. **Prefer platform features over custom code.** Use what .NET and Azure provide (auth, resilience, caching, health checks, rate limiting) before writing or buying alternatives.
10. **Write it down.** Decisions go in ADRs; operational knowledge goes in runbooks.

---

## Keeping this current

- Review these documents at least **once per .NET major release** (each November) and when Azure retires or launches a service you depend on.
- Treat changes like code: propose via pull request, discuss, merge.
- Items that were accurate at the time of writing but are prone to change are flagged with **⚠ Verify** — check the linked source before relying on them.

### Notable ecosystem changes to be aware of (as of late 2026)

- **.NET 8 and .NET 9 both reach end of support on 10 Nov 2026** (STS support was extended to 24 months). New work should target .NET 10.
- **Azure Functions in-process model support ends 10 Nov 2026** — use the isolated worker model only.
- **Azure AD B2C** stopped being sold to new customers on 1 May 2025; use **Entra External ID**.
- **Visual Studio App Center** was retired 31 Mar 2025 — mobile build/distribution/crash reporting must use alternatives (see [06](06-mobile-applications.md)).
- **Xamarin** support ended May 2024; MAUI is the successor.
- **Swashbuckle** was removed from the ASP.NET Core templates in .NET 9; the built-in `Microsoft.AspNetCore.OpenApi` package is the default (OpenAPI 3.1 in .NET 10).
- Several popular OSS libraries moved to **commercial licences** in 2025 (e.g., MediatR, AutoMapper, MassTransit v9+, FluentAssertions v8+). Check licence terms before adopting; prefer built-in alternatives or permissively-licensed forks. See [02](02-solution-structure-and-coding-standards.md#9-dependency-and-library-policy).
- "**.NET Aspire**" is now branded simply **Aspire** (v13+), with its own CLI and support for polyglot app hosts.
