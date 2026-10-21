import type { MetadataRoute } from 'next';
import { absoluteUrl } from '@/lib/seo';

export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: '*', allow: '/', disallow: ['/cart', '/account', '/search', '/admin', '/api'] },
    sitemap: absoluteUrl('/sitemap.xml'),
  };
}
