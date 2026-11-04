import { contrastRatio, ensureContrast, isHexColor, mix, onColor, type ThemeColors } from './contrast';

/**
 * The theme `<style>`: generated here from validated typed values only. Admin text never reaches the CSS: every color is
 * re-checked against `#rrggbb`, every enum is mapped to a constant, anything invalid falls back to the built-in default.
 */

export const FONT_IDS = ['vazirmatn', 'estedad', 'shabnam', 'sahel'] as const;
export type FontId = (typeof FONT_IDS)[number];

export const RADIUS_IDS = ['none', 'sm', 'md', 'lg'] as const;
export type RadiusId = (typeof RADIUS_IDS)[number];

export interface ThemeValues extends ThemeColors {
  font: FontId;
  radius: RadiusId;
}

/** Today's look: the semantic tokens of styles/tokens.css (brass on warm neutrals), used whenever no theme is available. */
export const DEFAULT_THEME: ThemeValues = {
  primary: '#8a4f00',
  secondary: '#51493e',
  accent: '#b46a00',
  background: '#ffffff',
  surface: '#f7f3ec',
  text: '#1b1712',
  success: '#067647',
  warning: '#9a3f00',
  danger: '#b42318',
  font: 'vazirmatn',
  radius: 'md',
};

const COLOR_KEYS = ['primary', 'secondary', 'accent', 'background', 'surface', 'text', 'success', 'warning', 'danger'] as const;

/** Each field validated on its own: a bad color or enum becomes the default for that field only. */
export function sanitizeTheme(input: unknown): ThemeValues {
  const source = (typeof input === 'object' && input !== null ? input : {}) as Record<string, unknown>;
  const theme: ThemeValues = { ...DEFAULT_THEME };
  for (const key of COLOR_KEYS) {
    const value = source[key];
    if (isHexColor(value)) theme[key] = value.toLowerCase();
  }
  if ((FONT_IDS as readonly unknown[]).includes(source.font)) theme.font = source.font as FontId;
  if ((RADIUS_IDS as readonly unknown[]).includes(source.radius)) theme.radius = source.radius as RadiusId;
  return theme;
}

// ---- fonts (files live in public/fonts/<family>/, see the README there) ----

const ARABIC_RANGE =
  'U+0600-06FF,U+0750-077F,U+0870-088E,U+0890-0891,U+0897-08E1,U+08E3-08FF,U+200C-200E,U+2010-2011,U+204F,U+2E41,U+FB50-FDFF,U+FE70-FE74,U+FE76-FEFC';
const LATIN_RANGE =
  'U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD';

interface FontFile {
  file: string;
  weight: string;
  range?: string;
  variable?: boolean;
}

interface FontSpec {
  family: string;
  folder: string;
  files: readonly FontFile[];
  /** The regular-weight file that is preloaded. */
  preload: string;
}

const FONTS: Record<FontId, FontSpec> = {
  vazirmatn: {
    family: 'Vazirmatn',
    folder: 'vazirmatn',
    files: [
      { file: 'vazirmatn-arabic-wght-normal.woff2', weight: '100 900', range: ARABIC_RANGE, variable: true },
      { file: 'vazirmatn-latin-wght-normal.woff2', weight: '100 900', range: LATIN_RANGE, variable: true },
    ],
    preload: 'vazirmatn-arabic-wght-normal.woff2',
  },
  estedad: {
    family: 'Estedad',
    folder: 'estedad',
    files: [
      { file: 'estedad-arabic-wght-normal.woff2', weight: '100 900', range: ARABIC_RANGE, variable: true },
      { file: 'estedad-latin-wght-normal.woff2', weight: '100 900', range: LATIN_RANGE, variable: true },
    ],
    preload: 'estedad-arabic-wght-normal.woff2',
  },
  shabnam: {
    family: 'Shabnam',
    folder: 'shabnam',
    files: [
      { file: 'Shabnam.woff2', weight: '400' },
      { file: 'Shabnam-Medium.woff2', weight: '500' },
      { file: 'Shabnam-Bold.woff2', weight: '700' },
    ],
    preload: 'Shabnam.woff2',
  },
  sahel: {
    family: 'Sahel',
    folder: 'sahel',
    files: [
      { file: 'Sahel.woff2', weight: '400' },
      { file: 'Sahel-Bold.woff2', weight: '700' },
    ],
    preload: 'Sahel.woff2',
  },
};

const FALLBACK_STACK = "system-ui, -apple-system, 'Segoe UI', Tahoma, sans-serif";

const fontUrl = (spec: FontSpec, file: string) => `/fonts/${spec.folder}/${file}`;

export function fontStack(font: FontId): string {
  return `'${FONTS[font].family}', ${FALLBACK_STACK}`;
}

/** `@font-face` rules for the one selected font. */
export function fontFaceCss(font: FontId): string {
  const spec = FONTS[font];
  return spec.files
    .map(
      (f) =>
        `@font-face{font-family:'${spec.family}';font-style:normal;font-display:swap;font-weight:${f.weight};` +
        `src:url(${fontUrl(spec, f.file)}) format('woff2${f.variable ? '-variations' : ''}');${f.range ? `unicode-range:${f.range};` : ''}}`,
    )
    .join('');
}

/** The `<link rel="preload" as="font">` for the selected font (regular weight). */
export function fontPreload(font: FontId): { href: string; type: 'font/woff2' } {
  const spec = FONTS[font];
  return { href: fontUrl(spec, spec.preload), type: 'font/woff2' };
}

/**
 * Every font file EVERY page needs: the preloaded regular file and, for the two-subset families, the Latin subset too (digits in
 * prices, the copyright sign and brand names are on all pages, so the browser always fetches it; preloading starts the request
 * with the HTML instead of after the style sheets). The other weights of the multi-file families load when used.
 */
export function fontPreloads(font: FontId): Array<{ href: string; type: 'font/woff2' }> {
  const spec = FONTS[font];
  const hrefs = new Set([fontUrl(spec, spec.preload)]);
  for (const f of spec.files) if (f.variable && f.range === LATIN_RANGE) hrefs.add(fontUrl(spec, f.file));
  return [...hrefs].map((href) => ({ href, type: 'font/woff2' }));
}

// ---- tokens ----

const RADII: Record<RadiusId, readonly [string, string, string]> = {
  none: ['0', '0', '0'],
  sm: ['0.25rem', '0.375rem', '0.625rem'],
  md: ['0.375rem', '0.625rem', '1rem'],
  lg: ['0.625rem', '1rem', '1.5rem'],
};

const WHITE = '#ffffff';
const BLACK = '#000000';

/**
 * A pale background for text in `color` (badges, soft buttons): a tint of the color itself, made paler until the color stays
 * readable (4.5:1) on it. A fixed 90% tint left some valid palettes (success #1b7f3b) at 4.42:1.
 */
function softTint(color: string, start: number): string {
  for (let amount = start; amount <= 0.97; amount += 0.01) {
    const soft = mix(color, WHITE, amount);
    if (contrastRatio(color, soft) >= 4.5) return soft;
  }
  return mix(color, WHITE, 0.97);
}

/** The CSS custom properties for a theme, as an ordered name → value map (fixed formulas, no free text). */
export function themeTokens(input: unknown): Record<string, string> {
  const t = sanitizeTheme(input);
  const pageColors = [t.background, t.surface];
  const mutedText = ensureContrast(mix(t.text, t.background, 0.2), t.text, pageColors, 4.5);
  const subtleText = ensureContrast(mix(t.text, t.background, 0.35), t.text, pageColors, 4.5);
  const controlBorder = ensureContrast(mix(t.background, t.text, 0.5), t.text, pageColors, 3);
  const [sm, md, lg] = RADII[t.radius];

  return {
    '--font-sans': fontStack(t.font),
    '--radius-sm': sm,
    '--radius-md': md,
    '--radius-lg': lg,

    '--color-bg': t.background,
    '--color-surface': t.background,
    '--color-surface-muted': t.surface,
    '--color-surface-sunken': mix(t.surface, BLACK, 0.05),
    '--color-border': mix(t.background, t.text, 0.14),
    '--color-border-control': controlBorder,

    '--color-text': t.text,
    '--color-text-muted': mutedText,
    '--color-text-subtle': subtleText,

    '--color-primary': t.primary,
    '--color-primary-hover': mix(t.primary, BLACK, 0.2),
    '--color-primary-active': mix(t.primary, BLACK, 0.35),
    '--color-primary-contrast': onColor(t.primary),
    '--color-primary-soft': softTint(t.primary, 0.9),
    '--color-secondary': t.secondary,
    '--color-secondary-contrast': onColor(t.secondary),
    '--color-accent': t.accent,
    '--color-accent-contrast': onColor(t.accent),

    '--color-danger': t.danger,
    '--color-danger-hover': mix(t.danger, BLACK, 0.2),
    '--color-danger-contrast': onColor(t.danger),
    '--color-danger-soft': softTint(t.danger, 0.92),
    '--color-success': t.success,
    '--color-success-contrast': onColor(t.success),
    '--color-success-soft': softTint(t.success, 0.9),
    '--color-warning': t.warning,
    '--color-warning-soft': softTint(t.warning, 0.9),

    '--color-focus': t.primary,
  };
}

/** The `:root{…}` block for a theme (light only; the dark theme is not editable). */
export function themeCss(input: unknown): string {
  const body = Object.entries(themeTokens(input))
    .map(([name, value]) => `${name}:${value}`)
    .join(';');
  return `:root{${body}}`;
}

/** The viewport `theme-color`. */
export function themeColor(input: unknown): string {
  return sanitizeTheme(input).primary;
}

/** The pair of derived tokens that must stay readable, exposed for tests. */
export function derivedContrast(input: unknown): { mutedOnBackground: number; borderControlOnBackground: number } {
  const tokens = themeTokens(input);
  const t = sanitizeTheme(input);
  return {
    mutedOnBackground: contrastRatio(tokens['--color-text-muted'], t.background),
    borderControlOnBackground: contrastRatio(tokens['--color-border-control'], t.background),
  };
}
