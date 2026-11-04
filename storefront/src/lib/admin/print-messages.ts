import adminDefaults from '../i18n/defaults.admin.json';
import { registerDefaults, t } from '../i18n/texts';

registerDefaults(adminDefaults);

/** Persian strings of the printable invoice and packing slip (keys: admin.print.*). */
export const printMessages = {
  get invoiceTitle() { return t('admin.print.invoice-title'); },
  get packingTitle() { return t('admin.print.packing-title'); },
  get print() { return t('admin.print.print'); },
  get size() { return t('admin.print.size'); },
  get sizeA4() { return t('admin.print.size-a4'); },
  get sizeA5() { return t('admin.print.size-a5'); },
  get back() { return t('admin.print.back'); },
  get hint() { return t('admin.print.hint'); },
  get orderNumber() { return t('admin.print.order-number'); },
  get date() { return t('admin.print.date'); },
  get seller() { return t('admin.print.seller'); },
  get recipient() { return t('admin.print.recipient'); },
  get phone() { return t('admin.print.phone'); },
  get postalCode() { return t('admin.print.postal-code'); },
  get row() { return t('admin.print.row'); },
  get item() { return t('admin.print.item'); },
  get sku() { return t('admin.print.sku'); },
  get unitPrice() { return t('admin.print.unit-price'); },
  get quantity() { return t('admin.print.quantity'); },
  get lineTotal() { return t('admin.print.line-total'); },
  get packed() { return t('admin.print.packed'); },
  get itemsCount() { return t('admin.print.items-count'); },
  get payment() { return t('admin.print.payment'); },
  get paymentStatus() { return t('admin.print.payment-status'); },
  get note() { return t('admin.print.note'); },
  get thanks() { return t('admin.print.thanks'); },
  get openInvoice() { return t('admin.print.open-invoice'); },
  get openSlip() { return t('admin.print.open-slip'); },
  get currency() { return t('admin.print.currency'); },
};
