import adminDefaults from '../i18n/defaults.admin.json';
import { registerDefaults, t, tf } from '../i18n/texts';

registerDefaults(adminDefaults);

/** Persian strings of the per-entity SEO panel (keys: admin.seo.*). */
export const seoMessages = {
  get title() { return t('admin.seo.title'); },
  get hint() { return t('admin.seo.hint'); },
  get metaTitle() { return t('admin.seo.meta-title'); },
  get metaTitleHint() { return t('admin.seo.meta-title-hint'); },
  get metaDescription() { return t('admin.seo.meta-description'); },
  get metaDescriptionHint() { return t('admin.seo.meta-description-hint'); },
  counter: (n: string, ideal?: string) => tf('admin.seo.counter', { n, ideal: ideal ?? '' }),
  counterLong: (n: string, ideal?: string) => tf('admin.seo.counter-long', { n, ideal: ideal ?? '' }),
  get ogImage() { return t('admin.seo.og-image'); },
  get ogImageHint() { return t('admin.seo.og-image-hint'); },
  get noindex() { return t('admin.seo.noindex'); },
  get noindexWarning() { return t('admin.seo.noindex-warning'); },
  get faqTitle() { return t('admin.seo.faq-title'); },
  get faqHint() { return t('admin.seo.faq-hint'); },
  get faqQuestion() { return t('admin.seo.faq-question'); },
  get faqAnswer() { return t('admin.seo.faq-answer'); },
  get faqAdd() { return t('admin.seo.faq-add'); },
  get faqRemove() { return t('admin.seo.faq-remove'); },
  faqRow: (n: string, ideal?: string) => tf('admin.seo.faq-row', { n, ideal: ideal ?? '' }),
  get introTitle() { return t('admin.seo.intro-title'); },
  get intro() { return t('admin.seo.intro'); },
  get introHint() { return t('admin.seo.intro-hint'); },
  get save() { return t('admin.seo.save'); },
  get saved() { return t('admin.seo.saved'); },
  get viewPage() { return t('admin.seo.view-page'); },
};
