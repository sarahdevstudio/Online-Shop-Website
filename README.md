# Shop MVP

Small, production-capable e-commerce MVP. ASP.NET Core (.NET 10 LTS) API, SQL Server, Angular storefront and admin (later phases).
Architecture decisions: see the ADR kept with the project history (`docs/`).

## Layout

```
src/
  Domain/          entities, enums, permission registry (no framework dependencies)
  Application/     use-case contracts, DTOs, validators, Result type, ports (ICurrentUser, IPaymentGateway)
  Infrastructure/  EF Core + Identity, migrations, startup seeding, adapters (FakePaymentGateway)
  Api/             minimal API, cookie auth, authorization policies, ProblemDetails, OpenAPI, health, Serilog
storefront/       Next.js storefront (SSR, SEO); reads the catalog API server-side, see "Storefront" below
tests/
  UnitTests/       domain, validators, result type, fake gateway
  IntegrationTests/ real API + real SQL Server (throwaway database per run)
```

## Run locally

Requires the .NET 10 SDK and SQL Server (LocalDB is fine on Windows).

```bash
dotnet run --project src/Api
```

In `Development` the API uses LocalDB (`ShopMvp_Dev`, no credentials) and applies migrations on startup. It also seeds a sample lighting catalog (5 categories, 5 brands, 20 products) into an empty database; no other environment gets sample data.
OpenAPI document: `/openapi/v1.json` (Development/Testing only). Health: `/health/live`, `/health/ready`.

### Configuration and secrets

Production deployment (every variable, first-deploy steps, backups, reverse proxy, health checks, smoke checklist): **`docs/09-deployment.md`**.

Nothing secret is committed. Provide values with environment variables (use `__` for `:`) or user-secrets:

| Setting | Purpose |
|---|---|
| `ConnectionStrings__Default` | SQL Server connection (required outside Development) |
| `Seed__AdminUsername` (or `Seed__AdminEmail`), `Seed__AdminPassword` | Development/Testing only: an admin account created at startup if missing. **Production refuses to start with `Seed__AdminPassword` set**: use `create-admin` below |
| `Auth__SessionHours` | Lifetime of the API sign-in cookie (default 8, sliding). The admin panel adds its own 60 min idle / 8 h absolute limits |
| `Auth__AccountAttemptsPerMinute` | Sign-in attempts per account per minute before `429` (default 5); five wrong passwords also lock the account for 15 minutes |
| `Storefront__ApiKey`, `Storefront__TrustedProxies__N` | Shared secret of the Next.js server (required, >= 32 random characters, in Production) and the addresses allowed to forward the client IP |
| `Database__MigrateOnStartup` | `true` to migrate at startup (default `false` outside Development) |
| `AllowedHosts` | Real host name(s), `;`-separated. A bare `*` stops a Production start |
| `SITE_ENV` | `production` \| `staging` \| `development`: anything but `production` sends `X-Robots-Tag: noindex, nofollow` on every response |
| `Auth__RequireSecureCookie` | Defaults to `true`; only `Development` turns it off |
| `Cors__AllowedOrigins__0` | Allowed browser origins (none by default) |
| `RateLimiting__AuthPermitLimit`, `RateLimiting__WindowSeconds` | Login/register throttle |
| `Media__RootPath` | Folder for uploaded product images (default `media`, relative to the API content root; served under `/media`). Must be outside the application's binary folder (startup fails otherwise), so a published deployment needs an absolute path. Git-ignored: **include it in production backups** |
| `Media__MaxImageBytes` | Largest accepted image upload (default 5242880 = 5 MB); JPEG/PNG/WebP only, checked by content |

```bash
dotnet user-secrets --project src/Api set "Seed:AdminUsername" "admin"
dotnet user-secrets --project src/Api set "Seed:AdminPassword" "<choose a strong password>"
```

### Creating the first admin

The admin account is created from the command line, never through the web and never with the password on the command line:

```bash
dotnet Shop.Api.dll create-admin --username owner [--email owner@example.com]
```

The password is asked at a **hidden prompt** (twice). For scripted installs it can instead be provided in the `SHOP_ADMIN_PASSWORD` environment variable for that one command. A `--password` argument is refused on purpose (it would end up in shell history and process lists). At least 12 characters; repetitive, digits-only passwords and passwords containing the username are refused. The command applies the same startup work as the API (migrations when `Database__MigrateOnStartup` is on, roles, store settings), so run it with the production connection string (`ConnectionStrings__Default`). It refuses a username that already exists; change a password later in the admin panel (Account > Change password).

## Storefront

```bash
cd storefront
npm ci
npm run dev        # http://localhost:3000, needs the API running (dotnet run --project src/Api)
npm test           # vitest
npm run build      # does not need the API
```

The admin panel is at `/admin` (sign in with an account made by `create-admin`, see above). It is never indexed, uses a sealed `admin_session` cookie, and in production needs `ADMIN_SESSION_SECRET` (>= 32 random bytes); see `storefront/.env.example` and `docs/08-admin-design.md`.

Configuration (see `storefront/.env.example`; copy it to `.env.local` for development):

| Setting | Purpose |
|---|---|
| `API_BASE_URL` | **Server-only** base URL of the API, e.g. `http://localhost:5167` (no `/api/v1`). Never `NEXT_PUBLIC_*`: the browser never calls the API. Read at request time; required in production (`npm run dev` defaults to `http://localhost:5167`) |
| `NEXT_PUBLIC_SITE_URL` | Public origin of the storefront for canonical URLs, sitemap and JSON-LD |

Pages render on request (nothing is prerendered from the API at build time). API responses are cached for 60 s (products) / 300 s (categories, sitemap); only successful responses are cached. If the API is unreachable or answers 5xx, pages show an error page with HTTP 500 (an App Router page can't return another status), `Cache-Control: no-store` and `noindex`; only a real API 404 becomes a 404 page. Within the cache lifetime, pages already cached keep being served from cache during an API outage. Product images are served from the site's own origin through `/media/*`, which passes them through from the API.

## Database migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/Infrastructure --startup-project src/Infrastructure
```

`ConnectionStrings__Default` is honoured by the design-time factory; without it a credential-free LocalDB string is used.

## Tests

```bash
dotnet test Shop.slnx
```

Integration tests create and drop their own database. They use LocalDB by default; set `SHOP_TEST_SQL_CONNECTION` to use another SQL Server (the database name is ignored).

## API conventions

- Base path `/api/v1`, JSON, camelCase. Errors are RFC 7807 `application/problem+json` with `code` and `traceId`.
- Cookie auth (`shop_auth`, HttpOnly, SameSite=Lax, Secure outside Development).
- CSRF: every POST/PUT/PATCH/DELETE needs the `X-XSRF-TOKEN` header from `GET /api/v1/auth/csrf`. Tokens are bound to the current identity: fetch a new one after login/logout.
- Admin endpoints use permission policies named like the permission (`Settings.Manage`); permissions are defined in `Shop.Domain.Security.Permissions` and synced to roles at startup.
