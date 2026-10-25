/** The cart is server-side; the browser holds only this random token. Read and written by the Next server, never by scripts. */
export const CART_COOKIE = 'cart';

/** Matches the API's 30-day inactivity expiry; refreshed on every cart change. */
export const CART_MAX_AGE_SECONDS = 30 * 24 * 60 * 60;

export function cartCookieOptions(production: boolean = process.env.NODE_ENV === 'production') {
  return {
    httpOnly: true,
    // Plain http on localhost in development; browsers refuse Secure cookies there only on non-localhost http.
    secure: production,
    sameSite: 'lax' as const,
    path: '/',
    maxAge: CART_MAX_AGE_SECONDS,
  };
}
