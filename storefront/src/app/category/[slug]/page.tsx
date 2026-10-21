import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Breadcrumb } from '@/components/Breadcrumb';
import { Pagination } from '@/components/Pagination';
import { ProductCard } from '@/components/ProductCard';
import { formatNumber } from '@/lib/format';
import { getCategories, getCategory, getProducts } from '@/lib/catalog';
import { pageMetadata } from '@/lib/seo';

type Props = {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ page?: string }>;
};

const parsePage = (value?: string) => {
  const n = Number(value);
  return Number.isInteger(n) && n > 0 ? n : 1;
};

export async function generateStaticParams() {
  return (await getCategories()).map((c) => ({ slug: c.slug }));
}

export async function generateMetadata({ params, searchParams }: Props): Promise<Metadata> {
  const { slug } = await params;
  const page = parsePage((await searchParams).page);
  const category = await getCategory(slug);
  if (!category) return {};
  // Page 1 and ?page=1 share one canonical URL; deeper pages canonicalize to themselves.
  return pageMetadata({
    title: page > 1 ? `${category.name} - صفحه ${formatNumber(page)}` : category.name,
    description: category.description,
    path: `/category/${slug}${page > 1 ? `?page=${page}` : ''}`,
  });
}

export default async function CategoryPage({ params, searchParams }: Props) {
  const { slug } = await params;
  const category = await getCategory(slug);
  if (!category) notFound();

  const result = await getProducts({ category: slug, page: parsePage((await searchParams).page) });

  return (
    <div className="ui-container ui-section ui-stack">
      <Breadcrumb items={[{ label: 'خانه', href: '/' }, { label: category.name }]} />
      <h1 className="ui-heading-2">{category.name}</h1>
      <p className="ui-text-muted">{category.description}</p>
      <div className="ui-product-grid">
        {result.items.map((p, i) => (
          <ProductCard key={p.slug} product={p} priority={i < 4} />
        ))}
      </div>
      <Pagination page={result.page} pageCount={result.pageCount} basePath={`/category/${slug}`} />
    </div>
  );
}
