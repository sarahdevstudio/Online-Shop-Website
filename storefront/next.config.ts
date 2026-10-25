import type { NextConfig } from 'next';

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
