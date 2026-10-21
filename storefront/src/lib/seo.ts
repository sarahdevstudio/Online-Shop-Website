import type { Metadata } from 'next';

/** Single source of truth for site-wide SEO values. Override per environment with env vars. */
export const site = {
  name: 'فروشگاه',
  description: 'خرید آنلاین با بهترین قیمت و ارسال سریع',
  /** Absolute public origin, no trailing slash. Needed for canonical URLs, sitemap and JSON-LD. */
  url: (process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000').replace(/\/$/, ''),
  locale: 'fa_IR',
  currency: 'IRR',
};

export const absoluteUrl = (path: string) => `${site.url}${path.startsWith('/') ? path : `/${path}`}`;

interface PageMetaInput {
  title: string;
  description: string;
  /** Path starting with "/", used for the canonical link. */
  path: string;
  image?: string | null;
  type?: 'website' | 'article';
  /** Keep a page out of the index (cart, account, filtered duplicates). */
  noindex?: boolean;
}

/** Builds title, description, canonical, Open Graph and Twitter tags in one place. */
export function pageMetadata({ title, description, path, image, type = 'website', noindex }: PageMetaInput): Metadata {
  const url = absoluteUrl(path);
  return {
    title,
    description,
    alternates: { canonical: url },
    robots: noindex ? { index: false, follow: false } : undefined,
    openGraph: {
      title,
      description,
      url,
      siteName: site.name,
      locale: site.locale,
      type,
      images: image ? [{ url: image }] : undefined,
    },
    twitter: { card: image ? 'summary_large_image' : 'summary', title, description, images: image ? [image] : undefined },
  };
}

export function organizationJsonLd() {
  return {
    '@context': 'https://schema.org',
    '@type': 'Organization',
    name: site.name,
    url: site.url,
  };
}

export function websiteJsonLd() {
  return {
    '@context': 'https://schema.org',
    '@type': 'WebSite',
    name: site.name,
    url: site.url,
    inLanguage: 'fa-IR',
    potentialAction: {
      '@type': 'SearchAction',
      target: `${site.url}/search?q={search_term_string}`,
      'query-input': 'required name=search_term_string',
    },
  };
}

export function breadcrumbJsonLd(items: readonly { label: string; href?: string }[]) {
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: items.map((item, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      name: item.label,
      ...(item.href ? { item: absoluteUrl(item.href) } : {}),
    })),
  };
}

interface ProductJsonLdInput {
  name: string;
  description: string;
  path: string;
  image?: string | null;
  brand?: string | null;
  sku?: string;
  price: number | null;
  inStock: boolean;
  rating?: number | null;
  ratingCount?: number | null;
}

export function productJsonLd(p: ProductJsonLdInput) {
  return {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: p.name,
    description: p.description,
    url: absoluteUrl(p.path),
    ...(p.image ? { image: [p.image] } : {}),
    ...(p.sku ? { sku: p.sku } : {}),
    ...(p.brand ? { brand: { '@type': 'Brand', name: p.brand } } : {}),
    ...(p.price !== null
      ? {
          offers: {
            '@type': 'Offer',
            url: absoluteUrl(p.path),
            priceCurrency: site.currency,
            price: p.price,
            availability: p.inStock ? 'https://schema.org/InStock' : 'https://schema.org/OutOfStock',
          },
        }
      : {}),
    ...(p.rating != null && p.ratingCount
      ? { aggregateRating: { '@type': 'AggregateRating', ratingValue: p.rating, reviewCount: p.ratingCount } }
      : {}),
  };
}
