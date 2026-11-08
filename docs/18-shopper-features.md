# 18 — Shopper features: comparison, search, reviews

Series E4.

## Product comparison (no login)

`/compare`, a cookie `compare` holding up to **four** product slugs (httpOnly, 30 days, validated on every read: only well-formed unique slugs count). The button «افزودن به مقایسه / حذف از مقایسه» is on the **product page** (a plain form posting to a Server Action, no script needed), a header link with the count appears once the list is not empty. The listing cards carry no button on purpose: a form per card would make every listing heavier (docs/14).
The page re-reads each product, so price and stock are current and a product that is no longer public drops out. Rows: price (a range over the variants), brand, stock, then every attribute once (variant axes such as wattage list all their options; text specs show their value), a dash where a product has none. A row where the products do not all agree is marked with the word «متفاوت» (never colour alone) and `?diff=1` shows only those rows. On a phone the table scrolls sideways inside a focusable region and the first column (the attribute name) is `position: sticky`. The page is `noindex` and a reserved path in both lists; `returnTo` of the actions only accepts a path on this site.

## Search

- **Index.** `ICatalogSearch` keeps the name key (name + brand) of every published product and active category in memory, rebuilt on the first query after any save that touches a product, variant, image, category or brand (`CatalogSearchInterceptor`; order stock changes do not rebuild it).
- **Matching.** Each query word (up to five, normalised like all Persian text) must match: as part of a name (a half-typed word counts), or within a small **edit distance** of a name word (optimal-string-alignment: a missing, extra, wrong or swapped letter is one typo; 0 typos allowed for 1–2 letter words, 1 up to 5, 2 beyond). Ranking: word-start hits, then other substrings, then typos, shorter names first.
- **Suggestions.** `GET /api/v1/catalog/suggest?q=` → up to 6 products (name, current path, cheapest price, photo) and 4 categories, `fuzzy: true` when only nearest spellings matched; the storefront calls it through `/api/suggest` (cached a few seconds).
- **Combobox.** The header box is an ARIA 1.2 combobox: `role="combobox"`, `aria-expanded`, `aria-controls`, `aria-autocomplete="list"`, `aria-activedescendant`; a `listbox` with product and category groups and a final «see all results» option; Down/Up (wrapping), Home/End, Enter (the active option, else the search), Escape (close, then clear), Tab leaves; a polite status announces the number of suggestions. Without JavaScript it is a plain form to `/search`.
- **Results page.** When no product contains the typed words, the API lists the nearest spellings (with the same filters, facets and sorting) and flags `fuzzyMatched`; the page says «نتیجهٔ دقیقی پیدا نشد؛ نزدیک‌ترین…».

## Reviews and ratings

Guests (no account) submit name, mobile number, 1–5 stars and a text (10–2000 characters). Nothing is public until a moderator approves it; **no review is ever seeded or invented**.

- **Submission.** `POST /api/v1/store/reviews` (storefront key, like checkout), per-IP rate limit (5 per 10 minutes), only for published products, **one review per mobile number per product** (a unique index; the number is normalised, so `+98…` and `09…` are the same person; a rejected review still uses the number). A hidden honeypot field makes a script get the normal «thanks» while nothing is stored.
- **Moderation.** Own permission `Reviews.Manage`; `/admin/reviews` (pending / approved / rejected tabs with counts, the number visible to staff only), approve and reject (an approved review can be taken down again), 409 on a stale row version, every decision in `ReviewModerationLogs` (who, when, which review) and shown on the page.
- **Public.** `GET /api/v1/catalog/reviews/{slug}`: approved reviews (name, stars, text, date; never the number), the count and the average (one decimal). The product page shows them under the FAQ, with the form behind a «write your review» disclosure.
- **Structured data.** `aggregateRating` and `review` (up to ten) are added to the Product JSON-LD **only when at least one approved review exists**; otherwise no rating at all. The shapes are validated in unit and e2e tests.

Not done on purpose: reviews of verified buyers only (no accounts), helpful votes, replies, photos in reviews, e-mail or SMS notifications (no external services).
