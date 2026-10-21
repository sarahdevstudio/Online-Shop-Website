import type { ProductCardModel } from '@/components/ProductCard';

/**
 * Catalog access. The API has no catalog endpoints yet (docs: phase 3), so this serves sample data behind
 * the functions the pages will keep calling. Replace the bodies with fetches to the API (use Next's
 * `fetch(..., { next: { revalidate, tags } })` for caching) and the pages stay unchanged.
 */

export interface Category {
  slug: string;
  name: string;
  description: string;
}

export interface Product extends ProductCardModel {
  description: string;
  sku: string;
  categorySlug: string;
}

export const categories: Category[] = [
  { slug: 'clothing', name: 'پوشاک', description: 'انواع پوشاک روزمره و رسمی با کیفیت بالا' },
  { slug: 'accessories', name: 'اکسسوری', description: 'کیف، کمربند و اکسسوری‌های متنوع' },
  { slug: 'home', name: 'خانه و آشپزخانه', description: 'لوازم کاربردی و دکوری برای خانه' },
];

const names = ['پیراهن نخی', 'کیف دستی چرم', 'ست ظروف سرامیکی', 'کفش اسپورت', 'شال حریر', 'ساعت رومیزی', 'کوله‌پشتی', 'لیوان سرامیکی'];

export const products: Product[] = Array.from({ length: 30 }, (_, i) => {
  const base = 250_000 + (i % 7) * 180_000;
  return {
    slug: `sample-${i + 1}`,
    sku: `SKU-${1000 + i}`,
    name: `${names[i % names.length]} مدل ${i + 1}`,
    description: `${names[i % names.length]} با جنس مرغوب و دوخت استاندارد. این متن نمونه است و با داده واقعی فروشگاه جایگزین می‌شود.`,
    categorySlug: categories[i % categories.length].slug,
    imageUrl: null,
    brand: i % 2 ? 'برند نمونه' : null,
    price: base,
    compareAtPrice: i % 3 === 0 ? Math.round(base * 1.25) : null,
    rating: i % 4 === 3 ? null : 3.5 + (i % 4) * 0.5,
    ratingCount: i % 4 === 3 ? null : 5 + i,
    inStock: i % 9 !== 8,
  };
});

export const PAGE_SIZE = 12;

export async function getCategories(): Promise<Category[]> {
  return categories;
}

export async function getCategory(slug: string): Promise<Category | null> {
  return categories.find((c) => c.slug === slug) ?? null;
}

export async function getProducts(opts: { category?: string; q?: string; page?: number }) {
  const q = opts.q?.trim();
  const filtered = products.filter(
    (p) => (!opts.category || p.categorySlug === opts.category) && (!q || p.name.includes(q)),
  );
  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const page = Math.min(Math.max(opts.page ?? 1, 1), pageCount);
  return { items: filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE), total: filtered.length, page, pageCount };
}

export async function getProduct(slug: string): Promise<Product | null> {
  return products.find((p) => p.slug === slug) ?? null;
}
