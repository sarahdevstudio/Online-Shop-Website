# PROGRESS

Resume rule: read this file first, continue from the first unchecked item. Audit: `docs/05-audit-report.md`.
Update this file in the same commit as each item.

## Decisions (approved 2026-10-21)

- Slugs: English, lowercase, hyphenated (`/product/led-bulb-9w-e27-warm`). Immutable: set on create, never edited in the MVP (so no redirect table is needed yet).
- Auth (revised 2026-10-22, again 2026-10-24): the MVP storefront has **no customer login**; everyone checks out as a guest (no cart merge, no account pages). The existing registration API stays but is unused by the storefront; customer login returns with mobile OTP post-MVP (primary, email optional). Admin login is still needed for M-8. SMS behind `ISmsSender` (fake for dev/test; provider Kavenegar or SMS.ir, decided later). Rate limit + cooldown per phone AND per IP on OTP requests.
- Payment: card-to-card and cash-on-delivery, manual admin confirmation. Order/Payment designed so an online gateway can be added later. All prices/totals computed server-side.
- Hosting: `[your-domain.ir]`, preferred host non-www, behind a reverse proxy: `ForwardedHeaders` with known proxies only. (Domain/provider still to be filled in.)
- Frontend: `storefront/` (Next.js) is the only storefront; admin panel lives in it under `/admin` (noindex, auth-protected). `web/` (Angular) is removed after tokens + RTL/contrast checks are ported.
- Build fixes: upgrade/pin vulnerable packages; never suppress NU1902/NU1903.
- Every non-production deployment sends `X-Robots-Tag: noindex` and serves disallow-all `robots.txt`.
- Listing indexing (2026-10-22): a clean category URL (no params, or only `?page=N`) is indexable and self-canonical. Any filter or sort param: `noindex, follow` and **no** canonical (never noindex combined with a cross-page canonical). Filter and sort links are `rel="nofollow"`; robots.txt does not disallow filter params; the sitemap lists clean category URLs only.
- Checkout (2026-10-22, expanded 2026-10-24): guest checkout only (no account). Details in `docs/07-checkout-design.md`: server-side cart in an httpOnly cookie token, prices always live, order snapshots, `CardToCard`/`CashOnDelivery`, stock decremented in one transaction, idempotency key, guest order page `/order/{token}`. Shipping = one flat fee from configuration plus an optional free-shipping threshold (order subtotal), both server-side.
- Client IP and abuse limits (2026-10-24): the API takes `X-Forwarded-For` only from the storefront (`Storefront:TrustedProxies`, one hop); Next derives the visitor IP only from what its trusted reverse proxy appended (`TRUSTED_PROXY_HOPS`; in development the socket address) and never forwards a browser-supplied header. At most 2 unpaid orders (AwaitingPayment/Pending) per mobile and per IP (configurable); order placement is rate limited per IP. `Storefront:ApiKey` is compared in constant time; production refuses to start with a missing/placeholder key or placeholder card details. Order tokens are masked in logs.
- Store settings (2026-10-25): shipping fee, free-shipping threshold, payment-method switches, card number/holder/bank, per-line quantity limit, store phone/address/hours/Instagram are one typed database row (`StoreSettings`), seeded from `StoreDefaults` only when missing, edited by admins with `Settings.Manage` (separate from `Catalog.Manage`, CSRF required, `SettingsAuditLogs` with card numbers masked to the last 4 digits), cached in memory and invalidated on update. Card-to-card is offered only while validly configured (Luhn, not a placeholder); orders snapshot the card details and shipping fee. API key, trusted proxies, rate limits and connection strings stay in configuration/env and are not editable from the admin. Design: `docs/07-checkout-design.md` (last section).
- Admin panel (2026-10-25): design in `docs/08-admin-design.md`. Next.js is a backend-for-frontend: the browser holds only a sealed `admin_session` cookie (httpOnly, Secure, SameSite=Strict, Path=/admin; idle 60 min, absolute 8 h, both configurable). Logout and password change revoke every session of the user on the server (security stamp, checked on each request). Changing the card number/holder/bank or switching a payment method needs the admin's current password (step-up; failures are audited and count towards the 15-minute lockout). First admin via `create-admin` (hidden prompt or `SHOP_ADMIN_PASSWORD`, never an argument, >= 12 characters). Admin stock changes are atomic and audited.
- External services (2026-10-25): no SMS provider and no online payment gateway are implemented in the MVP. `IPaymentGateway` stays an interface with the Development/Testing fake; `ISmsSender` will be introduced as an interface with a fake when OTP login is built (Post-MVP).
- Catalog (2026-10-22): one category per product; design in `docs/06-catalog-design.md` approved. Color temperature labels use customer search terms (آفتابی / یخی/طبیعی / مهتابی), slugs stay English.
- Persian text (2026-10-22): one shared normalizer (Arabic ي/ك → ی/ک, Persian/Arabic digits → Latin in search keys, trimmed/collapsed whitespace and ZWNJ); applied when saving names and when matching `q`. MVP search is a simple contains match on the normalized key.
- Media (2026-10-22): uploaded images live in `media/` (git-ignored). **Production: `media/` must be included in backups** (together with the database).

## MVP — required for launch

- [x] M-1 Fix build: pin `System.Security.Cryptography.Pkcs` 10.0.13 (was 9.0.11 via SqlClient); 27 unit + 38 integration tests green
- [x] M-2 Initial commit + CI for `storefront` (npm ci, typecheck, build, npm audit)
- [x] M-3 Paging: invalid or out-of-range `?page` returns 404, `?page=1` 308-redirects to the clean URL, canonical from the real page (category + search); vitest unit tests
- [x] M-4 Catalog backend: categories (max 2 levels), brands, products, simple variants, images, stock, filterable lighting attributes (wattage, base/socket, color temperature); English immutable slugs; integer Toman prices. Design: `docs/06-catalog-design.md`. Migration `Catalog`; public `/api/v1/catalog/*`, admin `/api/v1/admin/*` (`Catalog.Manage`); `IStockService` for checkout; Development-only sample seed
- [x] M-5 Public catalog API; storefront reads it (home, category, product, search, sitemap); remove sample data from `storefront/src/lib/catalog.ts`. Product page preselects the variant matching attribute params in the URL (e.g. `?wattage=9w&cct=3000k`). Server-side fetch via server-only `API_BASE_URL`; data cache 60 s products / 300 s categories+sitemap; nothing prerendered from the API at build; API failure → Next's 500 error page (no-store, noindex; App Router pages can't return 503), only a real API 404 → 404; ratings and AggregateRating removed; JSON-LD one Offer per variant in IRR (Toman × 10), archived = Discontinued; images via next/image through the `/media/*` pass-through
- [x] M-6 Category-page filters (brand, wattage, base, color temperature, price range, in stock) with counts from the API facets, sort (newest / price asc / desc), removable chips + clear all; works without JavaScript (links + GET price form, `<details>` panel on mobile); normalized URLs (fixed key order, sorted/deduplicated values, invalid keys/values and page=1 dropped, inverted price range swapped) with 308 for anything else; product links carry the active wattage/cct values. Indexing per the Decision below
- [x] M-7 Cart, guest checkout and orders (design `docs/07-checkout-design.md`). API `/api/v1/store/*` (server-to-server, `X-Storefront-Key`, no cookie auth) + admin order endpoints (`Orders.Manage`); migration `Orders`. Server-side cart behind an httpOnly cookie token (30 days), live prices with change/unavailable notices; checkout with name, mobile, address, postal code; card-to-card (transfer reference on the order page) or cash on delivery; flat shipping + optional free threshold from `Checkout:*`; order snapshots, sequential number + guest page `/order/{token}`; stock decremented for all lines in one transaction (all-or-nothing), restored once on cancel; idempotency key; max 2 unpaid orders per mobile and per IP; per-IP order rate limit; client IP only from the trusted proxy. Storefront cart/checkout/order pages work without JavaScript; `/cart`, `/checkout`, `/order` are noindex and disallowed in robots.txt
- [x] M-8 Admin panel in Next.js under `/admin` (design `docs/08-admin-design.md`): login/logout, change password, dashboard counts, orders (list, filters, detail, confirm payment, next status, cancel with a paid-order warning), products (create with variants, edit, atomic stock adjust + history, attributes grid, images with streamed upload up to 5 MB), categories, brands, settings with audit log (step-up password for card/payment changes), and a "data changed, reload" banner for 409s. Sealed `admin_session` cookie (httpOnly, Secure, SameSite=Strict, Path=/admin, idle 60 min / absolute 8 h), every `/admin` response noindex + no-store; the storefront moved into a `(shop)` route group so admin has its own shell. API additions in the previous commit
- [x] M-9 Info pages (about, contact, terms, returns, privacy): fixed slugs stored in `ContentPages` (title + Markdown body + row version), seeded only when missing as clearly marked Persian DRAFTS with [placeholders] (no real business facts); edited in `/admin/content` (textarea + live preview, permission `Content.Manage`, CSRF, 409 on a stale version, who/when audit); storefront `/about`, `/contact`, `/terms`, `/returns`, `/privacy` (indexable, self-canonical, in the sitemap with lastModified, linked from the footer); the contact page also shows phone/address/hours/Instagram from the store settings plus LocalBusiness (with an address) or Organization JSON-LD. Markdown is rendered with raw HTML disabled, images disabled, only http(s)/mailto/tel/relative links and `rel=noopener`, covered by XSS tests
- [x] M-10 Production readiness: `AllowedHosts` from configuration (a bare `*` stops a Production API start); `NEXT_PUBLIC_SITE_URL` (public https) and `API_BASE_URL` required when `SITE_ENV=production` (build and start stop otherwise); `SITE_ENV` (production|staging|development, default not-indexable): anything but production sends `X-Robots-Tag: noindex, nofollow` on every storefront and API response and serves a disallow-all robots.txt (read per request); storefront security headers (nosniff, `strict-origin-when-cross-origin` / `no-referrer` on `/order` and `/admin`, `X-Frame-Options: DENY` + `frame-ancestors 'none'`, HSTS in production; a full CSP stays Post-MVP); request logs show the real status (a stale row version is a 409, not a 500); create-admin tested in a Development host; `docs/09-deployment.md` (every variable, first deploy, backups of database AND media, reverse proxy, health checks, smoke checklist, IIS and nginx)

## Post-MVP

- [ ] Two-factor authentication (TOTP) for admin accounts
- [ ] Mobile OTP login (`ISmsSender`, per-phone + per-IP rate limit/cooldown); SMS notifications (order status, back-in-stock)
- [ ] Wishlist, product comparison (lamp specs)
- [ ] Reviews/ratings with moderation, Q&A
- [ ] Blog and guides (color temperature, LED vs halogen, lumens vs watts, IP rating)
- [ ] Advanced Persian search: autocomplete, typo tolerance
- [ ] CSV/Excel import/export
- [ ] Image pipeline (AVIF/WebP, srcset, derivatives)
- [ ] Analytics + e-commerce events, Search Console
- [ ] Persistent logging/monitoring, automated backups
- [ ] CSP and Permissions-Policy headers
- [ ] Remove `web/` (Angular) after porting `check:contrast` / `check:rtl` to `storefront/`

Also deferred (from the previous P1/P2 list, not in the MVP scope):

- [ ] SeoMetadata (manual title/description override), redirect manager (301/410)
- [ ] Split sitemaps with `lastmod`; indexable filter landing pages (e.g. "LED E27")
- [ ] JSON-LD Review/FAQPage/LocalBusiness + automated validation
- [ ] Customer account: addresses, order history/tracking, IDOR tests
- [ ] Remaining info pages (About, FAQ, Shipping, Privacy), trust badges, category buying-guide text
- [ ] Storefront tests (axe, RTL, SEO-meta snapshots); Core Web Vitals measurement (Lighthouse CI)
- [ ] Discontinued products: keep page / 301 to alternative
- [ ] Inventory reservations with expiry + stock movement ledger; coupons; reports

## Log

- 2026-10-21: audit approved; P0-1 and P0-2 done (API CI job was already present; storefront job added).
- 2026-10-21: P0-3 done; vitest added as storefront devDependency (`npm test`).
- 2026-10-22: roadmap re-split into MVP / Post-MVP (P0-1..P0-3 are now M-1..M-3); catalog design in `docs/06-catalog-design.md`; both approved with changes (cct labels, normalizer, variant preselect, media backup, checkout/shipping answers).
- 2026-10-22: M-4 done: catalog entities + one migration, Persian normalizer, public + admin catalog API, attribute reference data (all environments), Development-only sample seed (20 products), unit + integration tests; uploads: JPEG/PNG/WebP by content, size limit `Media__MaxImageBytes`, media root refused inside the app folder, `/media` serves image types only.
- 2026-10-22: M-5 done: storefront reads the catalog API (home, category, product with variant selector/preselect, archived "توقف فروش", search, sitemap); sample data and ratings removed; 503 not possible from App Router pages, 500 accepted.
- 2026-10-22: M-6 done: category filters, sort, chips, URL normalization (308), indexing rules corrected in Decisions (no canonical on noindex listings).
- 2026-10-24: M-7 done: cart, guest checkout and orders (API + storefront). Backend commit "feat(orders): cart, checkout and order API"; storefront commit "feat(storefront): cart and checkout". Smoke-tested on ShopMvp_Dev without JavaScript. Production needs `Storefront__ApiKey`, `Storefront__TrustedProxies` (API) and `STOREFRONT_API_KEY`, `TRUSTED_PROXY_HOPS` (storefront), plus the real card details entered in the admin settings (since 2026-10-25 they are no longer configuration).
- 2026-10-25: admin-editable store settings (backend `StoreSettings` + audit log + endpoints; storefront footer shows contact info from the API; header account link removed).
- 2026-10-25: M-8 done (admin API additions, then the admin panel). Production also needs `ADMIN_SESSION_SECRET` (>= 32 random bytes; the storefront refuses to start without it) and optionally `ADMIN_SESSION_IDLE_MINUTES` / `ADMIN_SESSION_ABSOLUTE_HOURS`; create the first admin with `create-admin` (see README).
- 2026-10-26: M-9 done. A fresh install now starts with card-to-card switched OFF (`StoreDefaults:CardToCardEnabled=false`): the settings rules refuse an enabled method without a valid card, so with it on no other setting could be saved before a card was entered.
- 2026-10-26: M-10 done. Indexing is opt-in: only `SITE_ENV=production` is indexable; staging builds of the same artifact stay hidden from search engines.
