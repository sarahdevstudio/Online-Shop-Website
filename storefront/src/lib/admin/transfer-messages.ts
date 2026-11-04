import adminDefaults from '../i18n/defaults.admin.json';
import { registerDefaults, t, type TextKey } from '../i18n/texts';

registerDefaults(adminDefaults);

/** Persian strings of the product import/export screen (keys: admin.transfer.*). */
export const transferMessages = {
  get title() { return t('admin.transfer.title'); },
  get back() { return t('admin.transfer.back'); },
  get link() { return t('admin.transfer.link'); },
  get intro() { return t('admin.transfer.intro'); },
  get exportTitle() { return t('admin.transfer.export-title'); },
  get exportHint() { return t('admin.transfer.export-hint'); },
  get exportCsv() { return t('admin.transfer.export-csv'); },
  get exportXlsx() { return t('admin.transfer.export-xlsx'); },
  get importTitle() { return t('admin.transfer.import-title'); },
  get file() { return t('admin.transfer.file'); },
  get fileHint() { return t('admin.transfer.file-hint'); },
  get preview() { return t('admin.transfer.preview'); },
  get apply() { return t('admin.transfer.apply'); },
  get applyHint() { return t('admin.transfer.apply-hint'); },
  get noFile() { return t('admin.transfer.no-file'); },
  get tooBig() { return t('admin.transfer.too-big'); },
  get failed() { return t('admin.transfer.failed'); },
  get working() { return t('admin.transfer.working'); },
  get summary() { return t('admin.transfer.summary'); },
  get sRows() { return t('admin.transfer.s-rows'); },
  get sCreateProducts() { return t('admin.transfer.s-create-products'); },
  get sUpdateProducts() { return t('admin.transfer.s-update-products'); },
  get sCreateVariants() { return t('admin.transfer.s-create-variants'); },
  get sUpdateVariants() { return t('admin.transfer.s-update-variants'); },
  get sUnchanged() { return t('admin.transfer.s-unchanged'); },
  get ok() { return t('admin.transfer.ok'); },
  get hasErrors() { return t('admin.transfer.has-errors'); },
  get errors() { return t('admin.transfer.errors'); },
  get errorsTruncated() { return t('admin.transfer.errors-truncated'); },
  get colRow() { return t('admin.transfer.col-row'); },
  get colColumn() { return t('admin.transfer.col-column'); },
  get colMessage() { return t('admin.transfer.col-message'); },
  get colProduct() { return t('admin.transfer.col-product'); },
  get colSku() { return t('admin.transfer.col-sku'); },
  get colAction() { return t('admin.transfer.col-action'); },
  get fileLevel() { return t('admin.transfer.file-level'); },
  get rows() { return t('admin.transfer.rows'); },
  get headerRow() { return t('admin.transfer.header-row'); },
  get applied() { return t('admin.transfer.applied'); },
  get formatTitle() { return t('admin.transfer.format-title'); },
  get formatIntro() { return t('admin.transfer.format-intro'); },
  get kept() { return t('admin.transfer.kept'); },
  actionLabel: (action: string): string => ACTIONS[action] ? t(ACTIONS[action]) : action,
  columnHelp: (column: string): string => t(`admin.transfer.columns.${column.replaceAll('_', '-')}` as TextKey),
};

const ACTIONS: Record<string, TextKey> = {
  'create-product': 'admin.transfer.action.create-product',
  'create-variant': 'admin.transfer.action.create-variant',
  'update-variant': 'admin.transfer.action.update-variant',
  'unchanged': 'admin.transfer.action.unchanged',
};

/** The fixed columns, in file order, as documented on the screen. */
export const TRANSFER_COLUMNS = ["product_slug", "product_name", "category", "brand", "status", "description", "path", "sku", "price", "compare_at_price", "stock", "variant_active", "sort_order", "image_ids"] as const;
