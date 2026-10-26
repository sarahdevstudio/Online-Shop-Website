import type { MetadataRoute } from 'next';
import { absoluteUrl } from '@/lib/seo';
import { isProductionSite } from '@/lib/site-env';

// Read per request: one build can be deployed to staging first and to production later.
export const dynamic = 'force-dynamic';

export default function robots(): MetadataRoute.Robots {
  // Anything that is not the production site tells every crawler to stay away.
  if (!isProductionSite()) return { rules: { userAgent: '*', disallow: '/' } };
  return {
    rules: { userAgent: '*', allow: '/', disallow: ['/cart', '/checkout', '/order', '/account', '/search', '/admin', '/api'] },
    sitemap: absoluteUrl('/sitemap.xml'),
  };
}
