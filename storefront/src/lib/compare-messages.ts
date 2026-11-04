import shopDefaults from './i18n/defaults.shop.json';
import { registerDefaults, t, tf } from './i18n/texts';

registerDefaults(shopDefaults);

/** Persian strings of the product comparison (keys: shop.compare.*). */
export const compareMessages = {
  get nav() { return t('shop.compare.nav'); },
  navWithCount: (count: string) => tf('shop.compare.nav-with-count', { count }),
  get title() { return t('shop.compare.title'); },
  get description() { return t('shop.compare.description'); },
  get add() { return t('shop.compare.add'); },
  get remove() { return t('shop.compare.remove'); },
  full: (max: string) => tf('shop.compare.full', { max }),
  get inList() { return t('shop.compare.in-list'); },
  open: (count: string) => tf('shop.compare.open', { count }),
  get emptyTitle() { return t('shop.compare.empty-title'); },
  emptyText: (max: string) => tf('shop.compare.empty-text', { max }),
  get oneMore() { return t('shop.compare.one-more'); },
  get allRows() { return t('shop.compare.all-rows'); },
  get onlyDifferences() { return t('shop.compare.only-differences'); },
  get differs() { return t('shop.compare.differs'); },
  get clear() { return t('shop.compare.clear'); },
  removeNamed: (name: string) => tf('shop.compare.remove-named', { name }),
  get tableCaption() { return t('shop.compare.table-caption'); },
  get scrollRegion() { return t('shop.compare.scroll-region'); },
  get rowBrand() { return t('shop.compare.row-brand'); },
  get rowPrice() { return t('shop.compare.row-price'); },
  get rowStock() { return t('shop.compare.row-stock'); },
  get inStock() { return t('shop.compare.in-stock'); },
  get outOfStock() { return t('shop.compare.out-of-stock'); },
  get none() { return t('shop.compare.none'); },
  get noDifferences() { return t('shop.compare.no-differences'); },
  get goToProduct() { return t('shop.compare.go-to-product'); },
};
