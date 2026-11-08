import adminDefaults from '../i18n/defaults.admin.json';
import { registerDefaults, t, tf } from '../i18n/texts';

registerDefaults(adminDefaults);

/** Persian strings of the review moderation screen (keys: admin.reviews.*). */
export const adminReviewMessages = {
  get nav() { return t('admin.reviews.nav'); },
  get title() { return t('admin.reviews.title'); },
  get intro() { return t('admin.reviews.intro'); },
  tabPending: (count: string) => tf('admin.reviews.tab-pending', { count }),
  tabApproved: (count: string) => tf('admin.reviews.tab-approved', { count }),
  tabRejected: (count: string) => tf('admin.reviews.tab-rejected', { count }),
  get tabAll() { return t('admin.reviews.tab-all'); },
  get empty() { return t('admin.reviews.empty'); },
  get colProduct() { return t('admin.reviews.col-product'); },
  get colAuthor() { return t('admin.reviews.col-author'); },
  get colRating() { return t('admin.reviews.col-rating'); },
  get colText() { return t('admin.reviews.col-text'); },
  get colDate() { return t('admin.reviews.col-date'); },
  get colStatus() { return t('admin.reviews.col-status'); },
  get colActions() { return t('admin.reviews.col-actions'); },
  get approve() { return t('admin.reviews.approve'); },
  get reject() { return t('admin.reviews.reject'); },
  get approved() { return t('admin.reviews.approved'); },
  get rejected() { return t('admin.reviews.rejected'); },
  get statusPending() { return t('admin.reviews.status-pending'); },
  get statusApproved() { return t('admin.reviews.status-approved'); },
  get statusRejected() { return t('admin.reviews.status-rejected'); },
  get by() { return t('admin.reviews.by'); },
  get audit() { return t('admin.reviews.audit'); },
  get auditEmpty() { return t('admin.reviews.audit-empty'); },
  get actionApproved() { return t('admin.reviews.action-approved'); },
  get actionRejected() { return t('admin.reviews.action-rejected'); },
  stars: (rating: string) => tf('admin.reviews.stars', { rating }),
};
