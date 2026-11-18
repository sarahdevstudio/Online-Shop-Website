import { describe, expect, it } from 'vitest';
import { lastNDaysRangeUtc, tehranDayRangeUtc, tehranIsoDate } from '../src/lib/admin/tehran';

describe('tehran time helpers', () => {
  it('tehranIsoDate: Tehran midnight is 20:30 UTC the previous day', () => {
    expect(tehranIsoDate(new Date('2026-07-01T20:29:00Z'))).toBe('2026-07-01');
    expect(tehranIsoDate(new Date('2026-07-01T20:31:00Z'))).toBe('2026-07-02');
  });

  it('tehranDayRangeUtc: one calendar day is exactly [20:30 UTC previous day, 20:30 UTC that day)', () => {
    const { fromUtc, toUtc } = tehranDayRangeUtc('2026-07-01');
    expect(fromUtc.toISOString()).toBe('2026-06-30T20:30:00.000Z');
    expect(toUtc.toISOString()).toBe('2026-07-01T20:30:00.000Z');
  });

  it('lastNDaysRangeUtc: 7 days ending today spans exactly 7×24h and ends at the next Tehran midnight', () => {
    const now = new Date('2026-07-10T12:00:00Z'); // 15:30 Tehran
    const { fromUtc, toUtc } = lastNDaysRangeUtc(7, now);
    expect(toUtc.toISOString()).toBe('2026-07-10T20:30:00.000Z');
    expect(toUtc.getTime() - fromUtc.getTime()).toBe(7 * 24 * 3600_000);
  });
});
