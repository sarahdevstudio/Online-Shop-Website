# 16 — Store operations: staff, printing, product import and export

Series E2. Each part has its own permission, CSRF (the API-wide `X-XSRF-TOKEN` filter), a 409 on stale data and an audit trail.

## 1. Staff and roles (`User.Manage`)

Screens: `/admin/staff` (accounts and the audit), `/admin/staff/new`, `/admin/staff/[id]`, `/admin/staff/roles`, `/admin/staff/roles/new`, `/admin/staff/roles/[id]`.
API: `/api/v1/admin/staff` (+ `/roles`, `/audit`), and `GET /api/v1/admin/me` (the signed-in account's permissions, used to hide menu items; the API still checks every call).

- **Accounts.** A staff account is a user with at least one role other than Customer. Create: username (`create-admin` rules), optional display name and e-mail, an initial password under the admin password rules (12+ characters, not digits only, not containing the username), at least one role. Edit the profile, **deactivate** (the sessions of that person end at their very next request, and the account cannot sign in; it looks exactly like a wrong password), **reactivate**, **reset the password** (the old password and every open session stop working; a lockout is lifted).
- **Roles.** `Admin` (every permission, kept in step by startup) and `Customer` are built in and cannot be edited or deleted. A custom role has a name (`Letters_digits-_`, 3–40), a Persian title and a set of the existing permissions. Editing a role ends the sessions of its members, because permissions are read when the session is created. A role that still has members cannot be deleted.
- **No lock-out.** After every change that could matter (roles of a user, deactivation, a role's permissions) the API checks that at least one **active** user still holds **every** permission; if not, the change is rolled back with `staff_lockout` (409) and a Persian explanation. You cannot deactivate your own account.
- **Step-up.** Creating an account, changing a user's roles, creating/editing/deleting a role and resetting a password need the caller's own password again (`currentPassword`). A wrong or missing one is 403 `step_up_failed`, is written to the audit (no values) and counts as a failed sign-in (the 5-failure lockout applies).
- **409.** Users and roles carry a `stamp` (Identity's concurrency stamp); a save with an old stamp is `concurrent_update`.
- **Audit** (`StaffAuditLogs`): created, updated, roles changed, deactivated, activated, password reset, reauth failed, role created/updated/deleted: who, when, which account or role, field and role names (never passwords).
- **Menu.** The panel menu shows only what the account may open; an account without Orders and Settings gets a welcome text instead of the dashboard.
- A revoked session (deactivated account, changed roles, reset password, signed out elsewhere) lands on `/admin/login?ended=1` with an explanation, instead of bouncing between the login page and `/admin`.

The permission is the existing `User.Manage` (the spec said `Users.Manage`; the registry already had this name and a rename would invalidate nothing but add churn).

## 2. Printing orders (`Orders.Manage`)

`/admin/print/[id]/invoice` and `/admin/print/[id]/packing-slip`, `?size=a4|a5` (A4 default). Opened from the order screen in a new tab.

- Persian, right-to-left, the shop's font. Header: order number (plain digits), Jalali date, the seller (site name from the SEO settings, phone and address from the store settings). Recipient box, lines table (row, item, SKU, quantity; the invoice adds unit price and line total), subtotal, shipping, total, payment method and status, the customer's note.
- The packing slip has **no prices**: a larger recipient block, a tick box per line and the item count.
- The print stylesheet hides the controls and the grey desk; the paper size is an `@page` rule injected as a nonce-tagged `<style>` (the CSP allows no other inline style element). The e2e suite renders both sizes to PDF and checks the page boxes (595×842 pt and 420×595 pt).

## 3. Product export and import (`Catalog.Manage`)

Screen `/admin/products/transfer`. API: `GET /api/v1/admin/catalog/export?format=csv|xlsx`, `POST …/import/preview` and `POST …/import/apply` (multipart `file`).

**Format.** One row per variant. Fixed columns: `product_slug, product_name, category, brand, status, description, path, sku, price, compare_at_price, stock, variant_active, sort_order, image_ids`; then one `spec:<code>` column per attribute (a value of the whole product) and one `axis:<code>` per attribute (a value of that variant). Required: `product_slug, product_name, sku, price`. Rows with the same `product_slug` are the variants of one product; product-level cells may repeat on every row but must agree. Export and import use the same columns, so an exported file imports back as "no changes" (tested).

**Dry run.** `preview` parses and validates everything and answers what would be created / updated per row and every problem as `{row, column, message}` with a Persian message (up to 200, then a "truncated" flag). It writes nothing. `apply` runs the same planner **inside one transaction**: any problem in any row refuses the whole file; a database conflict (a race for a path or SKU, a variant changed meanwhile) rolls back and answers 409.

**Rules.** An existing product is found by slug, a variant by SKU (a SKU that belongs to another product is an error). Variants missing from the file are **not** deleted and no product is ever deleted. Existing products: an empty product-level cell means "leave as is"; the `path` of an existing product cannot be changed here (use the product page, which records the redirect). Stock changes are written to the stock history with source `import`. Numbers accept Persian digits and thousands separators. Brands and categories must already exist. An attribute cannot be both a product spec and a variant axis (same rule as the product screen).

**Images.** Nothing is fetched from a URL (no outgoing calls). `image_ids` accepts only the ids of the product's own images, to reorder them (the complete set); a URL or a foreign id is a row error with an explanation; a new product cannot take images (upload them on the product page).

**Files and limits.** CSV (UTF-8, BOM written so Excel shows Persian; comma, semicolon or tab detected; quotes and embedded newlines) and `.xlsx` (read without a library: first worksheet, shared/inline strings, formulas never evaluated). Limits: 2 MB, 2,000 rows, 80 columns, 10,000 characters per cell, 100 zip entries, 20 MB uncompressed (counted while reading, not trusted from headers); DTDs are prohibited and no XML resolver is used. A file whose extension disagrees with its content, damaged, truncated, empty or not UTF-8 is refused with a Persian message under `file`.
**Spreadsheet formula injection.** Export prefixes a cell that starts with `= + - @` (or tab/CR) with an apostrophe; the import takes one off again.
**Audit.** `CatalogTransferLogs`: every export and every applied import (who, when, file name, counts).
