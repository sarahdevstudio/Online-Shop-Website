# PROGRESS

Resume rule: read this file first, continue from the first unchecked item. Audit: `docs/05-audit-report.md`.
Update this file in the same commit as each item.

## Decisions (approved 2026-10-21)

- Slugs: English, lowercase, hyphenated (`/product/led-bulb-9w-e27-warm`). Immutable: set on create, never edited in the MVP (so no redirect table is needed yet).
- Auth (revised 2026-10-22): MVP keeps the existing email+password accounts; checkout collects a phone number. Mobile OTP login moves to Post-MVP; when built it becomes primary, email optional. SMS behind `ISmsSender` (fake for dev/test; provider Kavenegar or SMS.ir, decided later). Rate limit + cooldown per phone AND per IP on OTP requests.
- Payment: card-to-card and cash-on-delivery, manual admin confirmation. Order/Payment designed so an online gateway can be added later. All prices/totals computed server-side.
- Hosting: `[your-domain.ir]`, preferred host non-www, behind a reverse proxy: `ForwardedHeaders` with known proxies only. (Domain/provider still to be filled in.)
- Frontend: `storefront/` (Next.js) is the only storefront; admin panel lives in it under `/admin` (noindex, auth-protected). `web/` (Angular) is removed after tokens + RTL/contrast checks are ported.
- Build fixes: upgrade/pin vulnerable packages; never suppress NU1902/NU1903.
- Every non-production deployment sends `X-Robots-Tag: noindex` and serves disallow-all `robots.txt`.
- Listing indexing (2026-10-22): a clean category URL (no params, or only `?page=N`) is indexable and self-canonical. Any filter or sort param: `noindex, follow` and **no** canonical (never noindex combined with a cross-page canonical). Filter and sort links are `rel="nofollow"`; robots.txt does not disallow filter params; the sitemap lists clean category URLs only.
- Checkout (2026-10-22): guest checkout allowed (phone + address, no account). Shipping = one flat fee from configuration plus an optional free-shipping threshold (order subtotal), both server-side.
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
- [ ] M-7 Cart (guest + logged-in, merged on login); guest checkout with phone number + address; flat shipping fee + optional free-shipping threshold from configuration; payment card-to-card or cash on delivery with manual admin confirmation; all prices/totals computed server-side; stock decremented atomically when the order is placed, restored on cancel
- [ ] M-8 Minimal admin in Next.js under `/admin` (noindex, auth): products (incl. variants, images, stock), categories, brands, orders (status + payment confirmation)
- [ ] M-9 Pages: contact, terms, returns
- [ ] M-10 Production config: `ForwardedHeaders` with known proxies (rate limiting by real client IP), restricted `AllowedHosts`, `NEXT_PUBLIC_SITE_URL` required in production builds; `X-Robots-Tag: noindex` + disallow-all `robots.txt` on every non-production deployment (storefront and API)

## Post-MVP

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
