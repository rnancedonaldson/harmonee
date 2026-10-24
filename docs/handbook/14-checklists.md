# 14 — Checklists

> Practical checklists distilled from the other documents. Copy them into issue templates, PR templates, and release runbooks.

---

## Contents

1. [New project bootstrap](#1-new-project-bootstrap)
2. [Pull request checklist](#2-pull-request-checklist)
3. [Production readiness review](#3-production-readiness-review)
4. [Release checklist](#4-release-checklist)
5. [Periodic reviews](#5-periodic-reviews)

---

## 1. New project bootstrap

### Repository and solution
- [ ] Repo created with branch protection on `main` (PR required, status checks, no force push), CODEOWNERS, PR template.
- [ ] `global.json` pinning .NET 10 SDK (`rollForward: latestFeature`).
- [ ] `Directory.Build.props` (nullable, warnings as errors, analyzers, lock files, NuGet audit).
- [ ] `Directory.Packages.props` (Central Package Management).
- [ ] `nuget.config` with `<clear/>` and package source mapping.
- [ ] `.editorconfig`, `.gitattributes`, `.gitignore`.
- [ ] `.slnx` solution; solution filters if mobile is included.
- [ ] Aspire `AppHost` + `ServiceDefaults` projects; `aspire run` works from a clean clone.
- [ ] `README.md` (purpose, quick start, links), `CONTRIBUTING.md`, `docs/adr/0001-record-architecture-decisions.md`.
- [ ] Dev container / Codespaces config (optional but recommended).
- [ ] Local tools manifest (`dotnet-ef`, Kiota, etc.).
- [ ] AI assistant instruction files aligned with these guidelines (if used).

### Architecture
- [ ] Quality attribute scenarios written (performance, availability, RPO/RTO, security, compatibility).
- [ ] Bounded contexts / modules identified (Event Storming or equivalent); module boundaries documented.
- [ ] C4 context and container diagrams in `docs/architecture/`.
- [ ] Tenancy model decided (if multi-tenant) — ADR.
- [ ] Key decisions recorded as ADRs (architecture style, web stack, data store, identity provider, hosting).
- [ ] Initial threat model.

### Code foundations
- [ ] Module template with Features / Domain / Infrastructure / Contracts structure.
- [ ] Problem Details + global exception handler.
- [ ] Validation approach wired (built-in `AddValidation` and/or FluentValidation filter).
- [ ] Authentication (Entra ID / External ID) and fallback authorization policy.
- [ ] API versioning and OpenAPI (with build-time document generation).
- [ ] Health endpoints (live/ready/startup).
- [ ] OpenTelemetry via ServiceDefaults; Azure Monitor exporter configured by environment.
- [ ] Resilience defaults on `HttpClient`s.
- [ ] Options classes with validation on start; no secrets in config files.
- [ ] `TimeProvider` registered; no direct `DateTime.UtcNow` in business code.
- [ ] Outbox pattern in place if publishing integration events.

### Testing
- [ ] Unit test project(s) with xUnit v3 on Microsoft.Testing.Platform.
- [ ] Integration test project with `WebApplicationFactory` + Testcontainers + Respawn.
- [ ] Architecture tests for module boundaries.
- [ ] bUnit project (web) and Playwright E2E skeleton.
- [ ] `Mobile.Core` unit tests (mobile).

### Infrastructure and delivery
- [ ] Azure subscriptions (prod / non-prod) within a landing zone; budgets and tags policy.
- [ ] Bicep (AVM) for all resources; parameter files per environment; PSRule/lint in CI.
- [ ] User-assigned managed identities and RBAC in IaC; key-based auth disabled.
- [ ] Key Vault (RBAC, soft delete, purge protection, private endpoint) and App Configuration per environment.
- [ ] Networking: VNet, private endpoints, Front Door + WAF, origins locked down.
- [ ] Log Analytics + Application Insights per environment (Entra auth for ingestion).
- [ ] CI pipeline: restore (locked) → build → format → tests → CodeQL → dependency review → secret scanning.
- [ ] CD pipeline with OIDC federation, environments (dev → test → prod) and approvals.
- [ ] Container publish via SDK (chiseled base image), image scanning, SBOM, provenance attestation.
- [ ] EF migration bundle + migration job.
- [ ] Dependabot (NuGet, Actions, Docker, npm) configured.
- [ ] Mobile: signing assets secured; TestFlight / Play internal track automation (if applicable).

---

## 2. Pull request checklist

Use as the PR template body (`.github/pull_request_template.md`):

```markdown
## What and why
<!-- Summary of the change and the problem it solves. Link the issue. -->

## How it was tested
<!-- Unit/integration/E2E tests added or updated; manual verification steps; screenshots for UI. -->

## Risk and rollback
<!-- What could go wrong? Feature flag? How do we roll back? -->

## Checklist
- [ ] Small, focused change (split if > ~400 lines without good reason)
- [ ] Tests cover the new behaviour (including failure and authorization paths)
- [ ] No secrets, credentials, or PII in code, config, logs, or test data
- [ ] Input validated; authorization enforced (incl. resource ownership)
- [ ] API changes are backward compatible (or versioned) and OpenAPI diff reviewed
- [ ] DB migrations reviewed; backward compatible with the running version (expand/contract)
- [ ] Telemetry: meaningful logs/traces/metrics added; no sensitive data logged
- [ ] Errors return Problem Details; no internal details leaked
- [ ] Async all the way; CancellationToken passed through; no blocking calls
- [ ] Performance considered (queries projected/paginated, no N+1, caching where appropriate)
- [ ] Accessibility considered for UI changes (keyboard, labels, contrast)
- [ ] New dependencies vetted (licence, maintenance, security) and justified
- [ ] Docs/ADR/runbook updated if behaviour, architecture, or operations changed
- [ ] Feature flag used for incomplete or risky features
```

### Reviewer focus
1. **Correctness** — does it do what it claims, including edge cases?
2. **Design** — right module/boundary? Simple? Consistent with conventions?
3. **Security** — authN/authZ, validation, injection, secrets, data exposure.
4. **Tests** — meaningful assertions; would they catch a regression?
5. **Operability** — telemetry, error handling, configuration, rollback.
6. **Readability** — naming, clarity, comments explain *why*.

---

## 3. Production readiness review

Complete before first go-live and for major new components. Each item has an owner and evidence (link).

### Architecture and design
- [ ] Architecture doc and ADRs current; C4 diagrams reflect reality.
- [ ] Threat model reviewed and mitigations implemented.
- [ ] WAF assessment completed; high-priority recommendations addressed.
- [ ] Dependencies (internal and external) documented with their SLAs and failure behaviour.

### Reliability
- [ ] SLOs defined for critical journeys; SLIs implemented; error budget policy agreed.
- [ ] Zone redundancy enabled for production compute and data.
- [ ] Health probes configured correctly (liveness doesn't check dependencies).
- [ ] Timeouts, retries (idempotent only), circuit breakers configured on all outbound calls.
- [ ] Graceful shutdown verified.
- [ ] Autoscaling rules configured and validated by load test; min replicas set for latency.
- [ ] Load test passed at expected peak + headroom (e.g., 2×).
- [ ] Backups configured; **restore tested**; RPO/RTO documented and met.
- [ ] DR strategy documented; failover tested (or scheduled).
- [ ] Message DLQs monitored with a replay procedure.
- [ ] Chaos experiments for key failure modes (optional for v1, required for critical systems).

### Security
- [ ] Authentication via Entra ID / External ID; MFA policies in place; admin access requires phishing-resistant MFA.
- [ ] Fallback authorization policy; resource-based authorization tested (negative tests).
- [ ] Managed identity everywhere; no secrets in code/config/pipelines; key-based auth disabled on resources.
- [ ] Key Vault with RBAC, soft delete, purge protection, private endpoint.
- [ ] Private endpoints for data services; public network access disabled.
- [ ] Front Door WAF in Prevention mode; origins accept traffic only from Front Door.
- [ ] Security headers (HSTS, CSP, etc.) verified.
- [ ] CodeQL, dependency, container, IaC scans clean (or exceptions documented).
- [ ] DAST scan completed; findings triaged.
- [ ] Penetration test completed for public/high-risk systems.
- [ ] Defender for Cloud plans enabled; Secure Score recommendations triaged.
- [ ] Production access via PIM only; break-glass accounts configured and monitored.
- [ ] Audit logging for sensitive operations.

### Data and privacy
- [ ] Data classified; PII inventory and data flow documented.
- [ ] Encryption at rest/in transit verified; sensitive columns protected as required.
- [ ] Retention and deletion policies implemented; data subject request process defined.
- [ ] No production data in non-prod environments.
- [ ] Privacy notice, consent (cookies/analytics), and store disclosures (mobile) complete.

### Observability and operations
- [ ] OpenTelemetry traces/metrics/logs flowing to Application Insights with correct resource attributes.
- [ ] End-to-end trace from web/mobile client through API to DB/broker verified.
- [ ] Dashboards for service health and critical journeys.
- [ ] Alerts: SLO burn rate, availability tests, 5xx spikes, DLQ, saturation, cert/secret expiry, budget — routed to on-call.
- [ ] Every alert links to a runbook.
- [ ] On-call rotation staffed with access and training.
- [ ] Incident process and status page/communication plan defined.
- [ ] Telemetry sampling/retention configured to control cost.
- [ ] Ability to capture dumps/traces in production verified.

### Delivery
- [ ] Fully automated deployment to production via pipeline; no manual steps.
- [ ] Rollback procedure documented and rehearsed (revision traffic shift / slot swap / flag off).
- [ ] Database migration process tested in staging with production-like data volume.
- [ ] Feature flags for risky features; kill switches documented.
- [ ] Deployment annotations in telemetry.

### Cost
- [ ] Cost estimate reviewed vs budget; budgets and alerts configured.
- [ ] Tags applied; cost reporting by workload/environment available.
- [ ] Non-prod scale-to-zero / auto-pause configured.
- [ ] Reservation/savings plan opportunities identified for steady-state production.

### Mobile (if applicable)
- [ ] Minimum supported version / forced upgrade mechanism working.
- [ ] Crash reporting and analytics configured (with consent handling).
- [ ] Push notifications end-to-end tested (iOS and Android); tokens registered via backend.
- [ ] Store listings, privacy manifests/data safety forms, account deletion flow complete.
- [ ] Staged/phased rollout plan with crash-free threshold.

### Accessibility
- [ ] WCAG 2.2 AA automated checks passing; manual screen reader test of key journeys (web and mobile).

---

## 4. Release checklist

### Before
- [ ] All CI checks green on the release commit; E2E passing in test.
- [ ] Release notes drafted (user-facing changes, breaking changes, deprecations).
- [ ] Migrations reviewed; backward compatible; backup/restore point noted.
- [ ] Feature flags set to intended initial state.
- [ ] Production currently healthy (no active incidents, error budget not exhausted).
- [ ] Stakeholders informed of release window (if customer-visible changes).

### During
- [ ] Migrations applied successfully.
- [ ] New revision deployed at 0% → smoke tests via revision label URL.
- [ ] Canary traffic steps with SLO checks (error rate, latency) between steps.
- [ ] Monitor dashboards and logs for anomalies.

### After
- [ ] 100% traffic on new revision; previous revision retained for quick rollback for an agreed period.
- [ ] Smoke/synthetic tests passing in production.
- [ ] Release tagged; release notes published.
- [ ] Feature flags progressively enabled per rollout plan.
- [ ] Clean-up tickets created (remove old flags, contract migrations, old revisions).

### Mobile release
- [ ] Release build from tagged commit; version/build numbers correct.
- [ ] TestFlight / Play internal testing sign-off.
- [ ] Store metadata and screenshots updated.
- [ ] Phased release / staged rollout started; crash-free rate monitored daily.
- [ ] API compatibility with previous app versions confirmed.

---

## 5. Periodic reviews

### Weekly
- [ ] Dependabot PRs reviewed and merged.
- [ ] Alerts and incidents reviewed; noisy alerts fixed.
- [ ] Flaky tests triaged.

### Monthly
- [ ] Azure Advisor and Defender for Cloud recommendations reviewed.
- [ ] Cost review against budget; anomalies investigated.
- [ ] SLO attainment and error budget review.
- [ ] Stale feature flags removed.
- [ ] Telemetry ingestion volume and cost reviewed.

### Quarterly
- [ ] Backup restore drill.
- [ ] Access review (RBAC assignments, app role assignments, PIM eligibility, guest users).
- [ ] Secret/certificate rotation verified.
- [ ] DORA metrics review; delivery bottlenecks addressed.
- [ ] Threat model refresh for significantly changed areas.
- [ ] Deprecated API version usage reviewed; removal plans progressed.
- [ ] Mobile minimum supported version reviewed.

### Annually
- [ ] WAF assessment re-run.
- [ ] DR failover exercise / game day.
- [ ] Penetration test.
- [ ] .NET/runtime/OS/mobile SDK upgrade plan (align with November .NET releases and annual iOS/Android platform requirements).
- [ ] Review and update this handbook.
