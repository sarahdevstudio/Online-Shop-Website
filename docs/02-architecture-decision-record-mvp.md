# E-commerce MVP: Architecture Decision Record (ADR)

Status: **PROPOSED, awaiting approval. No code, schema, dependency or configuration has been changed.**
Supersedes the architecture sections of `01-analysis-and-architecture.md` where they differ (this version is MVP-scoped and trimmed).
Evidence base: direct inspection of this repo. **[verify]** marks anything that must be checked against official docs or live systems before implementation.

Governing rule: *Simple → Clear → Maintainable → Extensible.* Every item below answers: "is this required for the MVP?"

---

## 0. Summary of decisions

| # | Decision | One-line reason |
|---|---|---|
| 1 | **Keep Angular** (upgrade to a supported release), SSR + selective prerender | Existing SSR/hydration/SEO code is real; no concrete problem a switch would solve |
| 2 | New **modular monolith** on a supported **.NET LTS**, single SQL Server DB | One deployable, clear module boundaries |
| 3 | **No generic repository, no UoW interface, no MediatR/AutoMapper**; use `DbContext` + small feature services | The legacy layers add boilerplate, not value |
| 4 | **Normalized variant model with EAV-style attributes** (no CSV columns) | New attribute types need no schema change |
| 5 | **Inventory reservation** with atomic conditional updates and `rowversion` | Fixes the legacy oversell defect |
| 6 | **Order / Payment / PaymentTransaction / Shipment** as separate tables with enum-backed states | Replaces JSON cart and raw `byte` status |
| 7 | **ASP.NET Core Identity + cookie auth + permission policies** | Standard, SSR-friendly, granular admin rights |
| 8 | SEO as data (`Slug`, `SeoMetadata`, `Redirect`) rendered by SSR; split into small services | Replaces the `method.service.ts` God-service |
| 9 | `IMediaStorage` port, derivatives generated at upload, local/S3-compatible adapters | Not coupled to S3/CDN |
| 10 | In-memory cache + HTTP caching in MVP; Redis only when running >1 instance | Avoid infrastructure the MVP doesn't need |
| 11 | Hosted-service background jobs (no Hangfire) | Three small jobs only |
| 12 | Tests prioritised on money/stock/auth/SEO paths | Legacy has none |
| 13 | **Legacy data is not migrated in the MVP** (mapping prepared, import deferred) pending approval | Avoids risk and scope |

---

## 1. Frontend framework decision

### 1.1 What exists (verified)
Angular **17.3.12** (installed), `@angular/ssr` + Express `CommonEngine` in `server.ts`, `prerender: true`, `provideClientHydration` with HTTP transfer cache, lazy standalone routes (~110 components), `SeoModel` from the API feeding title/canonical/Open Graph/JSON-LD, server-side 301 rules. Local toolchain: Node 24.19, npm 11.17.

### 1.2 Comparison

Scale: ● strong, ◐ adequate with work, ○ weak.

| Criterion | A. Current Angular 17 SSR | B. Modern Angular (upgraded) | C. Next.js | D. Nuxt |
|---|---|---|---|---|
| SEO (crawlable HTML) | ● | ● | ● | ● |
| SSR | ● (CommonEngine) | ● | ● | ● |
| Prerendering | ◐ (build-wide `prerender`, no per-route mode) | ● per-route `RenderMode` Server/Prerender/Client **[verify exact release]** | ● ISR/SSG | ● hybrid routeRules |
| Dynamic product/category pages | ● | ● | ● | ● |
| Blog pages | ● | ● | ● | ● |
| Structured data / metadata / canonical | ◐ works, but in a God-service | ● via small services + route resolvers | ● Metadata API | ● useHead/useSeoMeta |
| Sitemap | ◐ (static file today; must come from API) | ◐ same, API-generated | ◐ built-in helper or API | ◐ module or API |
| Core Web Vitals | ◐ heavy bundle today (Material prebuilt theme + two carousel libs) | ● with `NgOptimizedImage`, `@defer`, budgets, hydration improvements **[verify]** | ● `next/image`, RSC | ● `nuxt/image` |
| Image optimization | ◐ `NgOptimizedImage` not in use **[unverified]** | ● `NgOptimizedImage` + loader | ● built-in optimizer (needs Node runtime) | ● module |
| Developer complexity | ● team already knows it | ● small step | ○ new framework + React | ○ new framework + Vue |
| Migration cost | none | low-medium (version hops, replace stale libs) | high (rewrite ~110 components, 17.7k LoC TS) | high (same) |
| Reuse of existing Angular code | 100% | ~85% (services, models, routes, templates) | ~10% (API contracts, SEO rules, CSS) | ~10% |
| Long-term maintainability | ○ Angular 17 is out of support | ● | ● | ● |
| Integration with ASP.NET backend | ● (REST + cookie forwarding already works) | ● | ◐ extra Node tier, same as Angular SSR | ◐ same |

### 1.3 Decision
**Keep Angular. Upgrade to the current supported Angular release in a controlled step before new storefront work.**

- **Alternatives considered:** A (stay on 17), C, D.
- **Reason:** Angular SSR already delivers fully rendered HTML to crawlers; the SEO requirements (metadata, canonical, OG, JSON-LD, sitemap, robots, redirects, real status codes) are *backend data + SSR plumbing*, which any framework needs. No requirement in the brief is unachievable in Angular. The only real gaps are (a) Angular 17 is end-of-life, (b) bundle weight, (c) per-route render mode, and all three are solved by upgrading and refactoring.
- **Benefits:** reuse of ~85% of existing patterns, no retraining, one Node SSR tier already understood.
- **Risks:** multi-version upgrade breaks stale third-party packages (`ng-persian-datepicker`, `ngx-owl-carousel-o`, `ngx-cookie-service-ssr`, `@sweetalert2/ngx-sweetalert2`, `ng-apexcharts`); mitigated by replacing them (native CSS/`swiper`-only, small cookie helper) rather than upgrading.
- **Migration impact:** none to legacy. The new storefront is a **new Angular workspace** at the current version; legacy components are copied selectively after cleanup, not upgraded in place.
- **Re-open condition (the only one):** a spike on the new storefront shows TTFB/INP targets cannot be met with caching even after the modernization steps. At that point evaluate Next/Nuxt with concrete numbers. No such evidence exists today.

### 1.4 Target frontend architecture (MVP)

```
shop-web/
  projects/
    storefront/   SSR, per-route render mode, public site
    admin/        client-only (no SSR), back office
    shared/       models + generated API client + tiny UI primitives
```
- Standalone components, signals for local state, plain services over `HttpClient`. **No NgRx** in the MVP.
- Lazy feature routes: `catalog`, `product`, `cart`, `checkout`, `account`, `blog`, `pages`.
- Render modes: Prerender = static pages (about, FAQ, policies, contact); Server = home, category, product, blog; Client = cart, checkout, account.
- API client generated from the backend OpenAPI document (no hand-written DTO drift).
- SEO split into: `SeoService` (title/description/robots/OG/Twitter), `CanonicalService`, `JsonLdService` (+ small builders: product, breadcrumb, article, org/website), `ResponseStatusService` (SSR 404/301/410). A route resolver fills all of them from the API's `seo` block.
- Images: `NgOptimizedImage` with width/height and the API-provided derivative URLs; LCP image `priority`; below-fold sections `@defer`.
- Cart: server-owned; the client only mirrors it.
- Admin kept as a separate app so the public bundle never contains admin code. Use a simple table/form kit (Angular Material is acceptable in admin only).

---

## 2. Target backend architecture

### 2.1 Shape
**Modular monolith, one deployable, one database.** Layers with the dependency rule inward:

```
Shop.Domain          entities, enums, value objects, state-transition rules (no EF, no ASP.NET)
Shop.Application     feature services (use cases), validators, ports: IPaymentGateway, IMediaStorage, IEmailSender, IClock
Shop.Infrastructure  EF Core + migrations, Identity, payment adapters, storage, image processing, jobs
Shop.Api             minimal API/controllers, auth, ProblemDetails, OpenAPI, SEO endpoints
tests/               Domain.Tests, Application/Integration.Tests, Api.Tests
```
Modules (folders/namespaces inside those projects, not separate deployables): `Catalog`, `Inventory`, `Cart`, `Ordering` (orders+payments+shipments), `Customers`, `Content` (blog, pages, banners), `Seo`, `Media`, `Identity/Admin`.

- **Alternatives:** microservices; single-project "vertical slice" app; keeping the legacy 7-project layering.
- **Reason:** four projects keep the domain testable and infra swappable without ceremony; microservices add ops cost with no MVP benefit.
- **Risk:** boundaries erode. Mitigation: architecture tests (NetArchTest-style, **[verify package]**) enforcing "Domain references nothing" and "no module reaches into another module's tables".
- **Migration impact:** none to legacy; no project references between old and new.

### 2.2 Technology choices

| Concern | Decision | Notes |
|---|---|---|
| .NET | **Current supported LTS** (expected .NET 10) | **[verify]**: this machine has SDKs 6, 9 only (`dotnet --list-sdks`: 1.0, 5.0.408, 6.0.428, 9.0.317). Installing the LTS SDK is a prerequisite and needs approval. If not available, .NET 8 is a fallback but its support window ends soon **[verify date]** |
| Web API | Minimal APIs grouped per feature (or controllers if the team prefers); REST; `/api/v1` | Legacy is POST-everything; new uses proper verbs |
| ORM | **EF Core matching the .NET version**; Fluent API in `IEntityTypeConfiguration<T>` classes | No data annotations on entities |
| DB | SQL Server (legacy parity) **[confirm]** | Single DB |
| Reads | `Select` projections to DTOs, `AsNoTracking` | Replaces `includeProperties` magic strings |
| Writes | Load aggregate via `DbContext`, call domain method, `SaveChanges` in one transaction | `DbContext` is the unit of work |
| Validation | FluentValidation at API boundary; invariants in domain methods | Justified: many input forms |
| Errors | Global exception handler → RFC 7807 `ProblemDetails`; expected failures via a small `Result<T>` | No swallowed exceptions; unexpected errors logged and surfaced as 500 |
| API response | Success = plain DTO + proper HTTP status; error = ProblemDetails; list = `{ items, page, pageSize, total }` | Replaces `ResponseModel<T>` with in-body error strings |
| Logging | Built-in `ILogger` + Serilog sink (file/console) with correlation id | OpenTelemetry deferred unless ops asks |
| Mapping | Hand-written projections/mappers | AutoMapper/MediatR licensing changed **[verify]**; not needed |
| Caching | See §10 | |
| Background jobs | See §11 | |
| Files/images | See §9 | |

### 2.3 What replaces each legacy pattern

| Legacy | Replacement | Why it is better |
|---|---|---|
| ~100-property `IUnitOfWork` | `ShopDbContext` injected where needed; no interface wrapper | `DbContext` already is a UoW + repository; the wrapper hid nothing |
| Generic repository (`IGenericRepository<T>`) | Direct `DbSet` use in feature services; extract a named query class (e.g. `ProductListQuery`) for complex reads | No indirection to mock; tests use real SQL via Testcontainers or LocalDB |
| Dapper + EF duplicate repos | EF only; raw SQL/Dapper only for a proven hot query | One data-access path |
| Controller-heavy logic (`ProductApiController` 926 lines) | Thin endpoints → one feature service method per use case (`CheckoutService.PlaceOrder`) | Testable without HTTP |
| BUC classes with large logic | Feature services scoped to one aggregate/use case; behaviour on domain entities (`Order.MarkPaid()`) | Rules live next to data they protect |
| Magic-string `includeProperties` | Typed `Include(x => x.Variants)` or projections | Compile-time safety |
| Swallowed exceptions / `ErrorMessage` strings | Exceptions bubble to the global handler; domain rule failures return typed `Result` → 4xx ProblemDetails | Failures visible |
| CSV IDs in columns | Junction tables and attribute-value tables (§3.2) | Integrity, filtering, indexing |
| JSON `Cart` as order | Normalized `Order`/`OrderItem` with snapshot columns (§3.3) | Queryable, reportable, auditable |

---

## 3. Domain and database design (MVP)

Principle: create a table only if an MVP feature needs it. Below, **each entity** lists why, relationships, indexes, uniques and aggregate placement. Entities explicitly deferred are listed in §3.6.

### 3.1 Catalog

| Entity | Why | Relationships | Indexes / uniques | Aggregate |
|---|---|---|---|---|
| **Category** | navigation, listing, SEO | self-parent (`ParentId`); M:N to Product via `ProductCategory` | unique `Slug` (global); idx `ParentId`; `IsActive` | own root |
| **Brand** | filtering, SEO, product schema | 1:N Product | unique `Slug`, unique `Name` | own root (tiny) |
| **Product** | the sellable concept (name, description, SEO) | N:M Category; 1:N Variant, Image; N:M Tag; N:1 Brand | unique `Slug`; idx `(Status, PublishedAt)`; idx `BrandId`; `rowversion` | **root** of Product + Variants + Images + attribute values |
| **ProductCategory** | product in ≥1 category (one `IsPrimary`) | join | PK `(ProductId, CategoryId)`; idx `CategoryId` | child of Product |
| **ProductVariant** | purchasable unit; SKU, price, active flag | N:1 Product | unique `Sku`; idx `ProductId`; filtered idx `IsActive = 1`; `rowversion` | child of Product |
| **ProductImage** | gallery; optionally variant-specific | N:1 Product, N:1 Variant (nullable), N:1 Media | idx `(ProductId, SortOrder)` | child of Product |
| **Attribute** | defines an axis/spec (Color, Size, Material, ...) | 1:N AttributeOption | unique `Code` | own root (catalog metadata) |
| **AttributeOption** | allowed value (Red, XL) | N:1 Attribute | unique `(AttributeId, Value)` | child of Attribute |
| **ProductVariantAttribute** | which option a variant has per axis | join: Variant × Attribute × Option | PK `(VariantId, AttributeId)` → each variant has exactly one option per axis; idx `OptionId` (filtering) | child of Variant |
| **ProductAttributeValue** | non-variant specs (descriptive, filterable) | Product × Attribute × (Option or text) | PK `(ProductId, AttributeId)` | child of Product |
| **ProductTag / Tag** | legacy has product tags; optional for MVP | N:M | unique `Tag.Slug` | **Deferred unless required** (see Q) |

Variant price lives **on the variant** (MVP): `Price`, `CompareAtPrice` (nullable, for strike-through), `CurrencyCode`. A separate `Price` history table is Phase 2.
`MaxOrderQuantity` is a nullable column on the variant.

### 3.2 Product / variant model (replaces `ColorIDs`, `SizeIDs`, `WarrantyIDs`, `StoreProductPrice`)

- A **product** with no choices has exactly one default variant (so cart/order/inventory code has a single path).
- A product with choices (e.g. Color × Size) has one variant per valid combination. Each variant has one row per axis in `ProductVariantAttribute`. A product declares its axes by the set of attributes its variants use.
- **Color, Size, Warranty and any future attribute are rows in `Attribute`/`AttributeOption`**, not columns. Adding "Material" is an insert, not a migration. `Attribute.Type` (Option / Text / Number) and `IsVariantAxis` / `IsFilterable` flags drive admin UI and filtering.
- Legacy `Count`/`UsedCount`/`TotalUsedCount`/`MaxOrder` map to: `Inventory.OnHand` + `Inventory.Reserved` (stock), `ProductVariant.MaxOrderQuantity` (limit). `UsedCount` (sold counter) is **derived** from `OrderItem` when needed, not stored.
- Warranty: treated as a plain product attribute (text/option) in the MVP; warranty as a priced add-on is Phase 2.
- Variant-specific images: `ProductImage.VariantId` nullable (null = shown for all).
- Inactive: `ProductVariant.IsActive`, plus `Product.Status` (Draft/Published/Archived).

Trade-off: EAV-style attributes make arbitrary filtering queries slightly more complex than columns. Accepted: the number of filterable attributes is small, and the join tables are indexed. If filtering performance becomes a measured problem, add a materialized read table later.

### 3.3 Inventory (MVP: single stock location)

| Entity | Fields / purpose | Constraints |
|---|---|---|
| **InventoryItem** | `VariantId` (PK/FK 1:1), `OnHand`, `Reserved`, `rowversion` | `CHECK (OnHand >= 0 AND Reserved >= 0 AND Reserved <= OnHand)` |
| **InventoryReservation** | `Id`, `OrderId`, `VariantId`, `Quantity`, `Status` (Active/Committed/Released), `ExpiresAt` | idx `(Status, ExpiresAt)`; idx `OrderId`; unique `(OrderId, VariantId)` |
| **StockMovement** | append-only ledger (`VariantId`, `Delta`, `Reason`, `ReferenceType/Id`, `CreatedAt`, `ByUserId`) | idx `(VariantId, CreatedAt)`; **MVP-required for audit of manual stock edits** |

Available-to-sell = `OnHand − Reserved`. Multi-warehouse is Phase 2 (`InventoryItem` gets a `LocationId` then).

### 3.4 Inventory lifecycle: Available → Reserved → Purchased

Each transition is **one SQL transaction** using a conditional atomic update (no read-then-write):

1. **Reserve** (at checkout start, "place order"):
   `UPDATE InventoryItem SET Reserved = Reserved + @q WHERE VariantId=@v AND OnHand - Reserved >= @q` → if 0 rows affected, the item is out of stock for that quantity and the whole order fails with a clear 409 listing the lines. Insert `InventoryReservation(Active, ExpiresAt = now + 15 min)`. Both in the same transaction as the order insert. `rowversion` additionally protects admin edits that race checkout.
2. **Payment succeeded** (verified callback): in one transaction mark Payment `Captured`, Order `Paid`; for each reservation `UPDATE InventoryItem SET OnHand = OnHand - @q, Reserved = Reserved - @q`; reservation → `Committed`; write `StockMovement(Sale)`.
3. **Payment failed / user cancelled at gateway**: Payment `Failed`; reservation → `Released`; `Reserved = Reserved - @q`. Order moves to `PaymentFailed` with a retry window (the user may retry paying until the reservation expires; a retry extends `ExpiresAt` once if stock is still reserved).
4. **Timeout**: background job (every minute) selects `Active` reservations with `ExpiresAt < now` for orders not `Paid`, releases them, and marks the order `Expired`. A late payment callback for an expired order is treated as: verify with gateway → if captured, try to re-reserve; if stock unavailable → mark `PaidNeedsAttention`, alert admin, refund per policy (never silently lose money).
5. **Order cancelled before payment**: release reservation. **Cancelled after payment (before shipment)**: restock (`OnHand += q`, `StockMovement(CancelReturn)`) and start a refund (see §3.5).
6. **Return/refund after delivery**: admin action; restock is optional (flag), `StockMovement(Return)`.
7. **Admin manual stock edit**: sets `OnHand` via a movement (`Adjustment`); cannot reduce below `Reserved` (the CHECK constraint enforces).
8. **Many simultaneous buyers**: the conditional `UPDATE` serialises on the row; exactly the available quantity succeeds; the rest get 409. A concurrency test with N parallel buyers for stock M must show exactly M succeed.

Idempotency: gateway callbacks and "pay" calls are idempotent via `Payment.IdempotencyKey` and the unique `(Provider, ProviderReference)`; processing a callback twice must not double-commit stock.

### 3.5 Cart → Checkout → Order → Payment → Shipment

```
Cart (mutable, no stock held)
  └─ Checkout validation (prices, stock availability, address, shipping method, coupon)
       └─ Order created in PendingPayment + stock Reserved (one transaction)
            └─ Payment (Initiated → Pending → Captured | Failed)
                 └─ Order Paid → Processing → (Shipment) Shipped → Delivered → Completed
```
There is **no separate "Checkout" table in MVP**: checkout is a service operation that turns the cart into an order atomically. The cart is cleared on successful order creation.

| Entity | Why | Notes / constraints |
|---|---|---|
| **Cart** | server-owned cart for guests and users | `CustomerId?` or `AnonymousToken` (unique when set), `UpdatedAt`, `rowversion` |
| **CartItem** | lines | unique `(CartId, VariantId)`; `Quantity > 0` and ≤ `MaxOrderQuantity` validated on write |
| **Order** | the contract with the customer | unique `OrderNumber` (human-readable, non-sequential-guessable); `CustomerId`, `Status`, `Subtotal`, `DiscountTotal`, `ShippingTotal`, `GrandTotal`, `CurrencyCode`, **address snapshot columns** (recipient, phone, province, city, address, postal code), `CouponCode?`, `CreatedAt`, `rowversion`; idx `(CustomerId, CreatedAt DESC)`, idx `(Status, CreatedAt)` |
| **OrderItem** | lines with snapshots | `OrderId`, `VariantId`, **snapshot** `ProductName`, `Sku`, `VariantDescription`, `UnitPrice`, `DiscountAmount`, `Quantity`, `LineTotal`; idx `OrderId`, `VariantId` |
| **OrderStatusHistory** | audit of transitions | `OrderId`, `From`, `To`, `At`, `ByUserId?`, `Note`; idx `(OrderId, At)` |
| **Payment** | one per payment attempt group for an order | `OrderId`, `Provider`, `Amount`, `Status`, `IdempotencyKey` (unique), `ProviderReference` (unique per provider) |
| **PaymentTransaction** | each gateway interaction (init, verify, refund) | `PaymentId`, `Type`, `Status`, `Amount`, `RawResponse` (trimmed, no card data), `CreatedAt`; MVP-required for debugging and reconciliation |
| **Shipment** | MVP-simple: one shipment per order | `OrderId`, `Carrier` (free text), `TrackingNumber`, `Status`, `ShippedAt`, `DeliveredAt`; **no** shipment-item table, **no** carrier API |
| **ShippingMethod** | flat price choices (e.g. Standard, Express) | `Name`, `Price`, `IsActive`; zone-based/weight-based calculation is Phase 2 |

Address snapshot lives **in the order** so later address edits never change history.

**Enums (stored as strings or small ints with a lookup, never unexplained bytes):** C# `enum`s with `HasConversion<string>()` (readable in SQL, stable if reordered). Alternatives: int with documented values. Decision: **string conversion**, with `CHECK` constraints, because the status columns are low-cardinality and queryability/readability beats a few bytes.

State machines (enforced in domain methods; illegal transition throws a domain exception → 409):

| Aggregate | States and transitions |
|---|---|
| **Order** | `PendingPayment` → `Paid` \| `PaymentFailed` \| `Expired` \| `Cancelled`; `PaymentFailed` → `PendingPayment` (retry) \| `Expired` \| `Cancelled`; `Paid` → `Processing` \| `Cancelled` (refund path); `Processing` → `Shipped` \| `Cancelled`; `Shipped` → `Delivered`; `Delivered` → `Completed` \| `ReturnRequested`; terminal: `Completed`, `Expired`, `Cancelled`, `Refunded` |
| **Payment** | `Initiated` → `Pending` → `Captured` \| `Failed`; `Captured` → `Refunded` \| `PartiallyRefunded` |
| **Shipment** | `Pending` → `Shipped` → `Delivered` (\| `Returned`) |
| **Cart** | no states; it exists or is deleted/emptied; abandoned carts purged by job |

### 3.6 Customers

| Entity | Why | Notes |
|---|---|---|
| **User / Role / UserRole / RoleClaim** | ASP.NET Core Identity tables | admin and customers share identity; customers have no role claims |
| **Customer** | profile (name, phone, optional birth date) | 1:1 with User; unique `UserId` |
| **CustomerAddress** | address book (legacy: `StoreMemberLocation`) | `CustomerId`, recipient, phone, province/city/address/postal code, `IsDefault`; filtered unique idx: one default per customer; soft delete |
| **Wishlist** | **Deferred**: legacy "favorites" listed in the original brief; if kept, it is a single join table `WishlistItem(CustomerId, ProductId)` with unique pair (cheap), and stays "basic" in MVP |
| Province/City lookups | Needed for Iranian addresses | Seed table; reuse legacy `Location` data as a seed source **[verify content]** |

### 3.7 Marketing (MVP-minimal)

| Entity | Why | Notes |
|---|---|---|
| **Coupon** | the only promotion mechanism in MVP | `Code` (unique, case-insensitive), `Type` (Percent/Fixed), `Value`, `MinOrderAmount?`, `StartsAt`, `EndsAt`, `MaxRedemptions?`, `MaxPerCustomer?`, `IsActive`, `RedemptionCount` (updated atomically) |
| **CouponRedemption** | enforce per-customer limit, audit | unique `(CouponId, OrderId)`; idx `(CouponId, CustomerId)` |
| ProductDiscount, Campaign, Discount engine | **Deferred.** Sale price is represented by `Price` < `CompareAtPrice` on the variant |

Coupon redemption is counted when the order is created and **rolled back when the order expires/cancels unpaid** (same job as stock release).

### 3.8 Content & Reviews

| Entity | Why | Notes |
|---|---|---|
| **BlogPost** | blog list/detail | unique `Slug`; `Status`, `PublishedAt`, `AuthorId`, `CoverMediaId`, body (sanitised HTML), `rowversion`; idx `(Status, PublishedAt DESC)` |
| **BlogCategory** | blog navigation | unique `Slug`; 1:N posts (single category per post in MVP) |
| **BlogTag / BlogPostTag** | optional; legacy has News tags | **Deferred unless Q below says yes** |
| **Page** | static pages (about, FAQ, privacy, terms, shipping, returns, contact text) | unique `Slug`; body; `Status`; FAQ is just a page in MVP |
| **Media** | uploaded assets | see §9 |
| **Menu / MenuItem** | header/footer links | **Deferred**: use `SiteSetting` JSON or code config in MVP. Revisit if admins must edit menus |
| **Banner / HomeSection** | home page content | **MVP-minimal**: `Banner(ImageMediaId, LinkUrl, Title, SortOrder, IsActive)` only; no layout engine |
| **ProductReview** | basic ratings/reviews | `ProductId`, `CustomerId`, `Rating 1–5`, `Title?`, `Body`, `Status` (Pending/Approved/Rejected), `CreatedAt`; **unique `(ProductId, CustomerId)`**; idx `(ProductId, Status)`. `Product.RatingAverage` and `RatingCount` are updated on approve/reject (denormalised for list speed). Verified-purchase flag computed from OrderItem when reviewing |
| **SiteSetting** | key/value settings (store name, contact info, tax rate if any) | unique `Key` |
| **AuditLog** | who changed what in admin | `UserId`, `Action`, `EntityType`, `EntityId`, `At`, `Summary` (no full diffs in MVP) |

### 3.9 SEO tables

| Entity | Why | Notes |
|---|---|---|
| **Slug** (as a column) | Product, Category, Brand, BlogPost, BlogCategory, Page each have `Slug` with a **unique index per table** | A shared `Slug` table is not needed for MVP; uniqueness is per entity type and URL prefixes differ per type |
| **SeoMetadata** | `EntityType`, `EntityId`, `MetaTitle?`, `MetaDescription?`, `CanonicalUrl?`, `NoIndex`, `OgImageMediaId?` | unique `(EntityType, EntityId)`. Nullable fields fall back to generated defaults (§7). One table instead of repeating columns in six tables |
| **Redirect** | `FromPath` (unique, normalised lower-case, no query), `ToPath`, `StatusCode` (301/302/410), `IsAuto`, `CreatedAt` | Generalises legacy `PageRedirect`; **auto-created when a slug changes**; admin can add manual rows. Loop/self-redirect validation on write. Legacy `IsContain` (substring match) is **not** carried over |

### 3.10 Entity count check
Core MVP tables ≈ 40 including Identity. If this feels too large in implementation, the first cut to make is: `StockMovement` (keep the CHECK constraints and drop the ledger), `PaymentTransaction`, `OrderStatusHistory` (keep minimal logs) and `Banner`. I do **not** recommend cutting reservations or snapshots.

---

## 4. Authentication and authorization

| Decision | Detail |
|---|---|
| **ASP.NET Core Identity** (EF store) | Password hashing (PBKDF2/Argon-class default), lockout, email confirmation, optional phone OTP. Do not write custom password storage |
| **Cookie authentication** for both storefront and admin | `HttpOnly`, `Secure`, `SameSite=Lax` (or Strict where flows allow), short sliding expiry. Works with SSR (the Node SSR server forwards the cookie to the API; legacy `APP_SSR_COOKIES` shows the pattern). Requires **antiforgery** on mutating requests |
| JWT | **Not in the MVP.** Add only if a mobile app/third-party client appears. Avoids token storage and refresh complexity |
| Authorization | **Policy-based, permission claims.** Admin roles are bundles of permissions; endpoints use `[RequirePermission("Product.Update")]`-style policies built from a single registry |

**Permission model (small, resource.action; only what the MVP admin has):**

`Product.View|Create|Update|Delete`, `Category.Manage`, `Order.View|Update|Cancel|Refund`, `Customer.View`, `Blog.View|Create|Update|Delete`, `Review.Moderate`, `Coupon.Manage`, `Media.Manage`, `Seo.Manage` (redirects/metadata), `Settings.Manage`, `User.Manage`, `Report.View`.

- Stored as `RoleClaim` rows of type `permission`. Seeded roles: `SuperAdmin` (all), `Catalog Manager`, `Order Manager`, `Content Editor`. New permissions = a constant + a seed row, no schema change.
- **Alternative considered:** pure role-based. Rejected: the brief requires granular permissions, and role-only checks recreate the legacy string-matching problem.
- Customers have no permissions; customer-owned resources (orders, addresses) are authorised by ownership check (`order.CustomerId == currentUser`), covered by tests to prevent IDOR.
- **Risk:** permission sprawl. Mitigation: permissions are constants in one file; a test fails if an admin endpoint lacks a policy.

---

## 5. SEO architecture (MVP)

| Concern | Design |
|---|---|
| URLs | `/product/{slug}`, `/category/{slug}` (nested categories use the leaf slug; breadcrumbs carry the hierarchy), `/blog/{slug}`, `/{page-slug}` for static pages. Lowercase, hyphen, Persian slugs allowed (Unicode, URL-encoded in sitemap) **[confirm Q1]** |
| Slug generation | Server-side: normalise (lowercase, trim, hyphenate; Persian/Arabic character normalisation such as `ي→ی`, `ك→ک`), transliterate only if configured; ensure uniqueness by appending `-2`, `-3`; admin may edit |
| Slug change | In the same transaction: write `Redirect(old→new, 301, IsAuto)`; collapse redirect chains |
| Metadata | API returns a `seo` block per page: `title`, `description`, `canonical`, `robots`, `og`, `breadcrumbs`, `jsonLd[]`. Defaults: product = `{Name} | {Brand} | {Store}`, description = trimmed short description, OG image = primary image derivative (1200×630 where available) |
| Canonical | Absolute, built server-side from a configured `PublicBaseUrl` (never from request headers); strips tracking params; paginated pages self-canonical |
| Pagination | `?page=N`; real `<a href>` prev/next links; `noindex,follow` on filtered/sorted combinations beyond a whitelist |
| Noindex | `SeoMetadata.NoIndex` per entity; cart/checkout/account/search results always `noindex` |
| JSON-LD | `Product` + `Offer` (price, currency, availability) + `AggregateRating` when ≥1 approved review; `BreadcrumbList`; `BlogPosting`; `Organization` + `WebSite` on home. **Prices in JSON-LD come from the same source as the page**, never separately cached |
| Sitemap | API endpoint `/sitemap.xml` (index) → `/sitemaps/products-1.xml`, categories, blog, pages. **Same host as the site** (proxy via SSR/edge); real `lastmod` from `UpdatedAt`; max 50,000 URLs/file; cached 1h and refreshed by job |
| robots.txt | Served dynamically: allow all except `/cart`, `/checkout`, `/account`, `/admin`, `/api`, internal search params; `Sitemap:` line with absolute URL; environment-aware (`Disallow: /` on non-production) |
| Status codes | SSR must emit real **404** for unknown slug / unpublished, **410** for deliberately removed, **301** for redirects via the `Redirect` table (the Node SSR layer calls an API "resolve redirect" endpoint, or the API returns a typed result the resolver turns into `res.redirect`). **A soft-404 is a defect and gets a test** |
| Images | Alt text required in admin forms; width/height stored on `Media` |
| Internal linking | Breadcrumbs, related products (same category), category links in footer, latest blog on home, all as anchors |
| Hreflang/multi-language | **Out of MVP** (single locale) |

Legacy reference: `method.service.ts` SEO block and `server.ts` redirect/preload code are used as behaviour examples only.

---

## 6. Media / image architecture

- **Port:** `IMediaStorage { Put(key, stream, contentType), Get/Open, Delete, GetPublicUrl(key) }`. Adapters: `LocalDiskMediaStorage` (dev/small prod), `S3CompatibleMediaStorage` (S3/Arvan/MinIO via S3 API; reuse the legacy Arvan knowledge, **not** its code or credentials). Domain stores only `Media.StorageKey`; the URL is composed by `GetPublicUrl` from a configurable base (CDN origin), so changing CDN is configuration.
- **Processing:** one library, **ImageSharp** (managed, cross-platform). Legacy also has Magick.NET and LibHeif; drop them from MVP unless HEIC upload support is required (phones upload HEIC; if required, convert to JPEG/WebP at upload) **[Q]**. Check ImageSharp's license terms for the intended use **[verify]**.
- **Pipeline at upload** (synchronous for small images; job for large batches): validate (type sniffed from bytes, not extension; max size/dimensions; reject SVG upload or sanitise; decompression-bomb limits) → strip EXIF (keep orientation applied) → store original (private key) → generate **WebP** at fixed widths (e.g. 160, 320, 640, 1024, 1600) → AVIF only if CPU cost proves acceptable **[decide after measurement]** → record `Media(Id, StorageKey, MimeType, Width, Height, Bytes, Hash, Alt)` and `MediaVariant(MediaId, Width, Format, StorageKey)`.
- **Delivery:** immutable names (content hash) + `Cache-Control: public, max-age=31536000, immutable`; the storefront emits `srcset`/`sizes`; lazy-load below the fold; LCP image `fetchpriority=high`. A CDN is optional: serving from the app/object-store works for MVP; add a CDN by changing the base URL.
- **Admin media management (MVP):** upload, list with thumbnails, edit alt text, delete if unreferenced (check references first). No folders or tagging.
- **Risk:** CPU spikes on upload → cap concurrency (semaphore) and file size.

---

## 7. Caching strategy (MVP)

| Layer | MVP decision |
|---|---|
| Browser/CDN | Hashed static assets: immutable 1 year. SSR HTML for anonymous catalog pages: `Cache-Control: public, s-maxage=60, stale-while-revalidate=300` **only if** the response sets no cookie and is not user-specific |
| API | `IMemoryCache` for small slow-changing data (category tree, settings, brands, attribute definitions) with explicit invalidation on admin writes. ASP.NET output caching for anonymous list/detail endpoints with short TTL and tag-based eviction |
| Distributed | **Not in the MVP**: one app instance → memory cache is correct. Introduce Redis (`HybridCache`/`IDistributedCache`) **when a second instance is deployed**. Code uses `HybridCache`/an `ICacheService` seam so the switch is configuration **[verify API availability on chosen .NET]** |
| Never cached | Cart, checkout, stock availability on add-to-cart/checkout, prices at order time |
| Risk | Stale price/stock on product pages; mitigate via short TTL and by re-validating at cart/checkout |

---

## 8. Background jobs (MVP)

Use `BackgroundService` hosted services + a small `OutboxMessage` table for email/SMS. **No Hangfire/Quartz** for MVP (deliberately dropping the legacy dependency).

| Job | Cadence | Purpose |
|---|---|---|
| Reservation expiry | every minute | release stock, expire unpaid orders, roll back coupon use |
| Outbox dispatcher | every few seconds | send order/verification emails & SMS with retry/backoff (notifications must not block checkout) |
| Payment reconciliation | every 5–10 min | verify `Pending` payments with the gateway (callback lost) |
| Sitemap/cache refresh | hourly | optional; can be on-demand with caching |

All jobs are idempotent and take a DB-level lock (e.g. `sp_getapplock`) if more than one instance ever runs. **Alternative:** Hangfire (dashboard, retries). Reconsider only when job count/complexity grows.

---

## 9. Payment

- Port `IPaymentGateway { Initiate(order) → redirect url/ref; Verify(callbackData) → result; Refund(...) }`; one adapter per provider.
- **OPEN APPROVAL ITEM:** earlier answers were "Persian/RTL Iran" and "Stripe first". Stripe does not serve Iran-based merchants or IRR, so these conflict. I need either: an Iranian gateway (the legacy `BankProvider` is the reference to inspect, e.g. Zarinpal-type redirect gateways), or confirmation that the market is non-Iranian. **No payment code starts until this is settled.** A `FakeGateway` adapter (dev/test only) is built first so the whole flow is testable.
- Callback security: verify server-side with the gateway (never trust browser redirect params), validate amount and order id, idempotent processing, store raw response (without card data), HTTPS only, no secrets in URLs/logs.

---

## 10. Testing strategy (prioritised)

| Level | Tooling (**[verify choices]**) | What it covers |
|---|---|---|
| Domain unit tests | xUnit | Order/Payment/Shipment state machines, coupon rules, slug generation, price/total calculation, inventory arithmetic |
| Integration tests (real SQL Server) | xUnit + Testcontainers (or LocalDB) + `WebApplicationFactory` | **Concurrent purchase** (M stock, N buyers → exactly M succeed), reservation → commit/release/expire, idempotent payment callback, coupon limits, EF mappings/constraints, N+1 guard (query-count assertion on list endpoints), migrations apply cleanly |
| API tests | `WebApplicationFactory` | auth required/forbidden per permission; **IDOR** (customer A cannot read B's order); validation → ProblemDetails; product create → variant create → stock update → publish |
| SEO tests | API + unit | SEO block per entity, canonical absolute, noindex rules, sitemap contents/lastmod/limits, robots, redirect creation on slug change, redirect loop prevention, 404/410/301 status |
| Frontend | Jasmine/Karma or Jest (decide with Angular version; Angular's current default **[verify]**) | Component tests only for cart/checkout forms and SEO services |
| E2E / SSR validation | Playwright (small suite) | Browse → add to cart → checkout (fake gateway) → order visible in account → admin sees order; assert SSR HTML contains title/canonical/JSON-LD **without JS** and real HTTP status codes |
| Not doing | blanket coverage targets | focus on the paths above |

CI (new repo): build, test, format/analyzers, `npm audit`/`dotnet list package --vulnerable`, secret scanning, migrations check.

---

## 11. Security strategy

| Area | Measure |
|---|---|
| **Secrets** | None in git, ever. Config via environment variables / user-secrets (dev) / host secret store (prod); `.gitignore` for local configs; secret-scanning pre-commit + CI. **Nothing copied from legacy `maininfo.json`, `appsettings*.json`, `google_credentials.json`.** The legacy secrets should be rotated regardless (separate action for the legacy repo owner) |
| AuthN | Identity defaults, lockout, rate limit on login/register/OTP/password reset, email confirmation, generic error messages |
| Cookies/session | `HttpOnly; Secure; SameSite`, `__Host-` prefix where possible, sliding expiry, logout invalidates; no ASP.NET `Session` for cart (cart in DB) |
| CSRF | Antiforgery token for cookie-authenticated unsafe verbs; SameSite as defence in depth |
| XSS | Angular escaping by default; blog/page HTML sanitised server-side on save (allow-list) and never `bypassSecurityTrust*` on user content; CSP header (no `unsafe-inline` where achievable); JSON-LD serialised safely (escape `</script>`) |
| SQL injection | EF parameterisation; no string-built SQL; raw SQL only parameterised |
| File upload | Size limits (replace legacy `int/long.MaxValue`), content sniffing, extension allow-list, re-encode images, random storage keys, store outside web root, no execution, optional AV scan hook |
| CORS | Explicit allow-list of storefront/admin origins (no `AllowAnyOrigin`, no manual wildcard headers) |
| API | Authorize-by-default, ownership checks, input validation, ProblemDetails without stack traces in production (`ShowErrors` off), HSTS, security headers (nosniff, frame-ancestors, referrer-policy) |
| Rate limiting | Built-in rate limiter on auth, cart mutations, coupon apply, checkout, search |
| Passwords | Identity hasher only |
| Payment callbacks | Server-side verification, amount/order match, idempotency, no card data stored (PCI scope minimal via redirect/hosted payment page) |
| Audit logging | Admin write actions to `AuditLog`; order status history; stock movements |
| Privacy | Minimal PII, delete/anonymise on request process documented |
| Dependencies | Pinned, scanned in CI; update cadence defined |

---

## 12. Migration strategy from the existing Store module

**MVP decision:** the new platform **starts empty (seeded with admin + reference data)**. Legacy data import is a separate, later, opt-in project, because legacy order/stock semantics are unreliable (see debts #2, #3, #4) and importing them increases MVP risk. The map below is the prepared reference; **nothing destructive is done to the legacy DB at any point** (read-only access if an import is later approved).

| Existing entity | Target | Verb | Notes |
|---|---|---|---|
| `StoreProduct` | `Product` (+ default `ProductVariant`) | **Replace** (transform on import) | Split `ColorIDs/SizeIDs/WarrantyIDs` CSVs into variant/attribute rows; `TranslateTitle/TranslateDescription` → `Name/Description`; `IsEnabled/IsDeleted` → `Status`; generate slug |
| `StoreProductPrice` | `ProductVariant` (+`Price`) + `InventoryItem` | **Split** | One row = one variant; `Price` → variant price; `Count − UsedCount` → `OnHand` (needs reconciliation against actual orders); `MaxOrder` → `MaxOrderQuantity`; `TotalUsedCount`/`UsedCount` **deprecated** (derive from orders) |
| `StoreProductImage` | `ProductImage` + `Media` | **Rename/migrate** | Files re-processed through the new pipeline (derivatives) |
| `StoreCategory` | `Category` | **Rename** | Keep hierarchy; generate/verify slugs; keep legacy slug in `Redirect` if URLs differ |
| `StoreBrand` | `Brand` | **Rename** | |
| `StoreAttributeGroup / StoreAttribute / StoreAttributeItem / StoreProductAttribute` | `Attribute` / `AttributeOption` / `ProductAttributeValue` | **Merge** | Four tables → three; groups become a `Group` text column on `Attribute` |
| `StoreColor`, `StoreSize` | `Attribute(Code=color\|size)` + `AttributeOption` | **Merge into generic attributes** | |
| `StoreWarranty` | `Attribute(Code=warranty)` option | **Merge** | Priced warranty is Phase 2 |
| `StoreProductTag` / `Tag` | `Tag` (+ product join) | **Evaluate** | Only if tags are an MVP need (open question); else **Deprecate** |
| `StoreMemberCart` | `Cart`/`CartItem` | **Replace** | Carts are ephemeral; **do not migrate**, users start with empty carts |
| `StoreMemberOrder` (JSON `Cart`, `byte Status`, `Int16 AdminStatus`) | `Order` + `OrderItem` + `Payment` | **Replace** | Legacy status codes need a documented mapping table; address and totals copy into snapshot columns; JSON expanded to `OrderItem` rows only for historical read access |
| `StoreMemberOrderItem` | `OrderItem` | **Migrate/redesign** | Already has snapshots (`ProductTitle`, `Price`...); lacks SKU, so a synthetic SKU is created |
| `StoreMemberOrderLog` | `OrderStatusHistory` | **Replace** | Log is a full JSON dump per change; not worth converting |
| `StoreMemberLocation` | `CustomerAddress` | **Rename/redesign** | `CountryID/ProvinceID/CityID/AreaID` → province/city columns; keep `IsDefault`; `ReceiverName/Mobile` → recipient/phone |
| `StoreTransportation` | `ShippingMethod` | **Replace** (simplify) | Only flat price; complex rules deferred |
| `StorePreOrder` | n/a in MVP | **Deprecate (defer)** | Pre-order is Phase 2 |
| `StoreTranslate` | n/a | **Deprecate** | Single locale in MVP; localisation Phase 2 |
| `Translate` (site-wide) | Angular i18n/resource files (static UI text) | **Replace** | UI strings belong in the front-end resource files; DB-driven translation only if admins must edit UI text (not MVP) |
| `OffCode` | `Coupon` (+`CouponRedemption` replaces `OffCodeLog`) | **Replace** | Legacy has `UserList` as CSV, `EntityID` as string and `ProductType`; the new coupon covers percent/fixed, window, limits; product-scoped coupons Phase 2 |
| `StarRate` / `StarRateHistory` / `Comment` | `ProductReview` | **Evaluate → replace** | Legacy stores *aggregates* (`TotalRate`, `TotalRater`) keyed by type/entity; individual rating history needed for moderation and uniqueness |
| `News`, `NewsImage`, `NewsTag` | `BlogPost`, `Media`, (`BlogTag` optional) | **Rename/migrate** | Only blog-type rows; content HTML must be sanitised and image links re-pointed to the Media pipeline |
| `PageRedirect` | `Redirect` | **Reuse concept** | Drop `IsContain`; normalise paths; add loop check |
| `Menu` | `SiteSetting`/code config (MVP) | **Defer** | |
| `Media`, `MediaKeyWord`, `MediaOrder` | `Media` | **Replace** | Education-domain media; do not carry over |
| `SystemLog`, `MemberLog` | Structured logs + `AuditLog` | **Replace** | |
| `Member*` (auth) | Identity `User` + `Customer` | **Replace** | Passwords **cannot** be reused unless the hash scheme is known and compatible **[unverified]**; plan for "reset password" on first login if import happens |
| Teacher, Course, Organization, Booklet, TeacherRequest, DanaPlus, OnlineClass, Ticket and other education tables | n/a | **Out of scope; untouched** | Belong to the education product |

**Do-not-do list for migration:** no schema changes in the legacy DB, no data copied during MVP, no production credentials used by new code, no destructive step without approval of this map.

---

## 13. Dependency modernization plan

Targets are **proposals; each must be verified against official release/support pages before implementation** (my knowledge of exact current versions may be stale).

| Component | Current (verified in repo) | Target (to verify) | Breaking-change / compat concerns | Effort | Required for MVP? |
|---|---|---|---|---|---|
| .NET | net6.0 (EOL) | Current supported LTS | New solution; legacy untouched | Low (greenfield) | **Yes** |
| EF Core | 6.0.3 | Matches the .NET version | New project; `DateOnly`, JSON columns, bulk `ExecuteUpdate` available | Low | **Yes** |
| ASP.NET Core Identity | custom session auth | Built-in Identity | New schema | Low | **Yes** |
| AutoMapper | 7.0 DI ext | **Drop** | Commercial licence in current versions **[verify]** | n/a | No |
| MediatR | not used | **Do not add** | | n/a | No |
| FluentValidation | not used | Current | | Low | Yes (boundary validation) |
| Dapper | 2.0.123 | **Drop** (EF only) | | n/a | No |
| Redis (StackExchange.Redis 2.0.601, `Microsoft.Extensions.Caching.Redis` 2.2) | commented out | Add only with >1 instance | Use `Microsoft.Extensions.Caching.StackExchangeRedis` | Low | **No (MVP)** |
| Hangfire 1.7.x | registration commented out | **Drop** | Hosted services | n/a | No |
| `Microsoft.NETCore.App 2.2.8`, `Microsoft.AspNet.Mvc 5.3.0`, `Microsoft.AspNetCore.* 2.2` refs | present in legacy csproj | **Not carried over** | Legacy-only baggage | n/a | No |
| WebMarkupMin (HTML minify) | used | **Drop**; SSR output + gzip/Brotli at proxy | | n/a | No |
| Magick.NET, LibHeif, T4Image | present | Drop unless HEIC is required; ImageSharp only | License check on ImageSharp | Low | Decide (Q) |
| AWSSDK.S3 | 3.7.7.1 | Current, behind `IMediaStorage` | | Low | Only for S3-compatible deploy |
| Angular + CLI | 17.3.12 | Current supported release (new workspace) | Multi-major hops; builder `application`; `provideHttpClient(withFetch())`, zoneless is optional **[verify]** | Medium | **Yes** |
| `@angular/ssr`, Express | 17.3, Express 4 | Match Angular; evaluate Express 5 only if the Angular release supports it | | Low | Yes |
| TypeScript | 5.4 | As required by Angular | | Low | Yes |
| Node | v24 installed locally | Active LTS in CI/prod **[verify]** | | Low | Yes |
| `ng-persian-datepicker`, `jalali-ts` | 10, 8 | Replace/verify compatibility | Needed only for Jalali date UI (profile birth date); consider no datepicker in MVP | Low | Optional |
| `ngx-owl-carousel-o` + `swiper` | both | Keep **one** (`swiper` or CSS scroll-snap) | | Low | Reduce |
| `ngx-toastr`, `sweetalert2` | present | Keep one notification mechanism (or Material snackbar in admin) | | Low | Reduce |
| Material prebuilt theme | storefront | Not in storefront; admin only | CWV | Low | Reduce |
| `apexcharts` / `ng-apexcharts` | admin charts | Admin only; dashboard may start with simple numbers | | Low | Optional |
| `@auth0/angular-jwt` | JWT | **Drop** (cookie auth) | | n/a | No |
| `ngx-cookie-service(-ssr)` | present | Prefer Angular's request/response tokens + minimal helper | | Low | Reduce |
| Docker | .NET 6 images | New Dockerfiles for new stack | | Low | Yes |

Approach: **greenfield on current versions**, so we avoid in-place upgrades of legacy code; cherry-pick reusable ideas.

---

## 14. MVP scope map

**Must Have (MVP)** and where each lives:

| Feature | Notes |
|---|---|
| Home (banners, featured products/categories, latest posts) | `Banner` + queries |
| Categories, product list, search, basic filter/sort | Search = SQL `LIKE`/full-text on name/SKU/brand (**no search engine**); filter by category, brand, price range, variant options; sort by newest/price/popularity-lite |
| Product detail with variants, images | Core |
| Cart (guest + user, merge on login), checkout, payment, order creation | End-to-end flow, first priority |
| Customer auth, profile, addresses, order list/detail | |
| Basic reviews/ratings (moderated) | Included as "if required"; confirm (Q) |
| Coupon (basic) | Included as "if required"; confirm (Q) |
| Blog list/detail (+ blog categories) | |
| Static pages (about, FAQ, privacy, terms, shipping, returns, contact) | `Page` entity; contact form posts to API → email |
| 404 page | Real status |
| Admin: dashboard (counts/today's orders/low stock), products + variants + stock, categories, orders (status changes, shipment tracking), customers (read), blog, reviews moderation, coupons, media, SEO (redirects + metadata), site settings, users/roles/permissions | |
| Sitemap, robots, 301 redirects | |

**Phase 2 (explicitly not built):** advanced search/recommendations, complex promotions/campaigns, loyalty, multi-warehouse, marketplace, subscriptions, advanced shipping calculation, real-time notifications, personalisation, analytics/reporting beyond dashboard counters, multi-language/currency, wishlist beyond simple toggle, event bus, microservices, Redis/CDN architecture, JWT/mobile API.

Each Phase 2 item has a stated seam: stock location (InventoryItem), price history (Price table), promotions (new tables, coupons unchanged), search (`IProductSearch` can be introduced when needed), localisation (translation tables).

---

## 15. Implementation phases (end-to-end flow first)

| Phase | Deliverable | Exit criteria |
|---|---|---|
| **0** | Approvals (this doc); repo created; CI skeleton, analyzers, secret scanning; .NET LTS SDK + Node confirmed; secrets policy | CI green on empty solution |
| **1** | Backend foundation: solution, Identity (customer + admin), ProblemDetails, logging, OpenAPI, DB migrations, seed data, health check; Angular workspace skeleton (storefront SSR + admin) | Login works E2E; tests run in CI |
| **2** | **Vertical slice**: Category + Product + Variant + Inventory + Media (admin CRUD + public read) | Product visible on SSR page with correct SEO block |
| **3** | **Cart → Checkout → Order → FakeGateway Payment → Admin order view** with reservation lifecycle and background expiry | Concurrency test passes; E2E happy/failure paths pass. **Deployable MVP-alpha** |
| **4** | Real payment adapter, emails/SMS outbox, shipment tracking, order status flow | Sandbox payments verified incl. callback replays |
| **5** | Customer account (profile, addresses, orders), coupons, reviews + moderation | |
| **6** | SEO complete: sitemap, robots, redirects, JSON-LD, status codes; blog + pages + home/banners | Rich-result validation, no soft-404 |
| **7** | Admin completion: dashboard, customers, roles/permissions UI, audit log, settings | Permission tests green |
| **8** | Hardening: security review, load test checkout, CWV pass, backups/monitoring, go-live checklist | Go-live sign-off |

After each phase: demo + approval before the next.

---

## 16. Risks and trade-offs

| Risk / trade-off | Mitigation |
|---|---|
| Payment-gateway uncertainty (Stripe vs Iran) | Resolve before Phase 4; FakeGateway keeps others unblocked |
| EAV attributes → harder filter queries | Indexed joins; limit filterable attributes; read-model table later if measured |
| Single-instance memory cache | Documented switch to Redis at scale-out; cache behind a seam |
| No search engine: relevance is basic | Accept for MVP; `IProductSearch` seam |
| Angular upgrade breaks libraries | Greenfield workspace; replace stale libs |
| Legacy data not imported | Prepared map; separate approved project if needed |
| EF/SQL Server lock-in | Accepted; matches existing ops knowledge |
| Cookie auth limits non-browser clients | Add JWT later if mobile app appears |
| String enums are larger than bytes | Negligible at this scale; readability wins |
| Unverified assumptions about legacy SSR 404 behaviour and CWV | Measure on the legacy site first (cheap baseline) |
| .NET LTS SDK not installed locally (only 6/9) | Install on approval, or choose 8/9 temporarily |

---

## 17. Items requiring explicit approval

1. Keep Angular, build a **new** upgraded workspace (not an in-place upgrade of the legacy one).
2. New **separate repository** and its path (previous answer: separate repo; path not yet given).
3. Target .NET LTS and permission to install its SDK on this machine.
4. **Payment provider**: resolve the Iran/Stripe conflict (§9).
5. Single-seller, Persian/RTL, IRR single-currency assumptions (previous answers) **[reconfirm given Stripe]**.
6. Single SQL Server database; no Redis/Hangfire/search engine in MVP.
7. Dropping AutoMapper, MediatR, Dapper, Hangfire, WebMarkupMin and legacy 2.x/MVC5 references.
8. String-backed enums for statuses.
9. Starting with **no legacy data import**; migration map in §12 as reference only.
10. MVP scope list (§14): confirm *reviews*, *coupons*, *wishlist*, *product tags*, *blog tags*, *HEIC upload*, *menu editing* are in or out.
11. Image pipeline: ImageSharp only; AVIF deferred pending measurement.
12. Cookie auth (no JWT) for MVP.
13. Admin as a separate client-only Angular app.
14. A fake payment gateway is used for development and tests.

**Stopped here. Awaiting your review and approval; nothing has been created, installed, or modified besides these two documents in `docs/ecommerce/`.**
