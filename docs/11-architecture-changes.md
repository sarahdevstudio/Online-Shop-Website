# 11 — Architecture changes (design)

Status: **design only, nothing implemented.** Four proposals, in the order they were requested:

- **A** — replace the minimal-API endpoint groups with `[ApiController]` controllers (no behavior change).
- **B** — a `Translation` table and `Translate` service for every user-facing text.
- **C** — admin-managed URL paths with an API route table, a storefront catch-all and automatic redirects.
- **D** — the order in which A, B, C and the site customization of `docs/10-site-customization.md` are built.

Facts used below (measured on `main` = `5b1c65c`): 11 endpoint files with 86 `Map*` calls under `/api/v1`; the API already serves Persian error titles from `PersianProblemText` and Persian validation messages from `PersianValidation`; about 350 lines of Persian text live in `src/Domain|Application|Infrastructure|Api` and about 400 in the storefront (`lib/messages.ts` 161 lines, `lib/admin/messages.ts` 233 lines plus component literals); categories, products, brands and blog posts already have a unique `Slug`, and the storefront already has `/category/[slug]`, `/product/[slug]`, `/blog/[slug]` and a single-segment `/[slug]` for the five info pages.

Commit rules apply to every plan below: small commits (≤ 5–8 files), each builds and passes its tests on its own, conventional messages, no AI attribution, push and Sarah-sync once per series after the full suites pass.

---

## A. Controllers instead of minimal-API groups

### A.1 Why, and what it does not give

It is a convention change, not a feature: it gives one class per resource, attribute-visible routes, auth and rate limits next to the action, and the familiar MVC shape for anyone joining. It gives **no new capability**, so the whole design is about proving that nothing observable changes. The risk is real because MVC differs from minimal APIs in binding, validation and problem-details defaults — each difference is listed and neutralized below.

### A.2 Contract that must stay identical

Routes (method + template + constraints such as `{id:int}`, `{token:length(43)}`), status codes, authorization policy/permission, CSRF behavior, rate-limit policy, response bodies (including `application/problem+json` shape and the `code` extension), request-body binding, and the `X-Storefront-Key` / cart / idempotency header handling.

### A.3 How each cross-cutting piece is kept

| Today (minimal API) | Controller equivalent | Watch-out |
|---|---|---|
| `api.MapGroup("/api/v1")` + `AntiforgeryFilter` (endpoint filter on the group) | the same logic as an MVC **action filter** registered globally (`options.Filters.Add`), reading the same endpoint metadata | `StorefrontEndpointMetadata` (exempts `/store/*`) and `.DisableAntiforgery()` on the image upload must become metadata attributes the filter honors (`[DisableCsrf]`) |
| `StorefrontKeyFilter` on `/store` | action filter on the two store controllers (`[ServiceFilter]`) | runs before binding like today |
| `WithValidation<T>()` → `ValidationFilter<T>` (FluentValidation, 400 with Persian `title`) | one generic **validation action filter** that validates every bound argument that has an `IValidator<T>`; `[ApiController]` automatic 400 is turned off (`SuppressModelStateInvalidFilter = true`) so the response stays `Results.ValidationProblem(...)`-shaped | the filter must return the same body via a small `ResultActionResult : IActionResult` that executes an `IResult` — so `ToProblem()` / `ToHttpResult()` are reused **unchanged**; no second problem-details implementation |
| `Results.Ok/Json/NotFound` returned from lambdas | actions return `IActionResult` through the same `ResultActionResult` adapter (or typed `ActionResult<T>` for the success path, same JSON options) | JSON serializer options must be the same instance (camelCase, enum handling) — configure `AddControllers().AddJsonOptions` from the existing options |
| `RequireAuthorization(permission)` | `[Authorize(Policy = Permissions.X)]` on the controller | policies already exist by permission name |
| `RequireRateLimiting(policy)` | `[EnableRateLimiting(policy)]` | identical policy names |
| `.AllowAnonymous()` | `[AllowAnonymous]` | |
| malformed JSON / wrong content type | `BadHttpRequestException`/`JsonException` currently turned into the Persian 400/415 by `GlobalExceptionHandler`; MVC input formatters instead produce a `ModelState` 400 | `SuppressModelStateInvalidFilter` + a custom `InvalidModelStateResponseFactory` returning the existing `MalformedRequest` problem; covered by characterization tests (A.4) |
| `HttpRequest`/`CancellationToken` parameters | same types, bound the same way | `[FromBody]` is inferred by `[ApiController]`; query/route parameters get explicit `[FromQuery]`/`[FromRoute]` to avoid inference drift |
| Route order/conflicts | attribute routes with full literal templates (`[Route("api/v1/admin/products")]`) | no `[controller]` token, so a renamed class can never change a URL |
| Tags / operation ids in OpenAPI | `[Tags("Admin blog")]` kept; operation ids differ by nature | listed as an expected, reviewed difference (A.4) |

### A.4 Proof plan (built **before** the first controller)

1. **Route-table snapshot test** (new, first commit). A test enumerates `EndpointDataSource` and writes one line per endpoint: `HTTP method | route pattern | authorization policies | rate-limit policy | CSRF-exempt | storefront-key-required | request body type | produces status codes`. The file is committed as a golden snapshot (`tests/IntegrationTests/Snapshots/routes.txt`). Every controller commit must keep the snapshot **byte-identical**; this proves what OpenAPI cannot (policies, rate limits, CSRF).
2. **OpenAPI diff.** Capture `/openapi/v1.json` from the current build (golden `openapi.before.json`, kept in the repo only for the migration and removed in the last commit). After each commit a script normalizes both documents (sort keys; drop `operationId` and `tags` order) and diffs paths, methods, parameters, request/response schemas and status codes. Any difference other than tags/operationIds fails the commit.
3. **Characterization tests** added before migrating (they pass on today's code): malformed JSON body; empty body on a `POST` that needs one; wrong content type; unknown route (Persian 404); wrong method (Persian 405); validation failure body; a 409 stale row version; unauthenticated, forbidden and CSRF-failure bodies; rate-limit 429 body; a 6 MB image upload refused (413); the streaming 5 MB upload accepted. Each asserts status + `title` + `code` + field-error keys.
4. **Full suites** (unit + integration + storefront) after every commit; the integration suite already covers most endpoints end to end.
5. **Smoke through the BFF**: sign in to the admin, create/edit a product with an image, place a guest order — because the storefront and admin BFFs are the real API clients.

### A.5 Controllers (one per resource)

| # | Controller | Route base | Replaces |
|---|---|---|---|
| 1 | `AuthController` | `/auth` (csrf, register, login, logout, change-password, me) | `AuthEndpoints` |
| 2 | `CustomerController` | `/customer` | `CustomerEndpoints` |
| 3 | `AdminDashboardController` | `/admin` (ping, dashboard) | `AdminEndpoints` + dashboard |
| 4 | `AdminVariantsController` | `/admin/variants/{id}` (update, stock-adjust, stock-history) | variant parts of two files |
| 5 | `AdminCategoriesController` | `/admin/categories` | `CatalogAdminEndpoints` (part) |
| 6 | `AdminBrandsController` | `/admin/brands` | (part) |
| 7 | `AdminAttributesController` | `/admin/attributes` | (part) |
| 8 | `AdminProductsController` | `/admin/products` (+ variants create, attributes, image upload/order) | (part) |
| 9 | `AdminImagesController` | `/admin/images/{id}` | (part) |
| 10 | `AdminOrdersController` | `/admin/orders` | `OrderAdminEndpoints` |
| 11 | `AdminSettingsController` | `/admin/settings` | `SettingsAdminEndpoints` |
| 12 | `AdminContentPagesController` | `/admin/content-pages` | `ContentEndpoints` (admin) |
| 13 | `AdminBlogController` | `/admin/blog` | `BlogEndpoints` (admin) |
| 14 | `CatalogCategoriesController` | `/catalog/categories` | `CatalogEndpoints` (part) |
| 15 | `CatalogBrandsController` | `/catalog/brands` | (part) |
| 16 | `CatalogProductsController` | `/catalog/products` | (part) |
| 17 | `StoreInfoController` | `/catalog/store-info` | (part) |
| 18 | `SitemapController` | `/catalog/sitemap` | (part) |
| 19 | `ContentPagesController` | `/catalog/pages` | `ContentEndpoints` (public) |
| 20 | `BlogController` | `/catalog/blog` | `BlogEndpoints` (public) |
| 21 | `StoreCartController` | `/store/cart` | `StoreEndpoints` (cart) |
| 22 | `StoreOrdersController` | `/store/orders` (place, get, transfer-reference) | `StoreEndpoints` (orders) |

Health checks, `robots.txt` and `/openapi` stay as plain mapped endpoints (they are not resources).

### A.6 Small-commit plan (one controller per commit)

0. `test(api): route table snapshot and characterization tests` (goldens + the edge-case tests; no production code)
1. `refactor(api): add controllers infrastructure (JSON options, filters, ResultActionResult)` — nothing is switched yet
2. `refactor(api): CSRF, storefront-key and validation as MVC filters` — registered but the minimal endpoints still use their own
3–24. `refactor(api): <Name>Controller replaces <X>Endpoints` — one per row of A.5, each deleting the endpoint block it replaces; the snapshot must not change
25. `chore(api): remove endpoint extension classes and golden OpenAPI file; document controller conventions`

Rollback is per commit (each is self-contained). Estimated effort: the mechanical part is small; most time goes into step 0 and the filter parity.

**Recommendation:** worth doing only because it is done **first and cheaply** (§D): the API is at its smallest now and every later feature (translations, routes, site config) is then written as controllers directly instead of being migrated twice.

---

## B. Translation table and `Translate` service

### B.1 Data model

`Translations`

| Column | Type | Rule |
|---|---|---|
| `Id` | int identity | |
| `Title` | `varchar(150)`, ASCII | the **key**, e.g. `cart.empty`; format below |
| `Culture` | `varchar(10)`, default `fa-IR` | BCP-47; only `fa-IR` is seeded now |
| `Text` | `nvarchar(2000)` | Persian text, normalized with the existing `PersianText.Normalize` |
| `DefaultText` | `nvarchar(2000)` | the seeded text (for "reset to default" and "edited" indicator) |
| `UpdatedAtUtc`, `UpdatedByUserId` | | audit basics |
| `RowVersion` | rowversion | 409 on conflict |

Unique index `(Title, Culture)`. `TranslationAuditLogs` (key, culture, old text, new text, who, when) — texts are not secret, so full values are kept.

### B.2 Key naming convention

`<area>.<thing>[.<variant>]`, lower-case ASCII, dot-separated, words joined with `-` only where unavoidable; no sentences in keys; the key never changes once released (rename = new key + migration of the row).

| Prefix | Used by | Examples |
|---|---|---|
| `api.error.*` | problem titles / `Error.Message` | `api.error.not-found`, `api.error.concurrent-update`, `api.error.step-up-failed` |
| `api.validation.*` | FluentValidation / domain validation | `api.validation.required`, `api.validation.max-length`, `api.validation.slug-invalid` |
| `api.field.*` | property labels used inside validation messages | `api.field.mobile`, `api.field.new-password` (replaces the `PersianValidation.Labels` dictionary) |
| `common.*` | shared UI words | `common.save`, `common.cancel`, `common.loading` |
| `shop.<page>.*` | storefront pages | `shop.cart.empty`, `shop.checkout.place-order`, `shop.product.out-of-stock` |
| `admin.<area>.*` | admin panel | `admin.orders.confirm-payment`, `admin.settings.card-number` |
| `seo.*` | default titles/descriptions, only where not admin data | `seo.home.title` |

Placeholders are **named and ASCII**: `{count}`, `{max}`, `{field}`. Numbers and dates are formatted by code (Persian digits, Toman) **before** substitution, never inside the translation; no plural logic in texts (separate keys, e.g. `shop.cart.items-one`/`-many`, only if a real need appears).

### B.3 The `Translate` service

- **Contract** (Application layer, no EF): `string T(string key, params (string Name, object? Value)[] args)` and `string T(string key, string culture, …)`. The default culture comes from configuration (`fa-IR`); a request culture is a later extension.
- **Cache**: an immutable in-memory snapshot (`Dictionary<(culture,key), string>`) swapped atomically. It is loaded at startup, rebuilt after every admin edit (in-process, immediately), and for multi-instance safety each instance re-checks `COUNT(*), MAX(UpdatedAtUtc)` every 30 s and reloads on change. Lookups are lock-free and allocation-light.
- **Domain/Application stay pure**: they must not call a database service. Today `Domain` returns finished Persian strings (e.g. `BlogPost.Validate`). The change: domain and application results carry **`MessageKey` + `Args`** (extend `Error`: `Code`, `MessageKey`, `Args`); the translation happens in one place at the API boundary (`ToProblem`, the validation filter, the global exception handler). FluentValidation messages become keys too: a custom `ILanguageManager`/message factory resolves the key and the label key through the same service, replacing `PersianValidation` and `PersianProblemText`.
- **Missing key / missing culture**: (1) try the culture, (2) fall back to the **compiled-in default** (the seed manifest embedded in the assembly), (3) if even that is absent return a neutral generic Persian text (`api.error.unexpected`) — never the raw key, never an exception. Every miss is **logged once per key** (Warning, key + culture; operator-language English). A unit test fails the build if code references a key that is not in the manifest, so a miss in production can only come from a deleted row.
- **Storefront/admin**: see B.5.

### B.4 Seeding from today's hard-coded strings

1. A single manifest `translations.fa-IR.json` (embedded resource, the only place the default texts live) — `{ "cart.empty": "سبد خرید شما خالی است." … }`.
2. Seeder at startup: **insert if missing**, never overwrite an admin edit; it also refreshes `DefaultText` so "reset to default" always shows the current shipped text. Idempotent and fast (one query).
3. Extraction is done by hand in small commits **per area** (the strings are ~350 server lines and ~400 storefront lines): replace the literal with the key, move the text into the manifest, keep behavior byte-identical. A test asserts that every Persian literal remaining in the converted files is intentionally allow-listed (e.g. the `[پیش‌نویس — بازبینی شود]` draft marker lives in a `content.*` key too).
4. M-9 content seeds (info page bodies) are **data**, not translations: they stay in `ContentSeeds` and are edited through the content editor.

### B.5 How the storefront and admin load texts

- **Server-side only**: `getTexts(namespace)` calls `GET /api/v1/catalog/translations?prefix=shop.&culture=fa-IR` (public; `common.*`, `shop.*`, `seo.*`) or `GET /api/v1/admin/translations/bundle?prefix=admin.` (requires an admin session; `admin.*`). One JSON map, `ETag`, `Cache-Control: public, max-age=30`.
- Next data cache with **tag `translations`**, `revalidate: 60`; the admin edit action calls `revalidateTag('translations')` so the instance that served the edit updates instantly, others within the TTL (same trade-off as store settings).
- A typed `t('shop.cart.empty')` generated from the manifest keys (a script writes `keys.ts`; an unknown key is a compile error). Server components call `t()` via a `React.cache`d bundle; client components receive the few strings they need as props or through a provider scoped to a prefix, so the browser never downloads the whole table.
- **Resilience**: a bundled copy of the manifest (generated at build time from the same JSON) is the fallback when the API is down or a key is absent — the storefront never shows a blank label and never shows a key. A missed key logs once on the server.
- The admin error mapping (`lib/admin/form-state.ts`) keeps showing only Persian text from the API `title`/`errors`; it no longer needs its own message table.

### B.6 Admin editor

- Page `/admin/translations`, permission **`Translations.Manage`** (new; Admin role gets it; seeded like the others).
- Table of keys with: key, current text, default text, "edited" badge, last editor/time. **Search** by key or text (server-side `LIKE`, paged), filter by prefix and by "edited only" / "missing".
- Inline edit; Save → `PUT /api/v1/admin/translations/{id}` with the row version (**409** → the standard "data changed" flow), **CSRF** like every admin action.
- Validation: not empty; ≤ 2000 chars; **placeholder set must equal the default's** (cannot drop `{count}` or add an unknown one); plain text only — rendered escaped, never as HTML or Markdown; normalized with `PersianText.Normalize`.
- "Reset to default" per key; **audit** list per key (who/when/old/new).
- No create/delete in the UI: keys come from code. (A key removed from the manifest is flagged "unused" and can be pruned by a command.)

### B.7 What is NOT translated

Product, category, brand, blog and content-page data (they are DB data with their own editors); operator-facing text — startup/validation-of-configuration errors, structured logs, CLI output (`create-admin`), exception messages; API `code` values, enum names, JSON property names, slugs, HTTP reason phrases; audit `Action` codes; test names; `docs/`. Only user-facing text is translated.

### B.8 Small-commit plan

1. `feat(i18n): Translation entity, audit log and Translations.Manage permission`
2. `feat(i18n): EF configuration and migration for Translations`
3. `feat(i18n): translation cache and Translate service with fallback and miss logging` (+ unit tests)
4. `feat(i18n): manifest seeding (insert-if-missing) and key-usage test`
5. `feat(i18n): carry message keys in Error and translate at the API boundary` (ToProblem, exception handler, validation filter)
6. `refactor(i18n): FluentValidation messages and labels from keys` (retires `PersianValidation`)
7–12. `refactor(i18n): <area> messages use keys` — accounts, catalog, orders, content/blog, settings/security, framework titles (`PersianProblemText`)
13. `feat(i18n): public and admin translation endpoints`
14. `feat(i18n): admin translation management endpoints (search, edit, reset, audit)` (+ integration tests: 409, permission, placeholder rule)
15. `feat(storefront): translation client, typed keys and bundled fallback`
16–20. `refactor(storefront): <page group> texts via t()` — shop pages, checkout/order, header/footer, SEO defaults, account
21–24. `refactor(admin-ui): <area> texts via t()` — shell/login, catalog, orders, content/blog/settings
25. `feat(admin-ui): translations editor page`
26. `docs: translation guide (keys, seeding, adding a text)` + PROGRESS

Series gates: after commit 6 (backend core), after 14 (backend complete), after 20 (storefront), after 26 (everything). Behavior check each time: a **snapshot test of every API error/validation response body** taken before the work and compared after (the texts must be byte-identical until someone edits them).

---

## C. Dynamic routing (admin-managed URL paths)

### C.1 Your interpretation — confirmed, with six changes

Confirmed: paths of categories, products, info pages and blog posts are managed from the admin; the storefront uses a catch-all that asks an API **resolve** endpoint what a path points to; the API owns a route table; changing a path creates a permanent redirect from the old one; conflicts are rejected.

Proposed adjustments:

1. **Redirects are rows, not chains.** Old paths stay in the same table as non-current rows pointing at the *same entity*, so `old → entity → current path` is always **one hop**; A→B→C chains and loops cannot exist by construction.
2. **301 vs 308.** Next's `permanentRedirect()` sends **308**; Google treats 308 and 301 the same for SEO and 308 also preserves the method. A true 301 would require the resolve call inside `proxy.ts` for every request. Recommendation: **308**. (Decision in D.)
3. **Existing URLs are seeded unchanged** (`/category/x`, `/product/x`, `/blog/x`, `/about`) as the first current paths — turning the feature on moves nothing and breaks no existing link.
4. **Entities expose `path` in every DTO**; the storefront and sitemap never build URLs from slugs again (§C.6).
5. **Reserved prefixes** (the static routes) can never be taken (C.3).
6. **ASCII-only paths in v1** (lower-case letters, digits, `-`, `/`), so URLs stay shareable and never need percent-encoding; Persian paths are a decision (D).

The entity `Slug` stays as its internal, stable identifier (admin lists, `/catalog/products/{slug}` API lookups, tests) and as the **default** path component; the public URL is the route-table path.

### C.2 Tables

`RoutePaths`

| Column | Meaning |
|---|---|
| `Id` | |
| `Path` | normalized path, e.g. `/category/chandeliers` or `/lustre/crystal`; `varchar(200)` with a **case-insensitive, accent-insensitive-off** collation; **unique** across all rows (current and old) |
| `TargetType` | `category` · `product` · `contentPage` · `blogPost` (reserved: `brand`, `blogIndex`) |
| `TargetId` | int |
| `IsCurrent` | bit — exactly one current row per target (filtered unique index on `(TargetType, TargetId) WHERE IsCurrent = 1`) |
| `CreatedAtUtc`, `CreatedByUserId`, `RowVersion` | who/when; `RowVersion` for 409 |

`RouteAuditLogs`: who, when, target, `oldPath → newPath`, action (`created`, `changed`, `released`, `deleted-with-target`).

Rules (enforced in a domain `RoutePath` value object + DB constraints):
- Normalization: lower-case, trimmed, leading `/`, no trailing `/`, no `//`, only `[a-z0-9-]` segments, 1–4 segments, ≤ 200 chars, no segment starting/ending with `-`.
- **Reserved first segments**: `admin`, `api`, `cart`, `checkout`, `order`, `account`, `search`, `media`, `_next`, `health`, `robots.txt`, `sitemap.xml`, `favicon.ico` (and `blog` as a bare path). A test reads the storefront `app/` directory and fails if a static route exists that is missing from this list.
- **Conflicts rejected** with a Persian 409 naming what owns the path ("این نشانی برای «…» استفاده می‌شود"): a path equal to any current path, **or to an old path of a different entity**. The same entity can re-take one of its own former paths (it flips `IsCurrent`). Taking a path currently used as a redirect by another entity is possible only after an explicit admin action "release this old redirect" (audited).
- Resolution is an exact match on the full path, so `/lustre` and `/lustre/crystal` are independent rows; no prefix rule is needed.
- Changing a path: one transaction — old current row becomes `IsCurrent = 0` (kept), new/reused row becomes current, audit written, caches invalidated. **No automatic deletion of redirects** (inbound links and search results live for years); the admin may delete a stale redirect explicitly. Cap: 30 old paths per entity.
- Deleting/deactivating an entity: its current path stops resolving (404, or **410** for a deleted product — decision), redirects to it are removed with it; an inactive product keeps its rows so reactivation restores the URL.
- Home `/` and `/blog` (index) are fixed special routes, not in the table.

### C.3 Endpoints

| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /api/v1/catalog/resolve?path=/x/y` | public | → `{ "kind":"category\|product\|contentPage\|blogPost", "id":…, "slug":"…", "path":"/current" }` (200), or `{ "kind":"redirect", "to":"/current", "status":308 }`, or 404. Path is normalized and length-limited server-side; query string never reaches it |
| `GET /api/v1/catalog/routes` | public | the whole current-path list for the sitemap and for link building (small; cached; ETag) |
| `GET /api/v1/admin/routes?type=&targetId=` | `Routes.Manage` | current + old paths of an entity |
| `PUT /api/v1/admin/routes/{type}/{id}` | `Routes.Manage` | set the current path (body: `path`, `rowVersion`); 409 on conflict/stale version |
| `DELETE /api/v1/admin/routes/{id}` | `Routes.Manage` | delete an **old** (non-current) redirect |
| `POST /api/v1/admin/routes/{id}/release` | `Routes.Manage` | release a redirect so another entity can take the path |

Each admin entity editor (category, product, content page, blog post) gets a "نشانی (URL)" field showing the preview of the full URL, the list of old paths with a delete button, and a warning that changing the path creates a redirect. Permission `Routes.Manage` (or reuse each entity's manage permission — decision; recommendation: **reuse**, a separate permission only for "release/delete redirects"). CSRF, 409 and audit as everywhere.

### C.4 Storefront

- `app/(shop)/[[...path]]/page.tsx` (optional catch-all). Next resolves **static routes first** (`/cart`, `/checkout`, `/order/…`, `/account`, `/search`, `/admin`, `/api`, `/media`, …), so the catch-all only sees what nothing else claims.
- The page calls `resolve(path)` and renders the **existing** server components for the kind (`CategoryView`, `ProductView`, `InfoPageView`, `BlogPostView`, extracted from today's pages). `redirect` → `permanentRedirect(to)` (preserving the query string); 404 → `notFound()`; API unavailable → the existing error boundary (**never** cache or serve a 404 because the API was down).
- `generateMetadata` uses the resolved current `path` for the **canonical** and `og:url`; a request for any non-current path never renders — it redirects — so duplicate content cannot exist.
- Old route files (`/category/[slug]`, `/product/[slug]`, `/blog/[slug]`, `/[slug]`) are removed in the final step; until then they co-exist and resolve through the same table so the switch is reversible.

### C.5 Caching

- API: the route table is held in memory as two dictionaries (`path → row`, `(type,id) → current path`) rebuilt on every write (same invalidation pattern as store settings) plus a 30 s version check for other instances; `resolve` is O(1), no SQL per request.
- Storefront: `resolve` uses Next's data cache with **tag `routes`**, `revalidate: 60`; the admin action that changes a path calls `revalidateTag('routes')` (and `sitemap`). Worst case on another instance: ≤ 60 s where the old path still renders — harmless, because the canonical already points at the new one.
- Pages that render the entity use their existing caches (60/300 s); `resolve` + detail = two cached calls per page view.

### C.6 Sitemap, canonical, internal links

- `/sitemap.xml` lists **only current paths** (from `GET /catalog/routes`), with `lastmod`; redirects are never listed; entity DTOs, breadcrumbs, JSON-LD (`Product`, `BreadcrumbList`, `Organization`), `og:url`, product cards, the header menu and the site-config links (docs/10) all use `path` from the API.
- Canonical is self-referential on the current path; paths are lower-case so case variants (`/About`) redirect to the canonical form.
- SITE_ENV rules (M-10) are unaffected: noindex/robots logic is independent of routing.

### C.7 Risks and mitigations

| Risk | Mitigation |
|---|---|
| A path change loses SEO / breaks inbound links | automatic permanent redirect, never auto-deleted, one hop, tested |
| Catch-all swallows typos or static pages | static routes win in Next; reserved-prefix test; unknown path → proper 404 |
| Extra latency/outage coupling (resolve + detail) | in-memory table, tagged cache, both calls cached; API-down shows the error page, not a 404 |
| Stale cache after a rename on other instances | canonical tag + ≤ 60 s TTL; tag invalidation on the editing instance |
| Admin creates a confusing or colliding path | normalization + reserved + uniqueness, Persian error naming the owner |
| Redirect table growth | cap per entity, delete action, audit |
| Product URLs getting unstable because of category moves | paths are independent of the category tree; changing a category never rewrites product paths |
| Persian slugs requested later | the normalization is the single place to extend (percent-encoding and canonical form decided then) |
| 308 vs 301 | decision D-C1 |

### C.8 Small-commit plan

1. `feat(routes): RoutePath value object with normalization and reserved-prefix rules` (+ unit tests)
2. `feat(routes): RoutePaths entity, audit log and Routes.Manage permission`
3. `feat(routes): EF configuration and migration (filtered unique indexes)`
4. `feat(routes): seed existing slugs as current paths` (idempotent backfill; the same step runs for new entities)
5. `feat(routes): route service with in-memory table and change-path transaction`
6. `feat(routes): create a route when a category, product, page or post is created` (service hooks)
7. `feat(routes): resolve, routes and admin endpoints (as controllers)`
8. `test(routes): integration tests (conflicts, reserved, one-hop redirects, 409, permissions, caching)`
9. `feat(catalog): expose path in category, product, blog and page DTOs`
10. `feat(storefront): resolve client and entity views extracted from the pages`
11. `feat(storefront): optional catch-all route with permanent redirects`
12. `feat(storefront): sitemap, canonical and internal links from path` (+ tests)
13–16. `feat(admin-ui): URL field and redirect list in <category|product|content|blog> editor`
17. `chore(storefront): remove the per-type route files`
18. `docs: routing guide and PROGRESS`

---

## D. Order of implementation

**A → B → C → site customization (docs/10)**, each as its own series with the full test suites and a browser smoke before the push.

| Step | Why here |
|---|---|
| **1. A — controllers** | Pure refactor, no behavior change; cheapest when the API is smallest; everything after is written as controllers, so nothing is migrated twice. The golden route snapshot also becomes a permanent regression test for every later endpoint (B, C and docs/10 each add lines to it deliberately). |
| **2. B — translations** | Touches every message, so it must precede the large new UI (routes editors, the whole site-customization admin) — otherwise those screens are written with literals and converted afterwards. Also changes `Error` (keys + args), which C and docs/10 build on. |
| **3. C — routing** | Changes URLs, DTOs (`path`), sitemap and canonical. Site customization needs it: menu items, hero slides, banners and footer links should **pick an entity/route** instead of typing a path, and links stay valid when a path changes. |
| **4. docs/10 — site customization** | The biggest feature, last, so it uses controllers, translation keys (`admin.site.*`, `shop.*`) and route paths from its first commit. Its design is amended before it starts: error/UI texts as keys, link fields as "route picker or external https", Site.Manage audit unchanged. |

Dependencies between series: B needs A's filter/exception-handler placement; C needs B's `Error` keys for its Persian conflict messages; docs/10 needs C (route picker) and B (texts). Approximate size: A ≈ 25 commits, B ≈ 26, C ≈ 18, docs/10 ≈ 21 — about 90 small commits in four series. A and B are internal and invisible to the owner; the first thing the owner sees is C (URL control) and then the site customization, so if there is schedule pressure the cut line is: **do docs/10 before A+B only if the owner needs theme/home-page control soon** — at the cost of writing those admin screens twice (literals now, keys later).

Alternative order considered: B before A (translations first, since it is the largest invisible change) — rejected because B's boundary work (`ToProblem`, validation filter) is exactly the code A moves; doing A first means B edits controller-era code once.

---

## E. The 10 open decisions from `docs/10-site-customization.md` — recommendations

(Updated with facts found since: the blog now exists on `main`, and the catalog already has a `CompareAtPrice` "قیمت قبل از تخفیف".)

| # | Question | Recommendation |
|---|---|---|
| 1 | Brand **English name** | Use only «قصر روشنایی» everywhere. Leave the English name an *optional empty* SEO field until the owner states the official spelling; do not publish a transliteration as if official. |
| 2 | **Logo** (300×70 PNG, light/gold lettering made for a dark header, baked shadow) | Use it as an interim asset **only after the owner confirms they own it**, on a dark header band (the palette's `#5b0057`/`#40033d` works). Ask for an original hi-res PNG ≥ 600 px wide (transparent) or a vector for later; SVG upload stays unsupported in the MVP. Commit `docs/brand/logo.png` only after that confirmation. |
| 3 | **Favicon** (none exists) | Owner supplies a square PNG ≥ 192 px. Until then ship **no** favicon (browser default) — do not generate an invented monogram. |
| 4 | **Palette** | Keep ghr-co exactly: primary `#5b0057`, secondary `#76007c`, accent `#d8c831` (fill with dark text only), surface `#f2f2f2`; text `#1a1a1a` instead of pure black (same look, less harsh, passes AA by a wide margin). success/warning/danger stay my proposed values and are verified by the validator. |
| 5 | **Discounted-products row** | **Yes.** The catalog already has `CompareAtPrice`, so "discounted" = a variant whose compare-at price is above its price; no new concept is needed. |
| 6 | **Free-shipping threshold** (old site: free above 3,000,000 Toman, only in branch cities with own delivery) | **Do not seed it.** It is a conditional business commitment; the MVP has one store-wide rule. The owner sets it in *Store settings* in one minute if they want it store-wide. |
| 7 | **Promise boxes and about text** (warranty ≥ 1 year, 3 years service, free installation) | Seed them into the home page **as a disabled text block carrying the draft marker**, so nothing goes live that the owner has not confirmed; they enable it after review (same rule as the M-9 drafts). |
| 8 | **Blog («تازه‌ها»)** | The blog **now exists**, so keep a «وبلاگ» item in the menu/footer (already linked by the cloud-merged commits). Do **not** import the four 2019 posts from the old site (stale, and copying them would duplicate content with the old domain). Amend docs/10 §2 accordingly. |
| 9 | **Trust badges** (no enamad/samandehi found) | Build the feature, show nothing until IDs are entered. Ask the owner whether the shop has an enamad registration — Iranian online shops are commonly expected to display one — and enter the ID/code when they have it. |
| 10 | **Font allow-list** | Default **Vazirmatn** plus three more: **Estedad**, **Shabnam**, **Sahel** — four files to bundle and verify, enough variety. Each file's OFL license is confirmed at implementation; any font that cannot be confirmed is dropped. |

## F. Decisions needed for this document

| ID | Question | Recommendation |
|---|---|---|
| A-1 | Do the controller migration at all? | Yes, **first** (§D), with the snapshot + OpenAPI proof; skip it only if the risk of touching all 86 endpoints is not worth the convention gain. |
| B-1 | Translate the admin UI too, or only the storefront and API? | Both (same mechanism, `admin.*` prefix); an editor for admin labels is cheap once the machinery exists. |
| B-2 | Keep M-9 info-page bodies as data (not translations)? | Yes — they are content with their own editor and versioning. |
| C-1 | 308 (`permanentRedirect`) or true 301? | **308**: identical for search engines, no extra work in the proxy. Choose 301 only if a non-Google consumer requires it (costs a resolve call in `proxy.ts` on every request). |
| C-2 | Persian (non-ASCII) paths? | Not in v1; ASCII only. |
| C-3 | Deleted products: 404 or 410? | **410** for a hard-deleted product/post (tells search engines to drop it faster); 404 for everything else. |
| C-4 | Separate `Routes.Manage` permission? | Reuse each entity's manage permission; add `Routes.Manage` only for releasing/deleting redirects. |
| D-1 | Order A → B → C → docs/10? | Yes; swap docs/10 earlier only if the owner needs theme/home control soon, accepting rework. |
