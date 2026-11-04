# 17 — SEO managed from the admin

Series E3. The SEO fields live next to the thing they describe: a panel at the bottom of the product, category, info-page and blog-post editors, plus two site-level fields in the site SEO editor.

## Per-entity SEO (products, categories, info pages, blog posts)

API: `GET|PUT /api/v1/admin/seo/{type}/{id}` with `type` = `product | category | contentPage | blogPost`. The entity's own permission decides (`Catalog.Manage` for products and categories, `Content.Manage`, `Blog.Manage`); CSRF like every admin write; a save carries the `rowVersion` it loaded (409 `concurrent_update` when stale; `null` until the first save). `SeoMetas` (one row per entity; unique on type + id) and `FaqItems` hold the data; `Categories.IntroMarkdown` holds the guide.

| Field | Meaning | Limits |
|---|---|---|
| Meta title | Replaces the generated title (`<title>` and Open Graph). Empty = the page's own name, then the site's title template. | 120 characters; the form shows a live hint against ~60 |
| Meta description | Replaces the generated description. Empty = the page's own text. | 320; hint against ~155 |
| Share image | A site image (uploaded through the same uploader as the site's default share image). Empty = the product's first photo or the site default. | must exist |
| Noindex | `robots: noindex, follow`, and the page is left out of the sitemaps. The form shows a warning while it is on. | |
| FAQ (product, category) | Question/answer list under the page and `FAQPage` JSON-LD from the same items. Plain text. | 20 items, 200 / 2000 characters |
| Guide (category only) | Markdown, rendered safely (raw HTML never runs, links filtered) under the product grid **on the category's clean URL only** (no filter, sort or page), in the same place as the FAQ. | 20,000 |

The storefront reads one public document per page: `GET /api/v1/catalog/seo/{type}/{slug}` (empty overrides, not a 404, for an entity without a row; 404 for an unknown entity) and `GET /api/v1/catalog/seo/noindex` (slugs by type). Any API trouble means "no overrides": SEO extras never take a page down. Saving drops the shop's cached copies at once.

## Sitemaps

`/sitemap.xml` is a **sitemap index**; `/sitemaps/products.xml`, `categories.xml`, `pages.xml` (home, blog index, info pages) and `blog.xml` are the sitemaps, each URL with `<lastmod>` from the entity's last change (omitted when unknown, never invented). Only public, indexable URLs: published products, active categories, info pages, published posts, minus anything marked noindex. An empty sitemap is not listed in the index. (`/sitemaps` is a reserved path in both the API and the storefront lists.) In a non-production environment `robots.txt` disallows everything and does not advertise the sitemap, as before.

## Structured data

Product (one Offer per variant, IRR), `BreadcrumbList` on **every** entity page (home › … › page; the visible trail and the JSON-LD come from the same items; the blog post had a duplicate list that is gone, and info pages gained their trail), `FAQPage`, `BlogPosting`, `Organization`, `WebSite`, `LocalBusiness`/`Organization` on the contact page. `storefront/tests/helpers/jsonld-shapes.ts` checks the shapes Google requires (required properties, types, ISO currency and dates, absolute URLs, rating ranges, FAQ structure); unit tests validate the generators and the e2e suite validates the real JSON-LD of each live page type.

## Search Console and GA4 (site SEO editor)

Two fields in `/admin/site/seo`, part of the draft → publish flow like the rest of the SEO settings:

- **Search Console token** (`^[A-Za-z0-9_-]{20,100}$`; the content of the verification `<meta>`): rendered as `<meta name="google-site-verification">` only when set.
- **GA4 measurement id** (`^G-[A-Z0-9]{6,12}$`, normalized to upper case): when empty, nothing about analytics is rendered, loaded or allowed.

Both are validated by the API (field errors in Persian) and again by the storefront before use. **Loading:** the GA4 tag starts after the visitor's first interaction (pointer, key, scroll, touch) or, for a visitor who only reads, 4 seconds after the page finished loading and the browser is idle, whichever comes first (a quiet page counts as idle the moment it is hydrated, which would be too early). It is never loaded in a draft preview or in the admin. **CSP:** the policy names `https://www.googletagmanager.com` (script, connect, img) and the Analytics hosts only while an id is configured; the proxy asks the API for the id and keeps the answer for 30 s (a change takes up to that long to reach the policy; an unreachable API means the strictest policy). With `'strict-dynamic'` the script added by our own nonce-trusted code is allowed; the host names serve browsers without strict-dynamic. Remember to mention analytics in the privacy page.
