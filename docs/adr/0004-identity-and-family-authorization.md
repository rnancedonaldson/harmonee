# ADR 0004: Entra External ID for identity; family-scoped authorization in the API

| Status | Date |
|---|---|
| Accepted | 2026-10-24 |

## Context
Harmonee is a consumer product: families sign up themselves. Access to data depends on **which family** you're in and **your custom role in that family**, which changes often and is managed by family owners. Platform staff need to support any family.

## Decision
- **Identity:** Microsoft Entra External ID (an external tenant). Azure AD B2C is closed to new customers. We never store passwords.
- **Coarse authorization (in the token):** the API requires the delegated scope `Harmonee.Access`. Platform administrators get the app role `Harmonee.Admin`, defined on the API registration.
- **Fine authorization (in our database):** family membership, ownership and roles live in the Families module, not in the identity provider. Roles are per family and hold a set of `FamilyPermission`s. Every family-scoped endpoint declares the permission it needs with `RequireFamilyPermission(...)`.
- Non-members get **404**, not 403, so family IDs can't be probed.
- **Admin access is an override, not a membership.** Admins get every permission on any family, the response says so (`IsAdminOverride`), the UI shows a banner, and every request is audit-logged.
- Users are identified by the token's `tid` + `oid`, mapped to an internal user profile created on first use. We never key on email or name.

## Consequences
- Family owners manage roles without touching the identity provider.
- The access check is one indexed query per request. Cache it (HybridCache with tag invalidation) if telemetry shows it's needed.
- Admin powers are broad by design, so the audit log and admin-role assignment need care. Grant `Harmonee.Admin` sparingly; persisting the audit log is on the roadmap.
