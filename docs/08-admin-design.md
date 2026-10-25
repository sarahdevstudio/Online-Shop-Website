# Admin panel design (M-8) — proposal

Status: **approved 2026-10-25** with the changes in "Approved changes" at the end (they supersede the text they touch). Items marked **(new)** need small additions to the API; everything else uses endpoints that already exist (`/api/v1/admin/*`, `Catalog.Manage` / `Orders.Manage` / `Settings.Manage`). Admin pages live in the Next.js storefront under `/admin`; the API stays private.

## 1. Login and session

- **Backend-for-frontend.** The browser never holds the API's cookies. Next logs in server-to-server (`GET /auth/csrf`, `POST /auth/login`), reads `GET /auth/me` and **refuses anyone without the Admin role** (logging them out again), then stores the API's `shop_auth` cookie value inside its own cookie **`admin_session`**: AES-GCM sealed with `ADMIN_SESSION_SECRET` (≥ 32 random bytes, required in production), `httpOnly`, `Secure`, `SameSite=Strict`, `Path=/admin`, no `Domain`. The sealed value also carries `issuedAt` / `lastSeen`; no server-side session table is needed.
- **Every admin request** (page, action, route handler) first calls `requireAdmin()`: unseal → check expiry → else redirect to `/admin/login`. This runs in each handler, not only in a proxy, so no route can forget it.
- **CSRF without weakening the API.** The API's antiforgery filter stays exactly as is. For each write Next fetches a fresh token with the admin's auth cookie (`GET /auth/csrf`, tokens are identity-bound), then sends the `shop_csrf` cookie + `X-XSRF-TOKEN` header together with `shop_auth`. Browser → Next is protected by Server Actions' Origin/Host check, `SameSite=Strict`, and an explicit Origin check in any route handler.
- **Lifetime.** Idle timeout **60 min**, absolute **12 h**, enforced in Next on every request (`lastSeen` is refreshed on use). **(new)** the API cookie becomes configurable (`Auth:SessionHours`, default 8, was a fixed 7 days) so a copied cookie can't outlive that either; the security-stamp check interval drops to 1 min.
- **Logout.** `POST /auth/logout` + **(new)** rotate the user's security stamp, so any copy of the cookie dies within a minute; Next deletes `admin_session`. A "change password" page does the same rotation **(new `POST /auth/change-password`)**.
- **Brute force.** Existing: 10 login attempts / min per IP (the real client IP, forwarded only by the trusted storefront) and Identity lockout after 5 wrong passwords for 15 min. Added in Next: 5 attempts / min per IP + email (in memory), identical error text for wrong password / unknown email / locked account / non-admin, no user-enumeration. No CAPTCHA, no 2FA in the MVP (2FA is a Post-MVP item).
- **First admin in production (new).** `dotnet Shop.Api.dll create-admin --email <e>` prompts for the password on the console (≥ 12 chars, not stored in env or shell history), creates the Admin user and exits. The `Seed:AdminEmail/AdminPassword` environment seeding stays for Development/Testing only; in Production the API logs a warning (or refuses to start) if `Seed:AdminPassword` is set.

## 2. Pages (all Persian RTL, JavaScript allowed)

| Page | Contents |
|---|---|
| `/admin` dashboard | Counts that link to the filtered order list: new orders (Pending + AwaitingPayment), card orders **with a transfer reference** waiting for confirmation, Confirmed waiting to ship, today's orders; variants low on stock (≤ 3). **(new)** `GET /admin/dashboard`. |
| `/admin/orders` | Table with filters (status, payment status, search by number / mobile / name), paging. Detail: lines, customer and address, the order's own card snapshot, transfer reference (highlighted), status log with who. Actions: **confirm payment** (card, AwaitingPayment), **next status** (only the valid next one), **cancel**. Cancelling shows a confirmation dialog saying stock returns; for a **paid** order it adds a warning that the refund is manual and needs a ticked acknowledgement. |
| `/admin/products` | List (search, status, paging) · create (category, brand, name, slug — immutable, shown with that warning — description, status, first variant) · edit with sections: general, **variants** (SKU, price, compare-at, active; stock as "+/- adjust" or "set"), **attributes** (option pickers; per-variant axes), **images** (upload, reorder, alt text, delete). Archive instead of delete. |
| `/admin/categories`, `/admin/brands` | Inline create / edit / activate; delete only when empty / unused (the API already refuses otherwise and says why). Categories max 2 levels. |
| `/admin/settings` | The store settings form (shipping, payment methods, card, quantity limit, contact), a banner showing whether card-to-card is currently **offered to customers**, and the **audit log** (time in Jalali, admin e-mail, field, old → new; card numbers already masked). |
| `/admin/login`, `/admin/account` | Login; change password; logout. |

Image upload goes browser → Next (Server Action, `serverActions.bodySizeLimit` raised to the API's `Media:MaxImageBytes` + margin) → API multipart.

## 3. Concurrent edits (409 "data changed, reload")

Variants (`rowVersion`), settings (`rowVersion`) and orders (`concurrent_update`) return **409**. The UI never retries silently: it shows a fixed banner "این اطلاعات هم‌زمان توسط شخص دیگری تغییر کرد" with a **Reload** button that refetches and re-renders the form with the server's values and lists the fields the admin had changed ("your unsaved values") so they can re-apply them. Because checkout changes stock all day, **(new)** `POST /admin/variants/{id}/stock-adjust {delta}` is one atomic `UPDATE Stock = Stock + @delta WHERE Stock + @delta >= 0` with no row version; only "set exact value" and price edits use `rowVersion`.

## 4. JavaScript and indexing

Admin uses client components with Server Actions (`useActionState`); no no-JS requirement. Not indexed in four ways: `robots` metadata `noindex, nofollow` on the `/admin` layout, `X-Robots-Tag: noindex, nofollow` for `/admin/:path*`, `Disallow: /admin` (already in robots.txt), and no link to it from public pages. Admin responses also send `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`.

## 5. Test plan

- **API (integration)** for the **(new)** pieces: dashboard counts and permissions; stock-adjust (atomic under parallel calls, never below zero, 401/403/CSRF); change-password and logout rotating the stamp (the old cookie stops working); `create-admin` command; `Auth:SessionHours`.
- **Next (vitest):** session seal/unseal (tampered, wrong secret, idle and absolute expiry, `lastSeen` refresh); `adminFetch` (cookie + CSRF dance against a mocked API, 401 → redirect, 409 mapping); login throttle and the single generic error; non-admin refused and logged out; cookie attributes; noindex/no-store headers; 409 banner state.
- **End-to-end (Playwright, new dev dependency, against a test API + DB):** login → edit settings (see audit entry) → place an order as a guest → confirm payment → ship → cancel (paid-order warning); product create with a variant and an image; two browsers editing one variant (409 flow); expired session redirects; logout invalidates a replayed cookie.
- **Security checks:** unauthenticated access to every `/admin/**` page and action redirects/401s; wrong Origin on an action is rejected; the API key and `admin_session` secret never appear in client bundles; an accessibility pass (axe) on the main pages.

## Approved changes (2026-10-25)

1. **`admin_session` cookie:** `httpOnly`, `Secure`, `SameSite=Strict`, `Path=/admin`. The sealing secret comes from the environment (`ADMIN_SESSION_SECRET`, ≥ 32 bytes); a production start fails when it is missing or a placeholder. Idle timeout **60 min** (`ADMIN_SESSION_IDLE_MINUTES`), absolute **8 h** (`ADMIN_SESSION_ABSOLUTE_HOURS`), both configurable. **Logout and a password change revoke every existing session of that user on the server** (security-stamp rotation, checked on every API request), not just the browser's cookie. The API cookie lifetime is `Auth:SessionHours` (default 8).
2. **Step-up re-authentication:** changing the card number, card holder, bank name, or switching a payment method on/off needs the admin's current password in the same request (`currentPassword` on `PUT /admin/settings`). A missing or wrong password is `403` and writes an audit entry (field names only, never values) and counts as a failed login attempt for lockout. Other settings need no password. **2FA (TOTP) is Post-MVP** (PROGRESS.md).
3. **`create-admin`:** `dotnet Shop.Api.dll create-admin --email <e>`. The password is read from a hidden interactive prompt (entered twice) or from the `SHOP_ADMIN_PASSWORD` environment variable, **never from a command-line argument** (a `--password` argument is refused). At least 12 characters, no obvious weakness. Documented in README.md. A Production start also refuses `Seed__AdminPassword`.
4. **Image upload ≤ 5 MB through Next:** a route handler streams the multipart body to the API (no 1 MB Server Action limit involved). 4.9 MB succeeds; 5 MB + 1 byte is rejected with a clear Persian message.
5. **Login brute force:** per IP (API `auth` limiter on the real client IP, plus a Next limiter), per account (API throttle per e-mail, plus Identity lockout of **15 minutes after 5 failures**), and one generic error text for every failure, including lockout, throttling and non-admin accounts.
6. Every `/admin` response sends `X-Robots-Tag: noindex, nofollow`; `/admin` is disallowed in robots.txt.
