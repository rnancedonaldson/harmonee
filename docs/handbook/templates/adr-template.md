# ADR NNNN: <Short title in imperative form, e.g. "Use a modular monolith for the Shop API">

> Copy this file to `docs/adr/NNNN-short-title.md` in the product repo. Number sequentially; never reuse numbers.

| Field | Value |
|---|---|
| **Status** | Proposed · Accepted · Rejected · Deprecated · Superseded by [ADR-XXXX](XXXX-title.md) |
| **Date** | YYYY-MM-DD |
| **Deciders** | Names / roles of people who made the decision |
| **Consulted** | People whose input was sought (security, ops, product…) |
| **Informed** | People/teams who need to know |
| **Related** | Links to issues, other ADRs, design docs |

---

## Context

What is the issue that motivates this decision? Describe the forces at play: business drivers, technical constraints, quality attributes (performance, security, cost, team skills, timelines), and any assumptions. Keep it factual and neutral — a reader in two years should understand *why this was a question at all*.

## Decision drivers

- Driver 1 (e.g., "Team of 6 engineers; must ship MVP in 4 months")
- Driver 2 (e.g., "API must support mobile clients that may be up to 12 months old")
- Driver 3 (e.g., "Data must remain in the EU")

## Options considered

### Option A: <name>
Short description.
- ✅ Pro
- ✅ Pro
- ❌ Con
- 💰 Cost / effort notes

### Option B: <name>
- ✅ …
- ❌ …

### Option C: Do nothing / status quo (if relevant)
- ✅ …
- ❌ …

## Decision

We will **<chosen option>** because <primary reasons tied to the decision drivers>.

Be specific about scope: what this decision covers and what it does not.

## Consequences

### Positive
- …

### Negative / trade-offs accepted
- …

### Risks and mitigations
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| … | Low/Med/High | Low/Med/High | … |

### Follow-up actions
- [ ] Action (owner, due date)

## Validation

How will we know this decision is working? What signals would cause us to revisit it (e.g., "If deployment coordination between modules exceeds X per month, revisit extracting services")?

## References

- Links to documentation, benchmarks, spikes, prior art.

---

<!--
Guidance:
- Write an ADR for decisions that are expensive to reverse, affect multiple teams/components, or that someone will later ask "why did we do this?".
- Keep it short (1–2 pages). Link to longer analysis if needed.
- Once Accepted, don't edit the decision — write a new ADR that supersedes it and update the old one's status.
- Review ADRs in PRs like code.
-->
