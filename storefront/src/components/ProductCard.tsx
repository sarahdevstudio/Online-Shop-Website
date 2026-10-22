import Image from 'next/image';
import Link from 'next/link';
import { Badge } from './Badge';
import { Icon } from './Icon';
import { Price } from './Price';
import { discountFraction, formatPercent } from '@/lib/format';
import { messages } from '@/lib/messages';
import styles from './ProductCard.module.css';

/** What the card needs; pages map the API DTO onto this. */
export interface ProductCardModel {
  slug: string;
  name: string;
  imageUrl: string | null;
  imageAlt?: string;
  brand?: string | null;
  /** Server-calculated sell price; null renders "call for price". */
  price: number | null;
  compareAtPrice?: number | null;
  inStock: boolean;
}

interface ProductCardProps {
  product: ProductCardModel;
  linkBase?: string;
  /** Appended to the product link, e.g. `?wattage=9w` from a filtered listing (variant preselection). */
  linkQuery?: string;
  /** Above-the-fold cards load eagerly with high priority (LCP). */
  priority?: boolean;
  imageSize?: number;
}

const SIZES = '(min-width: 1280px) 20vw, (min-width: 1024px) 25vw, (min-width: 640px) 33vw, 50vw';

/** One link per card (the name, stretched over the card) so each product is met once by keyboard/screen readers. */
export function ProductCard({ product, linkBase = '/product', linkQuery = '', priority = false, imageSize = 480 }: ProductCardProps) {
  const fraction = product.price === null ? 0 : discountFraction(product.price, product.compareAtPrice);
  return (
    <div className={styles.host}>
      <article className={`${styles.card} ${product.inStock ? '' : styles.soldOut}`}>
        <div className={styles.media}>
          {product.imageUrl ? (
            <Image
              src={product.imageUrl}
              alt={product.imageAlt ?? ''}
              sizes={SIZES}
              width={imageSize}
              height={imageSize}
              priority={priority}
            />
          ) : (
            <Icon name="image" className={styles.placeholder} />
          )}
          {!product.inStock ? (
            <Badge className={styles.flag}>{messages.outOfStock}</Badge>
          ) : (
            fraction > 0 && (
              <Badge variant="sale" className={styles.flag}>
                {messages.discountOff(formatPercent(fraction))}
              </Badge>
            )
          )}
        </div>
        <div className={styles.info}>
          {product.brand && <span className={styles.brand}>{product.brand}</span>}
          <h3 className={styles.name}>
            <Link href={`${linkBase}/${product.slug}${linkQuery}`}>{product.name}</Link>
          </h3>
          <div className={styles.priceBox}>
            <Price amount={product.price} compareAt={product.inStock ? product.compareAtPrice : null} showBadge={false} />
          </div>
        </div>
      </article>
    </div>
  );
}

/** Same footprint as the card, for loading states (prevents layout shift). */
export function ProductCardSkeleton() {
  return (
    <div className={styles.skeleton} aria-hidden="true">
      <div className={styles.skelMedia} />
      <div className={styles.skelInfo}>
        <div className={styles.skelLine} style={{ inlineSize: '40%' }} />
        <div className={styles.skelLine} />
        <div className={styles.skelLine} style={{ inlineSize: '70%' }} />
      </div>
    </div>
  );
}
