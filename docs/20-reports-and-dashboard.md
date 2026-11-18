# 20. Cost price, reports and dashboard (F2)

## 1. Cost price (docs/06 companion)

`ProductVariant.CostPrice` (nullable, whole Toman, ≥ 0) is admin-only: never in `VariantDto` or any other
public catalog DTO, JSON-LD or sitemap (`CostPriceTests` proves it). Editable on the variant form with
`Catalog.Update`. The CSV/Excel transfer (docs/16) carries it as `cost_price`, right after `compare_at_price`;
unlike every other column, an **empty cell means "no change"**, not "clear it" — cost price is often entered
gradually, not for every row at once. An out-of-range value is a per-row error, same as `price`.

`OrderItem.UnitCost` snapshots the variant's cost price at the moment the order is placed, so a later cost-price
edit never changes what an already-placed order reports (`StoreOrderTests` proves it).

## 2. Reports (`/admin/reports`, permission `Reports.View`)

Counted orders: **Confirmed, Shipped, Delivered** only — a cancelled or still-pending/awaiting-payment order
never counts. All day boundaries are Tehran-local (UTC+03:30, fixed — Iran has had no DST since 2022):
`Shop.Domain.Common.TehranTime` on the server, `storefront/src/lib/admin/tehran.ts` (mirrored, unit-tested) on
the client, which converts the admin's chosen preset or custom range into a `[fromUtc, toUtc)` pair before
calling the API.

### Metric definitions

| Metric | Definition |
|---|---|
| Revenue | Σ `Order.Subtotal` (item totals) of counted orders in the period. No discount exists yet — F3 (discount codes) will subtract it once it does. |
| Shipping collected | Σ `Order.ShippingFee` of counted orders. Shown separately, not part of revenue or profit. |
| Cost of goods | Σ `UnitCost × Quantity` over order lines **whose variant had a cost price at order time**. A line with none is **skipped entirely**, never treated as a zero cost. |
| Gross profit | Revenue − Cost of goods. |
| Margin % | Gross profit ÷ Revenue × 100 (0 when revenue is 0). |
| Order count | Count of counted orders. |
| Average order value | Σ `Order.Total` (items + shipping) ÷ order count. |
| Orders/items without cost | How many counted orders/lines had no cost price — shown as a banner, so "no cost data" is never silently read as "zero cost, 100% margin". |

Daily series (for the two charts) buckets counted orders by their Tehran-local calendar day; a day's `profit`
only sums lines with a known cost, same rule as the summary. Top-10 tables aggregate by the variant's **current**
product (a variant's `ProductId` never changes — `OnDelete(Restrict)` on `OrderItem.VariantId` — so historical
lines always resolve to a real product), summed over the whole period, not per day.

### Worked example (from `ReportsTests.Summary_DailySeries_AndTop10_MatchAHandComputedWorkedExample`)

One Tehran day, two counted orders (a third left pending, a fourth cancelled — neither counts):

| | Order 1 | Order 2 |
|---|---|---|
| Line | 2× variant A (cost ۵۰,۰۰۰) | 1× variant B (no cost price) |
| Revenue | 200,000 | 80,000 |
| Shipping | 50,000 | 50,000 |
| Cost of goods | 100,000 | — (skipped, not 0) |

Revenue = 200,000 + 80,000 = **280,000**. Shipping collected = 50,000 + 50,000 = **100,000**. Cost of goods =
100,000 (order 2's line is skipped) = **100,000**. Gross profit = 280,000 − 100,000 = **180,000**. Margin % =
180,000 ÷ 280,000 × 100 = **64.3%**. Order count = **2**. Average order value = ((200,000+50,000) +
(80,000+50,000)) ÷ 2 = **190,000**. Orders without cost = **1** (order 2); items without cost = **1**.
Top by revenue: product A (200,000), then B (80,000). Top by profit: product A (100,000), then B (**0**, not
negative, not guessed — it simply has no cost-known line).

A day-boundary test (`ADayBoundaryOrder_IsBucketedByTehranMidnight_NotUtcMidnight`) places one order at 20:29
UTC and another at 20:31 UTC the same UTC calendar day, and confirms they land in two different Tehran days
(Tehran midnight is 20:30 UTC) — proving the bucketing uses Tehran time, not server/UTC time.

### Page

Period presets (7/30/90 days) or a custom Tehran-local date range; summary cards; two server-rendered inline SVG
charts (daily revenue/profit as two lines, daily order count as bars — no chart library) each paired with a
real, keyboard-reachable `<table>` of the same numbers in a `<details>` (the accessible alternative to the
chart, not a visually-hidden duplicate); top-10-by-revenue and top-10-by-profit tables linking to each product.

## 3. Dashboard (`/admin`)

Redesigned to be **field-level permission-aware**: `GET /admin/dashboard` itself needs only a signed-in staff
account (the same "at least one permission" rule `/admin/me` already used) — each card is independently `null`
in the response unless the caller holds that card's permission, so an account never receives (and the page never
renders) a number it has no permission to see:

| Card | Permission | Data |
|---|---|---|
| Orders (new / awaiting-confirmation / to-ship / today) | `Orders.View` | counts, same as before F2 |
| Revenue and profit (today, this month) + a 30-day revenue sparkline | `Reports.View` | reuses `IReportsService` for today's and the month-to-date range |
| Pending reviews | `Reviews.View` | count |
| Low-stock variants | `Catalog.View` | count + the configured threshold (3) |
| Draft products | `Catalog.View` | count |

Quick actions (new product, view orders, product transfer) are shown only alongside the card whose permission
they need. A discount-codes quick action is deferred to F3, once that feature exists.
