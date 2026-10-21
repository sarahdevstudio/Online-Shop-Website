# E-commerce MVP: Implementation Plan

Status: ADR (`02-architecture-decision-record-mvp.md`) approved. This plan was checked against the repository on 2026-10-03.

## 1. ADR vs repository verification

| Check | Result |
|---|---|
| Angular SSR/hydration/prerender exist | Confirmed (`server.ts`, `app.config.ts`, `angular.json`) |
| Legacy SEO contract | `SeoModel` (`WebApp.Common/Model`) already has title, description, canonical, NoIndex, StatusCode, JSON-LD list, breadcrumbs, OG type, social image, Twitter card. **Reused as the shape of the new `seo` block** (add `robots`, drop `PageKeyWord`/`PreLoad`) |
| Legacy product detail contract | `ProductDetailDTO` carries Prices/Sizes/Colors/Warranties/AttributeGroups/Images (variant concepts as separate lists). Used as a UI-behaviour reference only |
| Legacy pay flow | `GetPayment` computes `PayablePrice` from **client-supplied `PriceIds`** and keeps `PaymentInfo` in **server session**. New flow must price from DB cart server-side and persist state in Order/Payment. Confirms ADR §3.5 |
| Legacy must remain untouched | All `WebApp.*` projects, `WebApp.Web.Angular`. Working tree has one unrelated user change (`WebApp.Web.Angular/src/index.html`), which I will not touch |
| New code location | **Not in this repo's projects.** ADR says separate repository |
| .NET SDK | `dotnet --list-sdks`: 1.0.0, 5.0.408, 6.0.428, 9.0.317. **No .NET 10 (LTS) SDK installed**. Node 24.19 / npm 11.17 present; `winget` available |
| Payment | Stripe vs Iran conflict from ADR §9 still unresolved |

**Conflicts requiring a decision before Phase 1 code** (these are the only blockers):
1. Target repo path (ADR approved a separate repository; path never given).
2. Target framework: LTS SDK not installed (install .NET 10 SDK vs start on 9).
3. Payment gateway: does not block Phases 1-6 (FakeGateway first); blocks Phase 7 real adapter.

## 2. Phase plan (small, each ends with build + tests + report)

| Phase | Scope | Done when |
|---|---|---|
| 1 Foundation | Repo + solution (Domain/Application/Infrastructure/Api + tests), config/secrets via env + user-secrets, DbContext + first migration, Identity (customer + admin, cookie), permission policy foundation, ProblemDetails, FluentValidation, Serilog, health check, OpenAPI, CI | `dotnet build`/`test` green; register/login/logout and a permission-protected endpoint tested |
| 2 Catalog | Category, Brand, Product, ProductVariant (SKU, price, MaxOrderQuantity), Attribute/Option/VariantAttribute/ProductAttributeValue, ProductImage+Media (ImageSharp, `IMediaStorage`), InventoryItem; admin API + public read API | Pricing/variant tests; no CSV columns |
| 3 Storefront | Angular workspace (storefront SSR), home, category, listing (search/filter/sort/pagination), detail | SSR HTML contains product data |
| 4 SEO | `seo` block, slugs, SeoMetadata, Redirect, sitemap, robots, JSON-LD, real 404/410/301 under SSR | SSR HTML has meta/canonical/JSON-LD w/o JS; status-code tests |
| 5 Cart & checkout | Cart/CartItem, stock/variant validation, CustomerAddress, checkout service | Cart tests |
| 6 Inventory | Reservation/commit/release, expiry job, concurrency tests | N buyers / M stock = exactly M |
| 7 Orders & payment | Order/OrderItem/OrderStatusHistory/Payment/PaymentTransaction/Shipment, state machine, FakeGateway, idempotent callback, then real adapter | Payment success/fail/replay tests |
| 8 Account | Profile, addresses, order history/detail (ownership checks) | IDOR tests |
| 9 Blog | BlogPost/BlogCategory, SEO, JSON-LD | Public + sitemap |
| 10 Admin | Angular admin app: dashboard, products, categories, orders, customers, blog, users/permissions | Definition-of-Done flow runs |
| 11 Reviews/coupons | Per MVP confirmation | Simple, moderated |

Legacy Store stays running and unchanged; legacy data import is not part of this plan.
