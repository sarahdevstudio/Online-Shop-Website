# گزارش ممیزی (Audit) پروژه — فروشگاه لامپ و لوستر

تاریخ: 2026-10-21. این گزارش فقط خواندن کد است؛ هیچ کد کارکردی تغییر نکرده است (به‌جز ساخت `storefront/` در گام قبلی که مهاجرت فرانت بود).

## 1. خلاصه وضعیت و معماری

- **بک‌اند:** ASP.NET Core (.NET 10) minimal API، لایه‌های Domain / Application / Infrastructure / Api، EF Core + SQL Server، ASP.NET Identity با کوکی، Serilog، ProblemDetails، FluentValidation، rate limiting، antiforgery، health check، OpenAPI. CI با SQL Server موقت.
- **آنچه واقعاً ساخته شده:** فقط «فاز ۱ — Foundation»: ثبت‌نام/ورود/خروج مشتری، `/customer/profile`، یک endpoint ادمین با policy مبتنی بر permission، `IPaymentGateway` + `FakePaymentGateway` (فقط Development/Testing).
- **فرانت:** (الف) Angular: فقط design system (`shared-ui`) و styleguide، بدون هیچ صفحه‌ی فروشگاه. (ب) `storefront/` (Next.js 16، SSR): خانه، دسته، محصول، جستجو، SEO پایه — **با داده‌ی نمونه**، چون API هنوز کاتالوگ ندارد.
- **پایگاه داده:** یک migration (`InitialFoundation`) با جداول Identity و `Customer`. هیچ جدول کاتالوگ، سبد، سفارش، موجودی، بلاگ، ریدایرکت یا SEO وجود ندارد.
- **Git:** repository هنوز **هیچ commit ندارد** (همه‌چیز untracked).
- **مفروضات از کد/مستندات (چون placeholderهای پرامپت پر نشده بود):** فارسی/RTL، تومان، تاریخ جلالی (در `format.ts`)، پرداخت **کارت‌به‌کارت و پرداخت در محل** با تأیید دستی ادمین (addendum بند ۱) نه درگاه آنلاین.

## 2. چک‌لیست

### A. ویژگی‌های اصلی فروشگاه
| مورد | وضعیت | یادداشت |
|---|---|---|
| دسته/زیردسته/برند/واریانت | ❌ | طراحی در ADR هست (Category, Brand, Product, ProductVariant)؛ کدی نیست |
| صفحه‌ی محصول (گالری، زوم، قیمت، موجودی، مشخصات، مرتبط) | ⚠️ | فقط در `storefront` با داده‌ی نمونه: نام، قیمت، تخفیف، امتیاز. گالری/زوم/جدول مشخصات/مرتبط ندارد |
| ویژگی‌های ساختاریافته‌ی نورپردازی (W، lumen، K، سرپیچ، CRI، IP، dimmable…) | ❌ | فقط در addendum بند ۶ به‌عنوان نیاز آمده؛ مدل Attribute/Option ساخته نشده |
| فیلتر facet و قیمت/برند/موجودی | ❌ | |
| جستجو با autocomplete و تحمل غلط املایی فارسی | ❌ | فقط فرم جستجو + `String.Contains` روی داده‌ی نمونه |
| مرتب‌سازی | ❌ | |
| سبد (مهمان + کاربر) و mini-cart | ❌ | صفحه‌ی `/cart` فقط placeholder |
| Checkout (آدرس، ارسال، کد تخفیف) | ❌ | |
| درگاه پرداخت و callback verification | ❌ | تصمیم مالک: کارت‌به‌کارت؛ فقط port + Fake وجود دارد |
| حساب کاربری: ثبت‌نام/ورود با ایمیل+رمز | ✅ | به‌جز OTP |
| ورود با OTP پیامکی | ❌ | تعریف نشده؛ ثبت‌نام الان ایمیل‌محور است (بازار ایران معمولاً موبایل‌محور) |
| پروفایل، آدرس‌ها، تاریخچه/رهگیری سفارش | ⚠️ | فقط GET پروفایل؛ بقیه ❌ |
| Wishlist و مقایسه | ❌ | |
| نظرات/امتیاز/پرسش‌وپاسخ | ❌ | |
| اعلان‌ها (SMS/ایمیل، back-in-stock) | ❌ | تأیید ایمیل هم عمداً عقب افتاده (addendum بند ۵) |
| پنل ادمین | ❌ | فقط `/admin/ping` |
| import/export CSV | ❌ | |
| نمادها (اینماد/ساماندهی)، قوانین، تماس | ⚠️ | slot برای badge در Footer هست؛ محتوا/صفحات حقوقی ❌ |

### B. سئوی فنی
| مورد | وضعیت | یادداشت |
|---|---|---|
| SSR/SSG | ✅ | `storefront` (Next.js)؛ با curl تأیید شد |
| URLهای تمیز / تصمیم slug فارسی یا انگلیسی | ⚠️ | الگوها هست (`/category/x`, `/product/x`) ولی سیاست slug (فارسی/انگلیسی) هنوز تصمیم‌گیری نشده |
| title/description یکتا + override دستی | ⚠️ | تولید از قالب ✅؛ override دستی از ادمین ❌ (SeoMetadata فاز ۴) |
| canonical | ⚠️ | همه‌ی صفحات ✅ — **باگ:** `?page=N` خارج از محدوده canonical جعلی می‌دهد (`category/[slug]/page.tsx`، تابع `generateMetadata`) |
| مدیریت faceted navigation | ❌ | فیلتری وجود ندارد؛ `/search` noindex است ✅ |
| sitemap | ⚠️ | یک فایل واحد؛ تفکیک (products/categories/blog/pages) و بروزرسانی خودکار از API ❌؛ `lastModified` ندارد |
| robots.txt | ✅ | مسیرهای خصوصی بسته؛ CSS/JS بلاک نیست |
| JSON-LD: Product/Offer/AggregateRating/Breadcrumb/Organization/WebSite+SearchAction | ✅ | Review، LocalBusiness، FAQPage ❌. اعتبارسنجی با Rich Results Test انجام نشده |
| محصول ناموجود/متوقف: بدون soft-404 | ⚠️ | ناموجود: صفحه live با OutOfStock ✅؛ متوقف‌شده/301/410 ❌ |
| 404 واقعی + مدیریت ریدایرکت 301 | ⚠️ | 404 ✅ (`not-found.tsx`)؛ مدیریت ریدایرکت ❌ |
| `lang="fa" dir="rtl"` | ✅ | |
| سلسله‌مراتب heading (یک H1) | ✅ | در صفحات موجود |
| لینک‌دهی داخلی (breadcrumb، مرتبط، بلاگ) | ⚠️ | breadcrumb ✅؛ مرتبط/بلاگ ❌ |
| Open Graph / Twitter | ✅ | تصویر اشتراک‌گذاری ندارد (عکسی نیست) |
| HTTPS / دامنه‌ی ترجیحی | ❌ | HSTS و redirect در API هست؛ `NEXT_PUBLIC_SITE_URL` و www/non-www تنظیم نشده |

### C. Core Web Vitals
| مورد | وضعیت | یادداشت |
|---|---|---|
| اندازه‌گیری LCP/CLS/INP | ❌ | انجام نشده |
| تصاویر WebP/AVIF، srcset، alt | ⚠️ | کارت `<img>` ساده با width/height، lazy/eager و alt؛ pipeline تصویر (`IMediaStorage`) ❌ |
| فونت | ⚠️ | Vazirmatn variable از fontsource (self-host)، `font-display: swap` پیش‌فرض fontsource؛ subset/preload بررسی نشده |
| cache/CDN/bundle | ⚠️ | SSG پیش‌فرض Next؛ cache کاتالوگ نیاز به طراحی دارد (revalidate/tags) |
| کوئری‌های ایندکس‌شده‌ی فیلتر | ❌ | جدولی نیست |

### D. سئوی محتوا
| مورد | وضعیت | یادداشت |
|---|---|---|
| متن مقدمه/راهنمای خرید برای دسته | ⚠️ | فقط یک خط description؛ فیلد محتوای بلند ❌ |
| توضیح یکتای محصول | ❌ | داده‌ی واقعی نیست |
| بلاگ و راهنماها | ❌ | فاز ۹ |
| نقشه‌ی کلمات کلیدی | ❌ | |

### E. کیفیت، امنیت، آنالیتیکس
| مورد | وضعیت | یادداشت |
|---|---|---|
| موبایل‌اول و RTL | ✅ | design system با `check:rtl` و `check:contrast` (Angular) — این اسکریپت‌ها هنوز به `storefront` منتقل نشده‌اند |
| دسترس‌پذیری | ⚠️ | skip-link، aria، target size در کامپوننت‌ها ✅؛ تست axe برای storefront ❌ |
| اعتبارسنجی ورودی، CSRF، XSS، SQLi | ✅ | FluentValidation، antiforgery روی همه‌ی `/api/v1`، EF پارامتری، JSON-LD escape شده |
| rate limit ورود/OTP | ⚠️ | ✅ برای login/register (بر اساس IP) + lockout؛ **ریسک:** پشت reverse proxy همه IP یکسان می‌شوند (ForwardedHeaders تنظیم نشده)؛ OTP نیست |
| تأیید مبلغ سمت سرور | ⚠️ | اصل در ADR ثبت شده؛ سفارشی هنوز نیست تا تست شود |
| لاگ/مانیتورینگ خطا | ⚠️ | Serilog کنسول + `traceId`؛ sink ماندگار/alerting ❌ |
| Analytics، Search Console | ❌ | |
| بکاپ خودکار | ❌ | |

## 3. باگ‌ها و ریسک‌ها (به ترتیب شدت)

**Critical**
1. **`dotnet build/test` شکست می‌خورد.** `Directory.Build.props` هشدار را خطا می‌کند و پکیج transitive `System.Security.Cryptography.Pkcs 9.0.11` آسیب‌پذیری high/moderate دارد (NU1902/NU1903). نتیجه: هیچ تستی اجرا نمی‌شود، CI قرمز است و نمی‌توان «قبل/بعد» تست گرفت. (بدون این‌که اجرا شود: تعداد تست‌ها ۵ فایل unit + ۵ فایل integration است.)

**High**
2. **هیچ commit در git وجود ندارد.** تاریخچه، review و بازگشت ممکن نیست.
3. **IP در rate limit:** پشت proxy/CDN کلید پارتیشن یک IP ثابت می‌شود؛ یا همه‌ی کاربران یک سهمیه می‌گیرند یا حمله‌گر آسان دور می‌زند. نیاز به `UseForwardedHeaders` با proxy مورد اعتماد.
4. **ثبت‌نام ایمیل‌محور بدون تأیید ایمیل** و بدون OTP: برای بازار ایران و ضدسوءاستفاده ضعیف است؛ حساب‌های جعلی و قفل‌کردن (lockout) ایمیل دیگران ممکن است (`lockoutOnFailure` روی ایمیل هدف).

**Medium**
5. **canonical نادرست** برای `?page` خارج از محدوده در `storefront` (محتوای صفحه‌ی آخر با canonical صفحه‌ی ناموجود). باید 404 یا redirect شود.
6. **تفاوت `AllowedHosts: "*"`** در `appsettings.json` — باید در production محدود شود.
7. `storefront` فعلاً **بدون تست** است (واحد/e2e/axe/validate JSON-LD) و `check:contrast`/`check:rtl` Angular را ندارد.
8. داده‌ی نمونه در `storefront/src/lib/catalog.ts` در build تولیدی هم sitemap و صفحه می‌سازد؛ باید قبل از انتشار حذف/جایگزین شود (خطر ایندکس شدن محتوای ساختگی).
9. پوشه‌ی `web/` (Angular) و `storefront/` هر دو وجود دارند؛ ریسک سردرگمی و کد تکراری.

**Low**
10. `SecurityHeaders` فاقد CSP و Permissions-Policy؛ Referrer-Policy فقط روی API است (Next.js هدر ندارد).
11. `README` و CI هنوز به `storefront` اشاره نمی‌کنند.
12. آدرس `NEXT_PUBLIC_SITE_URL` پیش‌فرض localhost است؛ اگر فراموش شود canonical/sitemap اشتباه تولید می‌شود.

## 4. نقشه‌ی راه

### P0 — مانع انتشار
| # | کار | حجم |
|---|---|---|
| 1 | رفع شکست build (ارتقا/pin کردن `System.Security.Cryptography.Pkcs` یا وابستگی مولد آن) و سبز کردن تست‌ها | S |
| 2 | commit اولیه + `.gitignore` بازبینی + اتصال CI به `storefront` (build + typecheck) | S |
| 3 | کاتالوگ بک‌اند: Category/Brand/Product/Variant/Attribute (ویژگی‌های ساختاریافته‌ی نورپردازی)/Image/Inventory + API ادمین و عمومی (فاز ۲) | L |
| 4 | اتصال `storefront` به API واقعی، حذف داده‌ی نمونه، رفع باگ canonical صفحه‌بندی | M |
| 5 | سبد + checkout + سفارش با قیمت‌گذاری سمت سرور + تأیید دستی پرداخت (فازهای ۵ و ۷) | L |
| 6 | ForwardedHeaders + محدودکردن AllowedHosts + تصمیم OTP/تأیید موبایل | M |
| 7 | پنل ادمین حداقل: محصول، دسته، سفارش (فاز ۱۰) | L |

### P1 — سئو و ویژگی‌های مهم
| # | کار | حجم |
|---|---|---|
| 8 | مدل SeoMetadata/Slug/Redirect، 301/410، sitemap تفکیک‌شده و خودکار | M |
| 9 | facet filters + سیاست ایندکس صفحات فیلتر منتخب (مثلاً «لامپ LED E27»)، بقیه noindex/canonical | L |
| 10 | جستجوی فارسی با autocomplete و تحمل غلط (مثلاً SQL Server Full-Text یا موتور جستجو) | L |
| 11 | pipeline تصویر (AVIF/WebP، srcset) و `next/image` یا CDN | M |
| 12 | JSON-LD: Review/FAQPage/LocalBusiness + اعتبارسنجی خودکار | S |
| 13 | صفحات حقوقی/اینماد/تماس + متن مقدمه‌ی دسته‌ها | M |
| 14 | حساب کاربری: آدرس‌ها، سفارش‌ها، IDOR تست (فاز ۸) | M |
| 15 | تست‌های storefront: axe، RTL، contrast، snapshot متای سئو | M |
| 16 | اندازه‌گیری Core Web Vitals (Lighthouse CI) روی ۴ صفحه‌ی کلیدی | S |

### P2 — رشد
بلاگ و راهنماها (فاز ۹)، نظرات/پرسش‌وپاسخ، wishlist، مقایسه‌ی مشخصات لامپ، back-in-stock، کوپن، import/export CSV، analytics/Search Console، مانیتورینگ و بکاپ خودکار، CSP، حذف `web/` Angular.

## 5. سؤال‌های باز (قبل از پیاده‌سازی)
1. slug فارسی یا انگلیسی؟
2. ورود با OTP موبایل لازم است یا ایمیل کافی است؟ ارائه‌دهنده‌ی پیامک؟
3. تأیید می‌کنید پرداخت فقط کارت‌به‌کارت/در محل است (مطابق addendum) و درگاه آنلاین الان در دامنه نیست؟
4. دامنه‌ی نهایی و hosting (برای canonical، HSTS، proxy)؟
5. `web/` (Angular) حذف شود یا تا انتقال کامل بماند؟
