# Deployment guide

Host-agnostic: the API is a .NET 10 app (Kestrel), the storefront is a Next.js 16 server, SQL Server holds the data. In front of both sits one reverse proxy that terminates HTTPS. Sections for **IIS** and **Linux/nginx** are at the end.

```
browser ──HTTPS──▶ reverse proxy ──▶ Next.js (storefront + /admin, port 3000)
                                         │  server-side only
                                         ▼
                                   .NET API (private, port 5167) ──▶ SQL Server
                                         └──▶ media/ folder (uploaded images)
```

The API is **never exposed to the internet**: only the Next.js server calls it (with a shared key). The reverse proxy forwards only to Next.js.

## 1. Configuration reference

Environment variables (use `__` for `:` in .NET keys). "Secret" = keep out of source control and logs.

### API (`Shop.Api`)

| Key | Required | Example | Secret | Notes |
|---|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` | no | Production turns on the startup checks below, HTTPS redirection and HSTS. |
| `ConnectionStrings__Default` | yes | `Server=sql01;Database=Shop;User Id=shop_app;Password=…;Encrypt=True` | **yes** | A SQL login with rights on that database only. |
| `AllowedHosts` | **yes** | `api.internal;127.0.0.1` | no | The Host names the API answers to. A bare `*` (or empty) **stops the start in Production**. The API is called by Next.js, so list the name Next uses (`API_BASE_URL` host). |
| `Storefront__ApiKey` | **yes** | 32+ random characters | **yes** | Shared with the storefront (`STOREFRONT_API_KEY`). Missing, shorter than 32 or placeholder-like (`dev-…`, `test-…`, `change-me`…) **stops the start**. Generate: `openssl rand -base64 48`. |
| `Storefront__TrustedProxies__0` (`__1`…) | yes (if Next.js is not on the same machine) | `10.0.0.12` or `10.0.0.0/24` | no | Addresses (or CIDR networks) allowed to tell the API the visitor's IP via `X-Forwarded-For`. Default = loopback only. Must be the address the API **sees** the storefront connect from. |
| `SITE_ENV` | recommended | `production` | no | `production` \| `staging` \| `development`. Anything but `production` makes **every API response** carry `X-Robots-Tag: noindex, nofollow`. Unset = derived from `ASPNETCORE_ENVIRONMENT` (Production → production). The API's `/robots.txt` always disallows everything. |
| `Database__MigrateOnStartup` | first deploy | `true` | no | Applies EF migrations at start. Default `false` outside Development. Turn on for the first start / upgrades, then off (or keep on; it is idempotent). |
| `Seed__AdminUsername` / `Seed__AdminPassword` | **never in Production** | – | yes | Development/testing only. **A set `Seed__AdminPassword` stops a Production start**; use `create-admin` (below). |
| `Auth__RequireSecureCookie` | no | `true` (default) | no | Keep `true` (the cookie is only sent over HTTPS). |
| `Auth__SessionHours` | no | `8` (default) | no | Lifetime of the API sign-in cookie. |
| `Auth__AccountAttemptsPerMinute` | no | `5` (default) | no | Sign-in attempts per account per minute; 5 wrong passwords also lock the account for 15 minutes. |
| `RateLimiting__AuthPermitLimit`, `RateLimiting__WindowSeconds` | no | `10`, `60` | no | Login/register/change-password attempts per IP per window. |
| `Checkout__OrderPermitLimit`, `Checkout__OrderWindowMinutes` | no | `5`, `10` | no | Orders one IP may place per window. |
| `Checkout__MaxUnpaidOrdersPerMobile`, `Checkout__MaxUnpaidOrdersPerIp` | no | `2`, `2` | no | Unpaid (AwaitingPayment/Pending) orders allowed at once. |
| `Checkout__MaxCartLines` | no | `50` | no | |
| `Media__RootPath` | **yes** | `/var/lib/shop/media` or `D:\shop\media` | no | Folder for uploaded product images (served under `/media`). **Absolute path outside the application folder** (startup fails otherwise) and **part of the backup**. |
| `Media__MaxImageBytes` | no | `5242880` | no | Largest image upload (5 MB). JPEG/PNG/WebP only, checked by content. |
| `Cors__AllowedOrigins__0` | no | – | no | Leave empty: browsers never call the API. |
| `StoreDefaults__*` | first start only | `StoreDefaults__ShippingFlatFee=50000` | no | First values of the store settings (shipping fee/threshold, payment switches, card, quantity limit, contact info). Written **only if the settings row does not exist**; afterwards edit them in the admin panel. `CardToCardEnabled` defaults to `false` until a card is entered. |
| `SHOP_ADMIN_PASSWORD` | only for `create-admin` | – | **yes** | Alternative to the hidden prompt; set for that one command only. |

### Storefront (Next.js)

| Variable | Required | Example | Secret | Notes |
|---|---|---|---|---|
| `SITE_ENV` | **yes** | `production` | no | `production` \| `staging` \| `development`. **Only `production` is indexable.** Unset or a typo = not indexable (safe default); the start logs a warning. Read per request, so one build serves staging and production. |
| `NEXT_PUBLIC_SITE_URL` | **yes** when `SITE_ENV=production` | `https://shop.example.ir` | no | Public https origin, no trailing slash: canonical URLs, sitemap, JSON-LD. Inlined **at build time**, so set it for `npm run build` too. A missing/`localhost`/`http` value **stops build and start**. |
| `API_BASE_URL` | **yes** when `SITE_ENV=production` | `http://127.0.0.1:5167` | no | Private URL of the API, no `/api/v1`. Server-only (never sent to browsers). |
| `STOREFRONT_API_KEY` | yes | same value as `Storefront__ApiKey` | **yes** | Sent as `X-Storefront-Key` on cart/checkout/order calls. |
| `TRUSTED_PROXY_HOPS` | yes in production | `1` | no | How many reverse proxies are in front of Next.js. The visitor IP is taken from `X-Forwarded-For` that many places from the right; with `0` it is ignored (rate limits then share one IP, so production refuses order calls). |
| `ADMIN_SESSION_SECRET` | **yes** in production | 48 random bytes (base64) | **yes** | Seals the admin session cookie. Missing, < 32 bytes or placeholder-like **stops the start**. Generate: `openssl rand -base64 48`. |
| `ADMIN_SESSION_IDLE_MINUTES` / `ADMIN_SESSION_ABSOLUTE_HOURS` | no | `60` / `8` | no | Admin idle and absolute session limits. |
| `NODE_ENV` | set by `next start` | `production` | no | |
| `PORT`, `HOSTNAME` | no | `3000`, `127.0.0.1` | no | Bind Next.js to loopback; only the proxy should reach it. |

The storefront and the API must agree on one thing: `STOREFRONT_API_KEY` = `Storefront__ApiKey`.

## 2. First deploy

1. **Database.** Create an empty SQL Server database and a SQL login for the app.
2. **Build.** API: `dotnet publish src/Api -c Release -o /srv/shop/api`. Storefront: `cd storefront && npm ci && npm run build` with `SITE_ENV`, `NEXT_PUBLIC_SITE_URL` and `API_BASE_URL` set (the build checks them when `SITE_ENV=production`; building with `SITE_ENV=staging` first is fine). The build does not need the API.
3. **API environment** from section 1 (`ASPNETCORE_ENVIRONMENT=Production`, connection string, `AllowedHosts`, `Storefront__ApiKey`, `Storefront__TrustedProxies__0`, `Media__RootPath`, `Database__MigrateOnStartup=true`). Create the media folder and give the service account write access.
4. **First admin** (before or after the first start; both apply the migrations when `Database__MigrateOnStartup=true`):
   ```bash
   dotnet /srv/shop/api/Shop.Api.dll create-admin --username owner [--email owner@example.ir]
   ```
   The password is typed at a hidden prompt (twice), or comes from `SHOP_ADMIN_PASSWORD`; never as an argument. At least 12 characters. The command also creates the roles, the store-settings row and the five draft info pages.
5. **Start the API**, then the storefront (`npm run start`, or your process manager). Check `GET /health/ready` on the API.
6. **Sign in** at `https://<site>/admin` and set up the shop:
   - **Settings:** shipping fee and free-shipping threshold, **card number / holder / bank** (asks for your password), enable the payment methods, contact info (phone, address, hours, Instagram), max quantity per line. Card-to-card is only offered to customers once a valid card is saved.
   - **Content pages** (`/admin/content`): replace every `[placeholder]` in the five drafts and delete the line `[پیش‌نویس — بازبینی شود]`: about, contact, terms, returns, privacy. The pages are indexable, so do this **before** switching `SITE_ENV` to `production`.
   - **Catalog:** categories, brands, products with variants, stock, attributes and images.
   - **Change the admin password** if it was ever passed through an environment variable.
7. **Go live:** deploy first with `SITE_ENV=staging` (not indexable), run the smoke checklist (section 6), then set `SITE_ENV=production` on the storefront (and API) and restart.
8. Optionally set `Database__MigrateOnStartup=false` afterwards and apply future migrations deliberately.

## 3. Backup and restore

Back up **both**, together, from the same moment:

- **The SQL Server database** (full backup daily, log backups if you need point-in-time recovery).
- **The media folder** (`Media__RootPath`): uploaded product images live only there and are referenced from the database. A database restore without the matching media folder leaves broken images.

Keep copies off the server. **Test a restore** (database + media into a scratch environment, then browse a product page) before launch and after changes to the setup. Not covered by the app: refunds of paid orders are manual (the admin panel warns when cancelling a paid order).

## 4. Reverse proxy requirements

- **Forwarded headers.** The proxy must set `X-Forwarded-For` (client address) and keep the original `Host`. Use the **append** form (`$proxy_add_x_forwarded_for`) or overwrite with the client address; either works with `TRUSTED_PROXY_HOPS=1` because the storefront only trusts the entry the proxy added. If a CDN sits in front of the proxy, set `TRUSTED_PROXY_HOPS=2`.
- **API sees the storefront**, not the visitor: list the storefront server's address in `Storefront__TrustedProxies__0` so the visitor IP forwarded by Next.js is honoured by the API's rate limits. A forwarded address from any other peer is ignored.
- **Request body size ≥ 6 MB** for the image upload (5 MB image + multipart overhead): nginx `client_max_body_size 6m;`, IIS `maxAllowedContentLength="6291456"` (bytes) or more. The upload goes browser → Next.js route handler (streamed) → API.
- **Do not expose the API.** Route only the public host to Next.js. `/media/*` is served by Next.js (it proxies images from the API).
- **TLS** terminates at the proxy; Next.js sends `Strict-Transport-Security` in production. Redirect HTTP to HTTPS at the proxy.
- **Server Actions** check the request `Origin` against the `Host`: forward the original `Host` (or `X-Forwarded-Host`) unchanged, otherwise cart, checkout and admin forms are refused as "Invalid Server Actions request".
- Do not cache dynamic pages at the proxy (cart, checkout, order, admin); Next.js already sends `Cache-Control: no-store` where it matters.

## 5. Health checks

| Check | URL | Healthy |
|---|---|---|
| API alive | `GET http://127.0.0.1:5167/health/live` | `200` |
| API ready (database reachable) | `GET http://127.0.0.1:5167/health/ready` | `200` |
| Storefront alive | `GET http://127.0.0.1:3000/robots.txt` | `200` (does not need the API) |
| Storefront + API | `GET https://<site>/` | `200` with products |

Also monitor: free disk on the media volume, database backup success, and the API log for `ERR` lines (request lines show the real status, a `409` for a stale edit is normal).

## 6. Post-deploy smoke checklist

- [ ] `https://<site>/` loads over HTTPS; HTTP redirects to HTTPS.
- [ ] Response headers on `/`: `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`. On `/order/…`: `Referrer-Policy: no-referrer`.
- [ ] **Production:** `/robots.txt` allows crawling (and lists the sitemap) and there is **no** `X-Robots-Tag` header on `/`. **Staging:** `/robots.txt` is `Disallow: /` and every response has `X-Robots-Tag: noindex, nofollow`.
- [ ] `/sitemap.xml` lists categories, products and the five info pages with the real domain (not `localhost`).
- [ ] Product page → add to cart → checkout (cash on delivery) → order page. Card-to-card shows the card, holder and exact amount. Stock dropped by the ordered quantity.
- [ ] `/admin` redirects to the login; sign in; the dashboard counts the test order; confirm → ship → deliver; cancel a second test order and check the stock returned.
- [ ] Upload a product image of about 4.9 MB (works); a file over 5 MB is rejected with a clear message.
- [ ] Settings: change the shipping fee (no password), then the card number with a wrong password (refused) and the right one (accepted); the audit log shows both.
- [ ] Sign out; the previous session cookie no longer works.
- [ ] The API is **not** reachable from outside (`curl https://<site>:5167/…` and the API host/port from the internet fail).
- [ ] Info pages show no `[placeholder]` or draft marker; the contact page shows the real phone, address and hours.
- [ ] Backup ran once and a restore was tested.
- [ ] Delete the test orders (or keep them marked) and set stock back to the real values.

## 7. IIS (Windows)

- **API:** `dotnet publish -c Release`, install the **ASP.NET Core Hosting Bundle (.NET 10)**, create a site/app pool with *No Managed Code*, point it at the publish folder. Environment variables go in the app pool (or `web.config` `<environmentVariables>` inside `<aspNetCore>`); do not put secrets in a committed `web.config`. Bind it to an internal address (or a non-public port) only.
- **Storefront:** run `node node_modules/next/dist/bin/next start -p 3000 -H 127.0.0.1` as a **Windows service** (for example with NSSM) with the variables from section 1 set on the service.
- **Public site:** an IIS site with **URL Rewrite + Application Request Routing (ARR)** forwarding everything to `http://127.0.0.1:3000`; enable ARR proxy, keep *Preserve host header*, set `X-Forwarded-For` (ARR adds it). Limit request size:
  ```xml
  <system.webServer><security><requestFiltering><requestLimits maxAllowedContentLength="6291456" /></requestFiltering></security></system.webServer>
  ```
  and allow the same in ARR (`proxy` → *Response buffer threshold* default is fine). Terminate TLS in IIS (bind the certificate) and redirect HTTP → HTTPS.
- `TRUSTED_PROXY_HOPS=1`; `Storefront__TrustedProxies__0=127.0.0.1` when the storefront and API run on the same server.

## 8. Linux with nginx and systemd

`/etc/systemd/system/shop-api.service`:
```ini
[Service]
WorkingDirectory=/srv/shop/api
ExecStart=/usr/bin/dotnet /srv/shop/api/Shop.Api.dll --urls http://127.0.0.1:5167
EnvironmentFile=/etc/shop/api.env      # chmod 600; ASPNETCORE_ENVIRONMENT=Production, ConnectionStrings__Default=…, …
User=shop
Restart=always
```
`/etc/systemd/system/shop-web.service`:
```ini
[Service]
WorkingDirectory=/srv/shop/storefront
ExecStart=/usr/bin/npm run start -- -p 3000 -H 127.0.0.1
EnvironmentFile=/etc/shop/web.env      # chmod 600; SITE_ENV=production, NEXT_PUBLIC_SITE_URL=…, …
User=shop
Restart=always
```
nginx (`server` block for HTTPS; add a plain `listen 80` block that redirects):
```nginx
server {
  listen 443 ssl http2;
  server_name shop.example.ir;
  # ssl_certificate / ssl_certificate_key …

  client_max_body_size 6m;                      # image uploads (5 MB + overhead)

  location / {
    proxy_pass http://127.0.0.1:3000;
    proxy_http_version 1.1;
    proxy_set_header Host              $host;
    proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_request_buffering off;                # stream uploads
  }
}
```
Set `TRUSTED_PROXY_HOPS=1`. Run `create-admin` as the `shop` user with the same environment file loaded.

## 9. Upgrades

1. Back up the database and media folder.
2. Deploy the new API build and apply migrations (`Database__MigrateOnStartup=true` for that start). Migrations only add; they never touch your edited settings or content pages.
3. Build and deploy the storefront (set the three build-time variables again).
4. Run the smoke checklist.
