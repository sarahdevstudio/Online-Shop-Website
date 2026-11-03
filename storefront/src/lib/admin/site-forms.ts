import { text, whole } from './form-input';
import type { BadgesPayload, SeoPayload, ThemePayload } from './site-types';

/**
 * Editor form fields → the payloads the API stores. Field names are the payload paths (`logo.alt`), so the API's validation
 * keys land under the right field. Nothing is trusted or fixed up here beyond trimming and a missing `#`: the API validates.
 */

const COLOR_FIELDS = ['primary', 'secondary', 'accent', 'background', 'surface', 'text', 'success', 'warning', 'danger'] as const;

/** `5B0057` → `#5B0057`; anything else is passed on unchanged for the API to judge. */
function color(formData: FormData, name: string): string {
  const raw = text(formData, name);
  return /^[0-9a-fA-F]{6}$/.test(raw) ? `#${raw}` : raw;
}

export function parseThemeForm(formData: FormData): ThemePayload {
  const logoId = whole(formData, 'logo.mediaId');
  const faviconId = whole(formData, 'favicon.mediaId');
  const colors = Object.fromEntries(COLOR_FIELDS.map((name) => [name, color(formData, name)])) as Pick<ThemePayload, (typeof COLOR_FIELDS)[number]>;
  return {
    ...colors,
    font: text(formData, 'font') as ThemePayload['font'],
    radius: text(formData, 'radius') as ThemePayload['radius'],
    logo: logoId ? { mediaId: logoId, alt: text(formData, 'logo.alt') } : null,
    favicon: faviconId ? { mediaId: faviconId } : null,
  };
}

export function parseSeoForm(formData: FormData): SeoPayload {
  return {
    siteName: text(formData, 'siteName'),
    titleTemplate: text(formData, 'titleTemplate'),
    homeTitle: text(formData, 'homeTitle'),
    defaultDescription: text(formData, 'defaultDescription'),
    defaultOgMediaId: whole(formData, 'defaultOgMediaId'),
  };
}

/** A badge with every field empty is "no badge"; with only some fields it is sent as typed so the API names the missing ones. */
export function parseBadgesForm(formData: FormData): BadgesPayload {
  const enamad = { id: text(formData, 'enamad.id'), code: text(formData, 'enamad.code') };
  const samandehi = { id: text(formData, 'samandehi.id'), verifyCode: text(formData, 'samandehi.verifyCode'), logoCode: text(formData, 'samandehi.logoCode') };
  const empty = (values: Record<string, string>) => Object.values(values).every((v) => v === '');
  return { enamad: empty(enamad) ? null : enamad, samandehi: empty(samandehi) ? null : samandehi };
}
