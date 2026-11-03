# 12. Translations (texts you can edit)

Every user-facing text lives in one place: `translations/fa-IR.json` (key → Persian text). It is embedded in the API, seeded into the `Translations` table on startup, and copied into the storefront as a fallback bundle. Admins change texts in **/admin/translations** (permission `Translations.Manage`); a change is visible on the site and in API errors right after saving.

## Keys

`<area>.<thing>[.<variant>]`, lower-case ASCII, words joined with `-`, never changed once released.

| Prefix | Used by | Examples |
|---|---|---|
| `api.error.*` | problem titles and error messages | `api.error.not-found`, `api.error.cart-empty` |
| `api.validation.*` | validation messages (built-in library messages too) | `api.validation.max-length`, `api.validation.order-mobile` |
| `api.field.*` | field labels inside validation messages | `api.field.mobile` |
| `api.identity.*`, `api.site.*`, `api.order.*` | account errors, contrast report, order details | |
| `common.*`, `shop.*`, `seo.*` | storefront (served to browsers as the public bundle) | `shop.cart-empty-title` |
| `admin.*` | admin panel (served only to signed-in admins) | `admin.translations.save` |

Placeholders are named and ASCII: `{count}`, `{max}`. Numbers and dates are formatted by code before they are substituted. An edited text must keep exactly the placeholders of the shipped text; the editor and the API refuse anything else.

## Adding or changing a text in code

1. Add the key and its Persian text to `translations/fa-IR.json` (keep it sorted).
2. **C#**: Domain and Application return the key, never text: `Msg.Of("api.validation.max-length", ("max", 30))` (a plain `"api.error.x"` for no arguments). The API boundary turns it into text (`Translate.Resolve`): `ToProblem`, the validation filter, the exception handler. Operator logs and startup errors stay English.
3. **Storefront / admin**: `npm run i18n:sync` (in `storefront/`) copies the manifest into the two bundles; add an accessor to `lib/messages.ts` or `lib/admin/messages.ts` (`get x() { return t('shop.x'); }` or `tf('shop.x', { n })`). `t()` and `tf()` accept only existing keys: a typo is a compile error.
4. Run the tests: `TranslationUsageTests` (every key named in C# exists; no Persian text literal in C# outside the documented seed/marker files), `i18n.test.ts` (bundles in sync; no Persian text in storefront source outside the digit/regex helpers).

## Behavior worth knowing

- **Fallback**: edited text → shipped text → the generic `api.error.unexpected` text. A key is never shown and nothing throws; a miss is logged once per key.
- **Cache**: the API keeps an immutable snapshot of edited texts, reloaded right after an edit and every 30 s when another instance changed the table. The storefront keeps the dictionary in memory for 30 s (the instance that served the edit reloads at once) and falls back to the bundled texts if the API is down.
- **Not translated**: product/category/brand/blog/info-page content and the site-customization data (their own editors), seed/sample data, `code` values, slugs, operator logs, CLI output.
- **Admin login page**: it renders before a session exists, so it always uses the shipped admin texts.
- **Seeding**: insert-if-missing; an edited row keeps its text, an unedited row follows a changed shipped text.
