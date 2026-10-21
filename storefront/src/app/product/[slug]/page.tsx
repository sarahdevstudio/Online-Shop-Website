import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Badge } from '@/components/Badge';
import { Breadcrumb } from '@/components/Breadcrumb';
import { Button } from '@/components/Button';
import { JsonLd } from '@/components/JsonLd';
import { Price } from '@/components/Price';
import { Rating } from '@/components/Rating';
import { getCategory, getProduct, products } from '@/lib/catalog';
import { messages } from '@/lib/messages';
import { absoluteUrl, pageMetadata, productJsonLd } from '@/lib/seo';

type Props = { params: Promise<{ slug: string }> };

export async function generateStaticParams() {
  return products.map((p) => ({ slug: p.slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const product = await getProduct((await params).slug);
  if (!product) return {};
  return pageMetadata({
    title: product.name,
    description: product.description,
    path: `/product/${product.slug}`,
    image: product.imageUrl ? absoluteUrl(product.imageUrl) : null,
  });
}

export default async function ProductPage({ params }: Props) {
  const product = await getProduct((await params).slug);
  if (!product) notFound();
  const category = await getCategory(product.categorySlug);
  const image = product.imageUrl ? absoluteUrl(product.imageUrl) : null;

  return (
    <div className="ui-container ui-section ui-stack">
      <JsonLd
        data={productJsonLd({
          name: product.name,
          description: product.description,
          path: `/product/${product.slug}`,
          image,
          brand: product.brand,
          sku: product.sku,
          price: product.price,
          inStock: product.inStock,
          rating: product.rating,
          ratingCount: product.ratingCount,
        })}
      />
      <Breadcrumb
        items={[
          { label: 'خانه', href: '/' },
          ...(category ? [{ label: category.name, href: `/category/${category.slug}` }] : []),
          { label: product.name },
        ]}
      />
      <h1 className="ui-heading-2">{product.name}</h1>
      {product.brand && <p className="ui-text-muted">{product.brand}</p>}
      <Rating value={product.rating} count={product.ratingCount} />
      <Price amount={product.price} compareAt={product.inStock ? product.compareAtPrice : null} size="lg" />
      {product.inStock ? <Button type="button">افزودن به سبد</Button> : <Badge>{messages.outOfStock}</Badge>}
      <section aria-labelledby="desc" className="ui-prose">
        <h2 id="desc">توضیحات</h2>
        <p>{product.description}</p>
      </section>
    </div>
  );
}
