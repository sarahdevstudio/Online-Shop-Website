/**
 * WCAG 2.2 contrast math and the theme contrast report. Pure functions: the admin editor (live report) and the storefront
 * (derived colors) use this file; the API has the same algorithm with the same golden test vectors.
 */

export const HEX_COLOR = /^#[0-9a-fA-F]{6}$/;

export const NEAR_BLACK = '#111111';
export const WHITE = '#ffffff';

export function isHexColor(value: unknown): value is string {
  return typeof value === 'string' && HEX_COLOR.test(value);
}

export function parseHex(hex: string): [number, number, number] {
  return [1, 3, 5].map((i) => Number.parseInt(hex.slice(i, i + 2), 16)) as [number, number, number];
}

export function toHex([r, g, b]: readonly [number, number, number]): string {
  return `#${[r, g, b].map((v) => Math.round(Math.min(255, Math.max(0, v))).toString(16).padStart(2, '0')).join('')}`;
}

function channel(value: number): number {
  const s = value / 255;
  return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
}

export function luminance(hex: string): number {
  const [r, g, b] = parseHex(hex);
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

/** Contrast ratio between two `#rrggbb` colors, 1..21. */
export function contrastRatio(a: string, b: string): number {
  const la = luminance(a);
  const lb = luminance(b);
  return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
}

/** `#rrggbb` mixed `amount` (0..1) of the way from `from` toward `to`. */
export function mix(from: string, to: string, amount: number): string {
  const a = parseHex(from);
  const b = parseHex(to);
  return toHex(a.map((v, i) => v + (b[i] - v) * amount) as [number, number, number]);
}

/** The text color for a fill: white or near-black, whichever has the higher contrast (computed, never stored). */
export function onColor(fill: string): string {
  return contrastRatio(WHITE, fill) >= contrastRatio(NEAR_BLACK, fill) ? WHITE : NEAR_BLACK;
}

export interface ThemeColors {
  primary: string;
  secondary: string;
  accent: string;
  background: string;
  surface: string;
  text: string;
  success: string;
  warning: string;
  danger: string;
}

export interface ContrastItem {
  id: string;
  foreground: string;
  background: string;
  ratio: number;
  min: number;
  ok: boolean;
  message: string;
}

const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹';

/** `3.2` → `۳٫۲` (two decimals at most, trailing zero trimmed). */
export function persianRatio(ratio: number): string {
  const text = (Math.floor(ratio * 100) / 100).toFixed(2).replace(/\.?0+$/, '');
  return text.replace(/\d/g, (d) => PERSIAN_DIGITS[Number(d)]).replace('.', '٫');
}

interface PairSpec {
  id: string;
  title: string;
  foreground: (theme: ThemeColors) => string;
  background: (theme: ThemeColors) => string;
  min: number;
}

const PAIRS: readonly PairSpec[] = [
  { id: 'text-background', title: 'متن روی پس‌زمینه', foreground: (t) => t.text, background: (t) => t.background, min: 4.5 },
  { id: 'text-surface', title: 'متن روی سطح', foreground: (t) => t.text, background: (t) => t.surface, min: 4.5 },
  { id: 'primary-background', title: 'رنگ اصلی (به‌عنوان متن) روی پس‌زمینه', foreground: (t) => t.primary, background: (t) => t.background, min: 4.5 },
  { id: 'primary-surface', title: 'رنگ اصلی (به‌عنوان متن) روی سطح', foreground: (t) => t.primary, background: (t) => t.surface, min: 4.5 },
  { id: 'on-primary', title: 'متن روی رنگ اصلی', foreground: (t) => onColor(t.primary), background: (t) => t.primary, min: 4.5 },
  { id: 'on-secondary', title: 'متن روی رنگ ثانویه', foreground: (t) => onColor(t.secondary), background: (t) => t.secondary, min: 4.5 },
  { id: 'on-accent', title: 'متن روی رنگ تأکیدی', foreground: (t) => onColor(t.accent), background: (t) => t.accent, min: 4.5 },
  { id: 'success-background', title: 'رنگ موفقیت (متن) روی پس‌زمینه', foreground: (t) => t.success, background: (t) => t.background, min: 4.5 },
  { id: 'warning-background', title: 'رنگ هشدار (متن) روی پس‌زمینه', foreground: (t) => t.warning, background: (t) => t.background, min: 4.5 },
  { id: 'danger-background', title: 'رنگ خطا (متن) روی پس‌زمینه', foreground: (t) => t.danger, background: (t) => t.background, min: 4.5 },
  { id: 'focus-ring', title: 'حلقهٔ فوکوس (رنگ اصلی) روی پس‌زمینه', foreground: (t) => t.primary, background: (t) => t.background, min: 3 },
];

/** The WCAG AA report for a palette whose colors are all valid `#rrggbb`. */
export function contrastReport(theme: ThemeColors): ContrastItem[] {
  return PAIRS.map((pair) => {
    const foreground = pair.foreground(theme);
    const background = pair.background(theme);
    const ratio = contrastRatio(foreground, background);
    const ok = ratio >= pair.min;
    return {
      id: pair.id,
      foreground,
      background,
      ratio: Math.floor(ratio * 100) / 100,
      min: pair.min,
      ok,
      message: `${pair.title}: ${persianRatio(ratio)} به ۱ — حداقل ${persianRatio(pair.min)}`,
    };
  });
}

/** `color` moved toward `toward` just far enough to reach `min` against every background (used for derived tokens). */
export function ensureContrast(color: string, toward: string, backgrounds: readonly string[], min: number): string {
  for (let step = 0; step <= 20; step += 1) {
    const candidate = mix(color, toward, step / 20);
    if (backgrounds.every((bg) => contrastRatio(candidate, bg) >= min)) return candidate;
  }
  return toward;
}
