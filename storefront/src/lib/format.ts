const LOCALE = 'fa-IR';

const integerFormat = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 0 });
const decimalFormat = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 1, minimumFractionDigits: 0 });
const percentFormat = new Intl.NumberFormat(LOCALE, { style: 'percent', maximumFractionDigits: 0 });

/** Persian digits with Persian thousands separators: 1250000 -> ۱٬۲۵۰٬۰۰۰ */
export function formatNumber(value: number): string {
  return integerFormat.format(value);
}

/** One decimal at most, for ratings: 4.5 -> ۴٫۵ */
export function formatDecimal(value: number): string {
  return decimalFormat.format(value);
}

/** 0.2 -> ۲۰٪ */
export function formatPercent(fraction: number): string {
  return percentFormat.format(fraction);
}

/** Whole-percent discount between a compare-at price and the current price, or 0 when not discounted. */
export function discountFraction(price: number, compareAt: number | null | undefined): number {
  if (compareAt == null || compareAt <= price || compareAt <= 0) {
    return 0;
  }
  return Math.round(((compareAt - price) / compareAt) * 100) / 100;
}

/** Persian (Jalali) calendar date, e.g. ۱۴۰۵/۰۷/۱۲. Uses the built-in ICU calendar, no library. */
export function formatJalaliDate(value: Date | string | number): string {
  return new Intl.DateTimeFormat(`${LOCALE}-u-ca-persian`, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(value));
}

const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹';
const ARABIC_DIGITS = '٠١٢٣٤٥٦٧٨٩';

/** Converts Persian / Arabic-Indic digits to ASCII so user-typed numbers can be parsed. */
export function toAsciiDigits(value: string): string {
  return value.replace(/[۰-۹٠-٩]/g, (ch) => {
    const persian = PERSIAN_DIGITS.indexOf(ch);
    return String(persian >= 0 ? persian : ARABIC_DIGITS.indexOf(ch));
  });
}

/** Parses an integer typed in any digit system; returns null for empty or non-numeric input. */
export function parseLocalizedInteger(value: string): number | null {
  const cleaned = toAsciiDigits(value).replace(/[\s,٬،]/g, '');
  if (!/^-?\d+$/.test(cleaned)) {
    return null;
  }
  return Number(cleaned);
}
