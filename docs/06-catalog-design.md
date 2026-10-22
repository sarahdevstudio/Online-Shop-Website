# Catalog design (MVP) — proposal

Status: **approved 2026-10-22** (with the changes below folded in). Simplifies ADR §3.1–3.4 for the MVP scope in `PROGRESS.md` (M-4..M-6).

## 1. Data model

| Table | Columns (all `Id` int identity unless noted) | Rules |
|---|---|---|
| `Category` | `ParentId?`, `Name`, `Slug`, `Description`, `SortOrder`, `IsActive` | Max 2 levels: a parent must itself be a root. Unique `Slug`. |
| `Brand` | `Name`, `Slug`, `IsActive` | Unique `Slug`, unique `Name`. |
| `Product` | `CategoryId`, `BrandId?`, `Name`, `Slug`, `SearchKey`, `Description`, `Status` (Draft/Published/Archived), `CreatedAt`, `UpdatedAt` | One category per product (a root category lists its children's products). Unique `Slug`; idx `(CategoryId, Status)`, `BrandId`. |
| `ProductVariant` | `ProductId`, `Sku`, `Price` (bigint), `CompareAtPrice?` (bigint), `Stock` (int), `IsActive`, `SortOrder` | Every product has ≥1 variant. Unique `Sku`. `CHECK (Price > 0)`, `CHECK (Stock >= 0)`, `CHECK (CompareAtPrice IS NULL OR CompareAtPrice > Price)`. |
| `ProductImage` | `ProductId`, `Path`, `Alt`, `SortOrder` | Uploaded to local disk (`media/`, git-ignored, **must be backed up in production**), served as-is; derivatives are Post-MVP. |
| `Attribute` | `Code`, `Name`, `Unit?`, `Type` (Option/Text), `IsFilterable`, `SortOrder` | Unique `Code` (`wattage`, `base`, `cct`, …). |
| `AttributeOption` | `AttributeId`, `Value` ("۹ وات"), `Slug` (`9w`), `SortOrder` | Unique `(AttributeId, Slug)`. |
| `ProductAttributeValue` | `ProductId`, `VariantId?`, `AttributeId`, `OptionId?`, `Text?` | `VariantId` null = applies to the whole product (spec); set = variant axis. Exactly one of `OptionId`/`Text`. Unique `(ProductId, VariantId, AttributeId)`; idx `(AttributeId, OptionId)`. |

**Money:** Toman as `bigint`, no decimals, no currency column. Displayed price = cheapest active variant; computed server-side.
**Stock:** a plain `Stock` on the variant. Order placement runs `UPDATE ProductVariant SET Stock = Stock - @q WHERE Id = @id AND Stock >= @q` (0 rows → 409); cancelling restores it. No reservations/expiry/ledger in the MVP — an unpaid card-to-card order is cancelled by the admin, which returns the stock.
**Persian text:** one shared normalizer: Arabic ي/ك → Persian ی/ک; trim; runs of whitespace → one space; runs of ZWNJ → one ZWNJ, ZWNJ next to a space or at either end removed. Names/values are stored normalized. A separate search key (normalized, Persian/Arabic digits → Latin, lower-case, ZWNJ → space) is stored per product (`SearchKey`) and computed the same way from `q`; MVP search = `SearchKey LIKE %q%`.
**Slugs:** English, `^[a-z0-9]+(-[a-z0-9]+)*$`, ≤ 100 chars, typed by the admin on create; no endpoint can change them (immutable → no redirects needed). Draft = 404; Archived = page stays reachable as "unavailable", excluded from listings and sitemap.

## 2. Variants

One sellable model = one `Product`; each purchasable combination = one `ProductVariant` with its own SKU, price and stock. Example: *LED bulb 9W/12W × 3000K/6500K, E27* → 1 product, 4 variants; each variant has two rows in `ProductAttributeValue` (`wattage`, `cct`), and the product has one product-level row (`base = E27`). The product page builds a selector per axis from the variants' values; combinations that don't exist simply aren't offered. If the URL carries attribute params (`/product/x?wattage=9w&cct=3000k`) the matching variant is preselected (best match, else the default), and product links on a filtered category page carry the active attribute filter values. A chandelier with no choices has one variant and only product-level specs.

## 3. Attributes: generic option model (recommended) vs. typed columns

Typed columns (`Wattage`, `Base`, `Cct` on the variant) are the simplest for exactly three attributes, but bulbs and chandeliers don't share them: chandeliers need arm count, finish, material, recommended bulb; panels need size and IP rating. Each addition would be a migration + DTO + admin form + filter code, and most columns would be null for half the catalog.

**Recommendation: the small generic model above** (two metadata tables + one value table), because: filter and spec rendering are written once and work for any attribute; new attributes/options are data, not deploys; it matches the approved ADR decision #4. Kept deliberately small: only `Option` (filterable, discrete values — wattages and color temperatures are discrete in practice) and `Text` (display-only, e.g. lumen, lifetime). No numeric ranges, no per-category attribute sets (the filter sidebar shows attributes that have values in the current result set). Known simplification: filters match per product, so "9W AND 3000K" matches a product whose 9W variant is 6500K and whose 3000K variant is 12W; acceptable for the MVP.

## 4. API (`/api/v1`, list shape `{ items, page, pageSize, total }`)

**Public (anonymous, GET, cacheable):**
- `GET /catalog/categories` — 2-level tree
- `GET /catalog/categories/{slug}` — category, children, breadcrumb
- `GET /catalog/brands`
- `GET /catalog/products?category=&q=&brand=a,b&wattage=9w,12w&base=e27&cct=3000k&minPrice=&maxPrice=&inStock=true&sort=newest|price-asc|price-desc&page=` — paged cards + `facets` (brands and filterable attribute options present in the result, min/max price). Attribute filters use attribute codes as query keys (OR within a key, AND across keys); codes may not collide with reserved keys.
- `GET /catalog/products/{slug}` — product, variants (sku, price, compareAtPrice, inStock, axis values), specs, images, breadcrumb
- `GET /catalog/sitemap` — published product + category slugs with `updatedAt`

**Admin (cookie auth, `Catalog.Manage` permission, CSRF):**
- `GET|POST /admin/categories`, `PUT|DELETE /admin/categories/{id}` (delete only when empty)
- `GET|POST /admin/brands`, `PUT|DELETE /admin/brands/{id}` (delete only when unused)
- `GET /admin/attributes` (with options), `POST /admin/attributes/{id}/options` (attributes themselves are seeded in the MVP)
- `GET /admin/products?q=&status=&page=`, `GET|PUT /admin/products/{id}`, `POST /admin/products` (slug only here)
- `POST /admin/products/{id}/variants`, `PUT /admin/variants/{id}` (price, compareAtPrice, stock, active); variants are deactivated, not deleted, once ordered
- `PUT /admin/products/{id}/attributes` (replace the whole value set)
- `POST /admin/products/{id}/images` (multipart), `PUT /admin/products/{id}/images/order`, `DELETE /admin/images/{id}`

Cart/order/admin-order endpoints are designed with M-7.

## 5. Seed data (Development only, idempotent)

- **Categories (5):** `lamps` لامپ → `led-bulbs` لامپ LED حبابی, `candle-bulbs` لامپ شمعی و اشکی; `chandeliers` لوستر; `wall-lights` چراغ دیواری.
- **Brands (5):** پارس شهاب, زر, نمانور, مازی‌نور, فیلیپس.
- **Attributes:** `wattage` (5/7/9/12/15/20 W), `base` (E27, E14, GU10), `cct` with labels customers search for: "آفتابی (۲۷۰۰K)" `2700k`, "آفتابی (۳۰۰۰K)" `3000k`, "یخی/طبیعی (۴۰۰۰K)" `4000k`, "مهتابی (۶۵۰۰K)" `6500k` — filterable options; `lumen`, `lifetime`, `arms`, `finish`, `ip` — text.
- **Products (20, ~45 variants):** 8 LED bulbs (e.g. "لامپ LED حبابی ۹ وات E27" with 9/12 W × 3000K/6500K, 90–180 هزار تومان), 4 candle/flame bulbs (E14, 5–7 W, 2700K/6500K), 5 chandeliers (3/5/8/12 arms, gold/chrome/black finish, single variant, 4–35 میلیون تومان), 3 wall lights (1–2 variants, IP44 outdoor model, 1.2–4 میلیون تومان). Mix includes: one out-of-stock variant, one fully out-of-stock product, two with `CompareAtPrice`, one Draft and one Archived product, one product without brand. No images in seed (placeholder rendering is tested).

## 6. Implementation notes (M-4)

- Attributes and their options are reference data ensured at startup in **every** environment (insert-missing only); the sample categories/brands/products are seeded in Development only and only into an empty catalog.
- Facets include per-option product counts (a facet's own selection is ignored; zero-count options omitted).
- `SearchKey` = normalized product name + brand name; `q` is split into words and every word must match (`LIKE %word%`).
- Admin variant updates carry the variant's `rowVersion`; a stock change made meanwhile (e.g. by checkout) turns the update into a 409 instead of overwriting it.
- Option values and variants are referenced through composite foreign keys, so an option of another attribute or a variant of another product is rejected by the database.
- Seed: 20 products with 32 variants (fewer variants than the ~45 estimated above).

## 7. Decisions (answers to the open questions)

1. Guest checkout is allowed: phone + address, no account.
2. Shipping: one flat fee from configuration plus an optional free-shipping threshold on the subtotal.
3. One category per product.
