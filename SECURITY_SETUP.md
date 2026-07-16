# Security and operations setup

## Where secrets live

`InteriorDesignWeb/appsettings.json` contains only non-secret defaults. Local
development secrets are stored by .NET User Secrets under the project
`UserSecretsId`; they are outside this repository and are loaded when
`ASPNETCORE_ENVIRONMENT=Development`.

Set or replace a local value with:

```powershell
dotnet user-secrets set "JwtSettings:Secret" "<new-random-secret>" --project InteriorDesignWeb/InteriorDesignWeb.csproj
```

Production must use environment variables or the deployment platform's secret
manager. Nested configuration keys use double underscores, for example:

```text
ConnectionStrings__DesignDB
JwtSettings__Secret
COS__SecretId
COS__SecretKey
ComfyUI__AccountApiKey
Assistant__ApiKey
```

The AI design assistant also requires server-side configuration. For an
OpenAI-compatible chat endpoint set `Assistant__Enabled=true`,
`Assistant__BaseUrl`, `Assistant__Model`, and `Assistant__ApiKey`. The API key
must never be returned to the browser or stored in assistant messages.

For MiniMax's OpenAI-compatible endpoint keep
`Assistant__ResponseFormatMode=auto`. The client will not send the unsupported
generic `json_object` mode to MiniMax M2 models; it removes thinking tags,
attempts one isolated JSON repair request, and finally falls back to
display-only natural language that cannot create an executable action. Keep
`Assistant__RepairInvalidStructuredOutput=true` and
`Assistant__AllowNaturalLanguageFallback=true` unless diagnosing a provider.

Never copy production values into an appsettings file, log, archive, screenshot,
or chat message. User Secrets prevents new commits; it does not revoke a value
that has already been exposed. Rotate exposed database, JWT, COS, and ComfyUI
credentials at their issuing systems.

## User creation and the first administrator

Public registration is disabled. User creation is available only through the
administrator API and `/app/admin`. To bootstrap a database that has no
administrator, promote exactly one existing trusted account directly:

```sql
UPDATE users
SET Role = 'Administrator'
WHERE Username = '<trusted-existing-username>';
```

After that, sign in as that administrator and create users at `/app/admin`.
Do not expose a temporary anonymous bootstrap endpoint.

New passwords must contain at least 10 characters. Login attempts are limited
to five per IP per minute and unknown users receive the same response and
password-hash work as wrong passwords. Disabling an account, changing its role,
or resetting its password revokes refresh sessions and increments `AuthVersion`,
so previously issued access tokens fail validation immediately.

## JWT behavior

The access JWT is returned only as a `HttpOnly`, `SameSite=Strict` cookie.
Frontend JavaScript cannot read it. HTTPS deployments also mark it `Secure`.
The access JWT lasts at most two hours. A separate random refresh credential
keeps the user signed in for 14 days and renews that 14-day window whenever it
is successfully used. Only its SHA-256 hash is stored in the database, and it
is rotated on every refresh. Logout revokes the current refresh session and
removes both cookies. The administrator can revoke one session or all sessions
for a user from `/app/admin`; access-token validation also checks the current
account status, role, and `AuthVersion` against the database.

State-changing `/api/admin` requests require the custom
`X-DesignHub-Admin: 1` header in addition to authentication. The frontend adds
this automatically. Keep CORS restricted to trusted origins and never remove
the backend role authorization because the sidebar guard is only a UX feature.

## AI jobs and assets

AI job completion is refreshed by a database-backed hosted worker. A server
restart does not lose queued jobs. The current ComfyUI integration is polling
based because the configured provider does not currently offer a verified,
signed callback contract.

If a callback is added later, require an HMAC signature, timestamp and replay
nonce, keep result persistence idempotent, and retain the worker as recovery for
missed callbacks.

Deleted task entries do not delete images used by a project or project cover.
Unreferenced images enter a seven-day cleanup delay, are checked again, and only
then are removed from COS and the database.

## Operations

- `/health/live` checks whether the process can answer requests.
- `/health/ready` also verifies database connectivity.
- Persist Data Protection keys outside the publish directory with
  `DataProtection__KeysPath`; restrict the directory to the service account.
- Set `AllowedHosts` to the production domain and configure only the actual
  reverse proxy IP under `ReverseProxy__KnownProxies`.
- Swagger is available only in Development.
- Production logs are JSON and include method, path, status, duration and a
  request ID. They never intentionally log Cookie or Authorization values.
- Release publishing disables the application PDB so stack traces in production
  logs do not include the developer workstation source path. Method names remain
  available for diagnosis. Never run the server with `ASPNETCORE_ENVIRONMENT=Development`.

## AI governance

The immutable assistant safety policy remains compiled into the backend. It
treats conversation history, design briefs, prior model output and user claims
as untrusted data. Administrators can version and publish only the business
policy through `/app/admin`; business policy cannot grant tools or bypass
backend authorization.

Role and user AI policies control assistant chat, generation proposals, actual
AI generation, automatic project attachment, workflow allowlists and maximum
concurrent jobs. User overrides may expire and all administrator changes are
audited. Provider API keys remain environment-only; the management API returns
only whether a key is configured.
