import { describe, expect, it } from 'vitest';
import { SOCIAL_PLATFORMS, normalizeChrome, parseChromePayload, socialForSave } from '../src/lib/admin/site-chrome-model';

describe('chrome model', () => {
  it('fills missing parts with safe defaults', () => {
    const chrome = normalizeChrome({});
    expect(chrome.announcement).toEqual({ enabled: false, text: '', link: null, style: 'primary', dismissible: true });
    expect(chrome.headerMenu).toEqual([]);
    expect(chrome.footer).toEqual({ about: '', columns: [] });
    expect(Object.keys(chrome.social)).toEqual([...SOCIAL_PLATFORMS]);
    expect(Object.values(chrome.social).every((v) => v === null)).toBe(true);
  });

  it('keeps stored values', () => {
    const chrome = normalizeChrome({ social: { twitter: 'https://x.com/e_ghasr' } as never, announcement: { enabled: true, text: 'سلام', link: '/', style: 'accent', dismissible: false } });
    expect(chrome.social.twitter).toBe('https://x.com/e_ghasr');
    expect(chrome.social.telegram).toBeNull();
    expect(chrome.announcement.style).toBe('accent');
  });

  it('turns empty social inputs into null and trims', () => {
    const values = Object.fromEntries(SOCIAL_PLATFORMS.map((p) => [p, ''])) as Record<(typeof SOCIAL_PLATFORMS)[number], string>;
    values.telegram = '  my_shop ';
    const saved = socialForSave(values);
    expect(saved.telegram).toBe('my_shop');
    expect(saved.aparat).toBeNull();
  });

  it('parses defensively', () => {
    expect(parseChromePayload('{}')?.headerMenu).toEqual([]);
    expect(parseChromePayload('nope')).toBeNull();
    expect(parseChromePayload('5')).toBeNull();
  });
});
