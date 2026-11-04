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

## Entity links (menu, footer, announcement, hero, banner)

A link in the site customization is either a **typed URL** (the fallback) or a **reference to an entity** (category, product, info page, blog post). The reference is stored as `{ type, id }` (`entity` next to `link` in the payload; the entity wins and the typed URL is dropped). `SiteCompiler` turns it into the entity's **current path** when the document is compiled, so a path change shows up in the menu and footer directly, with no 308 hop. The compiled document is rebuilt when the route table changes (`IRouteTable.Version`), and the storefront drops its cached copy (`updateTag('site')`) in every path-changing action.

- A **deleted entity hides its link** (a menu item with no link and no children disappears, a footer column whose links are all dead disappears, a hero button or banner link is dropped); the draft keeps the reference, so an old version holding it can still be restored and published. The editor shows the warning under the field (`GET /admin/site/{kind}` returns `links`: label, current path and `exists` for every referenced entity).
- Editor: a "link type" select plus a searchable select (`ComboField` in the shared form system, `GET /admin/site/link-targets?type=&q=`, `Site.Manage`). Custom URL stays available.
- The home page's category "view all" link is generated from the category's current path.
- One-time conversion (`SiteLinkConverter`, at the first start with this version): a typed internal link that is exactly a current **or old** path of an entity becomes an entity reference, in every stored version (draft, published, history). Links with a query string or fragment, external URLs and paths of no entity stay typed. It runs once (an audit row with action `converted` is the marker) and logs the number converted.

## Known limits

- An entity link to an unpublished product, post or inactive category is not hidden: it follows the path and the storefront answers 404 until the entity is published.
- A deleted target is reported without its name (the row is gone); the warning says the target was deleted.
- `GET /api/v1/catalog/routes` from the design was not built: the sitemap and links get paths from the DTOs.
- Another storefront instance serves an old resolve answer for at most 60 s after a change; the canonical tag already points at the new path.

## Checks

`dotnet test` (RoutePath, reserved paths, routes API, dev-seed proof), `npm test` (resolve client, redirects, proxy 410, admin form helpers) the browser smokes `storefront/scripts/smoke-routing.cjs` and `smoke-entity-links.cjs` (need a running API in Development and the built storefront; see the header of the script).
