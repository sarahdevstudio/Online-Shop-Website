import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { addToCartAction } from '@/app/cart/actions';
import { Alert } from '@/components/Alert';
import { Badge } from '@/components/Badge';
import { Breadcrumb } from '@/components/Breadcrumb';
import { Button } from '@/components/Button';
import { JsonLd } from '@/components/JsonLd';
import { Price } from '@/components/Price';
import { ProductGallery } from '@/components/ProductGallery';
import { SpecsTable, type SpecRow } from '@/components/SpecsTable';
import { VariantSelector } from '@/components/VariantSelector';
import { getProduct, type ProductDetailDto, type VariantDto } from '@/lib/catalog';
import { messages } from '@/lib/messages';
import type { SearchParams } from '@/lib/paging';
import { absoluteUrl, pageMetadata, productJsonLd, type Availability } from '@/lib/seo';
import { pickVariant, selectionFromParams, variantQuery } from '@/lib/variants';

type Props = { params: Promise<{ slug: string }>; searchParams: Promise<SearchParams> };

// No generateStaticParams: products render on first request (the build never needs the API).

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const product = await getProduct((await params).slug);
  if (!product) return {};
  // Variant URLs (?wattage=…) canonicalize to the clean product URL. Archived products stay indexable.
  return pageMetadata({
    title: product.name,
    description: product.description ?? product.name,
    path: `/product/${product.slug}`,
    image: product.images[0] ? absoluteUrl(product.images[0].url) : null,
  });
}

function specRows(product: ProductDetailDto, variant: VariantDto | null): SpecRow[] {
  const axisRows = product.axes.flatMap((axis) => {
    const value = axis.options.find((o) => o.slug === variant?.attributes[axis.code])?.value;
    return value ? [{ label: axis.name, value }] : [];
  });
  const specs = product.specs.map((spec) => ({ label: spec.name, value: spec.unit ? `${spec.value} ${spec.unit}` : spec.value }));
  return [
    ...(product.brand ? [{ label: 'برند', value: product.brand.name }] : []),
    ...axisRows,
    ...specs,
    ...(variant ? [{ label: messages.sku, value: variant.sku }] : []),
  ];
}

export default async function ProductPage({ params, searchParams }: Props) {
  const product = await getProduct((await params).slug);
  if (!product) notFound();

  const basePath = `/product/${product.slug}`;
  const archived = product.status === 'archived';
  const variant = pickVariant(product.variants, selectionFromParams(product.axes, await searchParams));
  const category = product.breadcrumb.at(-1);
  const availability = (v: VariantDto): Availability => (archived ? 'Discontinued' : v.inStock ? 'InStock' : 'OutOfStock');

  return (
    <div className="ui-container ui-section ui-stack">
      <JsonLd
        data={productJsonLd({
          name: product.name,
          description: product.description ?? product.name,
          path: basePath,
          images: product.images.map((image) => absoluteUrl(image.url)),
          brand: product.brand?.name,
          offers: product.variants.map((v) => ({
            sku: v.sku,
            priceToman: v.price,
            availability: availability(v),
            path: `${basePath}${variantQuery(product.axes, v)}`,
          })),
        })}
      />
      <Breadcrumb
        items={[
          { label: messages.home, href: '/' },
          ...product.breadcrumb.map((c) => ({ label: c.name, href: `/category/${c.slug}` })),
          { label: product.name },
        ]}
      />

      <div className="ui-product-layout">
        <ProductGallery images={product.images} name={product.name} />

        <div className="ui-stack">
          <h1 className="ui-heading-2">{product.name}</h1>
          {product.brand && <p className="ui-text-muted">{product.brand.name}</p>}

          {archived ? (
            <Alert variant="warning" heading={messages.discontinued}>
              <p>{messages.discontinuedNote}</p>
              {category && <Link href={`/category/${category.slug}`}>{messages.seeCategory(category.name)}</Link>}
            </Alert>
          ) : (
            variant && (
              <>
                {product.axes.length > 0 && (
                  <VariantSelector basePath={basePath} axes={product.axes} variants={product.variants} selected={variant} />
                )}
                <Price amount={variant.price} compareAt={variant.inStock ? variant.compareAtPrice : null} size="lg" />
                {variant.inStock ? (
                  <>
                    <Badge variant="success">{messages.inStock}</Badge>
                    {/* A plain form posting to a Server Action: adds the selected variant with no JavaScript needed. */}
                    <form action={addToCartAction} className="ui-cluster">
                      <input type="hidden" name="variantId" value={variant.id} />
                      <label>
                        <span className="ui-visually-hidden">{messages.quantity}</span>
                        <input className="ui-control" style={{ inlineSize: '5rem' }} type="number" name="quantity" inputMode="numeric" min={1} defaultValue={1} />
                      </label>
                      <Button type="submit">{messages.addToCart}</Button>
                    </form>
                  </>
                ) : (
                  <Badge>{messages.outOfStock}</Badge>
                )}
              </>
            )
          )}

          <section aria-labelledby="specs" className="ui-stack">
            <h2 id="specs" className="ui-heading-4">
              {messages.specs}
            </h2>
            <SpecsTable caption={messages.specs} rows={specRows(product, variant)} />
          </section>
        </div>
      </div>

      {product.description && (
        <section aria-labelledby="desc" className="ui-prose">
          <h2 id="desc">{messages.description}</h2>
          <p>{product.description}</p>
        </section>
      )}
    </div>
  );
}
