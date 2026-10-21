import { ProductCardModel } from 'shared-ui';

/** Inline SVG placeholder (a glowing lamp) so the style guide needs no image files. */
export function placeholderImage(hue: number, label: string): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 480 480"><defs><radialGradient id="g" cx="50%" cy="42%" r="60%"><stop offset="0" stop-color="hsl(${hue} 90% 88%)"/><stop offset="1" stop-color="hsl(${(hue + 10) % 360} 45% 66%)"/></radialGradient></defs><rect width="480" height="480" fill="url(#g)"/><path d="M240 110c-55 0-92 40-92 88 0 34 18 55 34 75 9 11 12 20 12 33h92c0-13 3-22 12-33 16-20 34-41 34-75 0-48-37-88-92-88z" fill="hsl(${hue} 100% 97% / .92)"/><rect x="204" y="312" width="72" height="14" rx="5" fill="hsl(${hue} 25% 35%)"/><rect x="214" y="334" width="52" height="14" rx="5" fill="hsl(${hue} 25% 30%)"/><text x="240" y="420" font-size="34" text-anchor="middle" fill="hsl(${hue} 45% 18%)" font-family="sans-serif">${label}</text></svg>`;
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

export const SAMPLE_PRODUCTS: ProductCardModel[] = [
  {
    slug: 'crystal-chandelier-12-arm',
    name: 'لوستر کریستال ۱۲ شاخه مدل ورسای با پایه برنجی',
    brand: 'نورینا',
    imageUrl: placeholderImage(40, 'لوستر'),
    price: 18_500_000,
    compareAtPrice: 22_000_000,
    rating: 4.7,
    ratingCount: 64,
    inStock: true,
  },
  {
    slug: 'led-bulb-9w-e27',
    name: 'لامپ ال‌ای‌دی ۹ وات سرپیچ E27 مهتابی',
    brand: 'پرتوان',
    imageUrl: placeholderImage(55, 'لامپ'),
    price: 85_000,
    rating: 4.3,
    ratingCount: 512,
    inStock: true,
  },
  {
    slug: 'sold-out-wall-sconce',
    name: 'چراغ دیواری مدل آرتا ویژه راهرو و سرویس',
    brand: 'نورینا',
    imageUrl: placeholderImage(20, 'دیواری'),
    price: 1_450_000,
    compareAtPrice: 1_800_000,
    rating: 3.9,
    ratingCount: 11,
    inStock: false,
  },
  {
    slug: 'no-image-pendant',
    name: 'آویز سقفی مدرن تک‌شاخه (بدون تصویر)',
    imageUrl: null,
    price: 2_300_000,
    rating: null,
    inStock: true,
  },
  {
    slug: 'call-for-price-chandelier',
    name: 'لوستر چوبی دست‌ساز سفارشی ۲۴ شاخه با شیشه دست‌ساز و زنجیر آنتیک مناسب سالن‌های بزرگ',
    brand: 'لوستر پارس',
    imageUrl: placeholderImage(30, 'سفارشی'),
    price: null,
    rating: 5,
    ratingCount: 3,
    inStock: true,
  },
  {
    slug: 'table-lamp-ceramic',
    name: 'آباژور رومیزی سرامیکی با کلاهک پارچه‌ای',
    brand: 'پرتوان',
    imageUrl: placeholderImage(12, 'آباژور'),
    price: 1_120_000,
    compareAtPrice: 1_400_000,
    rating: 4.4,
    ratingCount: 138,
    inStock: true,
  },
];
