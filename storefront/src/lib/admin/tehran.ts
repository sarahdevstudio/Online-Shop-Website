/** Iran has had no daylight saving since 2022: every "today" in the admin is UTC+03:30, fixed. Mirrors src/Domain/Common/TehranTime.cs. */
const OFFSET_MS = 210 * 60_000;

/** The UTC instant of Tehran-local midnight of the day `at` falls in. */
function tehranMidnightUtc(at: Date): Date {
  const wall = new Date(at.getTime() + OFFSET_MS);
  return new Date(Date.UTC(wall.getUTCFullYear(), wall.getUTCMonth(), wall.getUTCDate()) - OFFSET_MS);
}

/** [fromUtc, toUtc) for the `days` Tehran-local calendar days ending today (today included). */
export function lastNDaysRangeUtc(days: number, now = new Date()): { fromUtc: Date; toUtc: Date } {
  const toUtc = new Date(tehranMidnightUtc(now).getTime() + 24 * 3600_000);
  return { fromUtc: new Date(toUtc.getTime() - days * 24 * 3600_000), toUtc };
}

/** A Tehran-local calendar day (yyyy-mm-dd, from a date input) as its [fromUtc, toUtc) range. */
export function tehranDayRangeUtc(isoDate: string): { fromUtc: Date; toUtc: Date } {
  const [y, m, d] = isoDate.split('-').map(Number);
  const fromUtc = tehranMidnightUtc(new Date(Date.UTC(y, m - 1, d, 12))); // noon avoids any DST-style edge case
  return { fromUtc, toUtc: new Date(fromUtc.getTime() + 24 * 3600_000) };
}

/** yyyy-mm-dd (Gregorian, for a date input's value) of the Tehran-local day a UTC instant falls in. */
export function tehranIsoDate(utc: Date): string {
  const wall = new Date(utc.getTime() + OFFSET_MS);
  return wall.toISOString().slice(0, 10);
}
