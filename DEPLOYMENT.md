# Production deployment

The production artifact is a single ASP.NET Core publish directory. Vite builds
the frontend into `wwwroot/dist`, and `dotnet publish` includes that directory
automatically.

## Build

From the repository root:

```powershell
dotnet publish InteriorDesignWeb/InteriorDesignWeb.csproj -c Release -o publish
```

The project sets `PublishRelease=true`, disables application PDB generation for
Release, and rejects a publish directory containing `InteriorDesignWeb.pdb`.
Production exception responses are sanitized; server logs still retain method
names and request IDs for diagnosis but no longer need local source file paths.

The publish target runs `npm run build`. If `node_modules` is absent it first
runs `npm ci`. The resulting directory must contain both:

```text
publish/InteriorDesignWeb.dll
publish/wwwroot/dist/index.html
```

Use `-p:SkipFrontendBuild=true` only when a trusted CI step already generated
`wwwroot/dist` before `dotnet publish`.

## Runtime configuration

Set `ASPNETCORE_ENVIRONMENT=Production` and provide secrets through environment
variables or the hosting platform's secret manager. Required secret names are
listed in `SECURITY_SETUP.md`.

## Database migrations

For a database whose historical SQL upgrades have already been applied but
which does not yet contain `schema_migrations`, register the existing state once:

```powershell
$env:ConnectionStrings__DesignDB = '<production connection string>'
./scripts/invoke-database-migrations.ps1 -BaselineExisting
```

For every later deployment, run without `-BaselineExisting` before restarting
the website:

```powershell
./scripts/invoke-database-migrations.ps1
```

The runner verifies SHA-256 checksums, skips applied files and stops if an
already-applied SQL file was modified. Never use `-BaselineExisting` for a new
pending migration.

The administrator backend requires `20260712_admin_management.sql`. After the
migration and application restart, administrators use `/app/admin` on the same
HTTPS origin and port as the main website. Do not expose a second administrator
port. Verify that a normal user receives 403 from `/api/admin/overview` before
opening production access.

Gallery lifecycle management additionally requires
`20260712_gallery_management.sql`. Apply it before deploying code that reads
`images.IsDeleted`; otherwise gallery queries will fail because the new columns
do not exist. Back up the database before running production migrations.

The AI design assistant requires `20260712_assistant_phase1.sql` and these
server-side settings: `Assistant__Enabled`, `Assistant__BaseUrl`,
`Assistant__Model`, and `Assistant__ApiKey`. Keep the assistant disabled until
the migration is applied and the model endpoint has been tested. Restart the
ASP.NET Core service after changing these environment variables.

MiniMax OpenAI-compatible deployments should use:

```text
Assistant__ResponseFormatMode=auto
Assistant__RepairInvalidStructuredOutput=true
Assistant__AllowNaturalLanguageFallback=true
```

This avoids sending MiniMax M2 models the generic `json_object` mode and keeps
provider-specific structured-output repair inside the backend.

AI usage enforcement requires `20260713_usage_quotas.sql`. Agent policy
versioning and role/user AI permissions require `20260714_ai_governance.sql`.
Apply both before starting a backend version that reads these tables. The AI
governance page is a protected tab inside `/app/admin`; no additional port is
required.

Start the published application from the publish directory:

```powershell
dotnet InteriorDesignWeb.dll
```

Deploy into a new, empty release directory and switch the service to it instead
of copying over an old publish directory. An overwrite can leave stale files
that are no longer produced, especially an older `InteriorDesignWeb.pdb` or
frontend asset. If an in-place deployment is unavoidable, remove the old
application directory first while the service is stopped, then copy the complete
validated publish package. Keep environment files and Data Protection keys
outside the release directory.

The same origin serves both the website and API:

```text
https://example.com/                 frontend
https://example.com/app/projects    frontend route with SPA fallback
https://example.com/api/...         backend API
https://example.com/health/live     process health
https://example.com/health/ready    database readiness
```

Unknown `/api`, `/health`, and `/swagger` paths return 404 and never fall back
to the frontend HTML.

Validate a publish directory before uploading it:

```powershell
./scripts/test-publish-package.ps1 -PublishPath ./publish
```

## Reverse proxy

Terminate HTTPS at IIS, Nginx, or another trusted reverse proxy and forward to
the ASP.NET Core process. Do not expose the development Vite server in
production. Restart the application after changing environment variables.

For a same-host Nginx deployment, bind Kestrel only to loopback and set the
public host explicitly:

```text
ASPNETCORE_URLS=http://127.0.0.1:5000
ASPNETCORE_ENVIRONMENT=Production
AllowedHosts=example.com
ReverseProxy__KnownProxies__0=127.0.0.1
DataProtection__KeysPath=/var/lib/interiordesign/keys
```

Create the Data Protection directory once and grant it only to the systemd
service account. These keys keep protected search cursors valid across process
restarts; do not publish the directory with the application or expose it over
HTTP.

Nginx must forward the original protocol and client address:

```nginx
location / {
    proxy_pass http://127.0.0.1:5000;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}

client_max_body_size 55m;
```

The application trusts loopback proxies by default. If the reverse proxy is on
another server, add only that proxy's fixed IP through additional
`ReverseProxy__KnownProxies__N` values. Never trust every forwarded address.
Production enables HSTS and response compression. A missing
`wwwroot/dist/index.html` now stops startup with a clear error instead of
silently serving an API-only website.
