import type { SearchParams } from './paging';

/** The parts of the API variant/axis DTOs the selector needs (kept minimal so it is easy to test). */
export interface SelectableVariant {
  id: number;
  inStock: boolean;
  attributes: Record<string, string>;
}

export interface Axis {
  code: string;
  options: { slug: string }[];
}

/**
 * Attribute selections from the URL (`?wattage=9w&cct=3000k`), only for codes that are axes of this product.
 * A link from a filtered listing may carry several values (`?wattage=12w,9w`): the first one this product has wins.
 */
export function selectionFromParams(axes: readonly Axis[], params: SearchParams): Record<string, string> {
  const selection: Record<string, string> = {};
  for (const axis of axes) {
    const raw = params[axis.code];
    const candidates = (Array.isArray(raw) ? raw : raw === undefined ? [] : [raw])
      .flatMap((v) => v.split(','))
      .map((v) => v.trim().toLowerCase());
    const value = candidates.find((slug) => axis.options.some((o) => o.slug === slug));
    if (value) selection[axis.code] = value;
  }
  return selection;
}

/**
 * The variant to show: the one matching the most selected values (an exact match wins); ties go to an in-stock
 * variant, then to API order. With no selection: the first in-stock variant, else the first one.
 */
export function pickVariant<T extends SelectableVariant>(variants: readonly T[], selection: Record<string, string>): T | null {
  let best: T | null = null;
  let bestScore = -1;
  for (const variant of variants) {
    const matches = Object.entries(selection).filter(([code, slug]) => variant.attributes[code] === slug).length;
    const score = matches * 2 + (variant.inStock ? 1 : 0);
    if (score > bestScore) {
      best = variant;
      bestScore = score;
    }
  }
  return best;
}

/**
 * Where an option button leads: the variant with this option that keeps as many of the current selections as
 * possible, so only combinations that exist are ever offered. Null when no variant has the option.
 */
export function variantForOption<T extends SelectableVariant>(
  variants: readonly T[],
  current: T,
  axisCode: string,
  optionSlug: string,
): T | null {
  const wanted = { ...current.attributes, [axisCode]: optionSlug };
  const candidates = variants.filter((v) => v.attributes[axisCode] === optionSlug);
  return candidates.length === 0 ? null : pickVariant(candidates, wanted);
}

/** Query string identifying a variant by its axis values (`?wattage=9w&cct=3000k`), in axis order. */
export function variantQuery(axes: readonly Axis[], variant: SelectableVariant): string {
  const params = new URLSearchParams();
  for (const axis of axes) {
    const value = variant.attributes[axis.code];
    if (value) params.set(axis.code, value);
  }
  const qs = params.toString();
  return qs ? `?${qs}` : '';
}
