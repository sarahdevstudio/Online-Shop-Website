/**
 * Order access tokens are in URLs (`/order/{token}`, `/orders/{token}`) and must never reach logs or error messages.
 * Everything that logs or throws with a path goes through this.
 */
export function maskTokens(text: string): string {
  return text.replace(/(\/orders?\/)[A-Za-z0-9_-]{43}(?![A-Za-z0-9_-])/g, '$1***');
}
