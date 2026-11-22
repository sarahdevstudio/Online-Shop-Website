/** The admin's phone-order draft cart (G3) is server-side, like the shop cart; the admin's browser holds only this
 * random token, scoped to /admin and kept separate from the shop's own `cart` cookie. */
export const DRAFT_CART_COOKIE = 'admin_draft_cart';

export function draftCartCookieOptions(production: boolean = process.env.NODE_ENV === 'production') {
  return {
    httpOnly: true,
    secure: production,
    sameSite: 'strict' as const,
    path: '/admin',
    maxAge: 60 * 60 * 4, // 4 hours: long enough to build one phone order, short enough to not linger
  };
}
