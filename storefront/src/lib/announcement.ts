/**
 * Dismissal of the announcement bar. It is kept in a cookie (not localStorage) so the SERVER leaves the bar out for a visitor who
 * closed it: a bar that is rendered and then removed after hydration shifts the whole page up (layout shift on every later visit).
 * The cookie holds a short hash of the text, so a new announcement shows again.
 */

export const ANNOUNCEMENT_COOKIE = 'announcement-dismissed';

/** A short, stable key of the announcement text (djb2, base 36). Not a secret: it only tells two texts apart. */
export function announcementKey(text: string): string {
  let hash = 5381;
  for (let i = 0; i < text.length; i++) hash = ((hash << 5) + hash + text.charCodeAt(i)) >>> 0;
  return hash.toString(36);
}

export function isAnnouncementDismissed(cookieValue: string | undefined, text: string): boolean {
  return cookieValue !== undefined && cookieValue === announcementKey(text);
}

/** The `document.cookie` string that remembers the dismissal for a year. */
export function dismissalCookie(text: string): string {
  return `${ANNOUNCEMENT_COOKIE}=${announcementKey(text)}; path=/; max-age=31536000; samesite=lax`;
}
