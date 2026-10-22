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
storefront/       Next.js storefront (SSR/SSG, SEO); `npm ci && npm run dev`, set NEXT_PUBLIC_SITE_URL for canonical URLs
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

Nothing secret is committed. Provide values with environment variables (use `__` for `:`) or user-secrets:

| Setting | Purpose |
|---|---|
| `ConnectionStrings__Default` | SQL Server connection (required outside Development) |
| `Seed__AdminEmail`, `Seed__AdminPassword` | Optional first admin account, created at startup if missing |
| `Database__MigrateOnStartup` | `true` to migrate at startup (default `false` outside Development) |
| `Auth__RequireSecureCookie` | Defaults to `true`; only `Development` turns it off |
| `Cors__AllowedOrigins__0` | Allowed browser origins (none by default) |
| `RateLimiting__AuthPermitLimit`, `RateLimiting__WindowSeconds` | Login/register throttle |
| `Media__RootPath` | Folder for uploaded product images (default `media`, relative to the API content root; served under `/media`). Must be outside the application's binary folder (startup fails otherwise), so a published deployment needs an absolute path. Git-ignored: **include it in production backups** |
| `Media__MaxImageBytes` | Largest accepted image upload (default 5242880 = 5 MB); JPEG/PNG/WebP only, checked by content |

```bash
dotnet user-secrets --project src/Api set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets --project src/Api set "Seed:AdminPassword" "<choose a strong password>"
```

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
