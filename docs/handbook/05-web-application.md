# 05 — Web Application

> Building the browser front-end: Blazor Web App by default, or a JavaScript SPA with an ASP.NET Core BFF.

---

## Contents

1. [Choosing the web stack](#1-choosing-the-web-stack)
2. [Blazor Web App render modes](#2-blazor-web-app-render-modes)
3. [Project structure](#3-project-structure)
4. [Component design](#4-component-design)
5. [State management](#5-state-management)
6. [Authentication and the BFF pattern](#6-authentication-and-the-bff-pattern)
7. [Calling the API](#7-calling-the-api)
8. [Forms and validation](#8-forms-and-validation)
9. [UI, design systems and styling](#9-ui-design-systems-and-styling)
10. [Accessibility](#10-accessibility)
11. [Performance](#11-performance)
12. [Web security](#12-web-security)
13. [Localization and SEO](#13-localization-and-seo)
14. [Hosting the web app on Azure](#14-hosting-the-web-app-on-azure)
15. [If you choose a JavaScript SPA](#15-if-you-choose-a-javascript-spa)
16. [Testing the web app](#16-testing-the-web-app)
17. [References](#17-references)

---

## 1. Choosing the web stack

| Option | Choose when | Trade-offs |
|---|---|---|
| **Blazor Web App** (default) | .NET team; want C# end-to-end, shared models/validation with API and MAUI (via Blazor Hybrid); line-of-business and authenticated apps; mixed static + interactive pages. | Smaller ecosystem than React; WASM download size for interactive WebAssembly; Interactive Server requires persistent connections. |
| **React / Angular / Vue SPA + ASP.NET Core BFF** | Team is JS/TS-first; need rich ecosystem of UI libraries; highly interactive consumer UI; existing front-end expertise. | Two languages/toolchains; must handle auth via BFF; separate build pipeline. |
| **Razor Pages / MVC** | Mostly server-rendered content, forms, simple interactivity. | Interactivity needs JS (htmx/Alpine work well). Blazor static SSR now covers much of this space. |
| **Static site (e.g., marketing)** | Content-heavy, public, SEO-critical, rarely changes. | Azure Static Web Apps or Blob + Front Door; generate with any static site generator. |

**Default:** **Blazor Web App** with **static server-side rendering (SSR) as the baseline** and **interactivity added per component/page** where needed.

---

## 2. Blazor Web App render modes

Since .NET 8, a single Blazor Web App can mix render modes per page or component:

| Mode | Where code runs | Connection | Best for | Watch out for |
|---|---|---|---|---|
| **Static SSR** (default) | Server, HTML per request | None after response | Content pages, forms (with enhanced form handling), SEO, fastest first load | No event handlers (`@onclick`) — use forms/links; add interactivity where needed |
| **Streaming rendering** | Server | Single response streamed | Pages with slow data — render shell first, stream content in | Still static once complete |
| **Interactive Server** | Server; UI diffs over SignalR (WebSocket) | Persistent circuit per user | Internal/LOB apps, low latency to server, keeping code server-side | Server memory per user; latency sensitive; needs sticky sessions or Azure SignalR Service at scale; reconnection UX |
| **Interactive WebAssembly** | Browser (.NET on WASM) | None (calls APIs via HTTP) | Offline-capable, high interactivity, offload server, PWA | Initial download size; code is visible to user (no secrets); must call APIs securely |
| **Interactive Auto** | Server first, then WASM once downloaded | Circuit → HTTP | Best perceived performance for interactive pages | Complexity of both modes; components must work in both |

### Guidance

- **Start static.** Most pages (lists, details, dashboards that refresh on navigation, forms) work well as static SSR with **enhanced navigation** and **enhanced form handling**.
- Add **Interactive Server** for internal apps where users are close to the server and you want simplicity (no API hardening for the browser).
- Add **Interactive WebAssembly/Auto** for public apps with rich interactivity or where you want to reduce server load.
- Apply render modes at the **component level** where possible (islands of interactivity), not globally — unless the whole app is truly interactive.
- **Prerendering** is on by default for interactive components: components render twice (server prerender, then interactive). Use **persistent component state** (`[PersistentState]` in .NET 10) to avoid fetching data twice and flicker.
- .NET 10 improvements to use: circuit state persistence/pause-resume for Interactive Server, the reconnection UI component in the template, the `ResourcePreloader`, and improved form validation for nested objects.

---

## 3. Project structure

```
src/
  Web/                          # Server project (Blazor Web App host, BFF)
    Components/
      App.razor
      Routes.razor
      Layout/
      Pages/                    # Static SSR / Interactive Server pages
    Features/                   # Optional: organise by feature (Orders/, Account/)
    Program.cs
  Web.Client/                   # WebAssembly project (only if using WASM/Auto)
    Pages/                      # Components that run in the browser
    Services/
  Web.Shared/                   # Optional Razor Class Library: components shared with MAUI Blazor Hybrid
```

- Components needed in **WebAssembly or Auto** mode must live in the **Client** project (or a shared RCL referenced by it).
- Put reusable UI components in a **Razor Class Library** so they can be shared with a MAUI **Blazor Hybrid** app.
- Organise by **feature** (Orders, Account, Catalog) rather than by type (Pages, Components, Services) as the app grows.

---

## 4. Component design

- **Small, focused components.** Split when a component exceeds ~150 lines of markup or handles multiple concerns.
- **Presentational vs container components**: presentational components receive data via `[Parameter]` and raise `EventCallback`s; container/page components fetch data and coordinate.
- **Parameters are inputs — don't mutate them** inside the child. Copy to local state if editing.
- Use **`[EditorRequired]`** for mandatory parameters.
- Use `EventCallback<T>` (not `Action<T>`) for child→parent events — it triggers re-render correctly.
- **Avoid unnecessary renders**: override `ShouldRender` for expensive components; use `@key` in lists; avoid creating lambdas in tight loops of large lists.
- **`IDisposable`/`IAsyncDisposable`** for components that subscribe to events or timers.
- **Code-behind** (`.razor.cs` partial classes) for non-trivial logic; keep markup readable.
- **Inject services** with `@inject` / `[Inject]`; inject `IDbContextFactory` (never `DbContext` directly) in Interactive Server components because circuits outlive requests.
- **JS interop** sparingly; isolate in JS modules (`IJSObjectReference`); dispose references; never pass secrets to JS.
- **Error boundaries** (`<ErrorBoundary>`) around risky regions so one component failure doesn't kill the whole circuit.

---

## 5. State management

| State type | Where |
|---|---|
| Component UI state | Fields in the component |
| Page/feature state shared by siblings | Cascading values or a scoped state service |
| User session state (Interactive Server) | Scoped services (per circuit) — lost on circuit loss unless persisted; .NET 10 circuit state persistence helps |
| User state in WASM | Scoped/singleton services in the browser; `localStorage`/`sessionStorage` for non-sensitive preferences only |
| URL state (filters, page, sort) | **Query string** (`[SupplyParameterFromQuery]`) — shareable, bookmarkable, survives refresh |
| Server/domain data | The API (cache via HybridCache server-side) |

- Prefer **URL state** for anything the user would expect to survive refresh or share.
- Keep a **simple scoped state container** pattern (service with state + `OnChange` event) before reaching for Flux libraries (Fluxor) — adopt those only for genuinely complex client state.
- **Never store tokens or sensitive data in browser storage.**

---

## 6. Authentication and the BFF pattern

### 6.1 The rule

**Browsers should not hold OAuth access or refresh tokens.** Tokens in `localStorage`/`sessionStorage` (or JS memory) are exposed to any XSS. Instead use the **Backend-for-Frontend (BFF)** pattern:

1. The web server (BFF) performs the **OpenID Connect authorization code flow (with PKCE)** as a **confidential client**.
2. It stores tokens **server-side** (or in an encrypted, HTTP-only cookie session) and issues the browser a **secure, HTTP-only, SameSite cookie**.
3. Browser calls go to the BFF with the cookie; the BFF attaches the access token when calling the API (directly in server code or via a reverse proxy such as **YARP**).

This is the IETF-recommended architecture for browser-based apps ([OAuth 2.0 for Browser-Based Apps](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/)).

### 6.2 Blazor Web App implementation

```csharp
// Web/Program.cs
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("EntraExternalId"))
    .EnableTokenAcquisitionToCallDownstreamApi(["api://contoso-shop-api/Orders.ReadWrite"])
    .AddDistributedTokenCaches();                       // token cache in Redis for multi-instance

builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;               // Strict if no cross-site navigation login flows
    o.SlidingExpiration = true;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateSerialization(); // flows auth state to WASM components (.NET 9+)

// Typed client that attaches the user's access token when calling the API
builder.Services.AddHttpClient<OrdersApiClient>(c => c.BaseAddress = new("https+http://api"))
    .AddHttpMessageHandler<UserAccessTokenHandler>()
    .AddStandardResilienceHandler();
```

- **Static SSR and Interactive Server** components call the API **from server code** using the user's token — the token never reaches the browser.
- **WebAssembly components** call **the BFF** (same origin, cookie-authenticated), and the BFF forwards to the API. Use **YARP** to proxy `/api/*` → core API with token attachment, or expose minimal BFF endpoints. Protect BFF endpoints against CSRF (cookie + antiforgery, or require a custom header like `X-CSRF: 1` that cross-site requests can't set without CORS).
- `AuthenticationStateProvider` in WASM receives serialized auth state (claims) for UI decisions — **UI checks are cosmetic; the API enforces authorization.**
- Sign-out must clear the local cookie **and** sign out at the IdP (`SignOutAsync` with OIDC scheme).

### 6.3 Identity provider

- **Customers / consumers**: **Microsoft Entra External ID** (external tenant) — sign-up/sign-in user flows, social identity providers, MFA, branding.
- **Employees / internal apps**: **Microsoft Entra ID** (workforce tenant).
- See [07](07-security-and-identity.md) for app registrations, scopes, and roles.

---

## 7. Calling the API

- Use the **generated typed client** from the API's OpenAPI document (Kiota/NSwag) — shared in `src/Shared/Api.Client`.
- Register via `IHttpClientFactory` with **resilience handlers**.
- Handle **Problem Details** responses uniformly: map validation errors onto forms, show friendly messages for known `type`/`code`s, show trace ID for unexpected errors ("Something went wrong — reference 4bf92f...").
- **Cancellation**: cancel in-flight requests when components are disposed or navigation occurs.
- **Loading and error states** for every async operation; skeleton loaders over spinners for perceived performance.
- In **Interactive Server**, calling the API over HTTP from the server is still preferred over calling the database directly from components when the API owns business logic — this keeps a single source of business rules for web and mobile. (For a pure internal admin app, direct use of application services in-process is acceptable if web and API are the same deployable.)

---

## 8. Forms and validation

- Use `EditForm` with `DataAnnotationsValidator` or **FluentValidation** (via Blazored.FluentValidation or similar) — ideally **sharing validators/DTOs with the API** so rules stay consistent.
- .NET 10 supports **nested object and collection validation** (`AddValidation()` + `[ValidatableType]`).
- **Static SSR forms**: use `[SupplyParameterFromForm]`, `FormName`, and `Enhance` for SPA-like posting without interactivity. Antiforgery tokens are included automatically.
- **Server-side validation is authoritative** — client validation is UX only. Map API validation Problem Details back onto form fields.
- Disable submit while submitting; prevent double-submit; use idempotency keys for critical posts.
- Accessible error messages: associated with fields (`aria-describedby`), summarised at top, focus moved to the first error.

---

## 9. UI, design systems and styling

- Choose **one component library** and stick to it:
  - **Microsoft Fluent UI Blazor** (MIT) — Fluent 2 design, aligns with Microsoft ecosystem.
  - **MudBlazor** (MIT) — Material design, very popular, rich components.
  - **Radzen Blazor** (MIT) — large component set incl. data grids.
  - Commercial suites (Telerik, Syncfusion, DevExpress) if you need advanced grids/charts/schedulers and accept licensing.
- Define a **design system**: tokens (colours, spacing, typography), component guidelines, and dark-mode support. Share tokens with MAUI where practical.
- **CSS isolation** (`Component.razor.css`) for component-scoped styles; global styles minimal.
- **Responsive, mobile-first** layouts — many users will hit the web app from phones.
- **QuickGrid** (built-in) for straightforward, virtualized data grids.

---

## 10. Accessibility

Target **WCAG 2.2 Level AA** as a minimum (often a legal requirement — e.g., the European Accessibility Act applies from June 2025 to many consumer-facing digital services).

- Semantic HTML first (`<button>`, `<nav>`, `<main>`, headings in order); ARIA only to fill gaps.
- Every interactive element reachable and operable by **keyboard**; visible focus indicators; logical tab order.
- **Colour contrast** ≥ 4.5:1 for text (3:1 for large text/UI components); never convey meaning by colour alone.
- **Text alternatives** for images; labels for all inputs.
- **Focus management** on navigation and dialogs (Blazor's `FocusOnNavigate`).
- Respect `prefers-reduced-motion` and support zoom to 200%/reflow at 320 px width.
- Announce dynamic changes (live regions) for async updates.
- **Automated checks** (axe-core via Playwright) in CI + **manual testing** with screen readers (NVDA, VoiceOver) before major releases.

---

## 11. Performance

### 11.1 Targets

Track **Core Web Vitals** (field data via Application Insights browser SDK/RUM):
- **LCP** (Largest Contentful Paint) < 2.5 s
- **INP** (Interaction to Next Paint) < 200 ms
- **CLS** (Cumulative Layout Shift) < 0.1

Set a **performance budget** (JS/WASM payload size, LCP) and check it in CI (Lighthouse CI).

### 11.2 Techniques

- **Static SSR + streaming rendering** for fastest first paint.
- **`MapStaticAssets`** (replaces `UseStaticFiles` for build-time compressed, fingerprinted assets with long-lived caching).
- **WebAssembly payload**: enable **trimming** (default in Release), consider **WASM AOT** only when CPU-bound code needs it (it increases download size), **lazy-load assemblies** for rarely used areas, use **Interactive Auto** to hide download time.
- **Virtualization** (`<Virtualize>`) for long lists.
- Minimise **Interactive Server** round-trips — avoid `@oninput` handlers that hit the server per keystroke; debounce.
- **Images**: responsive sizes, modern formats (WebP/AVIF), lazy loading, explicit dimensions to avoid CLS.
- **Caching**: Front Door/CDN for static assets; output caching for anonymous pages; HybridCache for data.
- **Compression**: Brotli at Front Door / static assets.

---

## 12. Web security

See [07](07-security-and-identity.md) for the full picture. Web-specific controls:

- **HTTPS everywhere** + **HSTS** (`app.UseHsts()` in non-dev; consider HSTS preload).
- **Content Security Policy (CSP)**: start strict (`default-src 'self'`), add what's required. Blazor needs `'wasm-unsafe-eval'` for WebAssembly; avoid `'unsafe-inline'` scripts — use nonces/hashes. Use `frame-ancestors 'none'` (or specific origins) to prevent clickjacking.
- Other headers: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` restricting unused features, `Cross-Origin-Opener-Policy: same-origin`.
- **Antiforgery**: on by default for Blazor forms (`app.UseAntiforgery()`); ensure BFF API endpoints called from WASM are CSRF-protected.
- **XSS**: Razor encodes output by default. **Never render untrusted content with `MarkupString`** unless sanitized (e.g., HtmlSanitizer). Be careful with JS interop that sets `innerHTML`.
- **Open redirects**: validate `returnUrl` (`Url.IsLocalUrl` / `NavigationManager` checks).
- **Interactive Server DoS**: configure circuit limits (`DisconnectedCircuitMaxRetained`, `MaxBufferedUnacknowledgedRenderBatches`, SignalR message size limits); rate limit at the edge.
- **No secrets in WebAssembly** — everything downloaded is public. Configuration in `wwwroot/appsettings.json` is visible to users.
- **Cookies**: `Secure`, `HttpOnly`, `SameSite=Lax|Strict`; `__Host-` prefix where possible.
- **Subresource Integrity (SRI)** for any third-party scripts (prefer self-hosting).

---

## 13. Localization and SEO

### Localization
- `IStringLocalizer<T>` with `.resx` resources; `RequestLocalizationMiddleware` with supported cultures; culture selection persisted in a cookie.
- For WASM, set `<BlazorWebAssemblyLoadAllGlobalizationData>` or load specific cultures; invariant globalization mode reduces size if you don't need culture-specific formatting.
- Format dates, numbers, currencies with the user's culture; store UTC.

### SEO (public pages)
- Static SSR (or prerendering) makes content crawlable.
- `<PageTitle>` and `<HeadContent>` for titles, meta descriptions, canonical links, Open Graph tags.
- `sitemap.xml` and `robots.txt`; clean URLs; structured data (JSON-LD) where relevant.

---

## 14. Hosting the web app on Azure

| Scenario | Host |
|---|---|
| Blazor Web App (SSR/Server/Auto) | **Azure Container Apps** (default) or **App Service** (Linux) |
| Standalone Blazor WASM or JS SPA (static files only) | **Azure Static Web Apps** (with linked API backend) or Blob Storage static website + Front Door |
| Interactive Server at scale | Add **Azure SignalR Service** (removes need for sticky sessions); otherwise configure session affinity |

Common requirements:
- **Azure Front Door** in front (WAF, TLS, CDN for static assets, custom domain).
- **Data Protection keys** persisted to Blob Storage and protected with Key Vault (`PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault`) so cookies/antiforgery tokens work across instances and restarts. **Mandatory for multi-instance deployments.**
- Distributed token cache (Redis) for `Microsoft.Identity.Web` when scaled out.
- Health probes, OpenTelemetry, and the same ServiceDefaults as the API.

---

## 15. If you choose a JavaScript SPA

If the team chooses React/Angular/Vue:

- **TypeScript in strict mode**; ESLint + Prettier; **Vite** as build tool (React/Vue) or the Angular CLI.
- **Host behind an ASP.NET Core BFF** (same origin): the BFF handles OIDC login and cookie session and proxies `/api` calls to the core API with **YARP**, attaching tokens. Libraries: **Duende BFF** (commercial licence) or a custom YARP + `Microsoft.Identity.Web` setup.
- **Generate the TypeScript API client** from OpenAPI (Kiota, openapi-typescript, orval).
- **Server state** via TanStack Query (React/Vue) or equivalent; avoid global stores for server data.
- **Component library + design tokens** (e.g., Fluent UI React).
- **Testing**: Vitest/Jest + Testing Library for components; Playwright for E2E.
- **Supply chain**: lock files committed, `npm audit`/Dependabot, pin versions, minimise dependencies.
- Static assets built in CI and served by the BFF or from Front Door/Static Web Apps; set long cache lifetimes on hashed assets and no-cache on `index.html`.
- **Aspire** can orchestrate the JS dev server alongside .NET projects (`AddViteApp`/`AddNpmApp` — ⚠ verify current API names).

---

## 16. Testing the web app

Summary (detail in [10](10-testing-strategy.md)):
- **bUnit** for Blazor component tests (rendering, parameters, events, interactions).
- **Integration tests** of the server host with `WebApplicationFactory` (auth, routing, SSR pages).
- **Playwright** for E2E user journeys across browsers, including accessibility checks (axe).
- **Visual regression** (Playwright screenshots) for key pages if the UI is design-critical.

---

## 17. References

- [ASP.NET Core Blazor](https://learn.microsoft.com/aspnet/core/blazor/)
- [Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes)
- [Secure ASP.NET Core Blazor Web Apps](https://learn.microsoft.com/aspnet/core/blazor/security/)
- [Blazor Web App with OIDC and the BFF pattern](https://learn.microsoft.com/aspnet/core/blazor/security/blazor-web-app-with-oidc)
- [Blazor performance best practices](https://learn.microsoft.com/aspnet/core/blazor/performance/)
- [Data Protection key storage](https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers)
- [YARP](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/yarp-overview)
- [OAuth 2.0 for Browser-Based Applications (IETF draft)](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/)
- [WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)
