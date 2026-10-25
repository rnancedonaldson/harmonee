# ADR 0007: Real SQL Server for local development and tests, with or without containers

| Status | Date |
|---|---|
| Accepted | 2026-10-24 |

## Context
The handbook calls for integration tests against a real database engine (never the EF in-memory provider) and recommends Testcontainers. Not every developer machine has Docker. The primary development machine has SQL Server LocalDB but no Docker.

## Decision
- **Local run:** if the AppHost has `ConnectionStrings:harmonee-db` configured (LocalDB by default in `appsettings.Development.json`), it's used. Otherwise Aspire runs SQL Server in a container. Azure deployments provision Azure SQL.
- **Integration tests:** use `HARMONEE_TEST_SQL` if set (CI runs a SQL Server 2022 service container), otherwise LocalDB. Each run creates a uniquely named database and drops it afterwards. Tests isolate data by using fresh users per test, so they can run in parallel without resets.

## Consequences
- Tests exercise real SQL Server behaviour (row versions, unique indexes, cascades) on every machine and in CI.
- LocalDB is Windows-only; macOS/Linux developers need Docker or a SQL Server instance. Moving to Testcontainers is easy if Docker becomes universal.
