import Link from 'next/link';
import { ProductCard } from '@/components/ProductCard';
import { getCategories, getProducts } from '@/lib/catalog';
import { pageMetadata, site } from '@/lib/seo';

export const metadata = pageMetadata({ title: `${site.name} | ${site.description}`, description: site.description, path: '/' });

export default async function HomePage() {
  const [{ items }, categories] = await Promise.all([getProducts({ page: 1 }), getCategories()]);

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-1">{site.name}</h1>
      <p className="ui-text-muted">{site.description}</p>

      <section aria-labelledby="cats">
        <h2 id="cats" className="ui-heading-3">
          دسته‌بندی‌ها
        </h2>
        <ul className="ui-cluster">
          {categories.map((c) => (
            <li key={c.slug}>
              <Link href={`/category/${c.slug}`}>{c.name}</Link>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="latest">
        <h2 id="latest" className="ui-heading-3">
          جدیدترین محصولات
        </h2>
        <div className="ui-product-grid">
          {items.slice(0, 8).map((p, i) => (
            <ProductCard key={p.slug} product={p} priority={i < 4} />
          ))}
        </div>
      </section>
    </div>
  );
}
