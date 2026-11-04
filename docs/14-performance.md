# 14. Performance (Core Web Vitals)

Measured with Lighthouse (mobile preset: Moto G Power emulation, simulated slow 4G at 1.6 Mbps / 150 ms RTT, 4x CPU slowdown) on a **production build** (`next build` + `next start`) with the API in Development (sample catalog), five runs per page, **median** reported. Pages: home, a category, a product, the cart and a blog post. The sample catalog has no blog post and no product photo, so `storefront/scripts/perf/seed.mjs` adds one published post and one photo-sized JPEG (1600x1600, 330 KB) to the measured product; otherwise the product page's LCP would be text.

Lab data has no INP (it needs real interactions); **TBT** is its lab proxy. Run-to-run noise on a shared machine is about ±0.3 s on LCP, so differences below that are not conclusions.

## Before / after

| Page | LCP before | LCP after | CLS | TBT before | TBT after | TTFB (after) | JS | Images before → after | Fonts before → after | Total before → after |
|---|---|---|---|---|---|---|---|---|---|---|
| `/` | 2.64 s | **2.06 s** | 0 → 0 | 59 ms | 73 ms | 29 ms | 167 KB | 16 → 7 KB | 80 → 36 KB | 295 → 242 KB |
| `/category/lamps` | 2.64 s | **1.99 s** | 0 → 0 | 51 ms | 86 ms | 25 ms | 165 KB | 13 → 7 KB | 80 → 36 KB | 297 → 245 KB |
| `/product/…` | 2.89 s | **2.50 s** | 0 → 0 | 60 ms | 77 ms | 25 ms | 165 KB | 44 → 39 KB | 80 → 36 KB | 321 → 272 KB |
| `/cart` | 2.67 s | **2.03 s** | 0 → 0 | 54 ms | 58 ms | 13 ms | 165 KB | 13 → 7 KB | 80 → 36 KB | 295 → 245 KB |
| `/blog/…` | 2.36 s | **2.12 s** | 0 → 0 | 81 ms | 60 ms | 24 ms | 165 KB | 13 → 7 KB | 80 → 36 KB | 286 → 235 KB |

JS is the same before and after (167/165 KB transferred, gzip); TTFB was 17-26 ms before. Scores went from 95-97 to 97-99. TBT moved within the noise (all far below the 200 ms budget); the LCP gains come from bytes: the first paint and the LCP element compete for the simulated 1.6 Mbps link with everything else.

**The product page is at the budget edge** (median 2.50 s; nine separate runs of that page alone gave 2.1-3.1 s). Its LCP is the photo (31 KB WebP at 750 px). The CI budget is asserted on the median of five runs; if it flakes on a slower runner, the options are a 3.0 s budget for that page or a lower image quality (`images.qualities`).

## What was done

| Finding | Change |
|---|---|
| Fonts were 80 KB (two variable subsets with all weights and every Arabic block) | `scripts/perf/subset-vazirmatn.py` subsets Vazirmatn to the Persian letters, digits, punctuation and ASCII the shop writes and narrows the weight axis to 400-700 (what the CSS uses): 81 KB → 36 KB. The `unicode-range` lists exactly those code points, so anything else falls back to the next family. Rendering was compared pixel by pixel on a Persian/Latin sample at 400/500/700 (identical except sub-pixel anti-aliasing). The other three font families are unchanged. |
| The Latin font file was requested only after the style sheets (it is on every page) | Both Vazirmatn files are preloaded (`fontPreloads`) |
| Fonts were served with `max-age=0` | `/fonts/*` is `public, max-age=31536000, immutable` (a changed font needs a new file name). `/_next/static`, `/media` and `/_next/image` already were immutable / one year |
| The header logo was a plain `<img>` (300 px, 12.5 KB PNG, shown 176 px wide) | `next/image` with `sizes="176px"`: resized WebP, 13 → 7 KB, preloaded |
| Next 16 no longer adds `fetchpriority="high"` for `priority` | The LCP candidates (product main image, the first cards of a listing, the first hero slide) pass `fetchPriority="high"` explicitly |
| The announcement bar was rendered, then removed after hydration for visitors who had closed it (localStorage) | A layout shift for every returning visitor that a fresh-profile lab run never sees. The close button now stores a cookie the **server** reads, so a dismissed bar is never rendered (`lib/announcement.ts`). A bar that was closed before this change shows once more |
| CLS | 0 on all five pages (images carry width/height or a sized container, the hero and cards reserve their space); nothing to fix, kept as a budget |

## Tried and not kept

- `experimental.inlineCss` (no render-blocking style sheets): LCP got slightly **worse** (+0.1-0.2 s) and the HTML grew from 11 KB to 38 KB gzipped (the CSS is repeated in the RSC payload).
- `quality={65}` on the product image: ignored by Next 16 unless `images.qualities` lists it; the effect (about 9 KB) is below the noise, and product photos look worse. Not applied.

## Measured, not changed

- **JS (165 KB gzip)** is React DOM (73 KB), the Next client runtime (46 KB) and ~45 KB of shop code (header, forms, hero slider, texts). The `noModule` polyfill chunk is not downloaded by current browsers. The shop dictionary (~4 KB gzip) is bundled more than once because the error boundary is its own chunk group; fixing it needs a different way to give client components their texts. The build has no heavier dependency to drop.
- **API response caching**: TTFB is 13-29 ms. The storefront already caches API answers (Next data cache: 60 s products, 300 s categories and sitemap; tags for instant invalidation), the API keeps the route table, translations and site document in memory, and the browser never calls the API. HTML is `no-store` on purpose (the header shows the cart count). Nothing to gain.
- Hydration cost (about 0.5 s of scripting at 4x slowdown) is the framework's; every client component that exists is needed (header, forms, slider).

## Lighthouse CI

`storefront/lighthouserc.json` + the `lighthouse` job in `.github/workflows/ci.yml`: starts SQL Server (throwaway, masked secrets), the API in Development, builds and starts the storefront, runs `scripts/perf/seed.mjs`, then `lhci autorun`: five runs of each page, **median-run** assertions as **errors**: LCP ≤ 2.5 s, CLS ≤ 0.1, TBT ≤ 200 ms. The reports are uploaded as the `lighthouse` artifact.

Locally: start the API and the storefront as in `scripts/smoke-routing.cjs`'s header, `node scripts/perf/seed.mjs`, then `npx @lhci/cli@0.15.1 autorun --config=lighthouserc.json` in `storefront/`; `node scripts/perf/report.mjs .lighthouseci` prints the table above.
