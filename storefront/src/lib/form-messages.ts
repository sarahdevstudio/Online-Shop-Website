import shopDefaults from './i18n/defaults.shop.json';
import { registerDefaults, t } from './i18n/texts';

registerDefaults(shopDefaults);

/** Texts of the shared form system (components/form), used by storefront and admin forms alike. */
export const formMessages = {
  get fixErrors() { return t('common.form.fix-errors'); },
  get errorPrefix() { return t('common.form.error-prefix'); },
  get requiredMark() { return t('common.form.required-mark'); },
  get requiredNote() { return t('common.form.required-note'); },
};
