# ADR 0001: Use a modular monolith for the core API

> **Example ADR** — illustrates how to fill in the [template](adr-template.md). Adapt or discard for real projects.

| Field | Value |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-17 |
| **Deciders** | Lead engineer, Architect, Engineering manager |
| **Consulted** | Platform/ops, Security, Product owner |
| **Informed** | All engineering |
| **Related** | [01 — Architecture Overview §3](../01-architecture-overview.md#3-choosing-an-architectural-style) |

---

## Context

We are building a new product consisting of an HTTP API, a Blazor web application, and .NET MAUI mobile apps, hosted on Azure. The initial team is six engineers. The domain has several identifiable areas (Catalog, Ordering, Billing, Notifications) but their boundaries are not yet fully proven. We need to reach an MVP within roughly four months while keeping the option to scale the team and system later.

## Decision drivers

- Small team; limited capacity for operating many deployables.
- Domain boundaries still being discovered — refactoring across them must be cheap.
- Need for strong consistency in some flows (order placement + stock reservation) during MVP.
- Desire to scale individual parts independently *eventually*, and to allow multiple teams later.
- Observability, CI/CD, and IaC must be in place from day one regardless of style.

## Options considered

### Option A: Microservices from the start (one service per domain area)
- ✅ Independent deployment and scaling per area.
- ✅ Clear ownership boundaries if teams grow.
- ❌ High operational overhead (N pipelines, N databases, distributed tracing, contract testing, network failure handling) for a six-person team.
- ❌ Boundaries likely wrong at this stage; moving logic between services is expensive.
- ❌ Distributed transactions/sagas needed immediately for order placement.

### Option B: Traditional layered monolith
- ✅ Simplest to start.
- ❌ No enforced boundaries; tends towards a "big ball of mud", making later extraction hard.

### Option C: Modular monolith (single deployable, enforced module boundaries, schema per module)
- ✅ One deployable and pipeline; simple operations and debugging.
- ✅ Module boundaries enforced by project references and architecture tests; each module owns its schema.
- ✅ Cheap to refactor boundaries while the domain is being learned.
- ✅ A module can be extracted to a service later with limited effort because it already communicates via contracts/events.
- ❌ Scales as a single unit (acceptable for projected load).
- ❌ Requires discipline and tooling to prevent boundary erosion.

## Decision

We will build the core API as a **modular monolith** (Option C): one ASP.NET Core deployable on Azure Container Apps, with modules aligned to bounded contexts (Catalog, Ordering, Billing, Notifications), each owning its own database schema and exposing a `*.Contracts` project. Cross-module communication uses in-process interfaces for queries and integration events (via outbox + Service Bus) for asynchronous reactions.

Scope: the core API only. Event-driven background processing (email, image processing) may use Azure Functions where appropriate.

## Consequences

### Positive
- Faster delivery of MVP with lower operational burden.
- Single transaction scope available where strong consistency is required.
- Clear path to service extraction later.

### Negative / trade-offs accepted
- All modules deploy together; a defect in one module can require redeploying all.
- Independent scaling per module is not available initially.

### Risks and mitigations
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Module boundaries erode over time | Medium | High | Architecture tests in CI; CODEOWNERS on `*.Contracts`; no cross-schema queries. |
| One module's load affects others | Low | Medium | Monitor per-endpoint metrics; extract module if sustained contention appears. |
| Team growth leads to merge/deploy contention | Medium | Medium | Revisit this ADR when > 3 teams or deployment coordination becomes a bottleneck. |

### Follow-up actions
- [ ] Create architecture tests for module dependencies (Lead engineer).
- [ ] Set up schema-per-module migrations (Lead engineer).
- [ ] Implement outbox for integration events (Team).

## Validation

Revisit if any of the following occur: more than three teams contributing to the API; a module requires materially different scaling (> 5× others) or release cadence; compliance requires isolating a module (e.g., payments).

## References

- Martin Fowler, "MonolithFirst"
- Sam Newman, *Monolith to Microservices*
- Microsoft, *Architect modern web applications with ASP.NET Core and Azure*
