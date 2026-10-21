import type { MetadataRoute } from 'next';
import { getCategories, products } from '@/lib/catalog';
import { absoluteUrl } from '@/lib/seo';

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const categories = await getCategories();
  return [
    { url: absoluteUrl('/') },
    ...categories.map((c) => ({ url: absoluteUrl(`/category/${c.slug}`) })),
    ...products.map((p) => ({ url: absoluteUrl(`/product/${p.slug}`) })),
  ];
}
