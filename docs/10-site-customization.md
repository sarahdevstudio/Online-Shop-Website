# 10 — Admin-customizable site (design)

Status: **design only, nothing implemented.** Prepared after reading the company's previous site (https://ghr-co.com/, Task 2 findings below). Follows the conventions of `06-catalog-design.md`, `07-checkout-design.md` and `08-admin-design.md`; nothing here weakens a rule from those documents.

## 1. Goal and non-goals

The shop owner changes how the storefront looks and what the home page shows **from the admin panel, without a developer and without a rebuild**.

Hard rules (apply to every section):

1. **No HTML, CSS or script typed by the admin is ever rendered.** Every field is a typed value (color, enum, number, URL, short text, Markdown rendered by the existing safe renderer). Free text is rendered as text. This includes trust badges (§7) and the announcement bar.
2. Everything the admin can set has a **validated, bounded schema on the server**; the storefront never trusts stored values (it re-validates when generating CSS).
3. The storefront must keep working when the site configuration is missing or invalid: it falls back to built-in defaults (the current tokens), never a blank or broken page.
4. No external services. Fonts are bundled with the storefront, not loaded from a CDN.

Non-goals (Post-MVP): a free-form page builder, custom CSS, custom fonts upload, dark theme editing, per-category themes, A/B testing, scheduled publishing, multi-language content.

## 2. Findings from ghr-co.com (initial values)

Read-only, from the live site (WordPress 5.5.3 + WooCommerce + the "Salient" theme + WPBakery). Nothing was changed in the repo except saving the logo (not committed).

| Item | Finding |
|---|---|
| Name (fa) | **قصر روشنایی** (site title: «فروشگاه قصر روشنایی – لوستر و لوازم روشنایی»; footer: «گالری قصر روشنایی») |
| Name (en) | Not shown as text. Domain `ghr-co.com`; social handles use `ghasr_e_roushanaei` / `e_ghasr`. "Ghasr-e Roushanaei" is a transliteration, **not an official English name — needs confirmation** |
| Slogans / hero texts | «ترکیبی از کلاسیک و مدرن»; «شکوه و زیبایی با لوسترها قصر روشنایی»; «نورپردازی و مشاوره حرفه ای»; «کیفیت، نوآوری و تنوع، ره آورد قصر روشنایی برای آرامش خانه شماست»; «همواره بالاترین کیفیت را بخواهید ولی هرگز، بدون بهترین خدمات، خرید نکنید!» |
| About (summary) | Gallery working continuously since 1385 (2006); leader in importing modern and classic chandeliers; promises quality, new products, real warranty and after-sales service, fair prices, customer-first attitude, invites feedback |
| Promises | Warranty ≥ 1 year and 3 years after-sales service; free installation in cities with a branch; free delivery for orders above 3 million Toman (own team in branch cities) |
| Phones | 02133945319 and 02133945516 (fax 02133903274) |
| Email | none published |
| Address | تهران، ابتدای خیابان لاله‌زار نو، پلاک ۴۳۴ |
| Working hours | **not published** |
| Social | Instagram `parsagallery2020`; Twitter `e_ghasr`; Pinterest `ghasr_e_roushanaei`; Aparat `ghasr_e_roushanaei`. Facebook, Telegram, WhatsApp and LinkedIn icons point to the bare domains (empty placeholders) — **not imported** |
| Licenses / enamad / samandehi | none found on the home page, about or contact pages |
| Menu | خانه · فروشگاه (لوستر و آویز · استند و آباژور ایستاده · چراغ‌های دیواری · چراغ‌های سقفی · چراغ رو میزی · اتاق کودک · «زیر منوی مرحله دو / دومین زیر منو» (leftover theme demo items, not imported)) · ساعت · درباره ما · تازه‌ها · تماس |
| Categories | لوستر و آویز، استند و آباژور ایستاده، چراغ‌های دیواری، چراغ‌های سقفی، چراغ رو میزی، اتاق کودک، ساعت |
| Brands | none shown (no brand taxonomy); products are the gallery's own model codes |
| Example products | لوستر H6-4, H7-4, H5-4, H1-5, SEN/6, H1410, KIT-3, ELVA-8, ۲FLW-B, ARMS, BASTI, CIR7 (all «لوستر»; listed prices are in **Rial**, e.g. 34,900,000 ریال = 3,490,000 Toman) |
| Home layout | full-width hero slider (3 slides, light-on-dark text, CTA button) → «جدیدترین محصولات» carousel (8 products) → category banner → 3 promise boxes (ضمانت کیفیت / نصب رایگان / ارسال رایگان) → category tile → latest blog posts (2019) → footer with about blurb, contact, social, menu |
| Blog | «تازه‌ها» (4 posts from 2019). The MVP has no blog; **not imported** |

### Theme values (exact, from `salient-dynamic-styles.css` and computed styles)

| Role | ghr-co value | Note |
|---|---|---|
| Primary | `#5b0057` (deep purple; link color, most used) | white on it = 13.27:1 |
| Secondary | `#76007c` (purple) | white on it = 10.19:1 |
| Accent | `#d8c831` (gold); also `#e8e400` | **only** as a fill behind dark text or on the dark purple (7.73:1 on `#5b0057`); on white it is 1.72:1 and must never be a text color |
| Background | `#ffffff` | |
| Surface / light | `#f2f2f2`, lilac `#d1bee2` / `#c8aee2` (section background) | purple text on `#d1bee2` = 7.69:1 |
| Text | `#000000` (body), muted `#494949`, `#393939` | |
| Dark | `#40033d` (hero/footer band), `#000000` | |
| Radius | buttons 2 px (square, classic look) | |
| Fonts | body **IranSans**, headings **MJ_Smooth Light**; Poppins/Montserrat for Latin | IranSans and MJ_Smooth are **commercially licensed — not allowed** here. Replacement: Vazirmatn (already the storefront default) |
| Logo | `https://ghr-co.com/wp-content/uploads/2019/07/logo-new-shadow.png`, 300×70 PNG with transparent background and a drop shadow, gold/white lettering | saved as `docs/brand/logo.png` (uncommitted). A transparent PNG meant for a dark header; the owner should confirm they own the file and ideally supply an SVG |
| Favicon | **none** (no icon link, `/favicon.ico` redirects to the home page) | a favicon must be supplied |

## 3. What is customizable — overview

| Area | Content | Draft/publish |
|---|---|---|
| Theme | palette, font, radius, logo, favicon | yes |
| Home page | ordered blocks | yes |
| Header and footer | announcement bar, header menu, footer columns, social links | yes |
| SEO defaults | site name, title template, default description, default OG image | yes |
| Trust badges | enamad, samandehi (by ID) | yes |

Store contact details (phone, address, hours, Instagram), shipping and payment stay in **Store settings** (M-8) — one source of truth. The footer, the contact page and JSON-LD keep reading them from there; this feature does not duplicate them.

## 4. Theme

### 4.1 Fields

| Field | Type / rule |
|---|---|
| `primary`, `secondary`, `accent`, `background`, `surface`, `text`, `success`, `warning`, `danger` | `#rrggbb` only (regex `^#[0-9a-fA-F]{6}$`, normalized to lower case). No names, no `rgb()`, no alpha |
| `font` | enum from the allow-list below |
| `radius` | enum `none` (0) · `sm` (0.375rem) · `md` (0.625rem, current) · `lg` (1rem) |
| `logo` | uploaded image (PNG/WebP/SVG-rasterized rules of M-8: JPEG/PNG/WebP, ≤ 5 MB), `alt` text required; header height is fixed by CSS so any aspect ratio is safe |
| `favicon` | uploaded PNG, square, ≥ 192×192; the server also derives 32×32 and 180×180 (apple-touch) |

SVG logo is **not** accepted in the MVP (SVG can carry script; sanitizing is Post-MVP).

### 4.2 Font allow-list (free, open-license, Persian)

Only fonts under SIL OFL or equivalent, **bundled as WOFF2 in the storefront** (`@fontsource` or the font's own release; license text kept in the repo), so nothing is requested from a third-party CDN:

- **Vazirmatn** (default — OFL)
- **Sahel** (OFL), **Shabnam** (OFL), **Samim** (OFL), **Tanha** (OFL), **Estedad** (OFL), **Gandom** (OFL)
- An implementation-time check confirms each file's license; any font whose license cannot be confirmed is dropped from the list. Commercial fonts (IranSans, IRANYekan, Dana, Yekan Bakh, MJ_*, B Nazanin …) are never offered.

Only the selected font's files are linked on a page (one `<link rel="preload">` for its regular weight), so the choice costs nothing for the others.

### 4.3 Contrast check on save (WCAG 2.2 AA)

The server validates on **save of the draft** (so the admin sees the problem immediately) and again on **publish** (so an invalid theme can never go live). The same pure function is shared with the existing `npm run check:contrast` script so CI and the admin agree.

Required pairs (normal text ≥ 4.5:1; non-text/UI boundary ≥ 3:1):

| Foreground | Background | Min |
|---|---|---|
| `text` | `background`, `surface` | 4.5 |
| `primary` (as text/link) | `background`, `surface` | 4.5 |
| `on-primary` (computed: white or near-black, whichever passes) | `primary` | 4.5 |
| `on-secondary` (computed) | `secondary` | 4.5 |
| `on-accent` (computed) | `accent` | 4.5 |
| `success`, `warning`, `danger` (as text) | `background` | 4.5 |
| `primary` focus ring | `background` | 3 |

`on-*` colors are **computed, not stored**: the check picks white (`#ffffff`) or `#111111`, whichever has the higher ratio; if neither reaches 4.5 the palette is rejected. The accent gold `#d8c831` passes only as a fill with dark text — exactly how ghr-co uses it — and the validator accepts it on that basis; it is not offered as a text color anywhere in the components.

Errors name the pair and the ratio ("متن روی رنگ اصلی: ۳٫۲ به ۱ — حداقل ۴٫۵"). Save of a *draft* with a failing pair is allowed (work in progress) but **publish is blocked** until it passes.

### 4.4 Applying it at request time (no rebuild)

The root layout reads the published theme and renders a single `<style>` element in `<head>`:

```css
:root{--color-primary:#5b0057;--color-primary-contrast:#ffffff; … --radius-md:…;--font-sans:'Sahel',system-ui,…}
```

- The CSS string is **generated by the storefront from the validated typed values** (hex regex re-checked, enums mapped to constants). Admin text never reaches the CSS.
- The existing semantic tokens in `src/styles/tokens.css` stay the defaults and the fallback; components already use only semantic tokens (rule of that file), so no component changes are needed beyond a few raw values to be migrated.
- Derived tokens (hover, subtle backgrounds, border) are computed from the palette with fixed formulas (mix with white/black) and contrast-checked by the same function.
- `themeColor` in the viewport and the favicon/apple-touch links come from the same record.
- Output has a fixed shape and length bound; the `<style>` has no user-controlled text, so a nonce/CSP concern does not arise, but it is emitted through one function so a future CSP can hash/nonce it.

## 5. Home page builder

### 5.1 Model

An ordered list (max **20**) of blocks. Each block: `id` (GUID), `type`, `enabled`, `data`. Order is the array order; enable/disable never deletes. Types are a closed enum; unknown types are dropped when reading (forward compatibility).

| Type | Data (all bounded) |
|---|---|
| `hero` | 1–6 slides: `image` (uploaded), `alt` (required), `title` ≤ 80, `text` ≤ 200, `linkLabel` ≤ 30, `link`; `autoplaySeconds` 0 or 4–12 |
| `categoryTiles` | 2–12 categories picked from the real category list (by id), optional custom label ≤ 40; image from the category, else a neutral tile |
| `productRow` | `title` ≤ 60; `source` ∈ `newest` · `discounted` · `category` (+ `categoryId`) · `handpicked` (+ 1–12 product ids); `limit` 4–12; `viewAllLink` (derived for category/newest/discounted) |
| `text` | `heading` ≤ 80, `body` Markdown ≤ 3000 chars rendered by `renderMarkdown` (html disabled, images disabled, links safe — see M-9) |
| `banner` | `image`, `alt` (required), `link` (optional), `height` ∈ `sm` · `md` · `lg`; image-only, no overlaid HTML |

Product rows resolve at render time from the catalog API, so prices/stock are never stale in the config; a hand-picked product that is later deleted or inactive is skipped silently, and a row with no products renders nothing. `discounted` needs the catalog's existing sale-price data (checked at implementation; if the catalog has no sale concept this source is hidden until it does — a decision for the user, §14).

### 5.2 Links

A link is either an **internal path** starting with a single `/` (validated against `^/[A-Za-z0-9\-._~%/?=&#]*$`, no `//` prefix) or an **absolute `https://` URL** (no `http`, `javascript:`, `data:`; reuses `isSafeLink`). External links render `rel="noopener noreferrer"` (+ `nofollow` for banners/hero).

### 5.3 Images

Uploaded through the existing streamed 5 MB image pipeline (same validation: JPEG/PNG/WebP, magic-byte check) into a new media kind `site`; stored once, referenced by id. Hero slides and banners must be ≥ 1200 px wide (checked on upload, warning below that). `alt` is mandatory (accessibility + SEO). Only the first hero slide is `priority`-loaded; all others lazy.

### 5.4 Empty/default state

On first start the home page is seeded with a sensible default (a `hero` with the shop name, `productRow` newest, `categoryTiles`) so the site is never empty. The seed uses the ghr-co-derived values of §12 only after the user confirms them.

## 6. Header, footer, announcement bar

| Part | Fields |
|---|---|
| Announcement bar | `enabled`, `text` ≤ 140 (plain text), optional `link`, `style` ∈ `primary` · `accent` · `neutral` (token-based, contrast guaranteed by §4.3), `dismissible` (remembered per visitor in `localStorage`; failure to store means it just shows) |
| Header menu | up to 8 items: `label` ≤ 30, `link`; one nested level (≤ 8 children) for dropdowns; an item can be "all categories" (auto-expands the category tree). Mobile menu is the same data |
| Footer | up to 4 columns, each `title` ≤ 40 and ≤ 8 links; plus a short "about" text ≤ 300 (plain text) |
| Social links | fixed platforms: Instagram, Telegram, WhatsApp, Aparat, Twitter/X, Pinterest, YouTube, LinkedIn. The admin enters a **handle or URL**; the server validates it against the platform's host/pattern and stores a normalized `https://` URL. Empty = hidden. (Instagram stays in Store settings as it is today — the form shows it read-only with a link to settings, to avoid two sources.) |
| Info pages | the M-9 pages stay linked automatically in the footer group "اطلاعات"; the footer builder only adds extra columns |

Contact phone/address/hours come from Store settings (unchanged).

## 7. SEO defaults

| Field | Rule |
|---|---|
| `siteName` | 2–60 chars; used in titles, OG `siteName`, JSON-LD Organization name |
| `titleTemplate` | must contain exactly one `%s`; ≤ 70 chars; e.g. `%s | قصر روشنایی` |
| `homeTitle` | ≤ 70; used for `/` |
| `defaultDescription` | ≤ 160 chars; used where a page has none |
| `defaultOgImage` | uploaded, ≥ 1200×630 recommended, JPEG/PNG/WebP; used when a page has no image |

Per-product/category SEO keeps coming from catalog data (M-4); these are only the fallbacks and the site-level pieces. Respects `SITE_ENV` (M-10): nothing here can make a non-production site indexable.

## 8. Trust badges (enamad / samandehi) — by ID only

The admin never pastes the snippet the authority gives. They enter only the **identifiers**, and the storefront renders a fixed template.

| Badge | Fields | Validation | Rendered by the site as |
|---|---|---|---|
| Enamad (e-Namad) | `id` (digits), `code` (alphanumeric) | `^[0-9]{1,10}$`, `^[A-Za-z0-9]{10,64}$` | the official anchor + image: link to `https://trustseal.enamad.ir/?id={id}&Code={code}`, image `https://trustseal.enamad.ir/logo.aspx?id={id}&Code={code}`, `referrerpolicy="origin"`, `target="_blank"` `rel="noopener"` |
| Samandehi | `id` (digits), `code` | same style of patterns | official link/image on `logo.samandehi.ir` |

- The exact URL shapes are **taken from each authority's current official snippet at implementation time** (they are the only external hosts the storefront will load images/links from, kept in one constant); the sketch above is the expected format, to be verified against the official pages, not guessed.
- Rendered only in the footer, only when both fields are set, and only from the template — no `<script>`, no inline style, no admin-controlled attribute.
- The visitor's browser (not our server) loads the badge image; this is the same behavior as the official snippet. A future CSP must allow exactly those two hosts for `img-src`.
- The shop's legal facts (company name, registration number) belong in the "terms"/"about" pages (M-9), not here.

## 9. Draft → preview → publish, versions, restore

### 9.1 Storage

One table `SiteConfigVersions` (single table keeps migration, audit and caching simple):

| Column | Meaning |
|---|---|
| `Id`, `Kind` | `theme` · `home` · `chrome` (header/footer/social/announcement) · `seo` · `badges` |
| `Version` | integer, increasing per kind |
| `State` | `draft` · `published` · `archived` |
| `Payload` | JSON, produced and parsed only by typed C# records (System.Text.Json, no dynamic) |
| `CreatedAt/By`, `PublishedAt/By` | who and when |
| `RowVersion` | optimistic concurrency |

Invariants (enforced by filtered unique indexes): at most **one** `draft` and **one** `published` per kind. Kinds are independent: the owner can publish a new home page without touching the theme.

### 9.2 Flow

1. **Edit** — the admin form works on the draft (created from the published one the first time). Every save: validate (schema + bounds + links + contrast for theme), write the draft with `RowVersion` check → **409** on a stale version, with the same "someone changed this" UX as M-8.
2. **Preview** — "پیش‌نمایش" sets a short-lived (30 min) sealed `site_preview` cookie (httpOnly, SameSite=Strict, Path=/, same sealing key as `admin_session`, bound to that admin session). While present (and the admin session still valid) the shop layout fetches the **draft** with `no-store` (never cached, never shared) and shows a fixed banner "پیش‌نمایش پیش‌نویس — منتشر نشده" with a link back. Responses carry `noindex` and `Cache-Control: private, no-store`. Anyone without the cookie sees only published content; the cookie is useless to others (sealed, session-bound).
3. **Publish** — one action: re-validates, then in a transaction demotes the current `published` to `archived`, promotes the draft to `published` (assigns `Version`), writes the audit row, and invalidates caches (§11). Needs `Site.Manage`, CSRF; no step-up (not a financial setting).
4. **History** — the last **20** published versions per kind are kept (older archived rows are pruned in the same transaction; audit rows stay forever).
5. **Restore** — "بازگردانی این نسخه" on any archived version: copies it into the draft (a safe step) and offers "publish now" — i.e. one click to restore *and* publish, while the previous live version is itself archived and restorable (restore is undoable). Versions are immutable once archived.
6. **Discard draft** — resets the draft to the published payload.

Uploaded images referenced by any non-pruned version are never deleted (a media row is only deletable when no version references it).

## 10. Permissions, CSRF, audit

- New permission **`Site.Manage`** (Admin role gets it; seeded like `Content.Manage`). All `/api/v1/admin/site/*` endpoints require it; the storefront admin pages hide the nav item without it.
- CSRF: the admin BFF's server-side double-submit exactly as in M-8 (Server Actions; `X-XSRF-TOKEN`). The public read endpoint is a GET with no state change.
- **Audit**: `SiteAuditLogs` — who, when, kind, action (`saved` · `published` · `restored` · `discarded` · `uploaded`), version, and a short change summary (field names changed, never full payloads). Shown on each admin page like the content-page audit.
- Concurrency: `RowVersion` → **409**; the 409 is logged as 409 (M-10 fix) and shown as "data changed".
- Uploads reuse the M-8 limits and magic-byte validation; the upload route stays streaming and size-capped, and the reverse proxy body limit (≥ 6 MB, doc 09) already covers it.

## 11. Caching, invalidation, and how storefront pages read it

### 11.1 API

- `GET /api/v1/catalog/site` (public, anonymous, `Cache-Control: public, max-age=30`) returns the **compiled published config** for all kinds in one small JSON document with a `version` string (hash of the published versions) as `ETag`. Image fields are media URLs.
- `GET /api/v1/admin/site/{kind}` (draft + published + history) and `PUT …/{kind}/draft`, `POST …/{kind}/publish`, `POST …/{kind}/restore/{version}`, `POST …/images` — all `Site.Manage`.
- The API keeps the compiled document in an in-memory cache, invalidated in the same transaction scope as publish (same pattern as the cached store settings).

### 11.2 Storefront

- One typed helper `getSiteConfig()` (like `getStoreInfo()`): `apiGet('/catalog/site', 60)` with the Next data cache **tag `site`**, wrapped in try/catch returning the **built-in defaults** on any failure (rule 3 of §1).
- The root layout renders the theme `<style>`, favicon, `themeColor`, and metadata defaults from it. The shop layout renders the announcement bar, header menu, footer and badges. `page.tsx` (home) maps blocks to server components; product rows fetch their products via the existing catalog functions (own cache, 60 s).
- Invalidation: after a successful publish, the admin Server Action calls `revalidateTag('site')`, so the storefront instance that served the admin updates **immediately**; every other instance converges within the 60 s TTL (same trade-off already used for store settings). No rebuild, no restart.
- Preview bypasses the cache (`no-store`) and is the only code path that reads the draft.
- Because the layout reads the config at request time, pages stay dynamic (they already are: `connection()` in `apiGet`). Build never needs the API.

## 12. Initial values (prefilled, from ghr-co.com)

Seeded **only if no config exists** (like content pages), as a published v1, and clearly editable. Everything that is a business fact comes from ghr-co.com and is listed here for the owner to approve before implementation; anything not found is left empty rather than invented.

| Area | Seed |
|---|---|
| Theme | primary `#5b0057`, secondary `#76007c`, accent `#d8c831`, background `#ffffff`, surface `#f2f2f2`, text `#1a1a1a` *(ghr-co uses pure black; `#1a1a1a` is an accessible near-black — owner may keep `#000000`)*; success `#1b7f3b`, warning `#8a5a00`, danger `#b3261e` *(not on ghr-co — proposed values, each ≥ 4.5:1 on white, to be verified by the validator)*; font Vazirmatn; radius `sm` |
| Logo / favicon | `docs/brand/logo.png` (uploaded at seed time through the media pipeline — **a decision**: logo is light/gold lettering for a dark header, see §14); favicon: none until supplied (a generated monogram is **not** invented) |
| SEO | siteName «قصر روشنایی»; titleTemplate `%s | قصر روشنایی`; homeTitle «فروشگاه قصر روشنایی – لوستر و لوازم روشنایی»; description «گالری قصر روشنایی از سال ۱۳۸۵ پیشرو در واردات لوسترهای مدرن و کلاسیک؛ ضمانت واقعی و خدمات پس از فروش.» (from the site's about text); default OG image: none until supplied |
| Home | `hero` (3 slides: «ترکیبی از کلاسیک و مدرن» → shop; «شکوه و زیبایی با لوسترها قصر روشنایی» → new collection; «نورپردازی و مشاوره حرفه‌ای» → contact; **images to be uploaded by the owner**, until then a single text-only hero) · `productRow` newest (title «جدیدترین محصولات», 8) · `categoryTiles` (all top-level categories) · `text` with the three promises (ضمانت کیفیت / نصب رایگان / ارسال رایگان) in Markdown |
| Announcement bar | disabled |
| Header menu | خانه · فروشگاه (all categories) · درباره ما · تماس (the M-9 slugs `/about`, `/contact`) |
| Footer | the "اطلاعات" group (automatic) + a column of top categories; about blurb from §2 |
| Social | Instagram `parsagallery2020`, Twitter `e_ghasr`, Pinterest `ghasr_e_roushanaei`, Aparat `ghasr_e_roushanaei` (the other platforms empty) |
| Trust badges | empty (none found) |
| Store settings (existing, separate) | suggested, **not changed by this feature**: phone 02133945319 / 02133945516, address «تهران، ابتدای خیابان لاله‌زار نو، پلاک ۴۳۴», free-shipping threshold 3,000,000 Toman, hours empty |

## 13. Commit plan (small commits, each builds on its own; push + Sarah sync once per series)

Series A — backend (each ≤ ~8 files)

1. `feat(site): site config domain model and Site.Manage permission` — entities (`SiteConfigVersion`, `SiteAuditLog`), kinds/states, typed payload records, permission constant.
2. `feat(site): EF configuration and migration for site config` — mapping, filtered unique indexes, migration.
3. `feat(site): payload validation (colors, links, bounds, contrast)` — validators + the shared contrast function + unit tests.
4. `feat(site): site config service with draft, publish, restore and audit` — Application contracts + Infrastructure service, cache, version pruning.
5. `feat(site): seed default site config` — `SiteSeeds` (§12) + initializer, only if missing.
6. `feat(site): admin and public site endpoints` — admin CRUD/publish/restore + `GET /catalog/site` + image upload to the `site` media kind.
7. `test(site): site config integration tests` — draft/publish/restore, 409, permission, CSRF, pruning, invalid payloads, public document.

Series B — storefront

8. `feat(storefront): bundled free Persian fonts and theme CSS generator` — font files + licenses, `theme-css.ts`, unit tests (injection-safe).
9. `feat(storefront): site config client with built-in fallbacks` — `site-config.ts`, types, tag `site`, tests.
10. `feat(storefront): apply theme, favicon and SEO defaults in the root layout`.
11. `feat(storefront): header, announcement bar, footer and social links from site config`.
12. `feat(storefront): trust badges rendered from IDs`.
13. `feat(storefront): home page blocks renderer` (hero, tiles, product rows, text, banner) — one commit per block group if over 8 files.
14. `feat(storefront): draft preview mode` — preview cookie, banner, no-store/noindex.

Series C — admin UI

15. `feat(admin): site settings navigation, theme editor with live contrast report`.
16. `feat(admin): home page block editor (add, edit, reorder, enable)`.
17. `feat(admin): header, footer, social and SEO editors`.
18. `feat(admin): trust badge fields`.
19. `feat(admin): version history, restore and audit view`.

Series D — close-out

20. `docs(brand): add ghr-co logo as brand asset` (the saved `docs/brand/logo.png`, only after the owner confirms rights).
21. `docs: site customization guide and deployment notes` — update `09-deployment.md` (font files, preview, cache TTL) and PROGRESS.md.

Before each series is pushed: `dotnet build` (0 warnings), unit + integration tests, storefront typecheck + vitest + build, and a browser smoke of the changed screens.

## 14. Decisions needed before implementation

1. **Brand English name** — not on the old site; use «قصر روشنایی» only (recommended), or give the official English name.
2. **Logo** — the file is a light/gold-lettered PNG built for a dark header with a baked-in shadow, 300×70 (small for retina). OK to use as-is, or supply an SVG/hi-res original? Also confirm the company owns it.
3. **Favicon** — none exists; supply one (recommended) or I generate nothing.
4. **Palette** — keep ghr-co purple/gold exactly (recommended; passes AA as described) and the near-black text `#1a1a1a`?
5. **Discounted products row** — only if the catalog has a sale price concept; confirm it should be offered.
6. **Free-shipping threshold** — the old site promises free delivery above 3,000,000 Toman *in branch cities with own delivery*. Seed it as the store-wide threshold (a simplification), or leave unset?
7. **Promise boxes / about text** — copy the old site's wording (warranty ≥ 1 year, 3 years service, free installation) into the seeds? These are business commitments; they are drafted from the old site and need the owner's confirmation before going live (same draft marker as M-9).
8. **Blog («تازه‌ها»)** — out of MVP; confirm it is dropped from the menu.
9. **Trust badges** — does the shop have an enamad / samandehi registration? (none shown on the old site.)
10. **Font list** — confirm the allow-list (Vazirmatn default + the six others); fewer is also fine.
