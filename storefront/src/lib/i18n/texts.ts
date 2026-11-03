/**
 * Text lookup for the storefront and the admin panel (docs/11 B.5). Every user-facing string is a key in
 * `translations/fa-IR.json`; this module returns the edited text when the API supplied one and otherwise the text shipped in the
 * bundle, so a page always renders with real words, whether or not the API is up. A key is never shown.
 *
 * Isomorphic on purpose: server components read the dictionary the server loaded (`./texts-server`), client components get the
 * edited texts through `<TextsProvider>` before they render, so the HTML and the hydration agree.
 */

// Type-only: the JSON itself is imported by `messages.ts` (shop) and `admin/messages.ts` (admin) and registered below, so the
// public pages never ship the admin panel's texts and the admin never ships the shop's more than it needs.
export type ShopTextKey = keyof typeof import('./defaults.shop.json');
export type AdminTextKey = keyof typeof import('./defaults.admin.json');
export type TextKey = ShopTextKey | AdminTextKey;

const defaults: Record<string, string> = {};

/** Registers the shipped texts of one bundle (called once by the module that owns the bundle). */
export function registerDefaults(texts: Readonly<Record<string, string>>) {
  Object.assign(defaults, texts);
}

let overrides: Readonly<Record<string, string>> = {};

/** Replaces the edited texts (a key → text map; entries that equal the shipped text are harmless). */
export function setTexts(next: Readonly<Record<string, string>>) {
  overrides = next;
}

export function currentTexts(): Readonly<Record<string, string>> {
  return overrides;
}

/** The shipped text for a key, ignoring edits (used by the editor's "default" column and by tests). */
export function defaultText(key: TextKey): string {
  return defaults[key];
}

export function t(key: TextKey): string {
  return overrides[key] ?? defaults[key];
}

/** Text with named placeholders: `tf('shop.cart.line', { count: '۳' })`. A placeholder without a value stays as written. */
export function tf(key: TextKey, args: Readonly<Record<string, string | number>>): string {
  return t(key).replace(/\{([A-Za-z][A-Za-z0-9]*)\}/g, (whole, name: string) => (name in args ? String(args[name]) : whole));
}
