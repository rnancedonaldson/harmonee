# 02 — Solution Structure & Coding Standards

> How repositories and solutions are laid out, how builds are configured, and how we write C#.

---

## Contents

1. [Repository strategy](#1-repository-strategy)
2. [Repository layout](#2-repository-layout)
3. [Build configuration](#3-build-configuration)
4. [Code quality and analyzers](#4-code-quality-and-analyzers)
5. [C# coding standards](#5-c-coding-standards)
6. [Asynchronous programming](#6-asynchronous-programming)
7. [Dependency injection, configuration and options](#7-dependency-injection-configuration-and-options)
8. [Logging and error handling in code](#8-logging-and-error-handling-in-code)
9. [Dependency and library policy](#9-dependency-and-library-policy)
10. [Dates, times, strings and globalization](#10-dates-times-strings-and-globalization)
11. [Git workflow and code review](#11-git-workflow-and-code-review)
12. [Versioning](#12-versioning)
13. [Documentation in code](#13-documentation-in-code)
14. [References](#14-references)

---

## 1. Repository strategy

**Default: one monorepo per product** containing the API, web app, mobile app, shared contracts, infrastructure code, and docs.

| | Monorepo (per product) | Polyrepo |
|---|---|---|
| Atomic changes across API + clients | ✅ One PR | ❌ Coordinated PRs |
| Shared contracts/tooling | ✅ Trivial | ⚠ Requires packages |
| Independent CI | ⚠ Needs path filters | ✅ Natural |
| Access control granularity | ⚠ Coarse | ✅ Fine |
| Scales to many teams | ⚠ With tooling | ✅ |

Use **path-filtered pipelines** so a change to `src/Mobile/**` doesn't redeploy the API. Move to multiple repos only when team boundaries or access control require it — and then share contracts via an internal NuGet feed (GitHub Packages / Azure Artifacts).

---

## 2. Repository layout

```
/
├── .github/
│   ├── workflows/                  # CI/CD pipelines
│   ├── dependabot.yml
│   ├── CODEOWNERS
│   └── pull_request_template.md
├── docs/
│   ├── adr/                        # Architecture Decision Records
│   ├── architecture/               # C4 diagrams, arc42-lite doc
│   └── runbooks/
├── infra/                          # Bicep (or Terraform)
│   ├── main.bicep
│   ├── modules/
│   └── environments/               # dev.bicepparam, test.bicepparam, prod.bicepparam
├── src/
│   ├── AppHost/                    # Aspire AppHost
│   ├── ServiceDefaults/            # Aspire ServiceDefaults (OTel, health, resilience)
│   ├── Api/                        # ASP.NET Core API host
│   ├── Modules/
│   │   ├── Ordering/
│   │   │   ├── Ordering/                # Module implementation
│   │   │   └── Ordering.Contracts/      # Public contracts for other modules
│   │   └── Catalog/ ...
│   ├── Web/                        # Blazor Web App (or SPA host / BFF)
│   ├── Web.Client/                 # Blazor WebAssembly client project (if interactive WASM/Auto)
│   ├── Mobile/                     # .NET MAUI app
│   ├── Shared/
│   │   ├── SharedKernel/
│   │   └── Api.Client/             # Generated typed API client (Kiota/NSwag) for web/mobile
│   └── Workers/                    # Background workers / Functions
├── tests/
│   ├── Ordering.UnitTests/
│   ├── Api.IntegrationTests/
│   ├── Architecture.Tests/
│   ├── Web.ComponentTests/         # bUnit
│   └── E2E.Tests/                  # Playwright
├── .editorconfig
├── .gitattributes
├── .gitignore
├── Directory.Build.props
├── Directory.Build.targets
├── Directory.Packages.props
├── global.json
├── nuget.config
├── MyProduct.slnx
├── azure.yaml                      # azd project file (if using azd)
└── README.md
```

### Naming

- Projects and namespaces: `Company.Product.Area` (e.g., `Contoso.Shop.Ordering`). Folder names may drop the prefix for brevity; set `<RootNamespace>`/`<AssemblyName>` in `Directory.Build.props` from a common prefix.
- Test projects: `<ProjectUnderTest>.UnitTests`, `.IntegrationTests`.
- One public type per file; file name matches type name.

### Solution files

Use the **`.slnx`** XML solution format (default for new solutions in the .NET 10 SDK; supported by Visual Studio 2022 17.13+/VS 2026, Rider, and VS Code C# Dev Kit). It's human-readable and merge-friendly. Convert existing solutions with `dotnet sln migrate`.

Consider **solution filters** (`.slnf`) for large repos (e.g., `Mobile.slnf` that excludes server projects, since MAUI workloads slow down loading).

---

## 3. Build configuration

### 3.1 `global.json` — pin the SDK

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`latestFeature` allows patch/feature-band updates (security fixes) without allowing a different major version. CI and every developer machine use the same SDK band. ⚠ Verify the `test.runner` setting against the SDK you use (introduced in .NET 10 for `dotnet test` on MTP).

### 3.2 `Directory.Build.props` — shared MSBuild properties

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Quality gates -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn> <!-- Missing XML comments: enable per-project for public libraries -->

    <!-- Deterministic, reproducible builds -->
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>

    <!-- Supply-chain -->
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <RestoreLockedMode Condition="'$(CI)' == 'true'">true</RestoreLockedMode>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>        <!-- include transitive packages -->
    <NuGetAuditLevel>low</NuGetAuditLevel>

    <!-- Metadata -->
    <Company>Contoso</Company>
    <Authors>Contoso Engineering</Authors>
    <Copyright>© Contoso</Copyright>
  </PropertyGroup>
</Project>
```

Notes:
- MAUI projects override `TargetFramework` with `TargetFrameworks` (`net10.0-android;net10.0-ios`) in their own `.csproj`.
- If `TreatWarningsAsErrors` is too disruptive at first, use `WarningsAsErrors` with a specific list (`nullable` at minimum) and ratchet up.
- Put test-specific settings in `tests/Directory.Build.props` (which imports the parent with `<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />`).

### 3.3 Central Package Management (`Directory.Packages.props`)

All package versions are declared once:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
    <PackageVersion Include="Asp.Versioning.Http" Version="8.1.0" />
    <PackageVersion Include="xunit.v3" Version="3.1.0" />
    <!-- ... -->
  </ItemGroup>
  <ItemGroup>
    <!-- Analyzers applied to every project -->
    <GlobalPackageReference Include="Meziantou.Analyzer" Version="2.0.0" />
  </ItemGroup>
</Project>
```

Project files then use `<PackageReference Include="..." />` with no version. (Versions shown are illustrative — use the latest stable.)

### 3.4 `nuget.config` — package source mapping

Prevent dependency-confusion attacks by mapping package ID patterns to sources:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="internal" value="https://pkgs.dev.azure.com/contoso/_packaging/internal/nuget/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="internal">
      <package pattern="Contoso.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

### 3.5 `.editorconfig`

Commit a comprehensive `.editorconfig` at the root. Start from the [.NET runtime's](https://github.com/dotnet/runtime/blob/main/.editorconfig) or generate via Visual Studio, then tune. Key settings:

```ini
root = true

[*]
indent_style = space
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
charset = utf-8

[*.{cs,razor}]
indent_size = 4
csharp_style_namespace_declarations = file_scoped:warning
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_prefer_braces = true:warning
csharp_style_prefer_primary_constructors = true:suggestion
dotnet_style_prefer_collection_expression = when_types_loosely_match:suggestion
dotnet_sort_system_directives_first = true

# Naming: private fields _camelCase
dotnet_naming_rule.private_fields_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_underscore.style = underscore_camel
dotnet_naming_rule.private_fields_underscore.severity = warning
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_camel.capitalization = camel_case
dotnet_naming_style.underscore_camel.required_prefix = _

# ConfigureAwait(false) is unnecessary in ASP.NET Core app code (re-enable in reusable libraries)
dotnet_diagnostic.CA2007.severity = none

[*.{json,yml,yaml,xml,csproj,props,targets,bicep}]
indent_size = 2
```

Run `dotnet format --verify-no-changes` in CI.

### 3.6 `.gitattributes`

```
* text=auto eol=lf
*.{cmd,bat} text eol=crlf
*.png binary
*.jpg binary
```

Normalise line endings to avoid noisy diffs between Windows and macOS/Linux developers.

---

## 4. Code quality and analyzers

### 4.1 Baseline

- **Nullable reference types: enabled everywhere.** Treat nullable warnings as errors. Use `required` members and constructors to make non-null state explicit; avoid `!` (null-forgiving) except where provably safe, with a comment.
- **.NET analyzers** (built into the SDK) at `latest-recommended`.
- **Code style enforced in build** (`EnforceCodeStyleInBuild`).
- **Trimming/AOT analyzers** (`IsTrimmable`, `IsAotCompatible`) on libraries that may be used in AOT contexts (workers, functions, MAUI).

### 4.2 Recommended third-party analyzers

| Analyzer | Why |
|---|---|
| [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) | Excellent best-practice rules (culture, async, perf). |
| [Roslynator](https://github.com/dotnet/roslynator) | Large set of refactorings and style analyzers. |
| [SonarAnalyzer.CSharp](https://github.com/SonarSource/sonar-dotnet) | Bug patterns and security hotspots; pairs with SonarQube Cloud if used. |
| [Microsoft.VisualStudio.Threading.Analyzers](https://github.com/microsoft/vs-threading) | Async/threading misuse (useful in MAUI). |

Don't install everything — overlapping rules cause noise. Pick a set, agree severities in `.editorconfig`, and keep the build clean.

### 4.3 Static analysis in CI

- **CodeQL** (GitHub Advanced Security) for SAST.
- **dotnet format** for style.
- Optional **SonarQube Cloud** for quality gates and trend tracking.
- Fail the build on new warnings; never merge with a red build.

---

## 5. C# coding standards

Follow the [.NET runtime coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md) and [C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions), with these specifics.

### 5.1 Naming

| Element | Convention | Example |
|---|---|---|
| Namespaces, types, methods, properties, events, public fields, constants | PascalCase | `OrderService`, `MaxRetries` |
| Interfaces | `I` + PascalCase | `IPaymentGateway` |
| Type parameters | `T` + PascalCase | `TResponse` |
| Parameters, locals | camelCase | `orderId` |
| Private/internal fields | `_camelCase` | `_dbContext` |
| Static private fields | `s_camelCase` (runtime style) or `_camelCase` — pick one | |
| Async methods | Suffix `Async` | `GetOrderAsync` |
| Boolean members | Question form | `IsActive`, `HasLines`, `CanSubmit` |

Avoid abbreviations except well-known ones (`Id`, `Url`, `Http`). Use domain language (ubiquitous language) in names.

### 5.2 Modern C# — use it

| Feature | Guidance |
|---|---|
| **File-scoped namespaces** | Always. |
| **Records** | Use `record`/`record struct` for DTOs, commands, queries, events, value objects. Prefer `readonly record struct` for small value types. |
| **`required` and `init`** | For DTOs/options to guarantee initialisation without large constructors. |
| **Primary constructors** | Good for DI in services/handlers. Remember parameters are captured, mutable, and not `readonly` — don't use them for state that must be immutable; assign to `readonly` fields if that matters. |
| **Collection expressions** | `int[] xs = [1, 2, 3];`, `List<Order> orders = [];`, spread `[..a, ..b]`. |
| **Pattern matching** | `is`/`switch` expressions, property and list patterns for clarity. |
| **Raw string literals** | For JSON/SQL/multi-line text in code and tests. |
| **`sealed` by default** | Seal classes not designed for inheritance — clearer intent and small perf gains. Analyzer CA1852 helps. |
| **Static lambdas / local functions** | Use `static` where no capture is intended to avoid accidental allocations. |
| **C# 14: extension members** | New `extension` blocks allow extension properties and static extension members — use for fluent, discoverable APIs; don't overuse. |
| **C# 14: `field` keyword** | Simplifies properties that need light logic on a backing field (`set => field = value.Trim();`). |
| **C# 14: null-conditional assignment** | `customer?.Order = order;` — concise, but don't hide important null-handling decisions. |
| **`var`** | Use when the type is obvious from the right-hand side; spell the type when it aids readability. |

### 5.3 Design rules

- **Small, focused types and methods.** A method should do one thing; aim for < ~30 lines. A class with many dependencies (> ~5 constructor params) is a smell.
- **Immutability by default.** Prefer immutable types and `IReadOnlyList<T>`/`IReadOnlyCollection<T>` in public signatures. Expose collections as read-only views.
- **Guard clauses** at public boundaries: `ArgumentNullException.ThrowIfNull(x)`, `ArgumentException.ThrowIfNullOrWhiteSpace(s)`, `ArgumentOutOfRangeException.ThrowIfNegative(n)`.
- **Avoid primitive obsession** in the domain: strongly-typed IDs and value objects (`Email`, `Money`, `Quantity`).
- **Composition over inheritance.** Inheritance hierarchies deeper than 2 levels are a smell.
- **Least visibility.** Default to `internal`/`private`. Use `InternalsVisibleTo` for tests sparingly — prefer testing via public behaviour.
- **No static mutable state.** Statics are fine for pure functions and constants.
- **No `#region`** to hide large code blocks — split the class instead.
- **Comments explain *why*, not *what*.** Code should be self-explanatory; comments capture intent, trade-offs, and non-obvious constraints.

### 5.4 Example: idiomatic handler

```csharp
namespace Contoso.Shop.Ordering.Features.PlaceOrder;

public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderLine> Lines);
public sealed record PlaceOrderLine(Guid ProductId, int Quantity);

internal sealed class PlaceOrderHandler(
    OrderingDbContext db,
    IPricingService pricing,
    TimeProvider clock,
    ILogger<PlaceOrderHandler> logger)
{
    public async Task<Result<OrderId>> HandleAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(new CustomerId(command.CustomerId), clock.GetUtcNow());

        foreach (var line in command.Lines)
        {
            var price = await pricing.GetPriceAsync(new ProductId(line.ProductId), ct);
            var added = order.AddLine(new ProductId(line.ProductId), new Quantity(line.Quantity), price);
            if (added.IsFailure) return added.Error;
        }

        var submitted = order.Submit();
        if (submitted.IsFailure) return submitted.Error;

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        Log.OrderPlaced(logger, order.Id.Value, order.Lines.Count);
        return order.Id;
    }
}

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} placed with {LineCount} lines")]
    public static partial void OrderPlaced(ILogger logger, Guid orderId, int lineCount);
}
```

---

## 6. Asynchronous programming

1. **Async all the way.** Never block on async code: no `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` in application code. These cause thread-pool starvation and deadlocks (especially in MAUI UI threads).
2. **Accept and pass `CancellationToken`** on every async method that does I/O. ASP.NET Core provides `HttpContext.RequestAborted` (bind `CancellationToken ct` in endpoints). Pass it to EF Core, `HttpClient`, Azure SDK calls.
3. **Return `Task`/`ValueTask`, never `async void`** — except event handlers (MAUI UI events), and even then wrap in try/catch.
4. **`ValueTask`** only for hot paths that frequently complete synchronously; never await a `ValueTask` twice.
5. **Don't wrap synchronous code in `Task.Run`** in ASP.NET Core — it just moves work between thread-pool threads. Use `Task.Run` in MAUI to move CPU work off the UI thread.
6. **`ConfigureAwait(false)`**: unnecessary in ASP.NET Core application code (no synchronization context). Use it in **reusable libraries** that might run under a UI context (e.g., shared code consumed by MAUI).
7. **Concurrency**: use `Task.WhenAll` for independent I/O; `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` for bounded parallel work; `Channel<T>` for producer/consumer. Never share a `DbContext` across concurrent tasks.
8. **Streaming**: use `IAsyncEnumerable<T>` for large result sets and streaming responses.
9. **Timeouts**: combine tokens with `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter`, or use resilience pipelines ([12](12-performance-resilience-and-scalability.md)).
10. **Background work in ASP.NET Core**: never fire-and-forget from a request. Use `BackgroundService` + `Channel<T>`, or a durable queue (Service Bus) for work that must survive restarts.

---

## 7. Dependency injection, configuration and options

### 7.1 Dependency injection

- Use the built-in container (`Microsoft.Extensions.DependencyInjection`). Add Scrutor only if you need assembly scanning/decoration.
- **Lifetimes**:
  - *Singleton*: stateless, thread-safe services; caches; `HttpClient` factories; options.
  - *Scoped*: per-request state, `DbContext`, unit-of-work, current user/tenant context.
  - *Transient*: lightweight, stateless services.
- **Avoid captive dependencies** (singleton depending on scoped). Enable validation: `builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; });` (on by default in Development).
- **Constructor injection only**; no service locator (`IServiceProvider.GetService` inside business code). Exceptions: factories and infrastructure plumbing.
- **Keyed services** (`AddKeyedSingleton`, `[FromKeyedServices]`) for multiple implementations selected by key.
- Each module exposes a single registration extension: `services.AddOrderingModule(configuration)`.
- **`HttpClient`**: always via `IHttpClientFactory` (typed or named clients) with resilience handlers; never `new HttpClient()` per call.
- Use **`TimeProvider`** (not `DateTime.UtcNow`) for testable time; register `TimeProvider.System`.

### 7.2 Configuration

Configuration sources, in increasing precedence (later wins):

1. `appsettings.json` — non-secret defaults only.
2. `appsettings.{Environment}.json` — non-secret environment overrides.
3. **User secrets** (`dotnet user-secrets`) — local development only.
4. **Environment variables** — set by Container Apps/App Service/Aspire.
5. **Azure App Configuration** — centralised, dynamic config and feature flags.
6. **Azure Key Vault** — secrets (prefer Key Vault references via App Configuration, or the Key Vault configuration provider).

Rules:
- **No secrets in `appsettings*.json` or source control — ever.** Secret scanning in CI enforces this.
- Prefer **managed identity** over secrets entirely: connection strings without passwords (`Authentication=Active Directory Default` for SQL; `DefaultAzureCredential`/`ManagedIdentityCredential` for Azure SDKs).
- Aspire injects connection info as environment variables locally and in deployed environments.

### 7.3 Options pattern

Bind configuration to strongly-typed options and validate at startup:

```csharp
public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    [Required, Url] public required string BaseUrl { get; init; }
    [Range(1, 60)] public int TimeoutSeconds { get; init; } = 10;
}

[OptionsValidator]
public sealed partial class PaymentOptionsValidator : IValidateOptions<PaymentOptions>; // source-generated, AOT-friendly

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(PaymentOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PaymentOptions>, PaymentOptionsValidator>();
```

- `IOptions<T>` — singleton, read once.
- `IOptionsSnapshot<T>` — scoped, re-read per request (supports reload).
- `IOptionsMonitor<T>` — singleton with change notifications (for dynamic App Configuration refresh).
- **Fail fast**: invalid configuration should crash on startup, not on the first request at 3 a.m.

---

## 8. Logging and error handling in code

### 8.1 Logging

- Use `ILogger<T>` everywhere. Logging is exported via OpenTelemetry ([09](09-observability-and-operations.md)).
- **Structured logging with message templates**, never string interpolation:
  ```csharp
  logger.LogInformation("Order {OrderId} shipped to {Country}", orderId, country);   // ✅
  logger.LogInformation($"Order {orderId} shipped to {country}");                     // ❌ (CA2254)
  ```
- Prefer **source-generated `[LoggerMessage]`** methods for frequently-called logs — zero allocations when the level is disabled, compile-time checking.
- **Log levels**: `Trace`/`Debug` (dev diagnostics), `Information` (significant business/lifecycle events), `Warning` (unexpected but handled; degraded), `Error` (operation failed), `Critical` (app/system failure). Production default: `Information` for app namespaces, `Warning` for `Microsoft.*` / `System.*`.
- **Never log secrets, tokens, passwords, or full PII.** Use `Microsoft.Extensions.Compliance.Redaction` with data classification attributes to redact sensitive fields automatically.
- Use **log scopes** / `Activity` tags for correlation (tenant, user ID hash, order ID).

### 8.2 Exceptions

- Throw specific exception types; don't throw `Exception` or `ApplicationException`.
- **Don't catch what you can't handle.** Catch at boundaries (global exception handler, message consumer, background loop) and log once.
- Never swallow exceptions silently (`catch { }`).
- Preserve stack traces: `throw;` not `throw ex;`.
- Use the **Result pattern** for expected business failures ([01](01-architecture-overview.md#44-the-result-pattern-vs-exceptions)).
- Global handling in ASP.NET Core via `IExceptionHandler` + Problem Details ([03](03-api-design-and-development.md#7-error-handling)).

---

## 9. Dependency and library policy

### 9.1 Prefer the platform

Before adding a package, check whether .NET or ASP.NET Core already provides it:

| Need | Built-in option |
|---|---|
| JSON | `System.Text.Json` (with source generation) |
| HTTP resilience | `Microsoft.Extensions.Http.Resilience` (Polly v8 under the hood) |
| Caching | `HybridCache`, `IMemoryCache`, output caching |
| Rate limiting | `Microsoft.AspNetCore.RateLimiting` |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` |
| Validation | DataAnnotations; Minimal API validation (`AddValidation()` in .NET 10) |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` |
| Background jobs | `BackgroundService`, `PeriodicTimer`; Container Apps Jobs / Functions for scheduled |
| Object mapping | Hand-written mapping methods or extension methods (or source-generated Mapperly) |
| Time abstraction | `TimeProvider` |
| Redaction / compliance | `Microsoft.Extensions.Compliance.*` |
| Telemetry | OpenTelemetry .NET + Azure Monitor distro |
| AI | `Microsoft.Extensions.AI` |

### 9.2 Vetting a new dependency

Ask, and record the answers in the PR description:

1. **Licence** — permissive (MIT, Apache-2.0, BSD) preferred. Check for commercial/dual licensing and *revenue thresholds*. Re-check on major upgrades.
2. **Maintenance** — recent releases, responsive maintainers, bus factor > 1.
3. **Security** — no unpatched advisories; signed packages; reputable publisher.
4. **Size/transitive dependencies** — does it pull in half of NuGet?
5. **AOT/trimming compatibility** — important for MAUI, functions, workers.
6. **Exit cost** — how hard is it to remove later? Wrap behind your own abstraction if exit cost is high.

### 9.3 Licensing changes to be aware of (2025)

Several widely used libraries moved to commercial licences for some or all users. Evaluate before adopting or upgrading:

| Library | Change | Common alternatives |
|---|---|---|
| **MediatR** | Commercial licence (from v13) | Direct handler injection; `Mediator` (source-generated, MIT) |
| **AutoMapper** | Commercial licence (from v15) | Manual mapping; **Mapperly** (source-generated, Apache-2.0) |
| **MassTransit** | v9+ commercial; v8 remains Apache-2.0 | Azure Service Bus SDK directly; Wolverine (MIT); NServiceBus (commercial) |
| **FluentAssertions** | v8+ commercial (Xceed) | **AwesomeAssertions** (community fork, Apache-2.0), **Shouldly**, xUnit asserts |
| **IdentityServer (Duende)** | Commercial (long-standing) | Entra ID / External ID; OpenIddict (Apache-2.0) if self-hosting an IdP |

⚠ Verify current terms — licensing can change again.

### 9.4 Keeping dependencies current

- **Dependabot** (or Renovate) for NuGet, GitHub Actions, Docker base images, and npm. Group minor/patch updates weekly.
- `NuGetAudit` fails restore on known vulnerabilities.
- Upgrade .NET major versions on a schedule — LTS to LTS within ~6 months.

---

## 10. Dates, times, strings and globalization

### 10.1 Time

- Store and transmit timestamps as **UTC** using **`DateTimeOffset`** (or `DateTime` with `Kind=Utc` consistently). Convert to local time only in the UI.
- Use **`DateOnly`** and **`TimeOnly`** for dates/times without a time zone (birthdays, business hours).
- Use **`TimeProvider`** for "now" — inject it; use `FakeTimeProvider` in tests.
- Store IANA time zone IDs (`Europe/London`) for user preferences; `TimeZoneInfo.FindSystemTimeZoneById` handles IANA IDs on all platforms in modern .NET.
- API payloads: ISO 8601 (`2026-10-17T14:30:00Z`).

### 10.2 Strings and culture

- Always specify `StringComparison` (`Ordinal`/`OrdinalIgnoreCase` for identifiers, keys, protocol strings; `CurrentCulture` only for user-visible sorting).
- Use `CultureInfo.InvariantCulture` for formatting/parsing machine-readable data.
- Money: `decimal`, never `double`. Store currency code alongside amount.
- Use `string.IsNullOrWhiteSpace` for user input checks.

### 10.3 Localization

- Externalise user-facing strings in `.resx` files with `IStringLocalizer<T>`, from the start — retrofitting is painful.
- Use `RequestLocalizationMiddleware` in web/API (for localized error messages); MAUI uses `.resx` with `CultureInfo.CurrentUICulture`.
- Don't concatenate localized fragments; use full-sentence templates with placeholders.

---

## 11. Git workflow and code review

### 11.1 Branching

**Trunk-based development** (details in [11](11-devops-and-delivery.md#2-branching-and-workflow)):
- `main` is always releasable and protected.
- Short-lived feature branches (≤ 2–3 days), merged via PR with squash or rebase.
- Incomplete features hidden behind feature flags.

### 11.2 Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(ordering): allow cancelling submitted orders within 30 minutes

Adds CancelOrder endpoint and domain rule. Emits OrderCancelled integration event.

Closes #123
```

Types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `ci`, `chore`. A `!` or `BREAKING CHANGE:` footer marks breaking changes. This enables automated changelogs and semantic versioning.

### 11.3 Pull requests

- **Small PRs** (ideally < 400 lines changed). Large changes are split or stacked.
- PR template includes: what/why, how tested, screenshots (UI), risk/rollback, checklist ([14](14-checklists.md#2-pull-request-checklist)).
- **Required checks**: build, tests, format, analyzers, CodeQL, secret scanning, dependency review.
- **At least one approving review**; CODEOWNERS for sensitive areas (security, infra, contracts).
- Reviewers focus on: correctness, design/boundaries, security, tests, observability, naming/readability. Style nits are automated, not debated.
- Review within one working day; authors respond promptly. Prefer conversation over gatekeeping.

---

## 12. Versioning

- **Semantic Versioning** for libraries, packages, and public API contracts.
- Derive versions from Git (tags) with **MinVer** or **Nerdbank.GitVersioning** — never hand-edit version numbers.
- Applications (API/web): version = build metadata (e.g., `2026.10.17.3` or SemVer from tags) stamped into the assembly and exposed via a `/version` endpoint or telemetry resource attributes (`service.version`).
- Mobile apps have **two** numbers: user-facing version (`ApplicationDisplayVersion`, e.g., `2.4.0`) and monotonically increasing build number (`ApplicationVersion`, e.g., `20417`) — automate both in CI ([06](06-mobile-applications.md#12-build-signing-and-distribution)).

---

## 13. Documentation in code

- **README.md at the repo root**: what the product is, how to run locally (ideally one command: `aspire run` / `dotnet run --project src/AppHost`), how to test, where docs live.
- **README per non-trivial project/module**: purpose, boundaries, key decisions.
- **XML docs** on public APIs of shared libraries and on API request/response types (they flow into OpenAPI descriptions).
- **ADRs** for decisions; **runbooks** for operations.
- Keep diagrams as code (Mermaid) so they render in GitHub and stay reviewable.

---

## 14. References

- [C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [.NET Framework design guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/)
- [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)
- [Package source mapping](https://learn.microsoft.com/nuget/consume-packages/package-source-mapping)
- [NuGet Audit](https://learn.microsoft.com/nuget/concepts/auditing-packages)
- [Code analysis in .NET](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview)
- [Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options)
- [Async guidance (David Fowler)](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md)
- [Compile-time logging source generation](https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator)
- [What's new in C# 14](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14)
