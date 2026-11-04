import { describe, expect, it } from 'vitest';
import { contrastRatio } from '../src/lib/contrast';
import { DEFAULT_THEME, derivedContrast, fontFaceCss, fontPreload, fontPreloads, sanitizeTheme, themeColor, themeCss, themeTokens } from '../src/lib/theme-css';

const SEED = { ...DEFAULT_THEME, primary: '#5b0057', secondary: '#76007c', accent: '#d8c831', surface: '#f2f2f2', text: '#1a1a1a', font: 'sahel', radius: 'sm' };

describe('themeCss', () => {
  it('maps the palette to the semantic tokens', () => {
    const css = themeCss(SEED);
    expect(css.startsWith(':root{')).toBe(true);
    expect(css).toContain('--color-primary:#5b0057');
    expect(css).toContain('--color-primary-contrast:#ffffff');
    expect(css).toContain('--color-accent-contrast:#111111');
    expect(css).toContain('--color-surface-muted:#f2f2f2');
    expect(css).toContain("--font-sans:'Sahel'");
    expect(css).toContain('--radius-md:0.375rem');
  });

  it('is the built-in look for no input at all', () => {
    expect(themeTokens(undefined)['--color-primary']).toBe('#8a4f00');
    expect(themeTokens(null)['--radius-md']).toBe('0.625rem');
    expect(themeTokens(42)['--font-sans']).toContain("'Vazirmatn'");
  });

  it('keeps derived text and control borders readable', () => {
    for (const theme of [SEED, DEFAULT_THEME, { ...SEED, text: '#000000' }]) {
      const d = derivedContrast(theme);
      expect(d.mutedOnBackground).toBeGreaterThanOrEqual(4.5);
      expect(d.borderControlOnBackground).toBeGreaterThanOrEqual(3);
    }
    expect(contrastRatio(themeTokens(SEED)['--color-primary-hover'], '#ffffff')).toBeGreaterThan(13);
  });

  it('uses the primary color for the viewport theme color', () => {
    expect(themeColor(SEED)).toBe('#5b0057');
  });
});

describe('injection safety', () => {
  const hostile = ['#5b0057;}body{display:none', 'red', 'url(javascript:alert(1))', '</style><script>alert(1)</script>', '#12345', '#5b0057\n', "'; background:url(//evil)"];

  it('never lets a hostile color reach the CSS', () => {
    for (const bad of hostile) {
      const css = themeCss({ ...SEED, primary: bad, secondary: bad, accent: bad, background: bad, surface: bad, text: bad, success: bad, warning: bad, danger: bad });
      expect(css).not.toMatch(/[<>]|script|evil|javascript|display:none|url\(/i);
      expect(css).toContain(`--color-primary:${DEFAULT_THEME.primary}`);
    }
  });

  it('never lets a hostile enum reach the CSS', () => {
    const css = themeCss({ ...SEED, font: "x';}</style>", radius: 'url(evil)' });
    expect(css).not.toMatch(/evil|<|style>/);
    expect(css).toContain("--font-sans:'Vazirmatn'");
    expect(css).toContain('--radius-md:0.625rem');
  });

  it('contains only the known custom properties', () => {
    const names = Object.keys(themeTokens({ ...SEED, primary: '</style>' }));
    expect(names.every((n) => /^--(font|radius|color)-[a-z-]+$/.test(n))).toBe(true);
  });

  it('sanitizes fields independently and lower-cases colors', () => {
    const t = sanitizeTheme({ primary: '#ABCDEF', font: 'comic-sans', radius: 'lg', text: 5 });
    expect(t.primary).toBe('#abcdef');
    expect(t.font).toBe('vazirmatn');
    expect(t.radius).toBe('lg');
    expect(t.text).toBe(DEFAULT_THEME.text);
  });
});

describe('fonts', () => {
  it('emits @font-face only for the selected font, from /fonts', () => {
    const css = fontFaceCss('shabnam');
    expect(css).toContain("font-family:'Shabnam'");
    expect(css).toContain('/fonts/shabnam/Shabnam-Bold.woff2');
    expect(css).not.toMatch(/Sahel|Estedad|Vazirmatn/);
    expect(css).not.toMatch(/https?:/);
  });

  it('uses variable files with unicode ranges for Vazirmatn and Estedad', () => {
    expect(fontFaceCss('vazirmatn')).toContain("format('woff2-variations')");
    expect(fontFaceCss('estedad')).toContain('unicode-range:U+0600-06FF');
  });

  it('preloads exactly one regular-weight file', () => {
    expect(fontPreload('vazirmatn')).toEqual({ href: '/fonts/vazirmatn/vazirmatn-arabic-wght-normal.woff2', type: 'font/woff2' });
    expect(fontPreload('sahel').href).toBe('/fonts/sahel/Sahel.woff2');
    // the default font is the subset one: Persian digits and the ZWNJ are covered, the weight axis is the one the shop uses
    expect(fontFaceCss('vazirmatn')).toContain('U+06F0-06F9');
    expect(fontFaceCss('vazirmatn')).toContain('font-weight:400 700');
    // the two-subset families also preload the Latin subset every page uses; the multi-weight families only the regular file
    expect(fontPreloads('vazirmatn').map((p) => p.href)).toEqual(['/fonts/vazirmatn/vazirmatn-arabic-wght-normal.woff2', '/fonts/vazirmatn/vazirmatn-latin-wght-normal.woff2']);
    expect(fontPreloads('sahel').map((p) => p.href)).toEqual(['/fonts/sahel/Sahel.woff2']);
  });
});
