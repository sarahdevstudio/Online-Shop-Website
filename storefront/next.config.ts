import type { NextConfig } from 'next';
import { requireProductionEnv } from './src/lib/site-env';

// SITE_ENV=production must have NEXT_PUBLIC_SITE_URL and API_BASE_URL: `next build` and `next start` both stop here otherwise.
requireProductionEnv();

const nextConfig: NextConfig = {
  poweredByHeader: false,
  reactStrictMode: true,
  // The dev server prints every request path; order pages carry the access token in the URL, so they are not printed
  // (production `next start` does not log requests at all).
  logging: { incomingRequests: { ignore: [/^\/order\//] } },
  async headers() {
    const noIndex = { key: 'X-Robots-Tag', value: 'noindex, nofollow' };
    return [
      // The order token is in the URL: never cache the page, never send it as a Referer, never index it.
      {
        source: '/order/:path*',
        headers: [{ key: 'Referrer-Policy', value: 'no-referrer' }, { key: 'Cache-Control', value: 'no-store' }, noIndex],
      },
      // The admin panel: never indexed, cached, framed, or leaked through a Referer header.
      {
        source: '/admin/:path*',
        headers: [
          noIndex,
          { key: 'Cache-Control', value: 'no-store' },
          { key: 'Referrer-Policy', value: 'no-referrer' },
          { key: 'X-Frame-Options', value: 'DENY' },
        ],
      },
      { source: '/admin', headers: [noIndex, { key: 'Cache-Control', value: 'no-store' }] },
      // Fonts never change under the same name (a new font is a new file name), so browsers keep them for a year and the
      // second page of a visit does not even ask. `_next/static` and `/media` are immutable already.
      { source: '/fonts/:path*', headers: [{ key: 'Cache-Control', value: 'public, max-age=31536000, immutable' }] },
      { source: '/cart', headers: [noIndex] },
      { source: '/checkout', headers: [noIndex] },
    ];
  },
  images: {
    // Product images come through the /media pass-through route (see src/app/media), never from other hosts.
    localPatterns: [{ pathname: '/media/**', search: '' }],
  },
};

export default nextConfig;
