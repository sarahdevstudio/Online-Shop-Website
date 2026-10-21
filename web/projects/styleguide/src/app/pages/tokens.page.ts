import { DOCUMENT } from '@angular/common';
import { Component, inject } from '@angular/core';

interface Swatch {
  name: string;
  note?: string;
}

@Component({
  selector: 'sg-tokens-page',
  template: `
    <main id="main" class="ui-container sg-page" tabindex="-1">
      <h1 class="sg-page-title">توکن‌های طراحی</h1>
      <p class="ui-text-muted">
        تنها منبع رنگ، فونت، فاصله و شکل. کامپوننت‌ها فقط از توکن‌های معنایی استفاده می‌کنند؛ تغییر برند = تغییر همین فایل
        (<code>tokens.css</code>). کنتراست همه جفت‌رنگ‌ها با <code>npm run check:contrast</code> بررسی می‌شود.
      </p>

      <section class="sg-section">
        <h2>رنگ</h2>
        @for (group of colorGroups; track group.title) {
          <h3 class="sg-label">{{ group.title }}</h3>
          <div class="swatches">
            @for (swatch of group.swatches; track swatch.name) {
              <div class="swatch">
                <span class="chip" [style.background]="'var(' + swatch.name + ')'"></span>
                <code dir="ltr">{{ swatch.name }}</code>
                <small dir="ltr">{{ value(swatch.name) }}</small>
                @if (swatch.note) {
                  <small>{{ swatch.note }}</small>
                }
              </div>
            }
          </div>
        }
      </section>

      <section class="sg-section">
        <h2>تایپوگرافی</h2>
        <p class="ui-text-muted">فونت وزیرمتن (متن‌باز، OFL) به‌صورت محلی و متغیر. ارتفاع خط متن فارسی ۱٫۸۵ است.</p>
        @for (size of typeScale; track size.token) {
          <div class="type-row">
            <code dir="ltr">{{ size.token }}</code>
            <span [style.font-size]="'var(' + size.token + ')'" [style.font-weight]="size.bold ? 700 : 400" [style.line-height]="size.bold ? 1.35 : 1.85">
              {{ size.sample }}
            </span>
          </div>
        }
        <h3 class="sg-label">اعداد و تاریخ</h3>
        <div class="sg-demo">
          <span>۱٬۲۵۰٬۰۰۰ تومان</span>
          <span>۴٫۵ از ۵</span>
          <span>۱۴۰۵/۰۷/۱۲</span>
          <span dir="ltr">09120000000</span>
          <span dir="ltr">user&#64;example.com</span>
        </div>
        <p class="ui-text-muted ui-text-sm">
          اعداد با <code>Intl.NumberFormat('fa-IR')</code> و تاریخ شمسی با تقویم داخلی ICU نمایش داده می‌شوند؛ کتابخانه اضافه لازم نیست.
        </p>
      </section>

      <section class="sg-section">
        <h2>فاصله‌گذاری (پایه ۴ پیکسل)</h2>
        <div class="space-list">
          @for (n of spaces; track n) {
            <div class="space-row">
              <code dir="ltr">--space-{{ n }}</code>
              <span class="bar" [style.inline-size]="'var(--space-' + n + ')'"></span>
            </div>
          }
        </div>
      </section>

      <section class="sg-section">
        <h2>شکل، سایه و حرکت</h2>
        <div class="sg-demo">
          @for (r of radii; track r) {
            <div class="shape" [style.border-radius]="'var(' + r + ')'"><code dir="ltr">{{ r }}</code></div>
          }
        </div>
        <div class="sg-demo">
          @for (s of shadows; track s) {
            <div class="shape" [style.box-shadow]="'var(' + s + ')'"><code dir="ltr">{{ s }}</code></div>
          }
        </div>
        <p class="ui-text-muted ui-text-sm">
          حرکت: ۱۲۰/۲۰۰/۳۲۰ میلی‌ثانیه با منحنی استاندارد؛ با <code>prefers-reduced-motion</code> تقریباً حذف می‌شود.
        </p>
      </section>

      <section class="sg-section">
        <h2>چیدمان و ریسپانسیو</h2>
        <ul>
          <li>رویکرد «موبایل‌اول»: استایل پایه همان نمای موبایل است.</li>
          <li>نقاط شکست: ۶۴۰ (sm)، ۷۶۸ (md)، ۱۰۲۴ (lg)، ۱۲۸۰ (xl) پیکسل.</li>
          <li>کانتینر حداکثر ۸۰ رِم؛ حاشیه ۱۶ → ۲۴ → ۳۲ پیکسل.</li>
          <li>شبکه محصول: ۲ ← ۳ ← ۴ ← ۵ ستون. حداقل ناحیه لمس ۴۴ پیکسل.</li>
          <li>فقط ویژگی‌های منطقی CSS (<code>margin-inline</code>، <code>inset-inline-start</code>…) تا RTL و LTR هر دو درست باشند؛ <code>npm run check:rtl</code> این را اجباری می‌کند.</li>
        </ul>
      </section>
    </main>
  `,
  styles: `
    .swatches {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
      gap: var(--space-3);
    }
    .swatch {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: var(--space-2);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      font-size: var(--font-size-xs);
    }
    .swatch small {
      color: var(--color-text-muted);
    }
    .chip {
      block-size: 2.5rem;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
    }
    .type-row {
      display: grid;
      grid-template-columns: 8rem 1fr;
      gap: var(--space-4);
      align-items: baseline;
      padding-block: var(--space-2);
      border-block-end: 1px solid var(--color-border);
    }
    .space-row {
      display: grid;
      grid-template-columns: 8rem 1fr;
      align-items: center;
      gap: var(--space-3);
      padding-block: 2px;
    }
    .bar {
      block-size: 0.75rem;
      background: var(--color-primary);
      border-radius: 2px;
    }
    .shape {
      display: grid;
      place-items: center;
      inline-size: 8rem;
      block-size: 5rem;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      font-size: var(--font-size-xs);
    }
  `,
})
export class TokensPage {
  private readonly document = inject(DOCUMENT);

  protected readonly colorGroups: { title: string; swatches: Swatch[] }[] = [
    {
      title: 'سطح‌ها',
      swatches: [
        { name: '--color-bg' },
        { name: '--color-surface' },
        { name: '--color-surface-muted' },
        { name: '--color-surface-sunken' },
        { name: '--color-border' },
        { name: '--color-border-control', note: 'حاشیه فیلد، ≥ ۳:۱' },
      ],
    },
    {
      title: 'متن',
      swatches: [{ name: '--color-text' }, { name: '--color-text-muted' }, { name: '--color-text-subtle' }],
    },
    {
      title: 'برند',
      swatches: [
        { name: '--color-primary' },
        { name: '--color-primary-hover' },
        { name: '--color-primary-active' },
        { name: '--color-primary-contrast', note: 'متن روی برند' },
        { name: '--color-primary-soft' },
      ],
    },
    {
      title: 'وضعیت‌ها',
      swatches: [
        { name: '--color-danger' },
        { name: '--color-danger-soft' },
        { name: '--color-success' },
        { name: '--color-success-soft' },
        { name: '--color-warning' },
        { name: '--color-warning-soft' },
        { name: '--color-info' },
        { name: '--color-info-soft' },
      ],
    },
    {
      title: 'تجارت',
      swatches: [
        { name: '--color-sale', note: 'نشان تخفیف' },
        { name: '--color-star' },
        { name: '--color-star-empty' },
        { name: '--color-focus', note: 'حلقه فوکوس' },
      ],
    },
  ];

  protected readonly typeScale = [
    { token: '--font-size-4xl', bold: true, sample: 'سرتیتر صفحه · لوستر و روشنایی' },
    { token: '--font-size-3xl', bold: true, sample: 'سرتیتر بخش · پرفروش‌ترین‌ها' },
    { token: '--font-size-2xl', bold: true, sample: 'عنوان زیربخش · ویژگی‌های محصول' },
    { token: '--font-size-xl', bold: true, sample: 'عنوان کارت و دیالوگ' },
    { token: '--font-size-lg', bold: false, sample: 'متن درشت برای مقاله‌های وبلاگ و توضیح محصول.' },
    { token: '--font-size-md', bold: false, sample: 'متن اصلی صفحه. ارسال سریع به سراسر کشور و ضمانت اصالت کالا.' },
    { token: '--font-size-sm', bold: false, sample: 'متن کمکی، برچسب فرم‌ها و توضیحات تکمیلی.' },
    { token: '--font-size-xs', bold: false, sample: 'کوچک‌ترین اندازه مجاز؛ برای واحد قیمت و تعداد نظرات.' },
  ];

  protected readonly spaces = [1, 2, 3, 4, 5, 6, 8, 10, 12, 16];
  protected readonly radii = ['--radius-sm', '--radius-md', '--radius-lg', '--radius-full'];
  protected readonly shadows = ['--shadow-sm', '--shadow-md', '--shadow-lg'];

  protected value(token: string): string {
    return getComputedStyle(this.document.documentElement).getPropertyValue(token).trim();
  }
}
