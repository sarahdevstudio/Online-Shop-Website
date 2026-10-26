const PERSIAN = /[؀-ۿ]/g;
const LATIN = /[A-Za-z]/g;

/**
 * API texts are Persian, but nothing an English-speaking dependency (framework, proxy, a missed message) produces may ever
 * reach a visitor: a text is shown only when it has Persian letters and no more Latin letters than Persian ones (a mention
 * of "JPEG" is fine, a whole English sentence is not); anything else becomes `fallback`.
 */
export function persianOr(text: string | null | undefined, fallback: string): string {
  if (!text) return fallback;
  const persian = text.match(PERSIAN)?.length ?? 0;
  const latin = text.match(LATIN)?.length ?? 0;
  return persian > 0 && latin <= persian ? text : fallback;
}
