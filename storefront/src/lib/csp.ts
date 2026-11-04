/**
 * Content-Security-Policy and Permissions-Policy (built per request by the proxy).
 *
 * Scripts: only `'self'` plus this request's nonce (Next puts the nonce on every script it emits) and `'strict-dynamic'` so the
 * chunks those scripts load run too. No `'unsafe-eval'` and no `'unsafe-inline'` for scripts in production; the development
 * server needs `'unsafe-eval'` for React's debugging features, nothing else.
 * Styles: stylesheets from `'self'` and nonce-tagged `<style>` elements (the theme). Inline `style=""` attributes are used by a
 * few components and cannot carry a nonce, so only attributes (`style-src-attr`) allow inline; elements do not.
 * Images, fonts and fetch/XHR: `'self'` only. Product photos come through the same-origin `/media` route (`data:` and `blob:`
 * images cover the admin upload previews and inline SVG icons).
 * Third parties (an analytics tag) are added by name through `extra`, only when the owner has configured one.
 */
export interface CspExtra {
  script?: string[];
  connect?: string[];
  img?: string[];
}

export interface CspOptions {
  nonce: string;
  development?: boolean;
  extra?: CspExtra;
}

export function newNonce(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}

export function contentSecurityPolicy({ nonce, development = false, extra = {} }: CspOptions): string {
  const scripts = ["'self'", `'nonce-${nonce}'`, "'strict-dynamic'", ...(development ? ["'unsafe-eval'"] : []), ...(extra.script ?? [])];
  const directives: [string, string[]][] = [
    ['default-src', ["'self'"]],
    ['script-src', scripts],
    ['style-src', ["'self'", `'nonce-${nonce}'`]],
    ['style-src-attr', ["'unsafe-inline'"]],
    ['img-src', ["'self'", 'data:', 'blob:', ...(extra.img ?? [])]],
    ['font-src', ["'self'"]],
    ['connect-src', ["'self'", ...(development ? ['ws:', 'wss:'] : []), ...(extra.connect ?? [])]],
    ['media-src', ["'self'"]],
    ['object-src', ["'none'"]],
    ['base-uri', ["'self'"]],
    ['form-action', ["'self'"]],
    ['frame-ancestors', ["'none'"]],
    ['manifest-src', ["'self'"]],
    ['worker-src', ["'self'", 'blob:']],
  ];
  return directives.map(([name, values]) => `${name} ${values.join(' ')}`).join('; ');
}

/** Browser features the shop never uses are switched off for the page and everything it embeds. */
export const PERMISSIONS_POLICY = [
  'accelerometer', 'autoplay', 'camera', 'display-capture', 'geolocation', 'gyroscope', 'hid', 'magnetometer',
  'microphone', 'midi', 'payment', 'serial', 'usb', 'xr-spatial-tracking',
]
  .map((feature) => `${feature}=()`)
  .join(', ');
