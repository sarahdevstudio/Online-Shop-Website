import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  poweredByHeader: false,
  reactStrictMode: true,
  images: {
    // Product images come through the /media pass-through route (see src/app/media), never from other hosts.
    localPatterns: [{ pathname: '/media/**', search: '' }],
  },
};

export default nextConfig;
