import Link from 'next/link';
import { HomeBlocks } from '@/components/home/HomeBlocks';
import { ProductCard } from '@/components/ProductCard';
import { getCategoryTree, getProducts } from '@/lib/catalog';
import { messages } from '@/lib/messages';
import { pageMetadata, site } from '@/lib/seo';
import { getSiteConfig } from '@/lib/site-config';

/** The home title is used as is (not run through the title template). */
export async function generateMetadata() {
  const { seo } = await getSiteConfig();
  return { ...pageMetadata({ title: seo.homeTitle, description: seo.defaultDescription, path: '/' }), title: { absolute: seo.homeTitle } };
}

const NEWEST_COUNT = 8;

export default async function HomePage() {
  const { home, seo } = await getSiteConfig();
  // The configured blocks, in order; with none configured (or the API unreachable) the built-in home page below.
  if (home.blocks.length > 0) {
    return (
      <>
        <h1 className="ui-visually-hidden">{seo.siteName}</h1>
        <HomeBlocks blocks={home.blocks} />
      </>
    );
  }

  const [categories, newest] = await Promise.all([getCategoryTree(), getProducts({ inStock: true, pageSize: NEWEST_COUNT })]);

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-1">{site.name}</h1>
      <p className="ui-text-muted">{site.description}</p>

      <section aria-labelledby="cats">
        <h2 id="cats" className="ui-heading-3">
          {messages.categories}
        </h2>
        <ul className="ui-cluster">
          {categories.map((c) => (
            <li key={c.slug}>
              <Link href={`/category/${c.slug}`}>{c.name}</Link>
            </li>
          ))}
        </ul>
      </section>

      {newest && newest.items.length > 0 && (
        <section aria-labelledby="latest">
          <h2 id="latest" className="ui-heading-3">
            {messages.newestProducts}
          </h2>
          <div className="ui-product-grid">
            {newest.items.map((p, i) => (
              <ProductCard key={p.slug} product={p} priority={i < 4} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
