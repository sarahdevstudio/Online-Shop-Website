# E-commerce Platform: Analysis & Architecture Decision Record

Status: **DRAFT for approval. No implementation has started.**
Basis: direct inspection of `D:\src\os\iCMSCore` (branch `Amirhassan/Feature/Sign-up`, 2026-10-03).
Items marked **[unverified]** were not confirmed in code and must be checked before they are relied on.

---

## 1. Current architecture summary

The repository is **Ostad Salam**, an education marketplace (teachers, courses, organizations, booklets, tutoring requests). It also contains a **smaller, already-working Store module** (products, cart, checkout, orders). It is not a generic CMS.

| Project | What it is (verified) |
|---|---|
| `WebApp.Common` | Constants, enums, `MainInfo` (config loaded from `maininfo.json`), helpers, image libs (Magick.NET, ImageSharp, LibHeif), AWS S3 |
| `WebApp.DataModel` | ~556 files: 126 entities, DTOs (`Api/*`), view entities, models |
| `WebApp.DataAccess` | `EFContext` (430 lines), `IGenericRepository<T>`, `IUnitOfWork` with one property per entity, **two implementations** (EF and Dapper) |
| `WebApp.Facade` | "BUC" business classes (`StoreProductBUC`, `StoreMemberOrderBUC`, ...) holding `IUnitOfWork`, returning `ResponseModel<T>` |
| `WebApp.Core` | `BaseController`, authorization handlers, JWT provider, routing and culture providers, `SiteMapHelper`, `RedirectionMiddleware`, DI wiring |
| `WebApp.Services` | Bank gateway, Google Calendar, online-class providers, AWS |
| `WebApp.Web` | ASP.NET Core (**net6.0**) MVC and Razor admin panels (`Controllers/Panel/*`, `Controllers/Client/*`) plus JSON API (`WebApi/FrontApi/*`) consumed by Angular |
| `WebApp.API` | CDN/file/minify controllers |
| `WebApp.Teaco` | A second site on the same core (a sibling tenant/brand) |
| `WebApp.Web.Angular` | **Angular 17.3 with SSR** (`@angular/ssr`, Express `CommonEngine`), `prerender: true`, hydration, lazy routes, about 110 standalone components, 17.7k lines of TS |
| `WebApp.UnitTest` | A single file, `UnitTest1.cs`. No Angular `*.spec.ts` files. **Effectively no automated tests.** |

Data flow: Angular SSR (Node/Express) calls `WebApp.Web/WebApi/FrontApi/*` (POST-heavy JSON, `ResponseModel<T>`). The same host serves MVC admin panels. Sessions plus cookies are used for panel auth. JWT is switchable via `AuthMode`.

## 2. Existing reusable components and features

### 2.1 Store module (verified)
- Entities: `StoreProduct`, `StoreCategory`, `StoreBrand`, `StoreProductPrice` (per color/size, with `Count`/`UsedCount`/`MaxOrder`), `StoreProductImage`, `StoreAttributeGroup/Attribute/Item`, `StoreProductAttribute`, `StoreProductTag`, `StoreMemberOrder(+Item,+Log)`, `StoreMemberLocation`, `StoreTransportation`, `StoreWarranty`, `StorePreOrder`, `StoreMemberCart`, `StoreTranslate`.
- `FrontApi/ProductApiController` (926 lines): list, tag list, detail, cart, location, payment, pay.
- Facade BUCs for all of the above (~5,900 lines).
- Angular: `pages/product/*` (list, detail, cart, location, payment, tag list, related), `product.service.ts`, `product.css`.
- Admin: `Controllers/Panel/Store/*` (Razor).

### 2.2 Cross-cutting pieces worth carrying over as *ideas or specs*
- **SEO service** in `method.service.ts` (~L619-700): title, description, canonical, full Open Graph, JSON-LD injection. Proven pattern, but it is a God-service.
- **Server-side 301 rules** in `server.ts` (underscore to hyphen) and `RedirectionMiddleware` plus the `PageRedirect` entity: redirect management is a real requirement.
- **Sitemap**: `SiteMapHelper` (backend) plus a sitemap-index pointing at API endpoints.
- **Comments, StarRate, Tag, Menu, OffCode (discount codes), Notification, SystemLog, TinyUrl, Translate**: the feature set maps onto reviews, tags, navigation, coupons and audit.
- **Image pipeline** (Magick/ImageSharp, HEIC, S3/Arvan CDN): reusable conceptually; replace with a storage abstraction plus WebP/AVIF derivatives.
- **Persian specifics**: Jalali date, RTL, `ng-persian-datepicker`, Iranian payment gateways (`BankProvider`, `yekpay-data.service.ts`).

## 3. Problems and technical debt (do not carry over)

| # | Finding | Evidence | Impact |
|---|---|---|---|
| 1 | **Secrets in git**: `maininfo.json`, `appsettings*.json` and `google_credentials.json` are tracked. `maininfo.json` holds a connection string and SMTP credentials. | `git ls-files` | **Critical.** Rotate all credentials; move to env/secret store. Do not copy these files into the new project. |
| 2 | **Oversell risk**: stock is decremented in `StoreMemberOrderBUC.InsertItemsAsync` *after* payment, with an **empty `catch {}`**, no transaction, no concurrency token, and via `Count`/`UsedCount` counters. | `StoreMemberOrderBUC.cs` | Lost stock updates, orders marked Done with inconsistent stock. |
| 3 | Order stores the cart as a JSON string (`Cart`) and status as raw `byte`/`Int16`. No explicit state machine. No separate Payment/Shipment entities. | `StoreMemberOrder.cs` | Weak integrity and reporting; payment state is not modelled. |
| 4 | Product has CSV-in-column fields (`ColorIDs`, `SizeIDs`, `WarrantyIDs`). Variants are implied by `StoreProductPrice` rows with nullable Color/Size. No SKU. | `StoreProduct.cs` | No FKs or integrity; cannot support N attributes or SKUs. |
| 5 | Business logic in controllers (`ProductApiController` 926 lines); BUCs swallow exceptions and return `ErrorMessage` strings. | controllers, BUCs | Not testable; errors hidden. |
| 6 | `IUnitOfWork` has ~100 properties; every repo is a thin generic wrapper over `DbSet`; duplicated EF and Dapper implementations; `includeProperties` is a magic string. | `IUnitOfWork.cs`, `EFGenericRepository.cs` | Boilerplate with no abstraction benefit; N+1 and over-fetch likely. |
| 7 | Authorization via custom handler matching `controller_action` strings and `LoginType` enums; session-based identity. | `AuthorizationProvider.cs` | Brittle; not claims/policy/permission based. |
| 8 | Platform rot: **net6.0 (EOL)**, EF Core 6.0.3, Angular 17 (EOL), `Microsoft.NETCore.App 2.2.8` and `Microsoft.AspNet.Mvc 5.3.0` references inside net6 projects, StackExchange.Redis 2.0.6, Hangfire 1.7. | `.csproj` files | Security patches missing; upgrade friction. |
| 9 | Distributed cache is `AddDistributedMemoryCache()`; Redis and Hangfire registration are commented out. | `Startup.cs` | Not actually distributed; no background jobs running. |
| 10 | CORS: `AllowAnyOrigin` plus manual wildcard headers; multipart limits set to `int/long.MaxValue`; `ShowErrors: true` in committed config. | `Startup.cs`, `maininfo.json` | Security and DoS exposure. |
| 11 | `robots.txt` is thin (`Disallow: /fa`, etc.). `sitemap.xml` has a static `lastmod` from 2023 and points to `frontapi.*` hosts. | `src/robots.txt`, `src/sitemap.xml` | The sitemap must be generated, not static. Cross-host sitemap entries are an SEO risk. |
| 12 | Catch-all route `:code` (NoteDetail) sits before `**`. Real HTTP 404 is signalled through `SeoModel.StatusCode` **[unverified that SSR turns it into a real 404 status]**. | `app.routes.ts` | Soft-404 risk. |
| 13 | Dead and duplicate files: `initialize.service copy.ts`, `serverBkp.ts`, large commented-out blocks in routes and Startup. | repo | Noise. |
| 14 | No CI, no tests, Dockerfile only for the web host. | repo root | No safety net for refactors. |

## 4. Angular and SEO assessment

**Verdict: KEEP Angular. Do not switch to Next.js or Nuxt.**

Evidence from the code:
- SSR is already wired: `@angular/ssr`, `CommonEngine`, `server.ts`, `provideClientHydration` with the HTTP transfer cache, `prerender: true`.
- A working SEO service already emits title, canonical, OG and JSON-LD.
- Server-side 301s exist and are tested in production.
- Routes are lazy standalone components. The code base is modern (standalone, not NgModules).
- The team already owns the Angular + Express + ASP.NET deployment shape. A framework change would add a rewrite cost with no SEO benefit, because Angular SSR gives crawlers fully rendered HTML.

What Angular must do better for e-commerce (all achievable without changing framework):

| Requirement | Plan |
|---|---|
| Currency of the platform | Upgrade to the current Angular LTS (17 is EOL). Verify the exact target at kickoff. |
| Per-route rendering | Use `RenderMode` route config (Angular 19+): **Prerender** for static pages (about, FAQ, policies); **Server** for product/category/blog (data changes); **Client** for cart, checkout, account. |
| Product/category/blog SEO | A route-level `SeoResolver`/`SeoService` fed by a single backend `SeoModel` (see §11). |
| Status codes | Set the real HTTP status from SSR (`RESPONSE_INIT`/response token) for 404 and 301/302. A soft 404 is a defect. |
| Core Web Vitals | `NgOptimizedImage` with responsive `srcset` and LCP priority; `@defer` for below-the-fold sections; drop duplicate carousels (both `swiper` and `ngx-owl-carousel-o` are present) and the Material prebuilt theme from the public bundle; preload only the LCP font and image; keep the `initial` budget enforced. |
| Caching | SSR output cached at the CDN/edge (`Cache-Control` + `stale-while-revalidate`) for anonymous traffic; no cookies on cacheable responses. |
| SSR safety | Never read `localStorage`/`window` outside `isPlatformBrowser`. The existing `LOCAL_STORAGE` token pattern is fine. |
| Admin | **Separate Angular app (client-rendered, no SSR)** in the same workspace. SEO does not apply, and it keeps the public bundle small. |

Re-open this decision only if a spike shows SSR TTFB or hydration cost cannot meet targets after caching. No evidence of that exists today.

## 5. Recommended frontend architecture

Angular workspace with three projects and a shared library:

```
web/
  projects/
    storefront/     SSR + selective prerender (public site)
    admin/          CSR only (back office)
    shared-ui/      design tokens, primitives (button, input, modal, price, rating)
    api-client/     generated typed client from OpenAPI (no hand-written DTOs)
```

- Standalone components, signals for local state, `resource`/`httpResource` or RxJS at the service boundary. No NgRx unless cart/checkout complexity proves it necessary.
- Feature areas (lazy): `catalog`, `product`, `cart`, `checkout`, `account`, `blog`, `content` (static pages), `search`.
- Cart state: server-owned cart (anonymous token cookie, merged on login). The client only mirrors it.
- Reuse (refactor, not copy): the SEO service logic, SSR cookie token, the HTTP interceptor ideas, bread-crumb, paging, star-rate, `auth.guard` concepts, `server.ts` redirect rules.
- Don't copy: `method.service.ts` as a God-service; the static `sitemap.xml`; the Persian-only assumptions unless the product is Persian-only (open question Q1).

## 6. Recommended backend architecture

**Modular monolith, Clean Architecture, new solution side by side with the legacy one.** Not a rewrite-in-place: legacy stays untouched and running.

```
src/
  Shop.Domain/          entities, value objects (Money, Slug), domain rules, state machines
  Shop.Application/     use cases (commands/queries), validators, ports (IPaymentGateway, IFileStorage, IClock)
  Shop.Infrastructure/  EF Core, caching, payment adapters, storage, email/SMS, background jobs
  Shop.Api/             HTTP endpoints, auth, versioning, ProblemDetails, OpenAPI
  Shop.Api.Tests / Shop.Domain.Tests / Shop.Integration.Tests
```

Decisions and why:

| Topic | Decision | Reason |
|---|---|---|
| Runtime | .NET current LTS (verify at kickoff; net6 is EOL) | Security and support |
| Repository + UoW | **No generic repository.** `DbContext` is already a UoW. Query side uses `Select` projections to DTOs; write side loads aggregates via `DbContext`. A named repository is added only for an aggregate that needs it. | The legacy generic repos add nothing and encourage over-fetch |
| CQRS / MediatR | Plain application-service classes or handlers; **no MediatR** | Avoids a dependency whose licensing changed **[verify]**; no pipeline need yet |
| Mapping | Explicit mapping/projection, not AutoMapper | Same licensing check; projections are clearer and EF-translatable |
| Validation | FluentValidation at the API boundary; invariants inside the domain | Clear benefit |
| Errors | Global exception handler to RFC 7807 ProblemDetails; Result type for expected failures; **no swallowed exceptions** | Fixes debt #5 |
| API style | Minimal APIs or controllers grouped per feature; REST with proper verbs; URL-segment versioning (`/api/v1`) only once a public contract exists | Legacy is POST-everything |
| Concurrency | `rowversion` on `InventoryItem`, `Order`, `Payment`; atomic conditional stock update (`UPDATE ... WHERE OnHand - Reserved >= @qty`) inside a transaction | Fixes debt #2 |
| Background jobs | Hosted service + outbox table first (emails, stock reservation expiry, sitemap rebuild, payment reconciliation). Add Hangfire/Quartz only if scheduling needs outgrow it. | Avoid unneeded tech |
| Caching | `HybridCache`/`IDistributedCache` backed by Redis in prod, memory in dev; output caching for anonymous catalog endpoints; explicit tag-based invalidation on write | Real distributed cache, unlike legacy |
| Logging | Serilog (or built-in logging) with structured logs, correlation id, OpenTelemetry traces and metrics | Observability |
| Files | `IFileStorage` port, with S3-compatible/Arvan/local adapters; derivatives generated on upload (WebP/AVIF, responsive sizes) | Reuse legacy S3 know-how |
| Payments | `IPaymentGateway` port; adapters per provider; idempotent callback handling; payment is a first-class entity | Debt #3 |
| Config/secrets | Environment variables + secret store; nothing secret in git | Debt #1 |

## 7. Recommended database architecture

SQL Server is assumed because the legacy system uses it **[confirm: Q4]**. EF Core with Fluent API configuration classes (no data annotations on entities).

### 7.1 Entities and relationships

**Catalog**
- `Category` (Id, ParentId, Slug *unique*, Name, Description, SortOrder, IsActive, Path/Depth for tree queries) - self-referencing.
- `Brand` (Id, Slug *unique*, Name, LogoMediaId).
- `Product` (Id, Slug *unique*, Name, ShortDescription, Description, BrandId, Status [Draft/Published/Archived], PublishedAt, DefaultCategoryId, `rowversion`).
- `ProductCategory` (ProductId, CategoryId) - M:N.
- `ProductAttribute` (Id, Code, Name, Type [Text/Number/Option], IsFilterable, IsVariantAxis) and `ProductAttributeOption` (Id, AttributeId, Value, SortOrder).
- `ProductAttributeValue` (ProductId, AttributeId, OptionId?/Value) - descriptive specs.
- `ProductVariant` (Id, ProductId, **Sku** *unique*, Barcode?, IsActive, Weight/dimensions, `rowversion`).
- `VariantOptionValue` (VariantId, AttributeId, OptionId) - the variant axes (e.g. color and size), replacing the CSV columns.
- `ProductImage` (Id, ProductId, VariantId?, MediaId, Alt, SortOrder, IsPrimary).
- `Price` (Id, VariantId, CurrencyCode, Amount, CompareAtAmount?, ValidFrom, ValidTo?) - a price list that is a *separate* table so scheduled prices and history work. A current-price lookup is indexed on (VariantId, ValidFrom).
- `InventoryItem` (VariantId PK/FK, WarehouseId?, OnHand, Reserved, `rowversion`) plus `StockMovement` (append-only ledger: reason, delta, reference).

**Promotions**
- `Discount` (rules: percent/fixed, scope [product/category/cart], dates, usage limits) and `Coupon` (Code *unique*, DiscountId, per-user/total limits, `CouponRedemption`).

**Customers**
- Identity tables (`User`, `Role`, `UserRole`, `Permission`, `RolePermission`, refresh tokens).
- `Customer` (UserId 1:1, profile) and `Address` (CustomerId, recipient, region, city, postal code, phone, IsDefault).
- `WishlistItem` (CustomerId, ProductId) *unique pair*.

**Ordering**
- `Cart` (Id, CustomerId? / AnonymousToken, ExpiresAt) and `CartItem` (CartId, VariantId, Qty) *unique (CartId, VariantId)*.
- `Order` (Id, Number *unique*, CustomerId, Status, totals, Currency, **snapshot** of shipping and billing address, `rowversion`) and `OrderItem` (OrderId, VariantId, **snapshot** of name/SKU/unit price/tax/discount, Qty).
- `OrderStatusHistory` (OrderId, From, To, At, By, Note).
- `Payment` (OrderId, Provider, Amount, Status [Initiated/Pending/Captured/Failed/Refunded/PartiallyRefunded], ProviderReference *unique per provider*, IdempotencyKey) and `PaymentAttempt`/webhook log.
- `Shipment` (OrderId, Carrier, TrackingNumber, Status) and `ShipmentItem`.
- `ShippingMethod` / `ShippingZone` (the legacy `StoreTransportation` concept).

**Content and SEO**
- `ProductReview` (ProductId, CustomerId, Rating 1-5, Body, Status [Pending/Approved/Rejected], VerifiedPurchase) *unique (ProductId, CustomerId)*.
- `BlogPost` (Slug *unique*, Title, Body, Excerpt, AuthorId, Status, PublishedAt, CoverMediaId), `BlogCategory`, `BlogTag`, `BlogPostTag`.
- `Media` (Id, StorageKey, MimeType, Size, Width, Height, Alt, Hash) - one table for all uploads.
- `SeoMetadata` (EntityType, EntityId, MetaTitle, MetaDescription, CanonicalUrl?, RobotsIndex, RobotsFollow, OgImageMediaId?) *unique (EntityType, EntityId)*. A single polymorphic table avoids repeating the same 6 columns on 5 tables.
- `SlugRedirect` (OldPath *unique*, NewPath, StatusCode) - generalises legacy `PageRedirect`; auto-written when a slug changes.
- `ContentPage` (Slug, Title, Body; about/FAQ/policies), `FaqItem`, `Banner`, `HomeSection`, `Menu`/`MenuItem`, `SiteSetting` (key/value).
- `AuditLog`.

### 7.2 Rules that must hold
- Prices, stock and order lines are **snapshotted** into the order so later catalog edits never change history.
- Order and payment transitions are enforced by a domain state machine; the DB stores the state and the history.
- Money is `decimal(18,2)` or minor units + `CurrencyCode`. Never `float`. (If the currency is IRR, use minor-unit-free integers; see Q1.)
- Indexes: unique slugs per type; `(CategoryId, Status, PublishedAt)` for listings; filtered index on `Status = Published`; `(VariantId)` on prices/inventory; `(CustomerId, CreatedAt desc)` on orders; full-text or an external search index for product search.
- Soft delete only where history matters (products, customers); never on orders.

## 8. API architecture

- Versioned REST: `/api/v1/...`, JSON, ProblemDetails errors, OpenAPI document as the source of truth for the generated Angular client.
- Public: `GET /products`, `/products/{slug}`, `/categories`, `/categories/{slug}/products`, `/search`, `/blog/...`, `/pages/{slug}`; cart (`/cart`, `/cart/items`), `/checkout`, `/orders`, `/account/...`, `/reviews`, `/wishlist`.
- Admin: `/api/v1/admin/...` with permission policies; separate route group and OpenAPI document.
- Listings: server-side pagination (cursor or page+size with a hard max), filter and sort with a whitelist of fields, facet counts from a dedicated query.
- Every catalogue response includes a `seo` block: title, description, canonical, robots, OG, breadcrumbs, JSON-LD-ready data. The SSR app does not compute SEO itself.
- Idempotency-Key header on checkout and payment initiation.

## 9. Authentication and authorization

- **Storefront**: HttpOnly, Secure, SameSite cookie auth issued by the API (BFF-style via the SSR server forwarding cookies; the legacy `APP_SSR_COOKIES` token already does this). It avoids tokens in `localStorage`, and cookies work with SSR.
- **Admin**: same identity, separate policies. Cookie plus antiforgery, or short-lived JWT + refresh rotation if a mobile or third-party client is added later.
- ASP.NET Core Identity (or equivalent) with password hashing, lockout, email/phone verification, rate limiting on login and OTP.
- **Authorization**: claims-based **permission policies** (`Products.Write`, `Orders.Refund`, ...) mapped from roles in the DB, evaluated via `[Authorize(Policy=...)]`. This replaces the legacy action-name string matching.
- CORS: explicit origins. Antiforgery on cookie-authenticated mutations. Security headers and CSP. Rate limiting on auth, cart and checkout.

## 10. Order and payment lifecycle

```
Order:   PendingPayment -> Paid -> Processing -> Shipped -> Delivered -> Completed
                |             \-> Cancelled / Refunded / PartiallyRefunded
                \-> Expired (reservation timeout)
Payment: Initiated -> Pending -> Captured | Failed ; Captured -> Refunded/PartiallyRefunded
```
Checkout flow: validate cart, **reserve stock in a transaction** (conditional update), create order + payment, redirect to gateway, verify the callback idempotently, confirm stock (convert reservation to deduction), emit outbox events (email/SMS). A background job releases expired reservations and reconciles pending payments.

## 11. SEO architecture

SEO is data, owned by the backend, and rendered by SSR:

| Concern | Design |
|---|---|
| URLs | `/p/{slug}` or `/product/{slug}`, `/c/{category-path}`, `/blog/{slug}`; lowercase, hyphenated, no IDs required; unique slug per type; slug history in `SlugRedirect` |
| Meta/OG/Twitter | From `SeoMetadata` with sensible fallbacks (name + brand, trimmed description, primary image 1200x630) |
| Canonical | Always absolute, from the API (strips tracking and filter params; page N canonicalises to itself) |
| Pagination | Self-referencing canonical per page; crawlable `<a href>` links (not JS-only); `noindex` on deep filter combinations |
| Index control | `RobotsIndex/Follow` per entity; `noindex` for cart/checkout/account/search results; `X-Robots-Tag` on non-HTML |
| JSON-LD | `Product` (+`Offer`, `AggregateRating`, `Review`), `BreadcrumbList`, `BlogPosting`, `Organization`, `WebSite` (+`SearchAction`), `FAQPage` |
| Sitemap | Generated by the API, **same host as the site**, sitemap index split by type and by 50k URLs, real `lastmod` from `UpdatedAt`; regenerate via background job; cached |
| robots.txt | Generated; points to the sitemap; disallows cart/checkout/account/search params |
| Status codes | Real 404/410 for missing/unpublished; 301 for slug changes and trailing-slash/case normalisation; 302 only for temporary |
| Images | Alt text required in admin; width/height always set; WebP/AVIF; responsive `srcset` |
| Internal linking | Breadcrumbs, related products, category and brand links, "latest posts" widgets, all as real anchors |
| Rendering | Server-render product, category, blog; prerender static pages; hydrate |

## 12. Performance strategy

Measure first, then fix. Baselines to take on the *legacy* site before comparing: Lighthouse/CrUX (LCP, INP, CLS), SSR TTFB, API p95 on list/detail endpoints.

Planned, because they are standard for catalogues, not speculative:
- DB: covering indexes for listing queries, `AsNoTracking` + projection for reads, no lazy loading, split queries where collections are included, an N+1 test (query-count assertion) on list endpoints.
- API: output cache for anonymous catalogue endpoints; Redis for shared data (categories, menus, settings); response compression (Brotli).
- Search: start with SQL full-text + indexed filters; move to a search engine (Meilisearch/OpenSearch/Elasticsearch) only if facets or relevance demand it.
- Frontend: items in §4. CDN in front of static assets and (anonymous) SSR HTML; immutable hashed assets; `Cache-Control` set deliberately per response type.
- Load test the checkout path (stock contention) before launch.

## 13. Migration and reuse strategy

| Category | Item | Action |
|---|---|---|
| **A. Reuse directly** | Angular infra patterns: SSR cookie token, `LOCAL_STORAGE` token, hydration + transfer-cache config, lazy route layout; generic UI (paging, breadcrumb, star-rate, empty-result, skeleton ideas); font/RTL assets if Persian | Move into `shared-ui`/`storefront` after lint cleanup |
| **A.** | Business rules documented by legacy Store (shipping zones, pre-order, warranty, tags, brand, category tree) | Treat as the *requirements* source; re-implement |
| **B. Refactor before reuse** | SEO service (`method.service.ts`), `server.ts` redirect/preload logic, `SiteMapHelper` logic, `RedirectionMiddleware` / `PageRedirect`, image processing helpers, bank provider integration, Comment/StarRate, OffCode | Extract into focused services/ports with tests, then wire into the new solution |
| **C. Redesign** | Order/cart/stock/payment model; variants & pricing; authorization; repository/UoW layer; BUC pattern; API shape (POST-everything); config/secrets; caching; background jobs | New design per §6-§10 |
| **D. New** | Coupon engine, inventory reservation + ledger, shipment tracking, wishlist, verified reviews with moderation, SEO metadata admin, slug-redirect manager, JSON-LD builders, generated sitemap/robots, reports, RBAC permission admin, outbox + notifications, CI/CD, test suites | Build |
| **Do not touch** | The legacy solution (Ostad Salam). It keeps running unchanged. No project references from new code into legacy. | Isolation |

Data migration (only if the new store must inherit legacy products/orders **[Q3]**): one-way import tool reading legacy `Store*` tables to the new schema (CSV columns split into variant axes; `Cart` JSON expanded to `OrderItem`; legacy statuses mapped), run in staging first, idempotent, with row-count and sum reconciliation.

## 14. Implementation phases

| Phase | Scope | Exit criteria |
|---|---|---|
| 0 | Decisions Q1-Q6; secret rotation of the legacy repo's exposed credentials (independent of this project); repo + CI skeleton, analyzers, test projects | Approved ADR; CI green |
| 1 | Foundation: solution layout, EF + migrations, Identity, ProblemDetails, logging/OTel, cache, OpenAPI, Docker | Health endpoint, login works, tests run in CI |
| 2 | Catalog: categories, products, variants, prices, inventory, media, admin CRUD, public read APIs, SEO metadata | Product/category pages data-complete; query-count tests pass |
| 3 | Storefront shell + catalog pages with SSR, SEO service, sitemap/robots, JSON-LD, redirects, 404 | Rich-results validation; Lighthouse budgets; real HTTP statuses |
| 4 | Cart, checkout, stock reservation, order state machine, payment adapter (sandbox), emails | Concurrency tests (no oversell); idempotent callbacks |
| 5 | Customer account: orders, addresses, wishlist, reviews (+moderation) | Flows E2E-tested |
| 6 | Blog, content pages, banners/home management, FAQ, policies | Content editable without deploys |
| 7 | Admin: dashboard, orders, customers, coupons/discounts, roles/permissions, reports | Permission matrix tested |
| 8 | Hardening: load test, security review, CDN/caching, backups, monitoring, runbooks; optional legacy data import | Go-live checklist |

## 15. Risks and possible breaking changes

- **Scope**: this is a multi-month product. Phases 1-4 give the first sellable slice; I'd stop for review at each phase boundary.
- **Legacy secrets exposure** (debt #1) is a live risk regardless of this project; it needs action now.
- **Angular major upgrades** across several versions can break third-party packages (`ng-persian-datepicker`, `ngx-owl-carousel-o`, `ngx-cookie-service-ssr`); replace rather than upgrade abandoned ones.
- **Payment gateway** behaviour (callbacks, refunds, sandbox quality) is the most likely source of schedule risk.
- **Search/facets** may force an external search engine earlier than planned.
- **Data import** quality: legacy CSV/JSON columns may contain inconsistent data. Needs profiling before committing to the import.
- **Breaking changes to the legacy project**: none planned. The plan adds new projects only. If we later decide to share code (e.g. a payment adapter), that goes through a separately reviewed change.
- **Unverified**: real-404 behaviour under SSR; whether legacy product pages actually pass Core Web Vitals; Redis/Hangfire runtime status in production; Teaco's relationship to the store.

---

## Open questions that block the build (answers change the design)

- **Q1 Market/locale**: Persian/RTL and Iranian gateways/IRR only, or multi-language/multi-currency?
- **Q2 Business model**: single seller, or multi-vendor marketplace?
- **Q3 Legacy data**: start empty, or import legacy store products/orders/customers?
- **Q4 Hosting/DB**: SQL Server and the current hosting, or something else? Redis available?
- **Q5 Placement**: new solution in this repository (e.g. `/shop`) or a separate repository?
- **Q6 Payment gateway(s)** to integrate first.
