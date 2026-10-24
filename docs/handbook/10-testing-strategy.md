# 10 — Testing Strategy

> What to test, how, and with which tools — across API, web, mobile, and infrastructure.

---

## Contents

1. [Philosophy](#1-philosophy)
2. [Test types and the right mix](#2-test-types-and-the-right-mix)
3. [Tooling](#3-tooling)
4. [Unit tests](#4-unit-tests)
5. [Integration tests](#5-integration-tests)
6. [Contract and API compatibility tests](#6-contract-and-api-compatibility-tests)
7. [Architecture tests](#7-architecture-tests)
8. [Component tests for Blazor](#8-component-tests-for-blazor)
9. [End-to-end tests](#9-end-to-end-tests)
10. [Performance and load testing](#10-performance-and-load-testing)
11. [Security, accessibility and mobile testing](#11-security-accessibility-and-mobile-testing)
12. [Test data management](#12-test-data-management)
13. [Coverage and test quality](#13-coverage-and-test-quality)
14. [Tests in CI](#14-tests-in-ci)
15. [References](#15-references)

---

## 1. Philosophy

1. **Tests exist to enable change with confidence.** A good suite lets you refactor freely and deploy on every merge.
2. **Test behaviour, not implementation.** Assert on observable outcomes (HTTP responses, persisted state, published events), not on which private methods were called. Over-mocked tests break on every refactor and catch few bugs.
3. **Favour realistic tests for services.** For APIs built on ASP.NET Core + EF Core, **integration tests with a real database in a container** catch far more real bugs per line than mock-heavy unit tests. Use the *testing trophy/honeycomb* shape for services: many integration tests, focused unit tests for complex logic, few E2E tests.
4. **Fast feedback.** The PR suite should complete in minutes. Slow tests run in parallel or in later stages.
5. **Deterministic.** No dependence on wall-clock time, random data without seeds, test ordering, or shared mutable state. Flaky tests are bugs — fix or quarantine immediately.
6. **Tests are production code.** Same standards for readability, naming, and review.

---

## 2. Test types and the right mix

| Type | What it verifies | Speed | Where it runs | Volume |
|---|---|---|---|---|
| **Unit** | Domain logic, value objects, validators, pure functions, view models | ms | Every build | Many — wherever logic is non-trivial |
| **Integration** | A slice of the app with real infrastructure: HTTP → handler → EF Core → real DB; message consumers; auth policies | 10–500 ms each (container start amortised) | Every PR | Many — every endpoint/use case |
| **Contract** | API compatibility with consumers (OpenAPI diff, consumer-driven contracts) | Fast | Every PR | Per API |
| **Architecture** | Module boundaries and dependency rules | Fast | Every PR | A handful |
| **Component (bUnit)** | Blazor component rendering and interaction | ms | Every PR | Per non-trivial component |
| **End-to-end** | Critical user journeys through deployed system (browser/mobile) | Seconds–minutes | Post-deploy to test env | Few (10–30 journeys) |
| **Performance/load** | Latency/throughput under load; capacity limits | Minutes–hours | Scheduled / pre-release | Per critical journey |
| **Security (DAST)** | Runtime vulnerabilities | Minutes | Post-deploy to test env | Per release |
| **Smoke** | Deployed app is alive and wired correctly | Seconds | After every deployment (incl. prod) | A handful |

---

## 3. Tooling

| Purpose | Default | Notes / alternatives |
|---|---|---|
| Test framework | **xUnit v3** | Runs on **Microsoft.Testing.Platform (MTP)**; MSTest 4 and NUnit 4 are also fine; **TUnit** is a modern source-generated option. Pick one per repo. |
| Test runner | **Microsoft.Testing.Platform** via `dotnet test` | Faster, no VSTest dependency; configure in `global.json` ([02 §3.1](02-solution-structure-and-coding-standards.md#31-globaljson--pin-the-sdk)). |
| Assertions | xUnit `Assert`, **Shouldly**, or **AwesomeAssertions** | FluentAssertions v8+ is commercially licensed — check before using. |
| Mocking | **NSubstitute** | Moq is widely used (note the 2023 SponsorLink controversy); FakeItEasy also fine. Prefer fakes/real implementations where cheap. |
| Fake data | **Bogus** | Seeded for determinism. AutoFixture for anonymous data. |
| Snapshot testing | **Verify** | Great for API responses, OpenAPI documents, generated code, emails. |
| Containers | **Testcontainers for .NET** | SQL Server, PostgreSQL, Redis, Azurite, Service Bus emulator, Cosmos emulator. |
| DB reset | **Respawn** | Fast table clean-up between tests. |
| HTTP stubs | **WireMock.Net** | Stub third-party APIs. |
| ASP.NET Core | **`WebApplicationFactory<TProgram>`** (`Microsoft.AspNetCore.Mvc.Testing`) | In-memory test server. |
| Aspire | **`Aspire.Hosting.Testing`** | Spin up the entire AppHost for distributed integration tests. |
| Time / logging / metrics fakes | `FakeTimeProvider`, `FakeLogger`, `MetricCollector<T>` (`Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.Extensions.Diagnostics.Testing`) | First-party test doubles. |
| Blazor components | **bUnit** | |
| Browser E2E | **Playwright for .NET** | Cross-browser, auto-waiting, tracing, accessibility checks with axe. |
| Mobile UI | **Appium** | See [06 §16](06-mobile-applications.md#16-testing-mobile-apps). |
| Architecture rules | **NetArchTest.Rules** or **ArchUnitNET** | |
| Load testing | **Azure Load Testing** (JMeter/Locust) or **k6** | NBomber is a .NET-native option (check licence). |
| Micro-benchmarks | **BenchmarkDotNet** | |
| Mutation testing | **Stryker.NET** | |
| Coverage | `Microsoft.Testing.Extensions.CodeCoverage` or **coverlet** + ReportGenerator | |

---

## 4. Unit tests

### 4.1 What to unit test

- Domain entities and aggregates (invariants, state transitions, domain events raised).
- Value objects (validation, equality, arithmetic).
- Validators.
- Non-trivial calculations, parsers, mappers with logic.
- MAUI view models (in `Mobile.Core`).
- Result → HTTP mapping logic.

Don't unit test: trivial getters/setters, framework behaviour, EF Core mappings (integration tests cover these), or thin handlers that just call the database (integration tests are better).

### 4.2 Conventions

- **Naming**: `MethodOrBehaviour_Scenario_ExpectedResult` or sentence style `Submitting_an_empty_order_fails`. Be consistent.
- **Arrange / Act / Assert** structure, one logical assertion per test (multiple asserts on one outcome are fine).
- **No logic in tests** (loops, conditionals) — use `[Theory]` with `[InlineData]`/`[MemberData]` for variations.
- **Test builders / object mothers** for complex setup (`OrderBuilder.Draft().WithLine(...).Build()`).
- Inject `TimeProvider` and use `FakeTimeProvider` for time-dependent logic.

```csharp
public sealed class OrderTests
{
    [Fact]
    public void Submit_with_no_lines_returns_EmptyOrder_error()
    {
        var order = Order.Create(CustomerIds.Any, Clock.Fixed);

        var result = order.Submit();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(OrderErrors.EmptyOrder);
        order.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Submit_with_lines_raises_OrderSubmitted()
    {
        var order = new OrderBuilder().WithLine(quantity: 2, unitPrice: 10m).Build();

        order.Submit();

        order.Status.ShouldBe(OrderStatus.Submitted);
        order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderSubmitted>()
             .Total.ShouldBe(Money.Gbp(20m));
    }
}
```

---

## 5. Integration tests

The backbone of API testing: exercise real HTTP requests through the real pipeline (routing, auth, validation, handlers, EF Core) against a **real database engine in a container**.

### 5.1 Fixture setup

```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private Respawner _respawner = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ordering-db", _sql.GetConnectionString());
        builder.UseSetting("ConnectionStrings:cache", _redis.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            // Replace JWT auth with a test scheme that builds a principal from request headers
            services.AddAuthentication(TestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(DateTimeOffset.Parse("2026-10-17T12:00:00Z")));
            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton<IMessagePublisher, InMemoryMessagePublisher>(); // assert on published events
        });
    }

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _redis.StartAsync());
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Database.MigrateAsync();
        _respawner = await Respawner.CreateAsync(_sql.GetConnectionString(), new() { SchemasToInclude = ["ordering"] });
    }

    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_sql.GetConnectionString());

    public override async ValueTask DisposeAsync()
    {
        await _sql.DisposeAsync();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

### 5.2 A test

```csharp
[Collection(nameof(ApiCollection))]
public sealed class PlaceOrderTests(ApiFactory factory) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Placing_a_valid_order_returns_201_and_publishes_event()
    {
        var client = factory.CreateClient().AsUser(TestUsers.Alice);
        var product = await factory.SeedAsync(Products.Widget(price: 9.99m));

        var response = await client.PostAsJsonAsync("/v1/orders",
            new { lines = new[] { new { productId = product.Id, quantity = 2 } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        factory.Published<OrderPlacedIntegrationEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_user_cannot_read_another_users_order()
    {
        var order = await factory.SeedOrderFor(TestUsers.Bob);
        var client = factory.CreateClient().AsUser(TestUsers.Alice);

        var response = await client.GetAsync($"/v1/orders/{order.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
```

### 5.3 Guidance

- **Share containers across a test collection** (start once per run); reset data between tests with Respawn or per-test transactions.
- **Never use the EF Core in-memory provider** for integration tests — it doesn't behave like a relational database. SQLite in-memory is closer but still differs from SQL Server/PostgreSQL; prefer the real engine.
- **Test authorization explicitly**: anonymous → 401; wrong role → 403; other user's resource → 404/403.
- **Test validation and error contracts**: Problem Details shape, `type`/`code`, `errors` dictionary.
- **Message consumers**: invoke handlers with real DB; for full fidelity use the Service Bus emulator via Testcontainers.
- **Snapshot API responses** with Verify to catch unintended contract changes.
- **Aspire testing** (`DistributedApplicationTestingBuilder`) for tests that need multiple services running together (API + worker + broker) — slower; use for a few cross-service flows.

---

## 6. Contract and API compatibility tests

- **OpenAPI snapshot**: generate the OpenAPI document in a test (or at build) and compare with the committed version using Verify. Any change requires an explicit approval in the PR.
- **Breaking-change detection**: run `oasdiff breaking` against the previous released spec; fail CI on breaking changes within a major version.
- **Consumer-driven contracts** (Pact) when separate teams own consumer and provider services and the consumer set is known. Usually unnecessary within a monorepo where clients are generated from the spec.
- **Generated clients compile** against the spec in CI — a cheap but effective guard for web and mobile.

---

## 7. Architecture tests

Enforce boundaries automatically so the modular monolith stays modular:

```csharp
public sealed class ModuleBoundaryTests
{
    private static readonly string[] Modules = ["Ordering", "Catalog", "Billing"];

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Modules_do_not_depend_on_other_modules_internals(string module, string other)
    {
        // Referencing Contoso.Shop.{other}.Contracts is allowed; anything else in the other module is not.
        string[] forbidden =
        [
            $"Contoso.Shop.{other}.Domain",
            $"Contoso.Shop.{other}.Features",
            $"Contoso.Shop.{other}.Infrastructure",
        ];

        var result = Types.InAssembly(AssemblyFor(module))
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_does_not_depend_on_infrastructure()
    {
        Types.InAssembly(typeof(Order).Assembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Azure")
            .GetResult().IsSuccessful.ShouldBeTrue();
    }

    public static TheoryData<string, string> ModulePairs()
    {
        var data = new TheoryData<string, string>();
        foreach (var a in Modules)
            foreach (var b in Modules.Where(b => b != a))
                data.Add(a, b);
        return data;
    }

    private static Assembly AssemblyFor(string module) => Assembly.Load($"Contoso.Shop.{module}");
}
```

Other useful rules: handlers are `internal sealed`; endpoints live in `Features`; no references from `Web` to module internals; no `DateTime.Now` usage (prefer `TimeProvider`).

---

## 8. Component tests for Blazor

Use **bUnit** to render components in isolation:

```csharp
public sealed class OrderSummaryTests : BunitContext
{
    [Fact]
    public void Shows_cancel_button_only_for_submitted_orders()
    {
        var cut = Render<OrderSummary>(p => p.Add(x => x.Order, OrderViewModels.Submitted()));

        cut.Find("button[data-testid=cancel]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Clicking_cancel_raises_OnCancel()
    {
        var cancelled = false;
        var cut = Render<OrderSummary>(p => p
            .Add(x => x.Order, OrderViewModels.Submitted())
            .Add(x => x.OnCancel, () => cancelled = true));

        await cut.Find("[data-testid=cancel]").ClickAsync(new());

        cancelled.ShouldBeTrue();
    }
}
```

- Test component logic, conditional rendering, parameter handling, event callbacks, and form validation.
- Use `data-testid` attributes for stable selectors (shared with Playwright).
- Mock services injected into components (e.g., the API client) with NSubstitute.

---

## 9. End-to-end tests

- **Few and focused**: cover critical user journeys (sign up, sign in, search, checkout, account management) — not every permutation (those belong in integration tests).
- **Playwright for .NET** with the xUnit/MSTest/NUnit integration; run against a deployed **test environment** after each deployment, and a smoke subset against production.
- **Page Object Model** (or screenplay pattern) to keep tests maintainable.
- **Stable selectors**: roles and accessible names (`GetByRole`, `GetByLabel`) first — this also validates accessibility; `data-testid` second; never brittle CSS paths.
- **Auth**: sign in once per test run using a dedicated test account and reuse storage state; never use real user accounts. Test accounts' credentials live in Key Vault/CI secrets.
- **Isolation**: each test creates its own data (unique identifiers) so tests can run in parallel and don't interfere.
- **Tracing on failure**: Playwright traces, screenshots, and videos uploaded as CI artefacts.
- **Accessibility**: run `axe-core` (Deque.AxeCore.Playwright) on key pages; fail on serious/critical violations.
- **Cross-browser**: Chromium on every run; Firefox and WebKit nightly.

---

## 10. Performance and load testing

### 10.1 Types

| Type | Question |
|---|---|
| **Load test** | Do we meet latency SLOs at expected peak load? |
| **Stress test** | Where does it break, and how does it fail (gracefully?) |
| **Soak test** | Are there leaks/degradation over hours? |
| **Spike test** | Do we handle sudden bursts (autoscale reaction time)? |

### 10.2 Practice

- Use **Azure Load Testing** (managed JMeter/Locust, integrates with Azure Monitor metrics of the system under test and CI pass/fail criteria) or **k6** (scriptable in JS, great DX).
- Model realistic **user journeys and data distributions** derived from production telemetry, not single-endpoint hammering.
- Test in a **production-like environment** (same SKUs, scaling rules, data volumes).
- Define **pass/fail criteria** from SLOs (e.g., p95 < 300 ms, error rate < 0.1% at 500 RPS) and run in the pipeline for release candidates (or nightly).
- Monitor the system under test (App Insights, DB metrics) during the run to find bottlenecks.
- **Micro-benchmarks** (BenchmarkDotNet) for hot code paths when optimising — always compare before/after with `[MemoryDiagnoser]`.

---

## 11. Security, accessibility and mobile testing

- **Security**: SAST (CodeQL) and dependency scanning on PRs; DAST (OWASP ZAP baseline + API scan from OpenAPI) post-deploy to test; authorization negative tests in integration suite; periodic pen tests. See [07 §12](07-security-and-identity.md#12-devsecops-security-in-the-pipeline).
- **Accessibility**: automated axe checks in Playwright and bUnit (where possible) + manual screen reader testing for major releases. See [05 §10](05-web-application.md#10-accessibility).
- **Mobile**: unit tests on `Mobile.Core`; Appium for critical journeys; device farm coverage. See [06 §16](06-mobile-applications.md#16-testing-mobile-apps).
- **Infrastructure**: Bicep linting, PSRule for Azure, `what-if` review; optionally deploy IaC to an ephemeral environment and run smoke tests.

---

## 12. Test data management

- **Never use production data in non-production environments.** If realistic data is essential, use properly anonymised/synthetic datasets with sign-off from data protection.
- **Builders + Bogus (seeded)** for unit/integration data; each test creates what it needs.
- **Reference data** applied via migrations/seed scripts identically in all environments.
- **E2E environments**: seed scripts for baseline data; tests create and clean up their own entities; periodic environment reset.
- **Test accounts** in Entra External ID test tenant, credentials in Key Vault, rotated.

---

## 13. Coverage and test quality

- **Coverage is a signal, not a target.** Track it to find untested areas; don't chase a number (it incentivises low-value tests). A reasonable expectation: high coverage of domain and application logic; lower is fine for glue/infrastructure covered by integration tests.
- **Coverage gates on changed code** (diff coverage, e.g., ≥ 80%) are more useful than global thresholds.
- **Mutation testing** (Stryker.NET) on core domain modules periodically to measure whether tests actually detect faults.
- **Review tests in PRs** as carefully as production code: do they assert meaningful behaviour? Would they fail if the feature broke?

---

## 14. Tests in CI

- **PR pipeline**: build → unit → architecture → integration (Testcontainers on Linux agents with Docker) → component → contract checks. Target < 10–15 minutes.
- **Post-merge/deploy**: E2E against test environment, DAST, smoke tests; load tests nightly or per release candidate.
- **Parallelise**: xUnit runs collections in parallel; split large suites across jobs.
- **Publish results** (TRX/JUnit) and coverage reports to the PR (test summary, coverage comment).
- **Flaky test policy**: quarantine (skip with a tracked issue) within a day; fix within a sprint. Track flakiness rate. Never "just re-run until green" as a habit.
- **Fail fast**: run the fastest suites first.

---

## 15. References

- [Testing in .NET](https://learn.microsoft.com/dotnet/core/testing/)
- [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
- [xUnit v3](https://xunit.net/docs/getting-started/v3/whats-new)
- [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
- [Aspire testing](https://learn.microsoft.com/dotnet/aspire/testing/write-your-first-test)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [bUnit](https://bunit.dev/) · [Playwright for .NET](https://playwright.dev/dotnet/)
- [Azure Load Testing](https://learn.microsoft.com/azure/load-testing/)
- [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/)
- [Unit testing best practices for .NET](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices)
