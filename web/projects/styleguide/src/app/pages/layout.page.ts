import { Component, signal } from '@angular/core';
import { FooterGroup, NavItem, UiBadge, UiBreadcrumb, UiSiteFooter, UiSiteHeader } from 'shared-ui';

@Component({
  selector: 'sg-layout-page',
  imports: [UiSiteHeader, UiSiteFooter, UiBreadcrumb, UiBadge],
  template: `
    <ui-site-header
      brandName="فروشگاه لوستر و روشنایی"
      [navItems]="nav"
      [cartCount]="cartCount()"
      accountLabel="سارا احمدی"
      showThemeToggle
      (search)="lastSearch.set($event)"
      (themeToggle)="lastSearch.set('(تغییر پوسته از بالای صفحه)')"
    />

    <main id="main" class="ui-container sg-page" tabindex="-1">
      <ui-breadcrumb [items]="[{ label: 'خانه', link: '/' }, { label: 'نمونه چیدمان' }]" />
      <h1 class="sg-page-title">هدر و فوتر</h1>
      <p class="ui-text-muted">
        هدر چسبنده است؛ در موبایل منو به دراور می‌رود و جستجو در ردیف جدا قرار می‌گیرد. «پرش به محتوای اصلی» با Tab اول دیده می‌شود.
      </p>
      <p>
        آخرین جستجو: <ui-badge variant="primary">{{ lastSearch() || 'هنوز جستجویی انجام نشده' }}</ui-badge>
      </p>
      <div class="demo-actions">
        <button type="button" (click)="cartCount.set(cartCount() + 1)">افزودن به سبد (+۱)</button>
        <button type="button" (click)="cartCount.set(0)">خالی کردن سبد</button>
        <button type="button" (click)="cartCount.set(120)">۱۲۰ لامپ</button>
      </div>
      <div class="filler" aria-hidden="true">
        <p>محتوای صفحه برای بررسی رفتار چسبندگی هدر.</p>
      </div>
    </main>

    <ui-site-footer
      brandName="فروشگاه لوستر و روشنایی"
      about="فروشگاه اینترنتی لوستر، لامپ و چراغ‌های تزئینی با ضمانت اصالت و ارسال بسته‌بندی‌شده و ایمن به سراسر کشور."
      [groups]="groups"
      phone="021-12345678"
      email="support@example.com"
      address="تهران، خیابان نمونه، پلاک ۱۰"
      copyright="© ۱۴۰۵ فروشگاه لوستر و روشنایی. تمامی حقوق محفوظ است."
    >
      <div uiFooterBadges class="badge-slot">محل نمادهای اعتماد (مثلاً اینماد)</div>
    </ui-site-footer>
  `,
  styles: `
    .demo-actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      margin-block: var(--space-4);
    }
    .demo-actions button {
      min-block-size: var(--control-height);
      padding-inline: var(--space-4);
      border: 1px solid var(--color-border-control);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      cursor: pointer;
    }
    .filler {
      min-block-size: 40vh;
    }
    .badge-slot {
      padding: var(--space-4);
      border: 1px dashed var(--color-border-control);
      border-radius: var(--radius-md);
      color: var(--color-text-muted);
      text-align: center;
    }
  `,
})
export class LayoutPage {
  protected readonly cartCount = signal(3);
  protected readonly lastSearch = signal('');

  protected readonly nav: NavItem[] = [
    { label: 'خانه', link: '/layout' },
    { label: 'لوستر', link: '/components' },
    { label: 'لامپ و منبع نور', link: '/tokens' },
    { label: 'آباژور و چراغ دیواری', link: '/tokens' },
    { label: 'راهنمای انتخاب نور', link: '/tokens' },
  ];

  protected readonly groups: FooterGroup[] = [
    {
      title: 'خرید',
      links: [
        { label: 'راهنمای خرید', link: '/tokens' },
        { label: 'شیوه‌های ارسال', link: '/tokens' },
        { label: 'روش‌های پرداخت', link: '/tokens' },
      ],
    },
    {
      title: 'پشتیبانی',
      links: [
        { label: 'سؤالات متداول', link: '/tokens' },
        { label: 'رویه بازگشت و شکستگی در مسیر', link: '/tokens' },
        { label: 'تماس با ما', link: '/tokens' },
      ],
    },
    {
      title: 'درباره ما',
      links: [
        { label: 'درباره فروشگاه', link: '/tokens' },
        { label: 'قوانین و مقررات', link: '/tokens' },
        { label: 'حریم خصوصی', link: '/tokens' },
      ],
    },
  ];
}
