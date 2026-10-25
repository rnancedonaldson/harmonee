# ADR 0003: Blazor Web App with static server rendering as the Backend-for-Frontend

| Status | Date |
|---|---|
| Accepted | 2026-10-24 |

## Context
We need a web front-end that signs users in and calls the API on their behalf. Browser-held tokens (in `localStorage` or JS memory) are exposed to XSS; the IETF recommendation for browser apps is a BFF. The team is .NET-first.

## Options considered
- **SPA (React) + BFF proxy.** Two toolchains and a separate auth proxy.
- **Blazor WebAssembly/Auto.** Rich interactivity, but needs a cookie-authenticated proxy for API calls, adds a download, and complicates token handling.
- **Blazor Interactive Server.** Simple, but long-lived circuits don't fit well with per-request token acquisition, and it costs server memory per user.
- **Blazor static SSR with enhanced navigation and forms** (chosen).

## Decision
`Harmonee.Web` renders every page on the server per request. It's a confidential OIDC client (Microsoft.Identity.Web): tokens live in a server-side cache, and the browser gets only an HTTP-only, `Secure`, `SameSite=Lax` `__Host-` cookie. API calls go through a typed `HttpClient` whose handler attaches the user's token. Forms use Blazor's enhanced form handling with antiforgery tokens and Post/Redirect/Get. There's a strict CSP with no inline scripts or styles.

## Consequences
- No tokens in the browser; one language; a small, fast, accessible HTML-first UI.
- Interactions are form posts. Rich client-side interactivity (drag and drop, live updates) would need interactive components later. Add those per component, routed through the BFF.
- In production the token cache must be distributed (Redis) and Data Protection keys must be shared (Blob + Key Vault). Both are already configurable; see the roadmap.
