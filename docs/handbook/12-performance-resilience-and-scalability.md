# 12 — Performance, Resilience & Scalability

> Keeping systems fast, keeping them up when dependencies fail, and scaling them as demand grows.

---

## Contents

1. [Approach](#1-approach)
2. [Resilience patterns](#2-resilience-patterns)
3. [Graceful degradation and load shedding](#3-graceful-degradation-and-load-shedding)
4. [Startup and shutdown](#4-startup-and-shutdown)
5. [ASP.NET Core performance practices](#5-aspnet-core-performance-practices)
6. [Memory and garbage collection](#6-memory-and-garbage-collection)
7. [Startup time and footprint: ReadyToRun, trimming, Native AOT](#7-startup-time-and-footprint-readytorun-trimming-native-aot)
8. [Concurrency and throughput](#8-concurrency-and-throughput)
9. [Scalability patterns](#9-scalability-patterns)
10. [Measuring: benchmarks, profiling and load tests](#10-measuring-benchmarks-profiling-and-load-tests)
11. [Performance and resilience checklist](#11-performance-and-resilience-checklist)
12. [References](#12-references)

---

## 1. Approach

1. **Set targets first.** Latency and throughput goals come from SLOs and quality-attribute scenarios ([01 §2.3](01-architecture-overview.md#23-quality-attribute-scenarios)). "Fast enough" is defined, not felt.
2. **Measure, don't guess.** Profile before optimising; most time is usually spent in I/O (database, network), not CPU.
3. **Fix the big things first**: N+1 queries, missing indexes, chatty calls, missing caching, synchronous blocking, unbounded result sets.
4. **Design for failure.** Every remote call can fail, be slow, or be throttled. Resilience is part of the design, not an afterthought.
5. **Prefer horizontal scale** of stateless services; push state into managed data services designed to scale.
6. **Keep performance tests in the pipeline** so regressions are caught before production.

---

## 2. Resilience patterns

### 2.1 The toolkit

| Pattern | Purpose | Notes |
|---|---|---|
| **Timeout** | Don't wait forever; free resources. | Every outbound call has a timeout shorter than the caller's. |
| **Retry with exponential backoff + jitter** | Recover from transient faults (network blips, 503, 429, SQL transient errors). | Only for **idempotent** operations or with idempotency keys. Cap attempts (e.g., 3). Honour `Retry-After`. |
| **Circuit breaker** | Stop hammering a failing dependency; fail fast; let it recover. | Opens after a failure ratio threshold; half-open probes. |
| **Bulkhead / concurrency limiter** | Isolate resources so one slow dependency can't exhaust all threads/connections. | Separate `HttpClient`s and limits per dependency. |
| **Fallback** | Provide degraded but useful response. | Cached data, default values, feature hidden. |
| **Hedging** | Send a parallel request if the first is slow (tail latency). | Only for idempotent reads; increases load. |
| **Rate limiting** | Protect your service from overload (inbound) or respect others' limits (outbound). | ASP.NET Core rate limiter inbound; `RateLimiter` strategy outbound. |
| **Idempotency** | Make retries safe. | Idempotency keys, inbox pattern, upserts. |
| **Queue-based load levelling** | Absorb bursts; process at sustainable rate. | Service Bus between API and workers. |

### 2.2 HTTP resilience with Microsoft.Extensions.Http.Resilience

Built on **Polly v8**. The **standard resilience handler** composes (outer → inner): rate limiter → total timeout → retry → circuit breaker → attempt timeout.

```csharp
builder.Services.AddHttpClient<InventoryClient>(c => c.BaseAddress = new("https+http://inventory"))
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        o.Retry.MaxRetryAttempts = 3;
        o.Retry.UseJitter = true;
        o.CircuitBreaker.FailureRatio = 0.2;
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    });

// For idempotent reads where tail latency matters:
builder.Services.AddHttpClient<CatalogClient>(/* ... */).AddStandardHedgingHandler();
```

Aspire ServiceDefaults applies `AddStandardResilienceHandler()` to all `HttpClient`s by default (`ConfigureHttpClientDefaults`). Tune per client where needed. Be careful not to **stack** retries (client retries × gateway retries × SDK retries = retry storms).

### 2.3 Non-HTTP resilience

Use `Microsoft.Extensions.Resilience` / Polly `ResiliencePipeline` for other operations:

```csharp
builder.Services.AddResiliencePipeline("payments-gateway", p => p
    .AddRetry(new() { MaxRetryAttempts = 3, BackoffType = DelayBackoffType.Exponential, UseJitter = true,
                      ShouldHandle = new PredicateBuilder().Handle<TransientPaymentException>() })
    .AddCircuitBreaker(new() { FailureRatio = 0.5, MinimumThroughput = 10, BreakDuration = TimeSpan.FromSeconds(30) })
    .AddTimeout(TimeSpan.FromSeconds(5)));

// usage: inject ResiliencePipelineProvider<string> and call pipeline.ExecuteAsync(...)
```

- **Azure SDKs** have built-in retry policies (configurable via client options) — don't wrap them in additional retries without reason.
- **EF Core** `EnableRetryOnFailure` for SQL transient faults.
- **Service Bus** handles redelivery; consumers should be idempotent and use max delivery count + DLQ.

### 2.4 Resilience telemetry

Polly v8 emits metrics and logs (retries, circuit state changes) — included via OpenTelemetry. Alert on circuits opening for critical dependencies.

---

## 3. Graceful degradation and load shedding

- **Identify critical vs non-critical features.** If recommendations fail, show the page without them; if search is down, offer category browsing.
- **Fallbacks**: serve stale cache (HybridCache can serve stale on factory failure with appropriate design), default content, or a clear "temporarily unavailable" section rather than failing the whole page.
- **Feature flags as kill switches** to turn off expensive or failing features instantly.
- **Load shedding**: when overloaded, reject excess requests early (429/503 with `Retry-After`) rather than letting latency climb for everyone. ASP.NET Core rate limiter with queue limits; concurrency limiter for expensive endpoints; priority for critical paths (checkout over browsing).
- **Timeout budgets**: propagate deadlines; if the remaining budget is too small, fail fast.
- **Client behaviour**: web and mobile clients back off with jitter and respect `Retry-After`; avoid synchronized retry storms (e.g., all mobile apps retrying at the top of the minute).

---

## 4. Startup and shutdown

### Startup
- Keep startup fast and predictable: avoid heavy synchronous work in `Program.cs`; don't run migrations at startup in production.
- Use **startup probes** so the orchestrator waits for warm-up; **readiness** only true once dependencies are reachable and caches primed (if needed).
- Warm up expensive singletons (e.g., JSON serializers, compiled queries) via an `IHostedService` if first-request latency matters.

### Shutdown
- Container platforms send `SIGTERM` then wait (Container Apps default grace period ~30 s) before killing.
- ASP.NET Core stops accepting new requests and drains in-flight ones; configure `HostOptions.ShutdownTimeout` within the platform grace period.
- `BackgroundService`s must honour the `stoppingToken`, finish or checkpoint current work, and exit promptly. Message consumers should stop receiving, complete in-flight messages, and let unfinished ones be redelivered.
- Ensure telemetry is flushed on shutdown (OTel exporters do this on dispose).

---

## 5. ASP.NET Core performance practices

From Microsoft's [ASP.NET Core best practices](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices):

- **Async all the way**; never block (`.Result`, `.Wait()`) — causes thread-pool starvation under load.
- **Avoid synchronous I/O** (synchronous `Stream.Read`, `HttpRequest.Body` reads); Kestrel disallows it by default — keep it that way.
- **Don't read large request bodies into memory**; stream them (to Blob Storage, for example). Use `IFormFile` carefully (it buffers) — prefer streaming `MultipartReader` or direct-to-blob SAS uploads.
- **Return `IAsyncEnumerable<T>`** or paginate large responses; never return unbounded collections.
- **Use `IHttpClientFactory`**; never create/dispose `HttpClient` per request (socket exhaustion).
- **Don't store `HttpContext`** or access it from background threads; copy needed values.
- **Minimise allocations in hot paths**: `System.Text.Json` source generation, `ArrayPool<T>`, `Span<T>`/`Memory<T>`, `StringBuilder` pooling, avoiding LINQ in tight loops (measure first — LINQ is much faster in modern .NET).
- **Compression**: let Front Door compress responses; app-level response compression only if no edge compression (and beware BREACH with secrets in compressed HTTPS responses).
- **HTTP/2 and HTTP/3**: enabled by default in Kestrel (HTTP/3 requires configuration and platform support); edge (Front Door) handles client protocols.
- **Output caching** for cacheable responses; **HybridCache** for data.
- **Avoid exceptions for control flow** — exceptions are expensive; use Result types for expected failures.
- **Frozen collections** (`FrozenDictionary`, `FrozenSet`) for read-heavy lookup tables built at startup.
- **Source-generated** regex (`[GeneratedRegex]`), logging (`[LoggerMessage]`), JSON, options validation, configuration binding — faster and AOT-friendly.
- **Minimal APIs** have lower overhead than MVC; the Request Delegate Generator (RDG) removes reflection.

---

## 6. Memory and garbage collection

- **Server GC** is the default for ASP.NET Core; **DATAS** (Dynamic Adaptation To Application Sizes, on by default since .NET 9 with Server GC) adapts heap count to load — good for containers and scale-out. Keep defaults unless profiling says otherwise.
- .NET respects **container memory limits** (cgroups) automatically; set container memory requests/limits realistically and leave headroom (~25%) above the steady-state working set.
- **Avoid the Large Object Heap** churn (objects ≥ 85 KB): pool large buffers (`ArrayPool<byte>.Shared`, `RecyclableMemoryStream`), stream instead of buffering.
- **Watch for leaks**: static collections that grow, event handlers not unsubscribed, caches without size limits, captured closures in long-lived objects, `IDisposable`s not disposed.
- Monitor GC metrics (`dotnet.gc.*`, heap size, pause time, % time in GC) via OpenTelemetry runtime instrumentation.
- Use `dotnet-gcdump`/`dotnet-dump` to analyse memory issues ([09 §12](09-observability-and-operations.md#12-diagnostics-and-profiling)).

---

## 7. Startup time and footprint: ReadyToRun, trimming, Native AOT

| Option | Benefit | Cost | Use for |
|---|---|---|---|
| **JIT (default)** + Tiered PGO | Best peak throughput after warm-up (Dynamic PGO on by default) | Slower startup | Long-running APIs/web apps |
| **ReadyToRun (R2R)** | Faster startup (precompiled), still JITs hot code | Larger binaries | Apps that scale out frequently / scale from zero |
| **Trimming** | Smaller deployment | Must fix trim warnings; reflection-heavy libraries may break | MAUI (on by default), WASM, AOT apps |
| **Native AOT** | Fastest startup (~ms), smallest memory, no JIT | No dynamic code/reflection emit; not all libraries compatible (EF Core AOT support is still limited; MVC not supported; Minimal APIs supported); longer builds | Functions, workers, small APIs, CLI tools, scale-to-zero microservices, sidecars |

Guidance:
- Main API/web: JIT + R2R is usually the sweet spot. Revisit Native AOT when your dependencies support it.
- Write **new libraries to be trim/AOT-compatible** (`<IsAotCompatible>true</IsAotCompatible>` enables analyzers): prefer source generators over reflection.
- Always measure: startup time, memory, throughput, image size.

---

## 8. Concurrency and throughput

- **Thread-pool starvation** is the classic .NET scalability failure: symptoms are rising latency with low CPU. Causes: sync-over-async, blocking calls, long `lock`s. Detect with `dotnet-counters` (thread pool queue length) and fix the blocking code — don't just raise `MinThreads`.
- **Bounded parallelism**: `Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = 8 }, ...)`; `SemaphoreSlim` to limit concurrent calls to a dependency.
- **Producer/consumer**: `System.Threading.Channels` (bounded channels give back-pressure).
- **Avoid shared mutable state**; use `ConcurrentDictionary`, immutable snapshots, or actor-like single-writer designs. Prefer `lock` (the new `System.Threading.Lock` type in .NET 9+) over complex lock-free code unless profiling demands it.
- **Database connection pools** are finite: keep transactions short; don't hold connections across remote calls; size pool and DB tier together.
- **Message consumers**: tune `MaxConcurrentCalls`/prefetch for Service Bus processors based on downstream capacity; scale replicas via KEDA on queue length.

---

## 9. Scalability patterns

| Pattern | Problem solved | Azure implementation |
|---|---|---|
| **Stateless services + horizontal scale** | Handle more requests by adding instances | Container Apps replicas with HTTP/KEDA scale rules |
| **Cache-aside** | Read load on databases | HybridCache + Azure Managed Redis |
| **CDN / edge caching** | Static and public content load and latency | Front Door caching |
| **Queue-based load levelling** | Bursty writes overwhelm backends | Service Bus queue + scalable workers |
| **Competing consumers** | Parallel processing of messages | Multiple worker replicas on one queue |
| **CQRS read models** | Read and write workloads differ | Projections in SQL/Cosmos/AI Search fed by events |
| **Asynchronous request-reply** | Long operations block requests | 202 + status endpoint ([03 §11](03-api-design-and-development.md#11-long-running-operations)) |
| **Throttling** | Protect resources from overuse | Rate limiter, APIM quotas |
| **Sharding / partitioning** | Single database limits | Cosmos partitioning, Azure SQL elastic pools/sharding, PostgreSQL with Citus (Cosmos DB for PostgreSQL) |
| **Deployment stamps** | Scale units for tenants/regions; blast-radius isolation | Repeated IaC stamps behind Front Door |
| **Geode / multi-region active-active** | Global latency and availability | Front Door + Cosmos multi-region writes |
| **Static content hosting** | Offload static files from compute | Blob/Static Web Apps + Front Door |

Reference: [Azure cloud design patterns](https://learn.microsoft.com/azure/architecture/patterns/).

---

## 10. Measuring: benchmarks, profiling and load tests

- **Micro-benchmarks** with **BenchmarkDotNet** for hot code (serialization, parsing, algorithms): always `[MemoryDiagnoser]`; compare baseline vs change; run on Release builds on quiet machines.
- **Profiling**: Visual Studio profiler / dotTrace / PerfView locally; Application Insights Profiler in production; `dotnet-trace` in containers.
- **Load tests** in a production-like environment with realistic scenarios ([10 §10](10-testing-strategy.md#10-performance-and-load-testing)).
- **Chaos engineering**: **Azure Chaos Studio** to inject faults (kill replicas, add latency, fail over databases, block network to a dependency) in pre-production and, when mature, in production during game days — verify resilience patterns actually work.
- **Performance budgets** in CI: fail if p95 latency, memory, or bundle size regress beyond thresholds.

---

## 11. Performance and resilience checklist

- [ ] SLOs defined for critical journeys; load tested against them.
- [ ] Every outbound call has a timeout; retries only for idempotent operations, with jitter; circuit breakers on critical dependencies.
- [ ] No sync-over-async; `CancellationToken` passed through.
- [ ] All collection endpoints paginated with max page size.
- [ ] EF Core queries projected, no-tracking for reads, no N+1, indexes in place.
- [ ] Caching strategy defined (what, TTL, invalidation); cache failure doesn't break the app.
- [ ] Health probes (liveness, readiness, startup) configured correctly; liveness doesn't check dependencies.
- [ ] Graceful shutdown tested; background services honour cancellation.
- [ ] Autoscale rules set and tested (min replicas for prod latency, max replicas within DB capacity).
- [ ] Non-critical features degrade gracefully; kill-switch flags exist for risky features.
- [ ] Rate limiting inbound; respect outbound limits (429 handling).
- [ ] Idempotency keys on unsafe operations from clients.
- [ ] Message consumers idempotent; DLQ monitored.
- [ ] Zone redundancy enabled in production; DR strategy documented and tested.
- [ ] Chaos experiments run for key failure modes.

---

## 12. References

- [ASP.NET Core best practices](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices)
- [Build resilient HTTP apps](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
- [Polly](https://www.pollydocs.org/)
- [Garbage collection in .NET](https://learn.microsoft.com/dotnet/standard/garbage-collection/)
- [Native AOT deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) · [ASP.NET Core Native AOT support](https://learn.microsoft.com/aspnet/core/fundamentals/native-aot)
- [Performance improvements in .NET 10 (Stephen Toub)](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)
- [Azure cloud design patterns](https://learn.microsoft.com/azure/architecture/patterns/)
- [WAF Reliability pillar](https://learn.microsoft.com/azure/well-architected/reliability/) · [WAF Performance Efficiency](https://learn.microsoft.com/azure/well-architected/performance-efficiency/)
- [Azure Chaos Studio](https://learn.microsoft.com/azure/chaos-studio/)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
