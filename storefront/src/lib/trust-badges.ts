import type { Badges } from './site-config';

/**
 * Trust badges (e-Namad, Samandehi) are rendered from IDENTIFIERS ONLY through the fixed templates below; the admin never
 * pastes the snippet the authority gives, so no script, inline style or admin-controlled attribute can reach a page.
 *
 * These are the ONLY external hosts the storefront links to or loads images from for badges (a future CSP must allow exactly
 * `trustseal.enamad.ir` and `logo.samandehi.ir` for `img-src`). Every URL shape lives in this file: if an authority changes its
 * snippet, change the template here and nothing else.
 *
 * Verification status:
 *  - Samandehi: VERIFIED against the authority's current snippet. link = Verify.aspx?id={id}&p={verifyCode},
 *    image = logo.aspx?id={id}&p={logoCode}; the two `p` values are different per-shop tokens.
 *  - e-Namad: templates written from memory of the official snippet and NOT YET VERIFIED against the live official page:
 *    link = ?id={id}&Code={code}, image = logo.aspx?id={id}&Code={code}. Verify before relying on it in production.
 */

export interface BadgeLink {
  /** Where the badge links to (the authority's verification page). */
  href: string;
  /** The badge image, loaded by the visitor's browser from the authority. */
  src: string;
}

const ID = /^[0-9]{1,10}$/;
const CODE = /^[A-Za-z0-9]{10,64}$/;

const ENAMAD = {
  link: (id: string, code: string) => `https://trustseal.enamad.ir/?id=${id}&Code=${code}`,
  image: (id: string, code: string) => `https://trustseal.enamad.ir/logo.aspx?id=${id}&Code=${code}`,
};

const SAMANDEHI = {
  link: (id: string, verifyCode: string) => `https://logo.samandehi.ir/Verify.aspx?id=${id}&p=${verifyCode}`,
  image: (id: string, logoCode: string) => `https://logo.samandehi.ir/logo.aspx?id=${id}&p=${logoCode}`,
};

/** Null unless both identifiers are present and well-formed (they are re-validated here, whatever the API said). */
export function enamadBadge(badge: Badges['enamad']): BadgeLink | null {
  if (!badge || !ID.test(badge.id) || !CODE.test(badge.code)) return null;
  return { href: ENAMAD.link(badge.id, badge.code), src: ENAMAD.image(badge.id, badge.code) };
}

export function samandehiBadge(badge: Badges['samandehi']): BadgeLink | null {
  if (!badge || !ID.test(badge.id) || !CODE.test(badge.verifyCode) || !CODE.test(badge.logoCode)) return null;
  return { href: SAMANDEHI.link(badge.id, badge.verifyCode), src: SAMANDEHI.image(badge.id, badge.logoCode) };
}
