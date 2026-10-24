# 09 — Observability & Operations

> Telemetry (logs, traces, metrics) with OpenTelemetry and Azure Monitor, SLOs, alerting, diagnostics, and incident management.

---

## Contents

1. [Goals](#1-goals)
2. [OpenTelemetry in .NET](#2-opentelemetry-in-net)
3. [Logging](#3-logging)
4. [Distributed tracing](#4-distributed-tracing)
5. [Metrics](#5-metrics)
6. [Client-side telemetry (web and mobile)](#6-client-side-telemetry-web-and-mobile)
7. [Health and synthetic monitoring](#7-health-and-synthetic-monitoring)
8. [SLIs, SLOs and error budgets](#8-slis-slos-and-error-budgets)
9. [Alerting](#9-alerting)
10. [Telemetry cost control](#10-telemetry-cost-control)
11. [Dashboards](#11-dashboards)
12. [Diagnostics and profiling](#12-diagnostics-and-profiling)
13. [Incident management and on-call](#13-incident-management-and-on-call)
14. [References](#14-references)

---

## 1. Goals

**Observability** is the ability to answer *new* questions about system behaviour from its outputs, without shipping new code. Monitoring tells you *that* something is wrong; observability helps you find *why*.

We want to be able to answer, quickly:
- Are users able to do what they came to do right now? (SLOs)
- If not, which component and which change caused it? (traces, deployment markers)
- Which users/tenants/app versions are affected? (high-cardinality dimensions)
- Is it getting worse? (metrics, trends)

Signals:
| Signal | Answers | .NET API |
|---|---|---|
| **Traces** | Where did time go / where did it fail across services? | `System.Diagnostics.Activity` / `ActivitySource` |
| **Metrics** | How much / how often / how fast — aggregated, cheap, alertable. | `System.Diagnostics.Metrics.Meter` |
| **Logs** | What happened, with context. | `Microsoft.Extensions.Logging.ILogger` |
| **Profiles / dumps** | Why is this code slow / leaking? | Profiler, `dotnet-trace`, `dotnet-dump` |

---

## 2. OpenTelemetry in .NET

### 2.1 Standard: OpenTelemetry → Azure Monitor

- Instrument with the **vendor-neutral OpenTelemetry (OTel)** .NET SDK. .NET's built-in `ILogger`, `Activity`, and `Meter` APIs are OTel-native.
- Export to **Azure Monitor (Application Insights + Log Analytics)** using the **Azure Monitor OpenTelemetry Distro** (`Azure.Monitor.OpenTelemetry.AspNetCore`) — or via OTLP to an OpenTelemetry Collector if you need multi-backend routing.
- Locally, the **Aspire dashboard** receives OTLP and shows everything.
- **Don't use** the legacy Application Insights SDK (`Microsoft.ApplicationInsights.AspNetCore`) for new apps — OTel is the forward path (the classic SDK 3.x is itself built on OTel).

### 2.2 ServiceDefaults wiring

```csharp
public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
{
    builder.Logging.AddOpenTelemetry(o =>
    {
        o.IncludeFormattedMessage = true;
        o.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(
            serviceName: builder.Environment.ApplicationName,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Contoso.Shop.*"))
        .WithTracing(t => t
            .AddSource(builder.Environment.ApplicationName, "Contoso.Shop.*", "Azure.*")
            .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()   // or SqlClient instrumentation
            .AddRedisInstrumentation());

    // Local/Aspire: OTLP exporter if OTEL_EXPORTER_OTLP_ENDPOINT is set
    if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        builder.Services.AddOpenTelemetry().UseOtlpExporter();

    // Azure: Azure Monitor exporter if connection string is set
    if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        builder.Services.AddOpenTelemetry().UseAzureMonitor();

    return builder;
}
```

- Set **resource attributes**: `service.name`, `service.version`, `service.namespace` (product), `deployment.environment.name`, `cloud.region`. These power filtering and deployment correlation.
- Use **Entra ID (managed identity) authentication** for Application Insights ingestion and disable local auth on the App Insights resource.
- One **Application Insights resource per environment** (shared by the product's services) backed by a **workspace-based** Log Analytics workspace — enables the end-to-end application map and cross-service queries.

---

## 3. Logging

### 3.1 Practices

- **Structured** logging with message templates ([02 §8](02-solution-structure-and-coding-standards.md#8-logging-and-error-handling-in-code)) — properties become queryable columns (`customDimensions` / `Properties` in Log Analytics).
- **Source-generated `[LoggerMessage]`** for hot paths.
- **Log events, not essays**: one log line per meaningful event; let traces show the flow.
- **Consistent property names** across services: `OrderId`, `TenantId`, `UserId` (pseudonymous ID, not email), `CorrelationId`. Define them as constants.
- **Correlation is automatic**: logs written within an `Activity` carry `TraceId`/`SpanId` — no need to log them manually.
- **Scopes** (`logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenantId })`) or `Activity.Current?.SetTag` for contextual enrichment.
- **Redaction**: use `Microsoft.Extensions.Compliance.Redaction` with data classification attributes (`[PersonalData]`, custom taxonomy) and `EnableRedaction()` on logging so sensitive values are masked/hashed automatically.

### 3.2 Levels in production

| Category | Default level |
|---|---|
| Application namespaces (`Contoso.*`) | `Information` |
| `Microsoft.AspNetCore` | `Warning` |
| `Microsoft.EntityFrameworkCore` | `Warning` (`Microsoft.EntityFrameworkCore.Database.Command` at `Warning`; enable `Information` temporarily to see SQL) |
| `System.Net.Http.HttpClient` | `Warning` |
| `Azure.*` | `Warning` |

Change levels dynamically via App Configuration refresh when diagnosing an issue — no redeploy needed.

### 3.3 What to log / not log

| Log | Don't log |
|---|---|
| Business events (order placed, payment failed) | Passwords, tokens, API keys, connection strings |
| Unexpected errors with exception details | Full request/response bodies by default |
| Warnings on degraded behaviour (fallback used, retry exhausted) | Raw PII (emails, names, addresses, card numbers) |
| Security events ([07 §14](07-security-and-identity.md#14-security-logging-detection-and-incident-response)) | High-frequency success noise ("entered method X") |
| Startup configuration summary (non-secret) | Health-check request logs |

---

## 4. Distributed tracing

- **Automatic spans** for incoming HTTP (ASP.NET Core), outgoing HTTP, EF Core/SqlClient, Redis, and Azure SDK calls (Service Bus, Storage, Cosmos — the Azure SDKs emit OTel-compatible activities under `Azure.*` sources).
- **Custom spans** for significant internal operations:

  ```csharp
  internal static class Telemetry
  {
      public static readonly ActivitySource Source = new("Contoso.Shop.Ordering");
  }

  using var activity = Telemetry.Source.StartActivity("PlaceOrder");
  activity?.SetTag("order.line_count", command.Lines.Count);
  activity?.SetTag("tenant.id", tenantId);
  // ... on failure:
  activity?.SetStatus(ActivityStatusCode.Error, error.Code);
  ```

- **Propagation across async messaging**: W3C trace context (`traceparent`) is propagated in Service Bus message application properties by the Azure SDK. When using the **outbox**, store `traceparent` with the outbox row and restore it when publishing, so the consumer's span links back to the original request.
- **Span links** for batch processing (one consumer span linked to many producer spans).
- **Client-to-server**: browser and mobile clients send `traceparent` headers so the user action is the root of the trace.
- Return the **trace ID** to clients in error responses so support can find the trace instantly.
- Use **semantic conventions** for attribute names (`http.request.method`, `db.system`, `messaging.system`, `user.id` …).

---

## 5. Metrics

### 5.1 Built-in metrics

.NET 8+ emits rich built-in metrics via `Meter`s: ASP.NET Core (`http.server.request.duration`, active requests, rate-limiting, Kestrel connections), `HttpClient` (`http.client.request.duration`), runtime (GC, thread pool, exceptions), and more. Enable them via OTel `AddAspNetCoreInstrumentation`/`AddRuntimeInstrumentation` or `AddMeter("Microsoft.AspNetCore.*")`.

### 5.2 Frameworks for choosing metrics

- **RED** for request-driven services: **R**ate, **E**rrors, **D**uration.
- **USE** for resources: **U**tilisation, **S**aturation, **E**rrors (CPU, memory, DB DTU, queue depth).
- **Golden signals**: latency, traffic, errors, saturation.
- **Business metrics**: orders placed per minute, checkout conversion, sign-ups, payment failures by provider — often the earliest indicator of problems.

### 5.3 Custom metrics

```csharp
public sealed class OrderingMetrics
{
    private readonly Counter<long> _ordersPlaced;
    private readonly Histogram<double> _orderValue;

    public OrderingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Contoso.Shop.Ordering");
        _ordersPlaced = meter.CreateCounter<long>("shop.orders.placed", unit: "{order}", description: "Orders placed");
        _orderValue = meter.CreateHistogram<double>("shop.orders.value", unit: "GBP");
    }

    public void OrderPlaced(string channel, decimal value)
    {
        _ordersPlaced.Add(1, new KeyValuePair<string, object?>("channel", channel));
        _orderValue.Record((double)value, new KeyValuePair<string, object?>("channel", channel));
    }
}
```

- Register as singleton; use `IMeterFactory` (DI-friendly, testable with `MetricCollector<T>`).
- **Keep metric dimensions low-cardinality** (channel, region, status code class — not user ID or order ID). High-cardinality data belongs in traces/logs.

---

## 6. Client-side telemetry (web and mobile)

### Web
- **Application Insights JavaScript SDK** (or OTel JS web SDK) for page views, Core Web Vitals, JS errors, and dependency calls, with **distributed tracing correlation** (`traceparent`) to the backend.
- For Blazor WASM, capture unhandled exceptions via a custom `ErrorBoundary`/logger provider and send to the backend or JS SDK.
- Respect consent: load analytics only after consent where required (GDPR/ePrivacy).

### Mobile
- Crash reporting (Sentry/Crashlytics) + OTel/App Insights for events and API timing ([06 §14](06-mobile-applications.md#14-observability-and-crash-reporting)).
- Tag all telemetry with **app version, OS version, device model class** — essential for "is this bug only on 2.3.x on Android 14?".
- Batch and send telemetry efficiently (battery, data usage); queue offline.

---

## 7. Health and synthetic monitoring

- **Health endpoints** for orchestrator probes ([03 §14](03-api-design-and-development.md#14-health-checks)).
- **Availability (synthetic) tests**: Application Insights **standard availability tests** hitting key public endpoints from multiple regions every 5 minutes; alert on failures from ≥ 2 locations.
- **Synthetic user journeys**: scheduled Playwright scripts (e.g., in a Container Apps Job or GitHub Actions schedule) that sign in with a test account and perform critical flows in production; report results as custom availability telemetry.
- **Dependency health**: Azure Resource Health and Service Health alerts for the services you depend on.

---

## 8. SLIs, SLOs and error budgets

### 8.1 Definitions

- **SLI (indicator)**: a measured ratio of good events to total events. E.g., *proportion of `POST /orders` requests that succeed (non-5xx) in < 1 s*.
- **SLO (objective)**: target for an SLI over a window. E.g., *99.5% over 28 days*.
- **Error budget**: `1 − SLO` — the allowed unreliability (0.5% of requests ≈ 3.4 h of total outage in 28 days).
- **SLA**: contractual commitment to customers (always looser than the SLO).

### 8.2 Practice

- Define SLOs for **critical user journeys** (sign in, browse catalogue, place order, receive notification), not every endpoint.
- Measure at the **edge closest to the user** (Front Door logs, API request metrics, client telemetry) — not at the database.
- Implement SLIs as Log Analytics/KQL queries or Azure Monitor metrics; visualise burn rate in a workbook or Azure Managed Grafana.
- **Error budget policy**: when the budget is exhausted, prioritise reliability work over features until it recovers. Agree this with product owners upfront.
- **Composite SLA awareness**: your availability is bounded by the product of your dependencies' availability — design redundancy for critical paths.

Example KQL SLI (API success rate):

```kusto
AppRequests
| where TimeGenerated > ago(28d)
| where Name == "POST /v1/orders"
| summarize total = count(), good = countif(toint(ResultCode) < 500 and DurationMs < 1000)
| extend sli = todouble(good) / total
```

---

## 9. Alerting

### 9.1 Principles

- **Alert on symptoms users feel** (SLO burn rate, error rate, latency, availability test failures), not on every cause (CPU 80%). Cause-based signals go on dashboards.
- **Every alert is actionable** and links to a **runbook**. If nobody needs to act, it's not an alert — make it a report.
- **Multi-window, multi-burn-rate** SLO alerts (e.g., fast burn: 2% budget in 1 h → page; slow burn: 10% in 3 days → ticket) reduce noise while catching real problems.
- **Severity levels**: Sev 1 (page immediately — customer-impacting outage), Sev 2 (page in business hours / urgent ticket), Sev 3 (ticket).
- **Alerts as code**: define alert rules, action groups, and workbooks in Bicep alongside the app.

### 9.2 Baseline alerts per service

| Alert | Signal |
|---|---|
| SLO fast/slow burn | Request success/latency SLIs |
| Availability test failures | Synthetic tests from ≥ 2 regions |
| Spike in 5xx / exceptions | `AppRequests`, `AppExceptions` vs baseline (smart detection / dynamic thresholds) |
| Dead-letter queue depth > 0 (or growth) | Service Bus metrics |
| Queue backlog age/length growing | Service Bus active message count / oldest message age |
| Database CPU/DTU/storage saturation | Azure SQL metrics (warning, ticket) |
| Container restarts / replica crash loops | Container Apps system logs/metrics |
| Certificate/secret expiry approaching | Key Vault events |
| Budget threshold | Cost Management |
| Mobile crash-free rate drop | Sentry/Crashlytics alert |

Route alerts via **action groups** to on-call tooling (PagerDuty, Opsgenie, Teams, email). Avoid email-only for Sev 1.

---

## 10. Telemetry cost control

Telemetry volume (especially logs and traces in Log Analytics) can become a significant cost. Control it deliberately:

- **Sampling**: the Azure Monitor distro supports **rate-limited or fixed-percentage sampling** for traces; keep 100% of errors where possible (tail-based sampling via an OTel Collector if you need "keep all failed traces").
- **Filter noise**: exclude health checks, static file requests, and chatty dependencies from traces; raise log levels for framework categories.
- **Table plans**: use **Basic/Auxiliary logs** for high-volume, rarely queried data (verbose diagnostics); Analytics plan for data used in alerts/dashboards.
- **Retention**: e.g., 30–90 days interactive, archive longer for compliance.
- **Daily cap** on non-production workspaces.
- **Metrics over logs** for counting things — metrics are pre-aggregated and cheap.
- Review the **Usage and estimated costs** blade monthly; find top tables/sources by ingestion.

---

## 11. Dashboards

- **Aspire dashboard** for local development (and optionally standalone for lightweight environments).
- **Application Insights** experiences: Application Map, Transaction search, Failures, Performance, Live Metrics.
- **Azure Workbooks** (in IaC) for per-service and per-journey views: SLOs, RED metrics, dependency health, deployments.
- **Azure Managed Grafana** if the team prefers Grafana — connects to Azure Monitor, Log Analytics, and Prometheus.
- Standard dashboard layout per service: SLO status → traffic/errors/latency → dependencies → saturation → recent deployments.
- **Deployment annotations**: pipelines emit a deployment event (release annotation) so regressions line up visually with deploys.

---

## 12. Diagnostics and profiling

| Tool | Use |
|---|---|
| **Application Insights Profiler** / **.NET Profiler for Azure Monitor** | Sampled production CPU traces; find hot paths. |
| **Snapshot Debugger** | Capture local variables at exception time in production. |
| **dotnet-counters** | Live runtime counters (GC, thread pool, exceptions, request rate). |
| **dotnet-trace** | Collect EventPipe traces for CPU/allocation analysis (view in PerfView, Visual Studio, speedscope). |
| **dotnet-dump** / **dotnet-gcdump** | Memory dumps and GC heap analysis for leaks/hangs. |
| **dotnet-monitor** | Sidecar/HTTP API to collect dumps, traces, metrics on demand or by trigger in containers. |
| **Container Apps console / log streaming** | Ad-hoc inspection (chiseled images have no shell — rely on telemetry and dotnet-monitor). |
| **Azure SQL Query Store / Intelligent Insights** | Database-side performance diagnostics. |

Make sure you can capture a dump from production **before** you need one (permissions, storage destination, procedure documented in a runbook).

---

## 13. Incident management and on-call

### 13.1 On-call

- Defined rotation with primary/secondary; reasonable load (track pages per shift; fix noisy alerts).
- On-call has access (via PIM) to the tools needed, and runbooks for common failures.
- Hand-over notes between shifts.

### 13.2 Incident lifecycle

1. **Detect** (alert or user report) → **declare** an incident with severity.
2. **Assign roles**: incident commander, communications lead, subject-matter responders.
3. **Mitigate first** (roll back, fail over, toggle feature flag off, scale out, block abusive traffic) — root cause later.
4. **Communicate**: internal channel, status page updates for customer-facing incidents at regular intervals.
5. **Resolve** and monitor.
6. **Post-incident review** within ~5 working days: **blameless**, timeline, contributing factors, what went well, action items with owners and dates. Track action items to completion.

### 13.3 Runbooks

Each alert links to a runbook in `docs/runbooks/` with: what the alert means, impact, diagnosis steps (queries, dashboards), mitigation steps, escalation contacts. Automate runbook steps where possible (scripts, Azure Automation, GitHub Actions workflows with manual dispatch).

### 13.4 Operational metrics

Track **DORA metrics** (deployment frequency, lead time for changes, change failure rate, time to restore service — see [11](11-devops-and-delivery.md#12-measuring-delivery-performance)), plus MTTA/MTTR, pages per week, and SLO attainment over time.

---

## 14. References

- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [.NET observability with OpenTelemetry](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel)
- [Azure Monitor OpenTelemetry Distro for .NET](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore)
- [Built-in .NET metrics](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics)
- [Data redaction in .NET](https://learn.microsoft.com/dotnet/core/extensions/data-redaction)
- [Application Insights availability tests](https://learn.microsoft.com/azure/azure-monitor/app/availability)
- [Azure Monitor cost and usage](https://learn.microsoft.com/azure/azure-monitor/cost-usage)
- [Google SRE Book — SLOs and alerting](https://sre.google/sre-book/service-level-objectives/) · [The Site Reliability Workbook — Alerting on SLOs](https://sre.google/workbook/alerting-on-slos/)
- [dotnet-monitor](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-monitor)
