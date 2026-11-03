import { describe, expect, it } from 'vitest';
import { contrastRatio, contrastReport, ensureContrast, isHexColor, mix, onColor, persianRatio } from '../src/lib/contrast';

const SEED = {
  primary: '#5b0057',
  secondary: '#76007c',
  accent: '#d8c831',
  background: '#ffffff',
  surface: '#f2f2f2',
  text: '#1a1a1a',
  success: '#1b7f3b',
  warning: '#8a5a00',
  danger: '#b3261e',
};

describe('contrast ratio (golden vectors shared with the API tests)', () => {
  it.each([
    ['#5b0057', '#ffffff', 13.27],
    ['#76007c', '#ffffff', 10.19],
    ['#d8c831', '#5b0057', 7.73],
    ['#d8c831', '#ffffff', 1.72],
    ['#000000', '#ffffff', 21],
    ['#ffffff', '#ffffff', 1],
  ])('%s on %s is %s', (a, b, expected) => {
    expect(contrastRatio(a, b)).toBeCloseTo(expected, 1);
    expect(contrastRatio(b, a)).toBeCloseTo(expected, 1);
  });
});

describe('onColor', () => {
  it('picks white or near-black, whichever reads better', () => {
    expect(onColor('#5b0057')).toBe('#ffffff');
    expect(onColor('#d8c831')).toBe('#111111');
    expect(onColor('#ffffff')).toBe('#111111');
    expect(onColor('#000000')).toBe('#ffffff');
  });
});

describe('helpers', () => {
  it('validates hex colors strictly', () => {
    expect(isHexColor('#5b0057')).toBe(true);
    expect(isHexColor('#5B0057')).toBe(true);
    for (const bad of ['5b0057', '#5b005', '#5b00577', 'red', 'rgb(0,0,0)', '#5b0057;}', '', null, 12]) expect(isHexColor(bad)).toBe(false);
  });

  it('mixes colors', () => {
    expect(mix('#000000', '#ffffff', 0.5)).toBe('#808080');
    expect(mix('#102030', '#102030', 0.7)).toBe('#102030');
  });

  it('writes ratios with Persian digits', () => {
    expect(persianRatio(3.2)).toBe('۳٫۲');
    expect(persianRatio(4.5)).toBe('۴٫۵');
    expect(persianRatio(13.279)).toBe('۱۳٫۲۷');
  });

  it('moves a color just far enough toward another to reach the minimum', () => {
    const adjusted = ensureContrast('#999999', '#000000', ['#ffffff'], 4.5);
    expect(contrastRatio(adjusted, '#ffffff')).toBeGreaterThanOrEqual(4.5);
    expect(ensureContrast('#000000', '#000000', ['#ffffff'], 4.5)).toBe('#000000');
  });
});

describe('contrastReport', () => {
  it('passes for the seeded palette, accent only as a fill', () => {
    const report = contrastReport(SEED);
    expect(report.every((item) => item.ok)).toBe(true);
    expect(report.map((item) => item.id)).toContain('on-accent');
  });

  it('names the failing pair and the ratio in Persian', () => {
    const report = contrastReport({ ...SEED, primary: '#d8c831' });
    const failing = report.find((item) => item.id === 'primary-background');
    expect(failing?.ok).toBe(false);
    expect(failing?.message).toBe('رنگ اصلی (به‌عنوان متن) روی پس‌زمینه: ۱٫۷۱ به ۱ — حداقل ۴٫۵');
  });
});
