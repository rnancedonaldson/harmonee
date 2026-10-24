## What and why
<!-- Summary of the change and the problem it solves. Link the issue. -->

## How it was tested
<!-- Tests added or updated; manual verification steps; screenshots for UI changes. -->

## Risk and rollback
<!-- What could go wrong? Is it behind a feature flag? How do we roll back? -->

## Checklist
- [ ] Small, focused change
- [ ] Tests cover the new behaviour, including failure and authorization paths
- [ ] No secrets, credentials or personal data in code, config, logs or test data
- [ ] Input validated; family-scoped endpoints use `RequireFamilyPermission(...)` and filter by `familyId`
- [ ] API changes are backward compatible; OpenAPI snapshot reviewed (`tests/Harmonee.Api.IntegrationTests/Snapshots`)
- [ ] Migrations reviewed and backward compatible with the running version (expand/contract)
- [ ] Telemetry added where useful; nothing sensitive logged
- [ ] Accessibility considered for UI changes (keyboard, labels, contrast)
- [ ] New dependencies vetted (licence, maintenance, security)
- [ ] Docs / ADRs updated if behaviour or architecture changed
