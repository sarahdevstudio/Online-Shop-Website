# PROGRESS

Resume rule: read this file first, continue from the first unchecked item. Audit: `docs/05-audit-report.md`.
Update this file in the same commit as each item.

## Decisions (approved 2026-10-21)

- Slugs: English, lowercase, hyphenated (`/product/led-bulb-9w-e27-warm`). Immutable after publish; any change auto-creates a 301.
- Auth: mobile OTP login/registration is primary; email optional. SMS behind `ISmsSender` (fake for dev/test; provider Kavenegar or SMS.ir, decided later). Rate limit + cooldown per phone AND per IP on OTP requests.
- Payment: card-to-card and cash-on-delivery, manual admin confirmation. Order/Payment designed so an online gateway can be added later. All prices/totals computed server-side.
- Hosting: `[your-domain.ir]`, preferred host non-www, behind a reverse proxy: `ForwardedHeaders` with known proxies only. (Domain/provider still to be filled in.)
- Frontend: `storefront/` (Next.js) is the only storefront; admin panel lives in it under `/admin` (noindex, auth-protected). `web/` (Angular) is removed after tokens + RTL/contrast checks are ported.
- Build fixes: upgrade/pin vulnerable packages; never suppress NU1902/NU1903.
- Every non-production deployment sends `X-Robots-Tag: noindex` and serves disallow-all `robots.txt`.

## P0 — launch blockers

- [x] P0-1 Fix build: pin `System.Security.Cryptography.Pkcs` 10.0.13 (was 9.0.11 via SqlClient); 27 unit + 38 integration tests green
- [ ] P0-2 Initial commit + CI for `storefront` (install, typecheck, build)
- [ ] P0-3 Small commit: out-of-range `?page` returns 404 (no false canonical)
- [ ] P0-4 Non-production noindex: `X-Robots-Tag` header + disallow-all `robots.txt` (storefront and API)
- [ ] P0-5 `ForwardedHeaders` with known proxies; restrict `AllowedHosts`; rate-limit partitioning by real client IP
- [ ] P0-6 Mobile OTP auth: `ISmsSender` + fake, OTP entity, per-phone + per-IP rate limit/cooldown, optional email, tests
- [ ] P0-7 Catalog backend: Category, Brand, Product, Variant, Attribute/Option (lighting attributes), Image/Media, Inventory; public read API + admin API; immutable slugs + 301 on change
- [ ] P0-8 Storefront on real API; remove sample data from `storefront/src/lib/catalog.ts`
- [ ] P0-9 Cart + checkout + order with server-side pricing; card-to-card / COD with manual admin confirmation (Payment/PaymentTransaction audit trail, gateway-ready)
- [ ] P0-10 Inventory reservation/commit/release with concurrency tests
- [ ] P0-11 Admin panel in Next.js under `/admin` (noindex, auth): products, categories, orders, customers
- [ ] P0-12 Port Angular design-system checks (`check:contrast`, `check:rtl`) to `storefront/`, then remove `web/`

## P1 — SEO foundations and important features

- [ ] SeoMetadata + manual title/description override; Redirect manager (301/410), no chains
- [ ] Split sitemaps (products, categories, blog, pages) with `lastmod`, generated from API
- [ ] Faceted filters + indexing policy (index chosen landing pages, noindex/canonical the rest)
- [ ] Persian search with autocomplete and typo tolerance
- [ ] Image pipeline (AVIF/WebP, srcset) + `alt` conventions
- [ ] JSON-LD: Review, FAQPage, LocalBusiness + automated validation
- [ ] Legal/info pages (About, Contact, FAQ, Shipping, Returns, Privacy, Terms), trust badges, category intro/buying-guide text
- [ ] Customer account: addresses, order history/tracking, IDOR tests
- [ ] Storefront tests: axe, RTL, SEO-meta snapshot tests
- [ ] Core Web Vitals measurement (Lighthouse CI) on home, category, product, cart
- [ ] Discontinued products: keep page / 301 to alternative, never soft 404

## P2 — growth

- [ ] Blog and guides (color temperature, LED vs halogen, lumens vs watts, IP rating)
- [ ] Reviews/ratings with moderation, Q&A
- [ ] Wishlist, product comparison (lamp specs)
- [ ] Back-in-stock alerts, order SMS/email notifications
- [ ] Coupons, CSV/Excel import/export, reports
- [ ] Analytics + e-commerce events, Search Console
- [ ] Persistent logging/monitoring, automated backups
- [ ] CSP and Permissions-Policy headers

## Log

- 2026-10-21: audit approved; P0-1 done.
