# ADR 0001: Record architecture decisions

| Status | Date |
|---|---|
| Accepted | 2026-10-24 |

## Context
Decisions that are expensive to reverse get forgotten, and someone later asks "why did we do this?".

## Decision
We record significant decisions as ADRs in `docs/adr/NNNN-title.md`, using the [handbook template](../handbook/templates/adr-template.md). An accepted ADR isn't edited; it's superseded by a new one. ADRs are reviewed in pull requests like code.

## Consequences
A small writing overhead per decision. New contributors can read the reasoning behind the codebase.
