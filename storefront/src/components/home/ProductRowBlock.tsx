import Link from 'next/link';
import { ProductCard, type ProductCardModel } from '@/components/ProductCard';
import { getProduct, getProducts } from '@/lib/catalog';
import { cardFromDetail, rowQuery, rowTitle } from '@/lib/home-products';
import { messages } from '@/lib/messages';
import type { HomeBlock } from '@/lib/site-config';
import styles from './ProductRowBlock.module.css';

type Data = Extract<HomeBlock, { type: 'productRow' }>['data'];

/** Products are resolved at render time, so prices and stock are never stale; a row with no products renders nothing. */
async function loadProducts(data: Data): Promise<ProductCardModel[]> {
  try {
    const query = rowQuery(data);
    if (query) return (await getProducts(query))?.items.slice(0, data.limit) ?? [];
    const details = await Promise.all(data.productSlugs.slice(0, data.limit).map((slug) => getProduct(slug).catch(() => null)));
    return details.flatMap((detail) => {
      const card = detail ? cardFromDetail(detail) : null;
      return card ? [card] : [];
    });
  } catch {
    return [];
  }
}

export async function ProductRowBlock({ data, headingId, firstRow }: { data: Data; headingId: string; firstRow: boolean }) {
  const products = await loadProducts(data);
  if (products.length === 0) return null;

  return (
    <section className="ui-container ui-section" aria-labelledby={headingId}>
      <div className={styles.head}>
        <h2 id={headingId} className="ui-heading-3">
          {rowTitle(data)}
        </h2>
        {data.viewAllHref && (
          <Link className={styles.all} href={data.viewAllHref}>
            {messages.viewAll}
          </Link>
        )}
      </div>
      <div className="ui-product-grid">
        {products.map((product, i) => (
          <ProductCard key={product.slug} product={product} priority={firstRow && i < 4} />
        ))}
      </div>
    </section>
  );
}
