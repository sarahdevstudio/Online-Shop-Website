import shopDefaults from './i18n/defaults.shop.json';
import { registerDefaults, t, tf } from './i18n/texts';

registerDefaults(shopDefaults);

/** Persian strings of the product reviews (keys: shop.reviews.*). */
export const reviewMessages = {
  get title() { return t('shop.reviews.title'); },
  summary: (average: string, count: string) => tf('shop.reviews.summary', { average, count }),
  get none() { return t('shop.reviews.none'); },
  stars: (rating: string) => tf('shop.reviews.stars', { rating }),
  get write() { return t('shop.reviews.write'); },
  get hint() { return t('shop.reviews.hint'); },
  get name() { return t('shop.reviews.name'); },
  get mobile() { return t('shop.reviews.mobile'); },
  get rating() { return t('shop.reviews.rating'); },
  ratingOption: (n: string) => tf('shop.reviews.rating-option', { n }),
  get text() { return t('shop.reviews.text'); },
  get textHint() { return t('shop.reviews.text-hint'); },
  get submit() { return t('shop.reviews.submit'); },
  get thanks() { return t('shop.reviews.thanks'); },
  get failed() { return t('shop.reviews.failed'); },
  get fixErrors() { return t('shop.reviews.fix-errors'); },
  get tooMany() { return t('shop.reviews.too-many'); },
  get website() { return t('shop.reviews.website'); },
};
