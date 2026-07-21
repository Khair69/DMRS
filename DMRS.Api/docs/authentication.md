# Authentication in Nabd

This document describes how Nabd establishes **who a caller is**. Authentication produces a validated
identity (a JWT); deciding what that identity may *do* is covered separately in
[`authorization.md`](authorization.md).

## Overview

Nabd delegates all credential handling to **Keycloak**, an OAuth2 / OpenID Connect (OIDC) identity
provider. No passwords are ever stored or verified by Nabd itself. The flow is:

```
Browser (Blazor WASM)  ──login──▶  Keycloak (realm "DMRS")  ──issues──▶  JWT access token
        │                                                                     │
        └──────────────── calls API with  Authorization: Bearer <JWT> ───────▶  DMRS.Api
                                                                                  │
                                                            validates signature, issuer, audience
```

- **Realm:** `DMRS`
- **API audience / client id:** `dmrs-api`
- **Authority (issuer):** `http://localhost:8080/realms/DMRS` (dev)

These values live in `appsettings.json` under `Keycloak` (API) and `wwwroot/appsettings.json` (client).

## OAuth2 / OpenID Connect

OpenID Connect is the identity layer on top of OAuth2. Nabd uses two of its components:

- **Access token** — a signed JWT the client attaches to every API call. It carries the caller's
  identity, realm `roles`, and SMART `scope`s. This is the only token the API inspects.
- **ID token** — proves the login to the client (Blazor) and is used to populate the UI's
  authentication state. The API does not consume it.

Refresh tokens are managed by the Blazor WebAssembly authentication library, which silently renews the
access token before it expires.

## How the client authenticates (Blazor WASM)

Configured in [`DMRS.Client/Program.cs`](../../DMRS.Client/Program.cs) via `AddOidcAuthentication`:

- **Flow:** Authorization Code with **PKCE** (`ResponseType = "code"`). PKCE is the standard for public
  clients (a WASM app can't keep a client secret), so the code exchange is protected without one.
- **Roles:** `RoleClaim = "roles"` so realm roles map into the client's `ClaimsPrincipal`.
- **Token attachment:** an `AuthorizationMessageHandler` attaches the bearer token **only** to requests
  bound for the configured API base URL (`authorizedUrls`), so tokens never leak to other hosts.
- A `KeycloakClaimsPrincipalFactory` shapes the incoming claims (e.g. normalizing the `roles` claim)
  into the principal the UI authorizes against.

## How the API validates tokens (DMRS.Api)

Configured in [`DMRS.Api/Program.cs`](../Program.cs) via `AddJwtBearer`:

- **Authority** = the Keycloak realm URL; the API fetches the realm's signing keys from its OIDC
  discovery document and validates the JWT signature against them.
- **`ValidateIssuer = true`** — the token's `iss` must match the configured authority.
- **`ValidateAudience = true`** with `Audience = "dmrs-api"` — the token must be intended for this API.
- **`RoleClaimType = "roles"`**, **`NameClaimType = "preferred_username"`** — so `User.IsInRole(...)`
  and `User.Identity.Name` work as expected.
- The default inbound claim-type mapping is **cleared** at startup
  (`JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()`) so claims keep their original JWT names
  (`sub`, `scope`, `roles`) instead of being rewritten to long Microsoft URIs — the authorization code
  relies on the raw names.

A request with a missing, expired, or improperly-signed token is rejected with **401 Unauthorized**
before any controller runs.

## Token claims Nabd relies on

| Claim | Used for |
|---|---|
| `roles` | Role-based access (`ROLE_SYSTEM_ADMIN`, `ROLE_ORG_ADMIN`, `ROLE_PRACTITIONER`, `ROLE_PATIENT`). |
| `scope` / `scp` | SMART access levels (`system/…`, `user/…`, `patient/…`). |
| `sub` | Keycloak user id — used to link an account to its FHIR `Patient`/`Practitioner` record. |
| `preferred_username` | Display name (`User.Identity.Name`). |
| `patient` / `practitioner` / `organization` / `fhirUser` | Compartment resolution (see authorization). |

## Login modes: why Direct Grant is DEV-only

Keycloak can issue tokens two ways, and Nabd uses different ones per environment:

- **Authorization Code + PKCE (the real flow).** The browser is redirected to Keycloak's login page;
  the user authenticates *there* and is redirected back with an authorization code that's exchanged for
  tokens. The application never sees the password. **This is the flow the Blazor client uses, and the
  only one suitable for production.**
- **Direct Access Grants (Resource Owner Password Credentials).** The client sends a username/password
  straight to Keycloak's token endpoint. This is convenient for scripted/manual API testing (e.g. the
  `.http` files) but **it puts the password in the hands of the client**, which defeats the point of a
  central identity provider. **Direct Access Grants are enabled only in development** and must be
  disabled on the `dmrs-api` client before any real deployment.

## Development vs. production

| Aspect | Development (current) | Production (intended) |
|---|---|---|
| Transport | `RequireHttpsMetadata = false`, HTTP allowed to Keycloak | TLS everywhere; `RequireHttpsMetadata = true` |
| Login flow | Auth Code + PKCE for the app; Direct Grant enabled for testing | Auth Code + PKCE only; Direct Grant disabled |
| Keycloak admin | `admin/admin` in `appsettings.json` | Secrets in a vault / env vars |
| Realm config | Configured by hand | Committed realm/client export for reproducibility |

See [`deployment.md`](deployment.md) for the full production-hardening checklist and
[`security.md`](security.md) for the threat model.
