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
  /**
   * Keep a page out of the index. `true`: noindex, nofollow (cart, account, search). `'follow'`: noindex, follow,
   * for filtered/sorted listings whose links should still be crawled.
   */
  noindex?: boolean | 'follow';
  /** Emit a canonical link (default). Off for noindex listings: noindex must not be mixed with a cross-page canonical. */
  canonical?: boolean;
}

/** Builds title, description, canonical, Open Graph and Twitter tags in one place. */
export function pageMetadata({ title, description, path, image, type = 'website', noindex, canonical = true }: PageMetaInput): Metadata {
  const url = absoluteUrl(path);
  return {
    title,
    description,
    ...(canonical ? { alternates: { canonical: url } } : {}),
    robots: noindex ? { index: false, follow: noindex === 'follow' } : undefined,
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

/** Prices are stored in Toman; schema.org needs an ISO 4217 code, and Toman has none: 1 Toman = 10 Rial (IRR). */
export const IRR_PER_TOMAN = 10;

export type Availability = 'InStock' | 'OutOfStock' | 'Discontinued';

export interface OfferInput {
  sku: string;
  /** Toman, as everywhere else in the shop. Converted to IRR for the structured data. */
  priceToman: number;
  availability: Availability;
  /** Product URL that preselects this variant. */
  path: string;
}

interface ProductJsonLdInput {
  name: string;
  description: string;
  path: string;
  images: readonly string[];
  brand?: string | null;
  offers: readonly OfferInput[];
}

/**
 * Product with one Offer per variant. No aggregateRating/review: there are no reviews yet, and invented ratings in
 * structured data violate Google's guidelines.
 */
export function productJsonLd(p: ProductJsonLdInput) {
  return {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: p.name,
    description: p.description,
    url: absoluteUrl(p.path),
    ...(p.images.length > 0 ? { image: p.images } : {}),
    ...(p.offers.length === 1 ? { sku: p.offers[0].sku } : {}),
    ...(p.brand ? { brand: { '@type': 'Brand', name: p.brand } } : {}),
    ...(p.offers.length > 0
      ? {
          offers: p.offers.map((offer) => ({
            '@type': 'Offer',
            sku: offer.sku,
            url: absoluteUrl(offer.path),
            priceCurrency: site.currency,
            price: offer.priceToman * IRR_PER_TOMAN,
            availability: `https://schema.org/${offer.availability}`,
            itemCondition: 'https://schema.org/NewCondition',
          })),
        }
      : {}),
  };
}
