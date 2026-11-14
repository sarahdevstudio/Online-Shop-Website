# 19. Initial content import (ghr-co.com)

Beyond the catalog (docs/06 §8), the previous site also had real about/contact text, four blog posts and
confirmed store information (phones, address, Instagram). This was imported once, the same way as the catalog:
dev-only tools, local files only at runtime, nothing published automatically.

## What was imported, and what was not

- **About / Contact pages**: the previous site's real text replaced the seeded placeholder body in
  `ContentSeeds.cs` (`src/Infrastructure/Content/ContentSeeds.cs`). The draft marker stays, so the pages are
  still hidden from the header menu until an admin reviews and publishes them (docs/11, entity-link draft
  hiding).
- **Terms, returns, privacy**: the previous site has no usable equivalent (terms/returns don't exist at all;
  `privacy-policy/` exists as a URL but its body is an empty shell with no real content). F0 (below) later
  replaced these three with store-specific drafted text instead of the previous site's text, since there was
  none to pull in.

## F0: store-specific terms/returns/privacy drafts

With no previous-site text to import for these three pages, `ContentSeeds.cs` was given real,
store-specific drafted text instead (not placeholder "[نام فروشگاه]" text): built from facts already known
(address, phones, the warranty/installation/shipping promises from the about page, the card-to-card payment
flow) plus Iran's e-commerce/consumer-protection law basics for a lighting retailer. The draft marker stays on
all three; genuinely unknown facts (legal name, national ID, exact payment/refund deadlines, the carrier for
other cities, data retention period) stay as `[...]` placeholders for the owner to fill in. The previous
(original placeholder) text is kept as `ContentSeeds.PreviousTermsBody` / `PreviousReturnsBody` /
`PreviousPrivacyBody`, the same way `PreviousAboutBody` / `PreviousContactBody` were kept.

`ImportInitialContentCommand`'s overwrite rule was generalized from the two pages (about/contact) to all five:
a `PreviousPageDefaults` dictionary (slug → previous title/body) drives one loop instead of two near-duplicate
blocks, so terms/returns/privacy are now also updated when a page is still on its previous seed, and left alone
(and reported as "kept (edited by owner)") otherwise — same rule, same idempotency.
- **Blog**: the previous site's 4 posts (2019) were scraped and are seeded as Draft `BlogPost` rows (never
  published by this import). `BlogPost` has no cover-image field in the current domain model, so each post's
  feature image is kept only as a local WebP file under `src/Api/SeedData/initial-content/images/`
  (~450 KB total) — not wired into the post row or rendered; markdown images are intentionally disabled in the
  storefront's renderer (`storefront/src/lib/markdown.ts`) for security, so embedding it in the body would not
  have rendered either.
- **Store information**: `StoreDefaults` in `appsettings.json` (`StorePhone`, `StoreAddress`,
  `InstagramHandle`) now carry the previous site's confirmed values. `WorkingHours` stays empty — not shown on
  the previous site. The chrome social links in `SiteDefaults.cs` (Twitter, Pinterest, Aparat) already matched
  the previous site exactly, so no code change was needed there; there is no Instagram field in `SocialLinks`
  (Instagram is the `StoreDefaults.InstagramHandle` setting instead).
- **Home hero slides, brand assets**: not imported. The previous site's 3 hero texts are documented below for
  reference only. The previous site's logo (300×70) is already byte-identical to `docs/brand/logo.png`; its
  favicon (80×80) is smaller than the one already seeded (512×512) — nothing to upgrade.

Previous-site hero slide texts (not imported, kept here for reference only):
1. "ترکیبی از کلاسیک و مدرن" / "ورود به فروشگاه"
2. "شکوه و زیبایی با لوسترها قصر روشنایی" / "مشاهده کالکشن جدید"
3. "نورپردازی و مشاوره حرفه‌ای" / "درخواست مشاوره"

## The two dev-only steps

1. `tools/content-scrape/scrape.mjs` crawls the previous site's blog posts politely (1 request/second,
   `shop-mvp-content-import` user agent) via its sitemap, reads title/date/feature-image from the theme's
   "latest posts" widget on `/news/` (the only place that shows them — the single-post template hides both),
   and writes `src/Api/SeedData/initial-content/manifest.json` plus WebP images. English ASCII slugs have no
   source equivalent and are assigned once in the script (`SLUG_BY_PATH`). Deterministic: posts sorted by
   slug, object keys sorted, so a re-scrape diffs cleanly.
2. `dotnet Shop.Api.dll import-initial-content [--dry-run]` reads the manifest and `ContentSeeds` (never the
   network) and applies it in one transaction:
   - **Content pages / store settings — owner edits always win.** A page or setting field is overwritten
     only when its current value is still *exactly* the value seeded before this import existed
     (`ImportInitialContentCommand.PreviousDefaults`, mirroring `ContentSeeds.PreviousAboutBody` /
     `PreviousContactBody`). Anything else is printed as "kept (edited by owner)" and left untouched. This
     also makes the command idempotent: after the first real run the stored value no longer equals the old
     default, so a re-run changes nothing.
   - **Blog posts — create-only by slug.** An existing post (by slug) is never updated or removed.
   - Never touches payment fields (`CardNumber`, `CardHolderName`, `BankName`, `CardToCardEnabled`).
   - Runs `RouteSeeder.EnsureAsync` afterwards, same as `import-initial-catalog`, so new blog posts get a
     route immediately instead of waiting for the next API start.

## Applying to an existing database (e.g. ShopMvp_Dev)

Back up first (`BACKUP DATABASE ... TO DISK`, plus the media folder), then:
```
dotnet Shop.Api.dll import-initial-content --dry-run
dotnet Shop.Api.dll import-initial-content
dotnet Shop.Api.dll import-initial-content   # re-run: 0 changes
```
Check afterwards: About/Contact contain the previous site's text and still have the draft marker (so they
stay hidden in the header); store info is visible in `/admin/settings`; payment fields are unchanged; the 4
blog posts exist as Draft in `/admin/blog`; nothing was published.
