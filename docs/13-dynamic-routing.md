# 13. Dynamic routing (admin-managed URL paths)

Design: `docs/11-architecture-changes.md` section C, with the approved decisions **C-1** (308), **C-2** (ASCII paths only), **C-3** (410 for hard-deleted entities, otherwise 404), **C-4** (an entity's own permission edits its path; `Routes.Manage` only for releasing/deleting redirects).

## How it works

- Table `RoutePaths`: one row per path of a category, product, info page or blog post. Exactly one row per entity is **current**; its older paths stay as non-current rows of the **same entity**, so an old URL is always one hop from the current one (no chains, no loops). A hard-deleted category/post keeps its rows as **gone** tombstones (410, never silently reused).
- Existing URLs are seeded unchanged at startup (`/category/x`, `/product/x`, `/blog/x`, `/about`); `RouteSeedTests` proves it for the whole development sample.
- `GET /api/v1/catalog/resolve?path=` answers from memory (rebuilt after every write, re-checked every 30 s for other instances): an entity, `redirect` (to the current path, 308), `gone` or `notFound`. Always HTTP 200 so the storefront can cache every answer.
- Every public DTO carries `path` (categories, cards, breadcrumbs, product/post/page detail, sitemap, cart lines). Links, canonical, `og:url`, JSON-LD and the sitemap use it; nothing is built from a slug any more.
- Storefront: one catch-all `app/(shop)/[...path]/page.tsx` resolves the path (data cache tag `routes`, 60 s) and renders the entity views in `_views/`. A redirect keeps the query string (filters, sort, page, variant choices). Static routes win in Next. `src/proxy.ts` turns a `gone` answer into HTTP 410 (a Next page cannot choose its status) by rewriting to `/gone`.
- Path rules (`Shop.Domain.Routing.RoutePath`): trimmed, lower-case, leading `/`, no trailing `/`, 1-4 segments of `[a-z0-9]` joined by single hyphens, at most 200 characters. `/x/` and `/X` redirect to `/x`. Conflicts (a live path, another entity's old path, a deleted entity's path) are refused with a Persian message naming the owner; at most 30 old paths are kept per entity.

## Reserved paths

One list: `ReservedPaths` in `src/Domain/Routing/ReservedPaths.cs` (`/admin`, `/api`, `/cart`, `/checkout`, `/order`, `/search`, `/media`, `/_next`, `/account`, `/robots.txt`, `/sitemap.xml`, `/site-icon`, `/fonts`, `/health`, `/gone`, `/favicon.ico`, and the exact path `/blog`). `ReservedPathsTests` reads `storefront/src/app` and `storefront/public` and **fails when a static route exists that is not listed**; `tests/reserved-paths.test.ts` fails when the proxy's copy (`lib/reserved-paths.ts`) differs. Adding a static route = add it to both lists in the same commit.

## Admin

- A "نشانی (URL)" field in the product, category, info-page and blog forms (shared `PathField`: resulting URL, redirect warning, list of old paths). Create sends `path` with the entity; edit changes it through `PUT /api/v1/admin/routes/{type}/{id}` **before** the entity is saved, so a refused path saves nothing.
- `/admin/redirects` (`Routes.Manage`): list, search by path or name, release (delete) with confirmation, and the audit of every path change (`RouteAuditLogs`).
- Cache invalidation: every action that changes, creates or deletes a path calls `updateTag('routes')`, `updateTag('catalog')` and refreshes the sitemap.

## Known limits

- Links typed by hand in the site-customization data (menu items, footer columns, home "view all" links) are plain strings; after a path change they keep working through the 308 but are not rewritten. The route picker for them belongs to the next series.
- `GET /api/v1/catalog/routes` from the design was not built: the sitemap and links get paths from the DTOs.
- Another storefront instance serves an old resolve answer for at most 60 s after a change; the canonical tag already points at the new path.

## Checks

`dotnet test` (RoutePath, reserved paths, routes API, dev-seed proof), `npm test` (resolve client, redirects, proxy 410, admin form helpers) and the browser smoke `storefront/scripts/smoke-routing.cjs` (needs a running API in Development and the built storefront; see the header of the script).
