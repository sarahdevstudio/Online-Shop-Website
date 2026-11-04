# Bundled fonts

Only free fonts under the SIL Open Font License 1.1, served from this folder (never from a CDN). Each folder keeps its license text.

| Family | Files | Source |
|---|---|---|
| Vazirmatn (default) | variable arabic + latin, **subset** (Persian letters/digits/punctuation, ASCII) with the weight axis 400-700, preloaded; made by `scripts/perf/subset-vazirmatn.py` from the package. Plus the **complete** font as `vazirmatn-full-{arabic,latin,latin-ext}-wght-normal.woff2` (the package files, unchanged: 46 + 34.5 + 22 KB), same family name and weights, with a `unicode-range` of everything the subset leaves out, never preloaded: a page downloads one only when it contains such a character | `@fontsource-variable/vazirmatn` 5.3.0 |
| Estedad | variable arabic + latin | `@fontsource-variable/estedad` 5.3.0 |
| Shabnam | 400 / 500 / 700 | `shabnam-font` 5.0.0 (Shabnam also contains Roboto glyphs, Apache-2.0, see its LICENSE) |
| Sahel | 400 / 700 | `sahel-font` 3.4.0 |

Commercial fonts (IRANSans, IRANYekan, Dana, Yekan Bakh, MJ_*, B Nazanin, ...) are never offered.

Files are served with `Cache-Control: public, max-age=31536000, immutable` (next.config.ts), so a changed font must get a new file name.
