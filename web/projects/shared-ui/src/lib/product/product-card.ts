import { booleanAttribute, ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiBadge } from '../feedback/badge';
import { UiIcon } from '../icon/icon';
import { UiPrice } from './price';
import { UiRating } from './rating';
import { UiSkeleton } from '../feedback/skeleton';
import { discountFraction, formatPercent } from '../core/format';
import { injectUiMessages } from '../core/ui-messages';

/** What the card needs; the storefront maps its API DTO onto this. */
export interface ProductCardModel {
  slug: string;
  name: string;
  imageUrl: string | null;
  imageAlt?: string;
  /** Optional responsive candidates from the media pipeline, e.g. "/m/a-320.webp 320w, /m/a-640.webp 640w". */
  imageSrcset?: string;
  brand?: string | null;
  /** Server-calculated sell price; null renders "call for price". */
  price: number | null;
  compareAtPrice?: number | null;
  rating?: number | null;
  ratingCount?: number | null;
  inStock: boolean;
}

/**
 * Listing card. One link per card (the product name, stretched over the whole card) so keyboard and
 * screen-reader users meet each product once. Give above-the-fold cards `priority` so the first row's
 * image is eager and high priority (LCP); everything else lazy-loads.
 */
@Component({
  selector: 'ui-product-card',
  imports: [RouterLink, UiBadge, UiIcon, UiPrice, UiRating],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.sold-out]': '!product().inStock' },
  template: `
    <article class="card">
      <div class="media">
        @if (product().imageUrl) {
          <img
            [src]="product().imageUrl"
            [attr.srcset]="product().imageSrcset ?? null"
            [attr.sizes]="product().imageSrcset ? sizes() : null"
            [alt]="product().imageAlt ?? ''"
            [attr.width]="imageWidth()"
            [attr.height]="imageHeight()"
            [attr.loading]="priority() ? 'eager' : 'lazy'"
            [attr.fetchpriority]="priority() ? 'high' : null"
            decoding="async"
          />
        } @else {
          <ui-icon class="placeholder" name="image" />
        }
        @if (!product().inStock) {
          <ui-badge class="flag" variant="neutral">{{ messages.outOfStock }}</ui-badge>
        } @else if (discountText()) {
          <ui-badge class="flag" variant="sale">{{ discountText() }}</ui-badge>
        }
      </div>
      <div class="info">
        @if (product().brand) {
          <span class="brand">{{ product().brand }}</span>
        }
        <h3 class="name">
          <a [routerLink]="link()">{{ product().name }}</a>
        </h3>
        <ui-rating [value]="product().rating" [count]="product().ratingCount" />
        <ui-price [amount]="product().price" [compareAt]="product().inStock ? product().compareAtPrice : null" [showBadge]="false" />
      </div>
    </article>
  `,
  styles: `
    :host {
      display: block;
      block-size: 100%;
    }
    .card {
      position: relative;
      display: flex;
      flex-direction: column;
      block-size: 100%;
      overflow: hidden;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      transition:
        box-shadow var(--duration-base) var(--ease-standard),
        border-color var(--duration-base) var(--ease-standard);
    }
    .card:hover {
      border-color: var(--color-border-control);
      box-shadow: var(--shadow-md);
    }
    .card:focus-within {
      box-shadow: var(--focus-ring);
    }
    .media {
      position: relative;
      aspect-ratio: 1;
      background: var(--color-surface-muted);
      display: grid;
      place-items: center;
    }
    .media img {
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
    }
    .sold-out .media img {
      opacity: 0.45;
      filter: grayscale(1);
    }
    .placeholder {
      --icon-size: 2.5rem;
      color: var(--color-text-subtle);
    }
    .flag {
      position: absolute;
      inset-block-start: var(--space-2);
      inset-inline-start: var(--space-2);
    }
    .info {
      display: flex;
      flex-direction: column;
      gap: var(--space-1);
      padding: var(--space-3);
      flex: 1;
    }
    .brand {
      font-size: var(--font-size-xs);
      color: var(--color-text-muted);
    }
    .name {
      margin: 0;
      font-size: var(--font-size-md);
      font-weight: var(--font-weight-medium);
      line-height: var(--line-height-snug);
      /* Two lines max keeps rows aligned; the full name is the link text for assistive technology. */
      display: -webkit-box;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 2;
      line-clamp: 2;
      overflow: hidden;
      min-block-size: calc(2 * 1.55em);
    }
    .name a {
      color: var(--color-text);
      text-decoration: none;
    }
    .name a::after {
      content: '';
      position: absolute;
      inset: 0;
    }
    .name a:focus-visible {
      box-shadow: none; /* the card shows the focus ring via :focus-within */
    }
    ui-price {
      margin-block-start: auto;
      padding-block-start: var(--space-2);
    }
    .sold-out ui-price {
      opacity: 0.7;
    }
  `,
})
export class UiProductCard {
  readonly product = input.required<ProductCardModel>();
  /** Route prefix: links to [linkBase, slug]. */
  readonly linkBase = input('/product');
  readonly priority = input(false, { transform: booleanAttribute });
  readonly imageWidth = input(480);
  readonly imageHeight = input(480);
  readonly sizes = input('(min-width: 1280px) 20vw, (min-width: 1024px) 25vw, (min-width: 640px) 33vw, 50vw');

  protected readonly messages = injectUiMessages();
  protected readonly link = computed(() => [this.linkBase(), this.product().slug]);
  protected readonly discountText = computed(() => {
    const { price, compareAtPrice } = this.product();
    const fraction = price === null ? 0 : discountFraction(price, compareAtPrice);
    return fraction > 0 ? this.messages.discountOff(formatPercent(fraction)) : null;
  });
}

/** Same footprint as the card, for loading states (prevents layout shift). */
@Component({
  selector: 'ui-product-card-skeleton',
  imports: [UiSkeleton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
  template: `
    <div class="card">
      <ui-skeleton class="media" shape="rect" height="auto" />
      <div class="info">
        <ui-skeleton height="0.8em" width="40%" />
        <ui-skeleton height="1em" />
        <ui-skeleton height="1em" width="70%" />
        <ui-skeleton height="1.2em" width="50%" />
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .card {
      overflow: hidden;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }
    .media {
      aspect-ratio: 1;
      border-radius: 0;
    }
    .info {
      display: flex;
      flex-direction: column;
      gap: var(--space-2);
      padding: var(--space-3);
    }
  `,
})
export class UiProductCardSkeleton {}
