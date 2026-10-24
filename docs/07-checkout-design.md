# Cart, checkout and orders design (M-7) — proposal

Status: **approved 2026-10-24** with the four changes in "Approved changes" below (they supersede the text they touch).

## Decisions recorded

Guest checkout only (no login, no cart merge, no account pages; existing registration stays unused; admin login is still needed for M-8). Server-side cart, items = `variantId + quantity`, prices read live, 30-day inactivity expiry, line quantity ≤ min(stock, `Checkout:MaxQuantityPerLine`, default 10). Payment: `CardToCard` / `CashOnDelivery`, each switched by config; card-to-card shows card number, holder and exact amount from config, the customer submits a transfer reference (tracking number, time, last 4 card digits), no receipt upload, admin confirms in M-8. Shipping = flat fee + optional free threshold (`null` = off), placeholder defaults. Integer Toman, server-computed. Stock decremented for all lines in one transaction, all-or-nothing, restored on cancel; no reservations. No SMS/email.

## 1. Who talks to whom

Browser → **Next.js server** (Server Actions and plain `<form method=post>`) → **API** (private network, never called from the browser; `API_BASE_URL` stays server-only).

- The browser holds one cookie, `cart`: 32 random bytes (base64url), `httpOnly`, `Secure` (off only in development), `SameSite=Lax`, `Path=/`, `Max-Age` 30 days, refreshed on every cart request. Only Next reads/writes it (in Server Actions, where cookies may be set) and forwards it to the API as `X-Cart-Token`. The DB stores only its SHA-256.
- **CSRF without weakening the API:** the API's `AntiforgeryFilter` keeps protecting every cookie-authenticated route unchanged (admin, auth). The new `/api/v1/store/*` group uses **no ambient credentials** (no auth cookie; identity = the explicit `X-Cart-Token` / order token), so there is nothing for CSRF to ride on. Instead of the antiforgery header it requires a secret `X-Storefront-Key` (config `Storefront:ApiKey`, shared by Next and the API; a startup error in production if empty), compared in constant time, so only the Next server can call it. The browser→Next hop is covered by Next's built-in Server Action `Origin`/`Host` check plus `SameSite=Lax`. **(proposed)** The alternative (Next fetches `/auth/csrf` and replays token + cookie on every POST) works with no filter change but adds a round trip per mutation and ties unrelated cookies to the cart.
- **Client IP for rate limiting:** Next derives the visitor's IP **only** from the trusted reverse proxy (`TRUSTED_PROXY_HOPS`/`TRUSTED_PROXIES` setting: take the address the trusted proxy appended to `X-Forwarded-For`, i.e. the entry `hops` from the right; in development with no trusted proxy, the socket address) and never forwards a browser-supplied `X-Forwarded-For`. It sends that single address to the API in `X-Forwarded-For` (always overwritten, never appended to). The API accepts forwarded IPs only from the storefront (`ForwardedHeaders` with `KnownProxies`/`KnownNetworks` = `Storefront:TrustedProxies`); from any other peer the header is ignored.
- Cart/checkout/order pages are dynamic and `no-store`; nothing here goes through the catalog data cache.

## 2. Tables

| Table | Columns | Constraints |
|---|---|---|
| `Cart` | `Id`, `TokenHash` binary(32), `CreatedAt`, `LastActivityAt` | unique `TokenHash`; idx `LastActivityAt`. Expired carts are ignored on read and purged when a new cart is created (no background job). |
| `CartItem` | `Id`, `CartId`, `VariantId`, `Quantity`, `SeenPrice` bigint | FK cascade to `Cart`, FK to `ProductVariant`; unique `(CartId, VariantId)`; `CHECK (Quantity > 0)`. **(proposed)** `SeenPrice` is the price the customer last saw, used *only* to show the "price changed" notice; it is never charged. |
| `Order` | `Id`, `Number` int, `AccessToken` varchar(43), `IdempotencyKey` uniqueidentifier, `Status`, `PaymentStatus`, `PaymentMethod`, `FullName`, `Mobile` char(11), `Province`, `City`, `Address`, `PostalCode` char(10), `Note?`, `Subtotal`, `ShippingFee`, `Total` bigint, `TransferTracking?`, `TransferTime?`, `TransferCardLast4?` char(4), `CreatedAt`, `UpdatedAt` | unique `Number` (from SQL `SEQUENCE`, starting at 1001), unique `AccessToken`, unique `IdempotencyKey`; `CHECK (Total = Subtotal + ShippingFee)`, `CHECK (Subtotal >= 0 AND ShippingFee >= 0)`; idx `(Status, CreatedAt)`, `Mobile`. The access token is stored in clear (an idempotent replay must return the same `/order/{token}` link). |
| `OrderItem` | `Id`, `OrderId`, `VariantId`, `ProductName`, `VariantLabel`, `Sku`, `UnitPrice` bigint, `Quantity` | FK to `Order` (cascade) and to `ProductVariant` (restrict, so ordered variants can only be deactivated); `CHECK (Quantity > 0 AND UnitPrice > 0)`. Names/prices are snapshots; later catalog edits never change an order. |
| `OrderStatusLog` **(proposed)** | `Id`, `OrderId`, `FromStatus`, `ToStatus`, `AdminUserId?`, `At` | Cheap audit of who confirmed payment / cancelled. |

Config (`Checkout:*`): `MaxQuantityPerLine`=10, `ShippingFee`, `FreeShippingThreshold`=null, `CardToCard:{Enabled,CardNumber,HolderName}`, `CashOnDelivery:Enabled`, `OrderRateLimit`. Defaults are placeholders (fee 50 000 Toman, sample card number); in production, startup fails if card-to-card is enabled with the placeholder card.

## 3. API

**Public `/api/v1/store` (needs `X-Storefront-Key`):**
- `GET /cart` (`X-Cart-Token`) → lines with live price, `seenPrice`, availability, max quantity, notices (`unavailable`, `quantity_exceeds`, `price_changed`; the first two block checkout); subtotal, shipping, total, enabled payment methods. Unknown/expired token = empty cart.
- `POST /cart/items {variantId, quantity}` — creates the cart if there is no valid token (response carries the new token for Next to set), adds to an existing line, clamps to the maximum and says so. `PUT /cart/items/{variantId} {quantity}` (0 removes), `DELETE /cart/items/{variantId}`.
- `POST /orders` (`X-Cart-Token`, `Idempotency-Key`; body: address fields, `paymentMethod`, `expectedTotal`). Mobile is normalized (Persian/Arabic digits, `+98`/`0098`/`98`/missing 0 → `09xxxxxxxxx`), postal code must be 10 digits; uses `PersianText.Normalize`. `201 {accessToken, number}`; replay of the same key → `200` with the same order (checked *before* the cart is read). `409 stock_insufficient {lines:[{variantId,name,requested,available}]}` (nothing decremented), `409 price_changed` (the cart total differs from `expectedTotal`; the customer re-reviews), `409 cart_empty`, `422` field errors, `429`.
- `GET /orders/{token}` → order, lines, payment instructions (card number, holder, exact total) while `AwaitingPayment`. `POST /orders/{token}/transfer-reference {tracking, time, cardLast4}` — card-to-card, only while `AwaitingPayment`, may be resubmitted until confirmed. **(proposed)** `time` is free text (digits normalized), because the admin reads it by eye and Jalali parsing is not worth it in the MVP.
- Rate limits per IP: order placement `Checkout:OrderRateLimit` (default 5 / 10 min), transfer reference and order lookups 30 / min, cart 120 / min. Known accepted risk: without reservations, a bot can park stock in unpaid orders; limited by the above and cleaned up by admin cancel.

**Order placement transaction:** validate fields → lock nothing up front → in one transaction, for each line **ordered by `VariantId`** (consistent lock order, no deadlocks) `UPDATE ProductVariant SET Stock = Stock - @q WHERE Id = @id AND Stock >= @q AND IsActive` (existing `IStockService`) and require product `Published`; any miss rolls everything back → `409` listing the failing lines. Then read the price, insert `Order` + `OrderItem` snapshots with `Number = NEXT VALUE FOR`, delete the cart, commit. A unique-key violation on `IdempotencyKey` (two simultaneous submits) is caught and answered with the existing order.

**Admin `/api/v1/admin/orders` (cookie auth, new `Orders.Manage` permission, existing antiforgery):** `GET ?status=&paymentStatus=&q=(number/mobile)&page=`, `GET /{id}`, `POST /{id}/confirm-payment` (card-to-card: `Paid` + `Confirmed`), `POST /{id}/status {Confirmed|Shipped|Delivered}`, `POST /{id}/cancel`.
Transitions: `AwaitingPayment|Pending → Confirmed → Shipped → Delivered`; cancel allowed from `AwaitingPayment|Pending|Confirmed` only. Every transition is a guarded `UPDATE … WHERE Status = @expected` (0 rows → 409), so cancel restores stock exactly once. **(proposed)** COD becomes `Paid` when marked `Delivered`; cancelling an already-`Paid` card order does not refund (done manually, outside the system; no `Refunded` state in the MVP).

## 4. Storefront pages

| Page | Notes |
|---|---|
| Product page | "Add to cart" `<form>` with the selected variant (selection is already URL-driven links) and quantity → Server Action. **Works without JavaScript** (a plain POST, then redirect to `/cart`); no JS-only mini-cart. |
| `/cart` | Lines, quantity/remove forms, notices, totals, "continue to checkout". No JS needed. |
| `/checkout` | Server-rendered form (fields + payment method radios + hidden fresh idempotency key + hidden `expectedTotal`); field errors re-render the page with entered values. No JS needed; a client component only disables the button on submit. |
| `/order/{token}` | Confirmation/status page, payment instructions and the transfer-reference form. `noindex`, `Cache-Control: no-store`, `Referrer-Policy: no-referrer` (token is in the URL). |

`robots.txt` gains `Disallow: /checkout` and `Disallow: /order` (`/cart` is already there). Header cart-count badge is read from the cart on the server.

## 5. Test plan

- **Unit:** mobile/postal normalization; shipping fee (below/at/above threshold, `null`); totals; max quantity; status transition table.
- **API integration (SQL Server, existing factory):** cart add/merge-line/clamp/expiry; notices for changed price and unavailable item; `expectedTotal` mismatch → 409; snapshots unchanged after a catalog price edit; every status transition and illegal ones; cancel restores stock once (second cancel 409); admin endpoints reject anonymous / missing permission / missing antiforgery; `/store/*` rejects a missing or wrong storefront key.
- **Concurrency:** two simultaneous checkouts for the last item (parallel requests, separate carts) → exactly one `201`, one `409 stock_insufficient`, stock 0 (never negative); a multi-line order where line 2 fails leaves line 1's stock untouched; two orders over overlapping variants in opposite line order complete without deadlock.
- **Idempotency:** same key sequentially → same order number and token, stock decremented once; same key sent in parallel (N = 10) → one order row; replay after the cart was cleared still returns the order.
- **Rate limit:** the (N+1)th order from one IP in the window → `429`.
- **Storefront (vitest):** cookie attributes, URL/robots rules, add-to-cart and checkout posts work with no client JS (form POST), error re-render.

## Approved changes (2026-10-24)

1. **Client IP:** as described in section 1. Tests: a spoofed `X-Forwarded-For` sent by a non-trusted peer does not change the rate-limit key; the trusted storefront's value does; the storefront's own helper ignores a browser-supplied header unless it comes through the trusted hop count.
2. **Unpaid-order cap:** at most `Checkout:MaxUnpaidOrdersPerMobile` and `Checkout:MaxUnpaidOrdersPerIp` (both default 2) orders in `AwaitingPayment` or `Pending` per mobile number and per IP (`Order.ClientIp` is stored for this). A third attempt returns `409 unpaid_limit` and the storefront shows a clear Persian message. Cancelling or confirming an order frees a slot.
3. **Order page:** `/order/{token}` sends `Referrer-Policy: no-referrer`, `noindex`, `Cache-Control: no-store`. Tokens are masked in API and storefront logs (request logging path is rewritten to `/order/***`; the token is never logged by our code). Tested by a log check.
4. **`X-Storefront-Key`:** compared in constant time (`CryptographicOperations.FixedTimeEquals`), read from `Storefront:ApiKey`; startup fails in Production if it is missing, shorter than 32 characters or a placeholder.

## Implementation notes (backend, as built)

- **Forwarded IP:** `UseForwardedHeaders` runs first, `XForwardedFor` only, `ForwardLimit = 1` (only the rightmost entry counts, so entries a browser put on the left are ignored). Trusted peers = `Storefront:TrustedProxies` (addresses or CIDR networks); when empty, only the framework default (loopback) is trusted, so a development storefront on the same machine works. From any other peer the header is ignored. The resolved address is used for the rate limits and stored as `Order.ClientIp` for the unpaid cap.
- **Storefront key / CSRF:** the `/api/v1/store` group carries `StorefrontEndpointMetadata`; only endpoints with it skip the cookie-CSRF filter, and the same group requires `X-Storefront-Key` (constant-time, SHA-256 of both sides). Production start fails (`StartupChecks.RequireProductionSettings`) if `Storefront:ApiKey` is missing, shorter than 32 characters or looks like a placeholder, or if card-to-card is enabled with the placeholder card/holder.
- **Placement transaction:** advisory locks (`sp_getapplock`, transaction-owned) are taken in the fixed order idempotency key → IP → mobile, then the unpaid counts are checked, variants are read, availability and `expectedTotal` are verified, stock is decremented per variant in id order with the existing conditional `UPDATE`, and order + lines + status log are inserted and the cart removed in one `SaveChanges`. The key lock makes parallel submits of one key return one order even for the last item; the mobile/IP locks make the unpaid cap exact under concurrency.
- **Order number:** SQL sequence `OrderNumbers` (starts at 1001); a rolled-back placement can leave a gap.
- **Admin:** permission `Orders.Manage` (granted to Admin at startup). Optimistic concurrency (`RowVersion`) on `Order`; cancel restores stock in the same transaction, so a double cancel restores once.
- **Logs:** request logs use a masked path (`/orders/***`), and the unhandled-exception log masks it too. Tokens are never logged by application code.
