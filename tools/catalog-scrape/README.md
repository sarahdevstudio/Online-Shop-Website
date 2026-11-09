# catalog-scrape

One-time import of the real catalog from the old site, ghr-co.com. Dev-only: never run by the app or CI,
and the app/API read only the local files this produces, never the network.

## Run

```
node tools/catalog-scrape/scrape.mjs
```

Crawls politely (1 request/second, `User-Agent: shop-mvp-catalog-import`), writes:

- `src/Api/SeedData/initial-catalog/images/<product-slug>-<n>.webp` — converted from the source JPEGs
  (max 1600px long edge, quality ~80, metadata stripped).
- `src/Api/SeedData/initial-catalog/manifest.json` — categories and products (name, slug, price in Toman,
  specs, description, image list) that `import-initial-catalog` reads.

Re-running it against the same source produces the same manifest byte-for-byte (sorted keys, products
sorted by slug), so a re-scrape diffs cleanly against the committed one.

Reuses the storefront's own `sharp` dependency directly from `storefront/node_modules` — this script adds
no dependency of its own.

## Test

```
node --test tools/catalog-scrape/scrape.test.mjs
```
