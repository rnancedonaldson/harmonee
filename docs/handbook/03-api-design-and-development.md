# 03 — API Design & Development

> Building HTTP APIs with ASP.NET Core (.NET 10) that serve web, mobile, and partner clients.

---

## Contents

1. [Principles](#1-principles)
2. [Minimal APIs vs controllers](#2-minimal-apis-vs-controllers)
3. [Resource and URL design](#3-resource-and-url-design)
4. [Request and response design](#4-request-and-response-design)
5. [Versioning](#5-versioning)
6. [OpenAPI and client generation](#6-openapi-and-client-generation)
7. [Error handling](#7-error-handling)
8. [Validation](#8-validation)
9. [Pagination, filtering and sorting](#9-pagination-filtering-and-sorting)
10. [Concurrency and idempotency](#10-concurrency-and-idempotency)
11. [Long-running operations](#11-long-running-operations)
12. [Caching](#12-caching)
13. [Security controls in the API](#13-security-controls-in-the-api)
14. [Health checks](#14-health-checks)
15. [Calling downstream services](#15-calling-downstream-services)
16. [Background processing](#16-background-processing)
17. [Other protocols: gRPC, SignalR, GraphQL, webhooks](#17-other-protocols-grpc-signalr-graphql-webhooks)
18. [API Management (gateway)](#18-api-management-gateway)
19. [Putting it together: Program.cs](#19-putting-it-together-programcs)
20. [References](#20-references)

---

## 1. Principles

1. **Contract-first mindset.** The OpenAPI document is a reviewed artefact. Clients (web, mobile, partners) are generated from it.
2. **Consistency beats cleverness.** Every endpoint follows the same conventions for naming, errors, pagination, dates, and IDs. Consumers learn the API once.
3. **Backward compatibility.** Mobile apps in the field can't be force-updated instantly. Additive changes only within a version; breaking changes require a new version and a deprecation period.
4. **Secure by default.** Endpoints require authentication unless explicitly marked anonymous (fallback authorization policy).
5. **Observable.** Every request is traced; errors return a correlation/trace ID.
6. **Efficient.** Async I/O, projections over full entities, pagination everywhere, caching where safe.

Follow the [Microsoft REST API Guidelines](https://github.com/microsoft/api-guidelines) and [Azure API design best practices](https://learn.microsoft.com/azure/architecture/best-practices/api-design) unless this document says otherwise.

---

## 2. Minimal APIs vs controllers

| | Minimal APIs | Controllers (MVC) |
|---|---|---|
| Microsoft's direction | ✅ Recommended for new APIs | Fully supported |
| Performance | Slightly faster, lower allocation | Good |
| Native AOT | ✅ Supported (with RDG) | ❌ Not supported |
| Fits vertical slices | ✅ Naturally | ⚠ Controllers group by resource |
| Filters / conventions | Endpoint filters, route groups | Rich filter pipeline, conventions |
| Validation | Built-in in .NET 10 (`AddValidation`) | Model binding validation |
| Familiarity | Newer | Long-established |

**Default: Minimal APIs organised with route groups, one file per feature/endpoint.** Controllers are acceptable when a team is deeply invested in MVC conventions; don't mix both styles within one module.

### 2.1 Organising Minimal APIs

Avoid a 2,000-line `Program.cs`. Each module maps its own endpoints:

```csharp
// Ordering/OrderingModule.cs
public static class OrderingModule
{
    public static IServiceCollection AddOrderingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<OrderingDbContext>(/* ... */);
        services.AddScoped<PlaceOrderHandler>();
        services.AddScoped<GetOrderHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        group.MapPlaceOrder();
        group.MapGetOrder();
        group.MapCancelOrder();
        return app;
    }
}

// Ordering/Features/GetOrder/GetOrderEndpoint.cs
internal static class GetOrderEndpoint
{
    public static RouteHandlerBuilder MapGetOrder(this RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", HandleAsync)
             .WithName("GetOrder")
             .WithSummary("Gets an order by ID")
             .Produces<OrderResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<OrderResponse>, NotFound>> HandleAsync(
        Guid id, GetOrderHandler handler, CancellationToken ct)
    {
        var order = await handler.HandleAsync(new OrderId(id), ct);
        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
    }
}
```

Use **`TypedResults`** and `Results<T1, T2, ...>` return types so OpenAPI metadata is inferred and handlers are unit-testable.

---

## 3. Resource and URL design

### 3.1 URLs

- **Nouns, plural, lowercase, kebab-case**: `/orders`, `/orders/{orderId}/lines`, `/payment-methods`.
- **Hierarchy only for true containment**, max ~2 levels deep: `/customers/{id}/orders` is fine; `/customers/{id}/orders/{id}/lines/{id}/discounts` is not — promote to `/order-lines/{id}/discounts` or flatten.
- **No verbs** in paths, except for actions that don't map to CRUD — use a sub-resource action sparingly: `POST /orders/{id}/cancel` (preferable to `PATCH status=cancelled` when it's a business operation with rules and side effects).
- **IDs are opaque** to clients. Prefer GUIDs (v7 for index-friendliness — `Guid.CreateVersion7()`) or other non-guessable IDs. Don't expose sequential database integers publicly (enumeration risk).

### 3.2 HTTP methods

| Method | Semantics | Idempotent | Safe | Typical success codes |
|---|---|---|---|---|
| `GET` | Read | ✅ | ✅ | 200, 304 |
| `POST` | Create / non-idempotent action | ❌ (make it so with Idempotency-Key) | ❌ | 201 + `Location`, 202, 200 |
| `PUT` | Full replace (or create at known URI) | ✅ | ❌ | 200, 204, 201 |
| `PATCH` | Partial update (JSON Merge Patch `application/merge-patch+json` preferred; JSON Patch if needed) | ❌* | ❌ | 200, 204 |
| `DELETE` | Remove | ✅ | ❌ | 204, 404 (or 204 if already gone) |

### 3.3 Status codes (use the right ones)

| Code | When |
|---|---|
| 200 OK | Successful read/update with body |
| 201 Created | Resource created; include `Location` header and usually the resource |
| 202 Accepted | Async processing started; include status URL |
| 204 No Content | Success, no body |
| 304 Not Modified | Conditional GET matched ETag |
| 400 Bad Request | Malformed request / validation failure (Problem Details with `errors`) |
| 401 Unauthorized | Missing/invalid authentication |
| 403 Forbidden | Authenticated but not permitted |
| 404 Not Found | Resource doesn't exist (or caller may not know it exists) |
| 409 Conflict | State conflict (duplicate, invalid state transition) |
| 412 Precondition Failed | `If-Match` ETag mismatch (optimistic concurrency) |
| 415 Unsupported Media Type | Wrong `Content-Type` |
| 422 Unprocessable Content | Semantically invalid (business rule violated) — optional; many teams use 400 for all validation; **pick one and be consistent** |
| 429 Too Many Requests | Rate limited; include `Retry-After` |
| 500 Internal Server Error | Unhandled failure (no internal details leaked) |
| 503 Service Unavailable | Temporarily unavailable / dependency down; `Retry-After` |

---

## 4. Request and response design

### 4.1 DTOs

- **Never expose EF Core entities or domain objects directly.** Use dedicated request/response records. This decouples the contract from persistence and prevents over-posting/mass-assignment.
- Responses are **shaped for clients**, not mirrors of tables.
- Request DTOs contain only fields the client may set.

```csharp
public sealed record CreateProductRequest(string Name, string? Description, decimal Price, string Currency);
public sealed record ProductResponse(Guid Id, string Name, string? Description, MoneyDto Price, DateTimeOffset CreatedAt);
```

### 4.2 JSON conventions

- `camelCase` property names (ASP.NET Core default).
- **Enums as strings** (`JsonStringEnumConverter`) — readable and robust to reordering. Clients must tolerate unknown enum values (forward compatibility).
- **Dates**: ISO 8601 with offset, UTC (`2026-10-17T14:30:00Z`). `DateOnly` as `2026-10-17`.
- **Money**: `{ "amount": 12.50, "currency": "GBP" }` — amount as number with decimal semantics, or string if clients have floating-point risk (JavaScript); be consistent.
- **Nulls**: omit or include consistently; document. Don't use `null` to mean two different things.
- **No envelope** for single resources (`{ data: ... }`) — return the resource. Collections use a consistent page object (§9).
- Use **System.Text.Json source generation** (`JsonSerializerContext`) for performance and AOT compatibility.
- Clients must **ignore unknown properties** (tolerant reader) so additive changes are non-breaking.

```csharp
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});
```

### 4.3 Headers

- Correlation: support W3C `traceparent` (automatic with OpenTelemetry). Return the trace ID in Problem Details and optionally an `X-Request-Id`/`traceresponse` header for support.
- `Idempotency-Key` for unsafe operations (§10).
- `ETag`/`If-Match`/`If-None-Match` for concurrency and caching.
- `Accept-Language` for localized messages.
- `Deprecation` and `Sunset` headers on deprecated endpoints (RFC 9745 / RFC 8594).

---

## 5. Versioning

### 5.1 Strategy

- **Default: URL path segment** — `/v1/orders`. Highly visible, cache-friendly, easy for mobile and partners. (Header or media-type versioning are valid alternatives but harder to debug and route at the edge.)
- Version the **whole API surface** (major versions), not individual endpoints, to keep mental model simple.
- Use [`Asp.Versioning.Http`](https://github.com/dotnet/aspnet-api-versioning) for Minimal APIs (and `Asp.Versioning.Mvc` for controllers).

```csharp
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1);
    o.ReportApiVersions = true;                  // api-supported-versions header
    o.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(o =>
{
    o.GroupNameFormat = "'v'V";
    o.SubstituteApiVersionInUrl = true;
});

var versionSet = app.NewApiVersionSet().HasApiVersion(new(1)).HasApiVersion(new(2)).ReportApiVersions().Build();
var v1 = app.MapGroup("/v{version:apiVersion}").WithApiVersionSet(versionSet);
```

### 5.2 What counts as breaking

| Non-breaking (allowed within a version) | Breaking (needs new version) |
|---|---|
| Adding an endpoint | Removing/renaming an endpoint or field |
| Adding an optional request field | Making an optional field required |
| Adding a response field | Changing a field's type or format |
| Adding an enum value (*if* clients are tolerant — document this) | Changing semantics/behaviour of an existing field |
| Relaxing validation | Tightening validation |
| | Changing error codes/status codes clients rely on |
| | Changing auth requirements |

### 5.3 Deprecation policy

- Support at least **N and N-1 major versions** simultaneously.
- For mobile: support every version used by app releases still above a usage threshold (e.g., > 2% of active users), and use **minimum supported app version** checks to prompt upgrades ([06](06-mobile-applications.md#11-app-versioning-and-forced-upgrades)).
- Announce deprecation ≥ 6 months ahead for partners; emit `Deprecation`/`Sunset` headers; monitor usage of deprecated versions by client before removal.

---

## 6. OpenAPI and client generation

### 6.1 Document generation

Use the built-in `Microsoft.AspNetCore.OpenApi` (Swashbuckle is no longer in the templates). .NET 10 generates **OpenAPI 3.1** by default and can serve YAML.

```csharp
builder.Services.AddOpenApi("v1", o =>
{
    o.AddDocumentTransformer<BearerSecuritySchemeTransformer>(); // describe auth
});

app.MapOpenApi();                    // /openapi/v1.json
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();     // Scalar UI (Scalar.AspNetCore package) — or Swagger UI / Redoc
}
```

Practices:
- Enrich metadata: `WithName` (operationId — stable, used by generators), `WithSummary`, `WithDescription`, `WithTags`, `Produces`, `ProducesProblem`. XML doc comments on DTOs flow into schema descriptions (.NET 10 XML comment generator).
- **Generate the document at build time** (`Microsoft.Extensions.ApiDescription.Server`) and commit it or publish it as a build artefact. Diff it in PRs to catch unintended breaking changes — tools like `oasdiff` can fail CI on breaking changes.
- Lint with **Spectral** using a ruleset that encodes these guidelines.
- Expose the UI only in non-production or behind auth.

### 6.2 Client generation

Generate typed clients from the OpenAPI document for web (C#/TypeScript) and mobile:

| Tool | Notes |
|---|---|
| **Kiota** (Microsoft) | Generates C#, TypeScript, and more; lightweight runtime; good for large APIs. |
| **NSwag** | Mature; C# and TypeScript; MSBuild integration. |
| **Refit** | Interface-based; you write interfaces (or generate with Refitter). Popular in MAUI. |

Put the generated C# client in `src/Shared/Api.Client` and regenerate in CI to guarantee clients match the API.

---

## 7. Error handling

### 7.1 Problem Details (RFC 9457) everywhere

All error responses use `application/problem+json`:

```json
{
  "type": "https://api.contoso.com/problems/order-already-shipped",
  "title": "Order already shipped",
  "status": 409,
  "detail": "Order 7f3c... was shipped on 2026-10-15 and can no longer be cancelled.",
  "instance": "/v1/orders/7f3c.../cancel",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "code": "ORDER_ALREADY_SHIPPED"
}
```

- `type` is a stable URI identifying the problem kind (document these); `code` is an optional stable machine-readable string for clients to switch on. **Clients branch on `type`/`code`, never on `title`/`detail` text** (which may be localized).
- Validation errors add an `errors` dictionary: `{ "errors": { "lines[0].quantity": ["Must be greater than 0."] } }`.
- **Never leak** stack traces, SQL, or internal hostnames in production.

### 7.2 Implementation

```csharp
builder.Services.AddProblemDetails(o =>
{
    o.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

app.UseExceptionHandler();
app.UseStatusCodePages();   // problem details for bare 404/405 etc.
```

```csharp
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            OperationCanceledException when ctx.RequestAborted.IsCancellationRequested => (499, "Client closed request"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status >= 500) logger.LogError(ex, "Unhandled exception");

        ctx.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new()
        {
            HttpContext = ctx,
            Exception = ex,
            ProblemDetails = { Status = status, Title = title }
        });
    }
}
```

### 7.3 Mapping Results to HTTP

Map domain `Result`/`Error` types to Problem Details in one place:

```csharp
public static IResult ToProblem(this Error error) => error.Kind switch
{
    ErrorKind.Validation => TypedResults.ValidationProblem(error.ValidationErrors),
    ErrorKind.NotFound   => TypedResults.Problem(statusCode: 404, title: error.Title, type: error.TypeUri, extensions: error.Extensions),
    ErrorKind.Conflict   => TypedResults.Problem(statusCode: 409, title: error.Title, type: error.TypeUri, extensions: error.Extensions),
    ErrorKind.Forbidden  => TypedResults.Problem(statusCode: 403, title: error.Title),
    _                    => TypedResults.Problem(statusCode: 500)
};
```

---

## 8. Validation

Validate at **three levels**:

1. **Shape/syntax** (required fields, lengths, formats, ranges) — at the API edge, before the handler runs.
2. **Business rules** (stock available, order not shipped) — in the domain/handler; return `Result` errors.
3. **Database constraints** (unique indexes, FKs, check constraints) — last line of defence.

### 8.1 Edge validation options

- **Built-in Minimal API validation (.NET 10)**: `builder.Services.AddValidation();` validates DataAnnotations on parameters and request types automatically and returns a 400 Problem Details. Source-generated and AOT-friendly. Good default for simple rules.
- **FluentValidation** (Apache-2.0): for complex/conditional rules; wire via an endpoint filter.

```csharp
internal sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        if (ctx.Arguments.OfType<T>().FirstOrDefault() is { } arg)
        {
            var result = await validator.ValidateAsync(arg, ctx.HttpContext.RequestAborted);
            if (!result.IsValid) return TypedResults.ValidationProblem(result.ToDictionary());
        }
        return await next(ctx);
    }
}
```

Rules:
- Validate **all** input, including route/query parameters and headers.
- Set **maximum lengths** on every string and **maximum counts** on every collection (DoS and storage protection).
- Reject unknown/extra fields only if you need strictness; by default ignore them.
- Configure request body size limits (Kestrel `MaxRequestBodySize`, per-endpoint `[RequestSizeLimit]`/`WithRequestSizeLimit` equivalents) — especially for uploads.

---

## 9. Pagination, filtering and sorting

**Every collection endpoint is paginated** with a server-enforced maximum page size.

### 9.1 Cursor (keyset) pagination — default for feeds and large/mutable data

```
GET /v1/orders?limit=50&cursor=eyJpZCI6IjAxOTI...
```

```json
{
  "items": [ /* ... */ ],
  "nextCursor": "eyJpZCI6IjAxOTI4ZjM...",
  "hasMore": true
}
```

- Stable under inserts/deletes; efficient (`WHERE (CreatedAt, Id) < (@c, @id) ORDER BY CreatedAt DESC, Id DESC`).
- Cursor is opaque (base64url-encoded JSON of sort keys) — clients must not parse it.
- Ideal for mobile infinite scroll.

### 9.2 Offset pagination — acceptable for small admin grids

```
GET /v1/products?page=3&pageSize=25
```

Returns `items`, `page`, `pageSize`, and optionally `totalCount` (expensive on large tables — make it opt-in).

### 9.3 Filtering and sorting

- Simple, explicit query parameters: `?status=shipped&createdAfter=2026-01-01&sort=-createdAt,name`.
- **Allow-list** sortable/filterable fields; never pass user input into dynamic SQL/`OrderBy` strings without validation.
- For rich querying needs consider OData (`Microsoft.AspNetCore.OData`) or GraphQL — but these expand the attack/performance surface; guard with limits.

---

## 10. Concurrency and idempotency

### 10.1 Optimistic concurrency with ETags

- Return `ETag` on GET (derived from a row version/`xmin`/hash).
- Require `If-Match` on `PUT`/`PATCH`/`DELETE` for resources edited by multiple users; return **412** on mismatch, **428 Precondition Required** if missing (when mandatory).
- Map to EF Core concurrency tokens (`[Timestamp]`/`rowversion` on SQL Server; `xmin` on PostgreSQL).

### 10.2 Idempotency keys

Networks are unreliable — especially mobile. Clients retry. Make unsafe operations safe to retry:

- Clients send `Idempotency-Key: <uuid>` on `POST` (and non-idempotent `PATCH`).
- Server stores `(key, user, request hash) → response` for a retention window (e.g., 24 h) in the database or Redis.
- Repeat with same key + same payload → return the stored response. Same key + different payload → `422`/`409`.
- Concurrent duplicates → one wins; the other gets `409` (or waits).

This is essential for payments, order placement, and anything triggered from mobile.

---

## 11. Long-running operations

Never hold an HTTP request open for minutes. Use the **asynchronous request-reply** pattern:

1. `POST /v1/reports` → validate, enqueue (Service Bus), return **202 Accepted** with `Location: /v1/operations/{operationId}` (and optionally `Retry-After`).
2. Client polls `GET /v1/operations/{id}` → `{ "status": "running" | "succeeded" | "failed", "resourceLocation": "/v1/reports/{id}", "error": {...} }`.
3. Optionally push completion via SignalR (web) or push notification (mobile), or call a client-registered webhook.

Process the work in a background worker / Function with durable state. See [Asynchronous Request-Reply pattern](https://learn.microsoft.com/azure/architecture/patterns/async-request-reply).

---

## 12. Caching

| Layer | Use | Notes |
|---|---|---|
| **HTTP caching headers** | `Cache-Control`, `ETag`, `Last-Modified` for client/CDN caching | Public, non-personalised data only for shared caches. `Cache-Control: private, no-store` for sensitive responses. |
| **Front Door / CDN** | Static assets, public GETs | Purge on deploy; vary carefully. |
| **Output caching** (`AddOutputCache`) | Server-side caching of whole responses | Tag-based eviction; Redis-backed store for multi-instance. Don't cache authenticated, per-user responses unless varied by user. |
| **HybridCache** | Application data (L1 memory + L2 Redis), stampede protection | Default for data caching. See [04](04-data-and-persistence.md#7-caching). |

Never cache responses containing personal data in shared caches.

---

## 13. Security controls in the API

Full details in [07 — Security & Identity](07-security-and-identity.md). API-specific essentials:

- **Authentication**: JWT bearer tokens from Entra ID / External ID via `Microsoft.Identity.Web` (or `AddJwtBearer`). Validate issuer, audience, lifetime, signature.
- **Authorization**: **fallback policy requires authenticated users**; endpoints opt in to `AllowAnonymous` explicitly. Use **policy-based** and **resource-based** authorization (`IAuthorizationService`) — check *ownership* (`order.CustomerId == currentUser.Id`), not just roles. Broken object-level authorization is the #1 API vulnerability (OWASP API1).
  ```csharp
  builder.Services.AddAuthorizationBuilder()
      .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
      .AddPolicy("orders:write", p => p.RequireScope("Orders.Write"))   // Microsoft.Identity.Web
      .AddPolicy("admin", p => p.RequireRole("Admin"));
  ```
- **HTTPS only**; HSTS (TLS termination at Front Door; still enforce at the app).
- **CORS**: only for browser clients calling the API directly; explicit origin allow-list; never `AllowAnyOrigin` with credentials. If the web app uses a BFF, the API may not need CORS at all.
- **Rate limiting** (`AddRateLimiter`): per user/client ID partition (token bucket or sliding window); global limits at Front Door/APIM too. Return 429 with `Retry-After`.
- **Request limits**: body size, header size, max collection lengths, query complexity, timeouts (`AddRequestTimeouts`).
- **Mass assignment protection**: DTOs with only permitted fields.
- **Security headers**: `X-Content-Type-Options: nosniff`, no `Server` header (`AddServerHeader = false`).
- **Audit logging** for sensitive operations (who, what, when, from where).

---

## 14. Health checks

Expose separate endpoints for orchestrator probes:

| Endpoint | Probe | Checks |
|---|---|---|
| `/health/live` | Liveness | Process is alive; **no dependency checks** (otherwise a DB blip causes restart storms). |
| `/health/ready` | Readiness | Critical dependencies reachable (DB, cache, broker). Instance taken out of rotation if failing. |
| `/health/startup` | Startup | Warm-up complete (migrations checked, caches primed). |

Aspire ServiceDefaults sets up `/health` and `/alive`. Add dependency checks with `AspNetCore.HealthChecks.*` packages (SqlServer, Npgsql, Redis, AzureServiceBus). Don't expose detailed health output publicly — restrict to internal network or require auth.

---

## 15. Calling downstream services

- Always via **`IHttpClientFactory`** with **typed clients**.
- Add **standard resilience** (`AddStandardResilienceHandler()`): retries with exponential backoff + jitter, circuit breaker, attempt timeout, total timeout. Aspire ServiceDefaults applies this to all clients.
- Use **service discovery** (`https+http://catalog`) with Aspire for local/dev; real URLs from configuration in Azure.
- Authenticate service-to-service with **managed identity** tokens (`DefaultAzureCredential` → token for the downstream app's audience) or Entra client credentials. Propagate user identity with **On-Behalf-Of** only when the downstream needs the user context.
- Set **timeouts** shorter than the caller's timeout. Propagate `CancellationToken`.
- Don't retry non-idempotent calls unless the downstream supports idempotency keys.

```csharp
builder.Services.AddHttpClient<PaymentsClient>(c => c.BaseAddress = new("https+http://payments"))
    .AddStandardResilienceHandler();
```

---

## 16. Background processing

| Need | Use |
|---|---|
| In-process, lightweight, loss-tolerant (cache warm-up, periodic metrics) | `BackgroundService` + `PeriodicTimer` |
| In-process queue for work triggered by requests (non-critical) | `Channel<T>` + `BackgroundService` |
| Must survive restarts / be retried / scale independently | **Service Bus** + worker (Container App with KEDA scaler) or **Azure Functions** |
| Scheduled jobs | **Container Apps Jobs** (cron) or Functions timer trigger |
| Long-running, multi-step workflows | **Durable Functions** / Durable Task Scheduler |
| Outbox publishing | Dedicated worker polling the outbox table ([04](04-data-and-persistence.md#6-consistency-transactions-and-the-outbox)) |

In multi-instance deployments, ensure scheduled jobs don't run on every replica (use Container Apps Jobs, or a distributed lock/lease).

Hangfire and Quartz.NET are viable if you need a self-hosted job dashboard; prefer platform services first.

---

## 17. Other protocols: gRPC, SignalR, GraphQL, webhooks

| Protocol | Use when | Notes |
|---|---|---|
| **gRPC** | High-performance internal service-to-service calls; streaming; strongly typed contracts. | HTTP/2; Container Apps supports it. Not ideal for browsers (gRPC-Web needed). Rarely necessary in a modular monolith. |
| **SignalR** | Real-time server→client updates for web (and foreground mobile). | Use **Azure SignalR Service** when scaled out; authenticate connections; keep messages small. |
| **GraphQL** | Many clients with very different data-shape needs; aggregation across sources. | Hot Chocolate (MIT) is the leading .NET server. Requires query cost limits, depth limits, persisted queries. Don't adopt just for novelty. |
| **Webhooks (outbound)** | Notifying partner systems. | Sign payloads (HMAC with per-subscriber secrets), include timestamp to prevent replay, retry with backoff, dead-letter, let subscribers verify. Consider CloudEvents format. Event Grid can deliver for you. |
| **Server-Sent Events** | Simple one-way streaming (e.g., AI token streaming). | `TypedResults.ServerSentEvents` in .NET 10. |

---

## 18. API Management (gateway)

Introduce **Azure API Management (APIM)** when you have **external/partner consumers** or multiple backend APIs to unify:

- Subscription keys/products, quotas, and rate limits per consumer.
- Developer portal with docs and onboarding.
- Version sets and revisions.
- Policies: JWT validation, IP filtering, request/response transformation, caching, mocking.
- Centralised analytics.
- **AI gateway** capabilities (token limits, semantic caching, load-balancing across model deployments) if exposing LLM endpoints ([13](13-ai-integration.md)).

Choose the **v2 tiers** (Basic v2 / Standard v2 / Premium v2) for VNet integration and faster provisioning. For first-party web/mobile clients only, APIM is optional — Front Door + the API's own auth and rate limiting may suffice.

Avoid putting business logic in gateway policies.

---

## 19. Putting it together: `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                         // Aspire: OTel, health checks, service discovery, resilience

// Data & infrastructure (Aspire client integrations)
builder.AddSqlServerDbContext<OrderingDbContext>("ordering-db");
builder.AddRedisDistributedCache("cache");
builder.AddAzureServiceBusClient("messaging");
builder.Services.AddHybridCache();

// Security
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("EntraId"));
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetTokenBucketLimiter(
            ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new TokenBucketRateLimiterOptions { TokenLimit = 100, TokensPerPeriod = 50, ReplenishmentPeriod = TimeSpan.FromSeconds(10) }));
});

// API plumbing
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddApiVersioning(/* see §5 */).AddApiExplorer(/* ... */);
builder.Services.AddOutputCache();
builder.Services.AddRequestTimeouts(o => o.DefaultPolicy = new() { Timeout = TimeSpan.FromSeconds(30) });

// Modules
builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseRequestTimeouts();
app.UseOutputCache();

app.MapOpenApi().AllowAnonymous();
app.MapDefaultEndpoints();                            // health endpoints from ServiceDefaults

var api = app.MapGroup("/v{version:apiVersion}").WithApiVersionSet(/* ... */);
api.MapOrderingEndpoints();
api.MapCatalogEndpoints();

app.Run();

public partial class Program;                         // for WebApplicationFactory in integration tests
```

---

## 20. References

- [ASP.NET Core fundamentals](https://learn.microsoft.com/aspnet/core/fundamentals/)
- [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview)
- [What's new in ASP.NET Core 10](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0)
- [OpenAPI in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
- [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api)
- [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457)
- [Microsoft REST API Guidelines](https://github.com/microsoft/api-guidelines)
- [Azure API design best practices](https://learn.microsoft.com/azure/architecture/best-practices/api-design)
- [ASP.NET API Versioning](https://github.com/dotnet/aspnet-api-versioning)
- [Rate limiting middleware](https://learn.microsoft.com/aspnet/core/performance/rate-limit)
- [OWASP API Security Top 10](https://owasp.org/API-Security/)
