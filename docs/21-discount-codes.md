# 21. Discount codes (F3)

## 1. Admin (`/admin/discounts`, permissions `Discounts.View/Create/Update/Delete`)

A `DiscountCode` is a code, a type (`percent` 1–100, or `fixed` — a whole-Toman amount), an optional minimum
subtotal, an optional total usage limit, a per-mobile-number limit (default 1), an optional expiry, and
active/inactive. The code is alphanumeric only (no hyphen or space), 3–30 characters, normalized to upper-case,
and unique; it never changes after creation (it may already be on placed orders). Create/update/delete follow the
project's standard admin-feature shape: CSRF, `Discounts.*` permission per action, 409 on a stale `RowVersion`,
and an audit log (`created`/`updated`/`deleted`) shown on the edit page.

**No separate "used" counter is stored.** `UsageLimit` and `PerMobileLimit` are enforced by counting
**non-cancelled** orders that reference the code's `Code` (`Order.DiscountCode`) — the admin list's "used" column
and the checkout-time checks both run this same count. A cancelled order stops counting, which is how cancelling
an order "releases" its usage, with no separate ledger or reconciliation needed.

## 2. Cart and checkout

The cart (`Cart.DiscountCode`, a column on the existing guest-cart row) holds at most one applied code.
`POST /store/cart/discount` (and `DELETE`, its own controller/rate-limit policy — guessing codes is an attack
surface the rest of the cart is not) validates the code exists and is active, then attaches it; the cart's own
quote (`GET /store/cart`) re-validates it against the *current* subtotal and expiry on every read and returns
`CartDiscountDto { code, amount, reason }` — `amount` is 0 and `reason` is set (a translated message) when the
code has stopped being usable (expired, deactivated, or the cart dropped below its minimum), so the customer sees
why rather than silently losing their entry. This preview never checks usage limits (that needs a database read
under a lock, done only at placement) — a code that would be rejected for usage reasons still previews its full
amount.

At placement (`OrderService.PlaceCoreAsync`), inside the existing checkout transaction, the applied code is
re-resolved under its own advisory lock (`sp_getapplock "discount-code:{code}"`, the same pattern as the
order-key/mobile/ip locks already there): active, not expired, cart subtotal still clears the minimum, the
per-mobile count (`DiscountCode` + `Mobile`, non-cancelled) is below `PerMobileLimit`, and the global count
(non-cancelled orders with that code) is below `UsageLimit` when one is set. A code that fails any of these is
**silently dropped** (0 discount) rather than erroring — the existing `subtotal + shipping - discount !=
ExpectedTotal` check then naturally reports `price_changed`, since the client's expected total assumed the
discount still applied; the customer's next quote shows it gone. Two checkouts racing for the last use of a
usage-limited code serialize on the per-code lock exactly like two checkouts racing for the last unit of stock
(`StoreOrderTests.TwoCheckoutsForTheLastItem_...`): the loser's count already reflects the winner's order.

`Order.DiscountCode` (string, nullable) and `Order.DiscountAmount` (long, default 0) are snapshots — set once at
placement, never changed by a later edit or deletion of the `DiscountCode` row — and are never more than the
order's `Subtotal` (`CK_Orders_Amounts`). `Order.Total = Subtotal + ShippingFee - DiscountAmount`
(`CK_Orders_Total`). The snapshot is shown everywhere an order's totals are shown: the cart and checkout summary
(before placement, from the cart's live preview), the order confirmation page, the admin order detail page, and
the printable invoice (never the packing slip, which never shows prices). `/cart` and `/checkout` stay `noindex`
as before; the code is only ever sent in a request body, never a URL.

## 3. Reports

`Reports.View`'s Revenue metric (docs/20) is now **net of the discount**: Σ `(Order.Subtotal − Order.DiscountAmount)`
of counted orders, both in the summary and the daily series — docs/20's forward note ("No discount exists yet —
F3 will subtract it once it does") is resolved. The daily chart's `profit` keeps its original per-item,
known-cost-only formula (docs/20 §2) and then subtracts that day's total discount, so a day with no cost-known
lines still reports a defined (zero, not negative) profit once its discount is taken out. The top-10-by-product
tables are **not** discount-adjusted (a discount is an order-level concept, not allocated per line); their sum
can exceed the summary's net revenue by the period's total discount — a documented simplification, not a bug.
