import { InjectionToken, inject } from '@angular/core';

/**
 * All user-facing strings inside the design system. Persian by default; provide UI_MESSAGES to override
 * (tests, another locale later). Components never hard-code visible or screen-reader text.
 */
export interface UiMessages {
  close: string;
  loading: string;
  required: string;
  increaseQuantity: string;
  decreaseQuantity: string;
  quantity: string;
  pagination: string;
  previousPage: string;
  nextPage: string;
  page: (n: string) => string;
  skipToContent: string;
  searchLabel: string;
  searchPlaceholder: string;
  searchSubmit: string;
  cart: string;
  cartWithCount: (count: string) => string;
  account: string;
  menu: string;
  mainNavigation: string;
  outOfStock: string;
  rating: (value: string, max: string) => string;
  ratingCount: (count: string) => string;
  noRating: string;
  breadcrumb: string;
  discountOff: (percent: string) => string;
  retry: string;
  errorTitle: string;
  errorMessage: string;
  emptyTitle: string;
  currencyUnit: string;
  footerNavigation: string;
  theme: string;
  previousPrice: string;
  callForPrice: string;
  errorReference: string;
}

export const DEFAULT_UI_MESSAGES: UiMessages = {
  close: 'بستن',
  loading: 'در حال بارگذاری…',
  required: 'الزامی',
  increaseQuantity: 'افزایش تعداد',
  decreaseQuantity: 'کاهش تعداد',
  quantity: 'تعداد',
  pagination: 'صفحه‌بندی',
  previousPage: 'صفحه قبل',
  nextPage: 'صفحه بعد',
  page: (n) => `صفحه ${n}`,
  skipToContent: 'پرش به محتوای اصلی',
  searchLabel: 'جستجوی محصولات',
  searchPlaceholder: 'جستجو در محصولات…',
  searchSubmit: 'جستجو',
  cart: 'سبد خرید',
  cartWithCount: (count) => `سبد خرید، ${count} کالا`,
  account: 'حساب کاربری',
  menu: 'منو',
  mainNavigation: 'منوی اصلی',
  outOfStock: 'ناموجود',
  rating: (value, max) => `امتیاز ${value} از ${max}`,
  ratingCount: (count) => `${count} نظر`,
  noRating: 'هنوز امتیازی ثبت نشده',
  breadcrumb: 'مسیر صفحه',
  discountOff: (percent) => `${percent} تخفیف`,
  retry: 'تلاش دوباره',
  errorTitle: 'مشکلی پیش آمد',
  errorMessage: 'درخواست شما انجام نشد. لطفاً دوباره تلاش کنید.',
  emptyTitle: 'موردی پیدا نشد',
  currencyUnit: 'تومان',
  footerNavigation: 'پیوندهای پایین صفحه',
  theme: 'تغییر پوسته',
  previousPrice: 'قیمت قبل از تخفیف',
  callForPrice: 'تماس بگیرید',
  errorReference: 'کد پیگیری',
};

export const UI_MESSAGES = new InjectionToken<UiMessages>('UI_MESSAGES', {
  providedIn: 'root',
  factory: () => DEFAULT_UI_MESSAGES,
});

export function injectUiMessages(): UiMessages {
  return inject(UI_MESSAGES);
}
