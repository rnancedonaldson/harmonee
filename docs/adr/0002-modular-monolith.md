# ADR 0002: Replace the per-service layout with a modular monolith

| Status | Date | Supersedes |
|---|---|---|
| Accepted | 2026-10-24 | The earlier Auth/Family/Kitchen/Schedule microservice skeleton |

## Context
The previous repository split the product into five services (Auth, Family, Kitchen, Schedule, Migration), each with its own database, plus Clean Architecture shared layers. None of them had functionality yet. Nearly every feature (calendar, lists, meals, notes) needs the same family-membership and role check, so separate services would make every request a cross-service authorization call. One developer is building the MVP.

## Options considered
- **Keep microservices.** Independent deployment, but heavy operational cost (N pipelines, N databases, service-to-service auth, distributed tracing) and an authorization round-trip on every request. Module boundaries are also unproven.
- **Layered monolith.** Simple, but no enforced boundaries.
- **Modular monolith** (chosen). One API deployable; modules aligned to bounded contexts; each owns a schema; modules interact only through `*.Contracts`.

## Decision
One ASP.NET Core API (`Harmonee.Api`) composes four modules: **Families**, **Calendar**, **Kitchen**, **Notes**. Each module owns a schema in a single Azure SQL database and its own `DbContext` and migrations. Inside a module, code is organised as vertical slices (`Features/`) with a rich domain model only where invariants justify it (the `Family` aggregate). Identity moved to Microsoft Entra External ID (see ADR 0004), so the old ASP.NET Identity "Auth service" is gone.

## Consequences
- Fast iteration; one pipeline; family authorization is an in-process call.
- Boundaries are enforced by `tests/Harmonee.Architecture.Tests`.
- A module can later be extracted into a service. It already has its own schema and talks to others only through contracts.
- Revisit if a module needs very different scaling or release cadence, or if more than about three teams work on the API.
