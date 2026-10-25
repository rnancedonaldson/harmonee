# ADR 0005: Share HTTP contracts as a project instead of generating clients (for now)

| Status | Date |
|---|---|
| Accepted | 2026-10-24 |

## Context
The handbook prefers clients generated from the OpenAPI document (Kiota/NSwag). All current clients (web, and mobile soon) are .NET and live in this monorepo.

## Decision
Request/response records, permission names and field limits live in `Harmonee.Api.Contracts`, a dependency-free library referenced by the API, the web app, and later the MAUI app. The web app has a small hand-written typed client (`HarmoneeApiClient`) over those contracts. The **OpenAPI document is still the published contract**: a test compares it with a committed snapshot, so every API change is visible in review.

## Consequences
- Compile-time safety across API and clients with no generation step.
- Coupling: client and API must be released together, which is fine in a monorepo. Additive changes only within `v1`.
- Revisit (switch to Kiota) when a non-.NET client or an external consumer appears.
