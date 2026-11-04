/** The shapes the API enforces for the two site-level tags (no server-only imports: the client Analytics component uses them too). */
export const SEARCH_CONSOLE_TOKEN = /^[A-Za-z0-9_-]{20,100}$/;
export const GA4_ID = /^G-[A-Z0-9]{6,12}$/;
