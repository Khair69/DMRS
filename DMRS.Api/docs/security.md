# Security in Nabd

This document records the security posture of Nabd: the threats it defends against, the mechanisms that
do the defending, and the honest line between what is implemented today and what is deferred to
production hardening. It complements [`authentication.md`](authentication.md) (who you are) and
[`authorization.md`](authorization.md) (what you may do).

> **Scope note.** Nabd is a graduation project that handles **simulated patient data only** (Synthea
> bundles). It is engineered to the right *shape* for a clinical system, but production-grade compliance
> (HIPAA/GDPR) is explicitly out of scope — see "Deferred to production" below and
> [`deployment.md`](deployment.md).

## Threat model

The main assets are **patient health information (PHI)** and the **integrity of clinical records**. The
threats Nabd is designed to resist:

| Threat | Defense |
|---|---|
| **Unauthorized access** — an anonymous or wrong-role caller reaching data | JWT bearer auth on every endpoint; role + SMART-scope authorization (see below). |
| **Cross-patient / cross-org data leaks** — a valid user seeing records they shouldn't | Compartment checks: patient-owned and org-owned ownership gates in `FhirScopeHandler`. |
| **Privilege escalation** — a patient acting as staff | Role gates on the `System`/`User` access levels stop default wildcard scopes from over-granting. |
| **Token forgery / tampering** | Signature validation against Keycloak's published signing keys; issuer + audience validation. |
| **Token replay to the wrong service** | The client attaches tokens only to the configured API origin (`authorizedUrls`). |
| **Silent data loss / undetectable edits** | Every write is versioned and retained; deletes are soft. A record's history is an integrity trail. |
| **Secret exposure (stored model endpoints)** | External-AI endpoint secrets are encrypted at rest with ASP.NET Data Protection. |

Out of the current threat model (accepted for the demo): network-level attackers (no TLS in dev),
malicious insiders with database access, and denial-of-service.

## How identity is trusted: JWT validation

The API trusts a request only after the bearer token passes every check (configured in
[`Program.cs`](../Program.cs)):

- **Signature** — verified against the realm's signing keys, fetched from Keycloak's OIDC discovery
  document. A token not signed by the realm is rejected.
- **Issuer** (`ValidateIssuer`) — must equal the configured authority.
- **Audience** (`ValidateAudience`) — must be `dmrs-api`; tokens minted for another client are rejected.
- **Expiry** — built-in lifetime validation; an expired token yields 401.

Any failure short-circuits with **401 Unauthorized** before controller code executes.

## Token lifetimes

Access-token and refresh-token lifetimes are set in **Keycloak** (realm/client settings), not in DMRS.
The design intent:

- **Short-lived access tokens** limit the window a leaked token is useful.
- **Refresh tokens** let the Blazor client renew silently, so short access-token lifetimes don't harm UX.
- On **logout**, the client clears its tokens and redirects through Keycloak's end-session endpoint
  (`PostLogoutRedirectUri`).

For production, access-token lifetime should be tuned down (minutes, not hours) and refresh-token
rotation enabled in the realm.

## Authorization defense-in-depth

Security does not rely on the UI hiding buttons. Every protected operation is enforced **server-side**:

- The Blazor `<AuthorizeView>` checks are a UX convenience only.
- The API independently re-checks roles, SMART access level, and resource ownership on every call.

A caller who crafts a raw HTTP request still hits the full `FhirScope` / `CdsAdmin` pipeline. See
[`authorization.md`](authorization.md) for the per-request flow.

## Logging & audit trail

Two complementary mechanisms exist today:

- **Request logging** — Serilog (`UseSerilogRequestLogging`) records every HTTP request to daily
  rolling JSON files under `logs/` (`log-*.json`, 30-file retention, compact JSON format). This
  captures method, path, status, and timing for operational forensics.
- **Data-change audit** — the FHIR storage model retains **every version** of every resource
  (`FhirResourceVersions`) and uses soft deletes, so *what changed* is always reconstructable from the
  `_history` trail.

**Gap (deferred):** there is no dedicated **PHI-access audit log** that records *which user read which
patient's record*. The request log captures the route but not a structured "user X viewed patient Y"
event. A real clinical deployment must add this — it is listed as planned work in
[`deployment.md`](deployment.md).

## Secrets

| Secret | Today (dev) | Production (intended) |
|---|---|---|
| Database connection string | Plaintext in `appsettings.json` | Secrets manager / env vars |
| Keycloak admin credentials (`admin/admin`) | Plaintext in `appsettings.json` | Secrets manager / env vars |
| External-AI endpoint secrets | **Encrypted at rest** via ASP.NET Data Protection | Same, with a persisted/managed key ring |

The committed dev secrets are intentional for a one-command local demo and are safe only because the
stack is local and the data is synthetic.

## Development vs. production differences

| Area | Development (current) | Production (intended) |
|---|---|---|
| Transport security | HTTP allowed; `RequireHttpsMetadata = false` | TLS with real certificates; HTTPS-only; `RequireHttpsMetadata = true` |
| Direct Access Grants | Enabled for API testing | Disabled — Auth Code + PKCE only |
| Secrets | In `appsettings.json` | In a vault / environment |
| CORS | Hardcoded `https://localhost:7099`, `http://localhost:5155` | Real client origin(s) only |
| PHI-access audit | Not implemented | Structured access logging required |
| Data Protection keys | Ephemeral (local) | Persisted, protected key ring |

## Summary

Nabd is built with the right security primitives — central identity, validated JWTs, layered
role-and-compartment authorization, encrypted endpoint secrets, and a full data-change history. The
remaining gaps (TLS, externalized secrets, PHI-access auditing, DoS protection) are deliberate,
documented deferrals appropriate to a synthetic-data academic project, not oversights.

See also: [`authentication.md`](authentication.md) · [`authorization.md`](authorization.md) · [`deployment.md`](deployment.md)
