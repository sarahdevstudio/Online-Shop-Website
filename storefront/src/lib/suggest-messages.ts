import shopDefaults from './i18n/defaults.shop.json';
import { registerDefaults, t, tf } from './i18n/texts';

registerDefaults(shopDefaults);

/** Persian strings of the search suggestions (keys: shop.suggest.*). */
export const suggestMessages = {
  get products() { return t('shop.suggest.products'); },
  get categories() { return t('shop.suggest.categories'); },
  get nearest() { return t('shop.suggest.nearest'); },
  allResults: (q: string) => tf('shop.suggest.all-results', { q }),
  count: (count: string) => tf('shop.suggest.count', { count }),
  get none() { return t('shop.suggest.none'); },
  price: (price: string) => tf('shop.suggest.price', { price }),
  get loading() { return t('shop.suggest.loading'); },
  fuzzyNotice: (q: string) => tf('shop.suggest.fuzzy-notice', { q }),
};
