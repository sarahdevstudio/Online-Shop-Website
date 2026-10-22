using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Catalog;

/// <summary>
/// <see cref="EnsureAttributesAsync"/>: the lighting attributes every environment needs (admins add options, not
/// attributes, in the MVP); only inserts what is missing, never edits. <see cref="SeedDevelopmentDataAsync"/>: sample
/// categories, brands and products; the caller runs it in Development only, and it skips a non-empty catalog.
/// </summary>
public sealed class CatalogSeeder(ShopDbContext db, TimeProvider clock, ILogger<CatalogSeeder> logger)
{
    private sealed record AttributeSeed(string Code, string Name, string? Unit, AttributeType Type, bool Filterable, (string Slug, string Value)[] Options);

    private static readonly AttributeSeed[] Attributes =
    [
        new("wattage", "توان", null, AttributeType.Option, true,
            [("5w", "۵ وات"), ("7w", "۷ وات"), ("9w", "۹ وات"), ("12w", "۱۲ وات"), ("15w", "۱۵ وات"), ("20w", "۲۰ وات")]),
        new("base", "سرپیچ", null, AttributeType.Option, true, [("e27", "E27"), ("e14", "E14"), ("gu10", "GU10")]),
        new("cct", "رنگ نور", null, AttributeType.Option, true,
            [("2700k", "آفتابی (۲۷۰۰K)"), ("3000k", "آفتابی (۳۰۰۰K)"), ("4000k", "یخی/طبیعی (۴۰۰۰K)"), ("6500k", "مهتابی (۶۵۰۰K)")]),
        new("lumen", "شار نوری", "لومن", AttributeType.Text, false, []),
        new("lifetime", "طول عمر", "ساعت", AttributeType.Text, false, []),
        new("arms", "تعداد شاخه", null, AttributeType.Text, false, []),
        new("finish", "رنگ بدنه", null, AttributeType.Text, false, []),
        new("ip", "درجه حفاظت", null, AttributeType.Text, false, []),
    ];

    /// <summary>Codes of the built-in attributes (used as query-string keys).</summary>
    public static IReadOnlyList<string> AttributeCodes { get; } = Attributes.Select(a => a.Code).ToList();

    public async Task EnsureAttributesAsync(CancellationToken ct)
    {
        var existing = await db.Attributes.Include(a => a.Options).ToDictionaryAsync(a => a.Code, ct);
        for (var i = 0; i < Attributes.Length; i++)
        {
            var seed = Attributes[i];
            if (!existing.TryGetValue(seed.Code, out var attribute))
            {
                attribute = AttributeDefinition.Create(seed.Code, seed.Name, seed.Unit, seed.Type, seed.Filterable, (i + 1) * 10);
                db.Attributes.Add(attribute);
            }

            for (var j = 0; j < seed.Options.Length; j++)
            {
                if (attribute.Options.All(o => o.Slug != seed.Options[j].Slug))
                {
                    attribute.Options.Add(AttributeOption.Create(seed.Options[j].Value, seed.Options[j].Slug, (j + 1) * 10));
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Sample products must never reach a real shop: Development only, not Testing, Staging or Production.</summary>
    public static bool ShouldSeedSampleData(IHostEnvironment environment) => environment.IsDevelopment();

    public async Task SeedDevelopmentDataAsync(CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct) || await db.Brands.AnyAsync(ct) || await db.Products.AnyAsync(ct))
        {
            return;
        }

        var lamps = Category.Create("لامپ", "lamps", null, "انواع لامپ LED برای روشنایی خانه و محل کار.", 10, true);
        var chandeliers = Category.Create("لوستر", "chandeliers", null, "لوسترهای کلاسیک، کریستالی و مدرن.", 20, true);
        var wallLights = Category.Create("چراغ دیواری", "wall-lights", null, "چراغ‌های دیواری داخلی و فضای باز.", 30, true);
        db.Categories.AddRange(lamps, chandeliers, wallLights);
        await db.SaveChangesAsync(ct);

        var ledBulbs = Category.Create("لامپ LED حبابی", "led-bulbs", lamps.Id, "لامپ‌های حبابی LED با سرپیچ E27 و E14.", 10, true);
        var candleBulbs = Category.Create("لامپ شمعی و اشکی", "candle-bulbs", lamps.Id, "لامپ‌های شمعی و اشکی مناسب لوستر و آباژور.", 20, true);
        db.Categories.AddRange(ledBulbs, candleBulbs);

        var brands = new Dictionary<string, Brand>
        {
            ["pars-shahab"] = Brand.Create("پارس شهاب", "pars-shahab", true),
            ["zar"] = Brand.Create("زر", "zar", true),
            ["namanoor"] = Brand.Create("نمانور", "namanoor", true),
            ["mazinoor"] = Brand.Create("مازی‌نور", "mazinoor", true),
            ["philips"] = Brand.Create("فیلیپس", "philips", true),
        };
        db.Brands.AddRange(brands.Values);
        await db.SaveChangesAsync(ct);

        var attributes = await db.Attributes.Include(a => a.Options).ToDictionaryAsync(a => a.Code, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var products = SampleProducts(ledBulbs, candleBulbs, chandeliers, wallLights);
        for (var i = 0; i < products.Length; i++)
        {
            var s = products[i];
            var brand = s.Brand is null ? null : brands[s.Brand];
            // Staggered creation times so "newest" ordering is stable and meaningful.
            var product = Product.Create(s.Category.Id, brand?.Id, brand?.Name, s.Name, s.Slug, s.Description, s.Status, now.AddMinutes(-i));
            foreach (var v in s.Variants)
            {
                product.Variants.Add(ProductVariant.Create(v.Sku, v.Price, v.CompareAt, v.Stock, isActive: true, sortOrder: product.Variants.Count));
            }

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            for (var v = 0; v < s.Variants.Length; v++)
            {
                foreach (var (code, slug) in s.Variants[v].Axes)
                {
                    product.AttributeValues.Add(ProductAttributeValue.ForOption(
                        attributes[code].Id, attributes[code].Options.Single(o => o.Slug == slug).Id, product.Variants[v].Id));
                }
            }

            foreach (var (code, value) in s.Specs)
            {
                var attribute = attributes[code];
                product.AttributeValues.Add(attribute.Type == AttributeType.Option
                    ? ProductAttributeValue.ForOption(attribute.Id, attribute.Options.Single(o => o.Slug == value).Id, null)
                    : ProductAttributeValue.ForText(attribute.Id, value, null));
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded development catalog: {Products} products", products.Length);
    }

    private sealed record VariantSeed(string Sku, long Price, long? CompareAt, int Stock, (string Code, string Slug)[] Axes);

    private sealed record ProductSeed(
        Category Category, string? Brand, string Name, string Slug, string Description, ProductStatus Status,
        (string Code, string Value)[] Specs, VariantSeed[] Variants);

    private static VariantSeed V(string sku, long price, int stock, string? wattage = null, string? cct = null, long? compareAt = null) =>
        new(sku, price, compareAt, stock,
            [.. new[] { ("wattage", wattage), ("cct", cct) }.Where(x => x.Item2 is not null).Select(x => (x.Item1, x.Item2!))]);

    private static ProductSeed[] SampleProducts(Category ledBulbs, Category candleBulbs, Category chandeliers, Category wallLights) =>
    [
        // LED bulbs
        new(ledBulbs, "pars-shahab", "لامپ LED حبابی پارس شهاب", "pars-shahab-led-bulb-e27",
            "لامپ حبابی LED با نور یکنواخت و مصرف کم، مناسب روشنایی عمومی منزل.", ProductStatus.Published,
            [("base", "e27"), ("lifetime", "۱۵۰۰۰")],
            [V("PS-LB-9W-3000", 98_000, 40, "9w", "3000k"), V("PS-LB-9W-6500", 98_000, 60, "9w", "6500k"),
             V("PS-LB-12W-3000", 128_000, 25, "12w", "3000k"), V("PS-LB-12W-6500", 128_000, 35, "12w", "6500k")]),
        new(ledBulbs, "zar", "لامپ LED حبابی ۱۲ وات زر", "zar-led-bulb-12w-e27",
            "لامپ ۱۲ وات زر با سه رنگ نور، جایگزین لامپ رشته‌ای ۱۰۰ وات.", ProductStatus.Published,
            [("base", "e27"), ("lumen", "۱۰۵۰")],
            [V("ZR-LB-12W-3000", 115_000, 30, "12w", "3000k", 135_000), V("ZR-LB-12W-4000", 115_000, 12, "12w", "4000k"),
             V("ZR-LB-12W-6500", 115_000, 50, "12w", "6500k")]),
        new(ledBulbs, "namanoor", "لامپ LED حبابی ۱۵ وات نمانور", "namanoor-led-bulb-15w-e27",
            "لامپ پرنور ۱۵ وات برای فضاهای بزرگ مانند پذیرایی و مغازه.", ProductStatus.Published,
            [("base", "e27"), ("lumen", "۱۴۰۰")],
            [V("NN-LB-15W-3000", 165_000, 18, "15w", "3000k"), V("NN-LB-15W-6500", 165_000, 22, "15w", "6500k")]),
        new(ledBulbs, "mazinoor", "لامپ LED حبابی ۲۰ وات مازی‌نور", "mazinoor-led-bulb-20w-e27",
            "لامپ ۲۰ وات مهتابی برای کارگاه و انبار.", ProductStatus.Published,
            [("base", "e27"), ("lumen", "۱۹۰۰")],
            [V("MZ-LB-20W-6500", 210_000, 0, "20w", "6500k")]),
        new(ledBulbs, "philips", "لامپ LED حبابی ۷ وات فیلیپس", "philips-led-bulb-7w-e27",
            "لامپ کم‌مصرف ۷ وات فیلیپس با نور گرم یا سرد.", ProductStatus.Published,
            [("base", "e27"), ("lumen", "۸۰۶")],
            [V("PH-LB-7W-2700", 145_000, 20, "7w", "2700k"), V("PH-LB-7W-6500", 145_000, 15, "7w", "6500k")]),
        new(ledBulbs, "pars-shahab", "لامپ LED هالوژنی ۵ وات GU10 پارس شهاب", "pars-shahab-led-spot-5w-gu10",
            "لامپ هالوژنی LED برای چراغ‌های سقفی توکار.", ProductStatus.Published,
            [("base", "gu10")],
            [V("PS-SP-5W-3000", 85_000, 70, "5w", "3000k"), V("PS-SP-5W-6500", 85_000, 90, "5w", "6500k")]),
        new(ledBulbs, null, "لامپ LED حبابی ۹ وات E14", "led-bulb-9w-e14",
            "لامپ حبابی کوچک با سرپیچ E14 برای آباژور و چراغ خواب.", ProductStatus.Published,
            [("base", "e14")],
            [V("GN-LB-9W-E14-6500", 72_000, 45, "9w", "6500k")]),
        new(ledBulbs, "zar", "لامپ LED حبابی ۹ وات زر", "zar-led-bulb-9w-e27",
            "پیش‌نویس: هنوز منتشر نشده است.", ProductStatus.Draft,
            [("base", "e27")],
            [V("ZR-LB-9W-3000", 89_000, 10, "9w", "3000k", 99_000), V("ZR-LB-9W-6500", 89_000, 10, "9w", "6500k")]),

        // Candle and flame bulbs
        new(candleBulbs, "pars-shahab", "لامپ LED شمعی ۵ وات پارس شهاب", "pars-shahab-candle-5w-e14",
            "لامپ شمعی مناسب لوسترهای کلاسیک.", ProductStatus.Published,
            [("base", "e14")],
            [V("PS-CN-5W-2700", 79_000, 80, "5w", "2700k"), V("PS-CN-5W-6500", 79_000, 40, "5w", "6500k")]),
        new(candleBulbs, "namanoor", "لامپ LED اشکی ۷ وات نمانور", "namanoor-flame-7w-e14",
            "لامپ اشکی با طراحی شعله برای لوستر و دیوارکوب.", ProductStatus.Published,
            [("base", "e14")],
            [V("NN-FL-7W-3000", 92_000, 0, "7w", "3000k"), V("NN-FL-7W-6500", 92_000, 25, "7w", "6500k")]),
        new(candleBulbs, "philips", "لامپ LED شمعی ۵ وات فیلیپس", "philips-candle-5w-e14",
            "لامپ شمعی فیلیپس با نور گرم.", ProductStatus.Published,
            [("base", "e14")],
            [V("PH-CN-5W-2700", 118_000, 30, "5w", "2700k")]),
        new(candleBulbs, "mazinoor", "لامپ فیلامنتی شمعی ۷ وات مازی‌نور", "mazinoor-filament-candle-7w-e14",
            "این مدل دیگر تولید نمی‌شود.", ProductStatus.Archived,
            [("base", "e14")],
            [V("MZ-FC-7W-2700", 135_000, 0, "7w", "2700k")]),

        // Chandeliers: one variant, specs only
        new(chandeliers, "mazinoor", "لوستر کریستالی ۵ شاخه طلایی", "crystal-chandelier-5-arm-gold",
            "لوستر کریستالی با بدنه طلایی برای پذیرایی و ناهارخوری.", ProductStatus.Published,
            [("base", "e14"), ("arms", "۵"), ("finish", "طلایی")],
            [V("MZ-CH-CR5-GLD", 6_800_000, 4, compareAt: 7_500_000)]),
        new(chandeliers, "namanoor", "لوستر مدرن ۸ شاخه مشکی", "modern-chandelier-8-arm-black",
            "لوستر فلزی مدرن با طراحی مینیمال.", ProductStatus.Published,
            [("base", "e27"), ("arms", "۸"), ("finish", "مشکی")],
            [V("NN-CH-MD8-BLK", 9_400_000, 3)]),
        new(chandeliers, "namanoor", "لوستر سقفی ۳ شاخه کروم", "ceiling-chandelier-3-arm-chrome",
            "لوستر سقفی کوچک برای اتاق خواب و راهرو.", ProductStatus.Published,
            [("base", "e14"), ("arms", "۳"), ("finish", "کروم")],
            [V("NN-CH-CL3-CHR", 3_900_000, 7)]),
        new(chandeliers, "mazinoor", "لوستر کلاسیک ۱۲ شاخه طلایی", "classic-chandelier-12-arm-gold",
            "لوستر بزرگ کلاسیک برای سالن‌های با سقف بلند.", ProductStatus.Published,
            [("base", "e14"), ("arms", "۱۲"), ("finish", "طلایی")],
            [V("MZ-CH-CL12-GLD", 34_500_000, 2)]),
        new(chandeliers, "mazinoor", "لوستر حبابی ۶ شاخه برنجی", "bubble-chandelier-6-arm-brass",
            "لوستر حبابی شیشه‌ای با بدنه برنجی.", ProductStatus.Published,
            [("base", "e27"), ("arms", "۶"), ("finish", "برنجی")],
            [V("MZ-CH-BB6-BRS", 12_800_000, 5)]),

        // Wall lights
        new(wallLights, "pars-shahab", "چراغ دیواری LED مدرن ۱۲ وات", "modern-led-wall-light-12w",
            "چراغ دیواری با LED داخلی، بدون نیاز به لامپ.", ProductStatus.Published,
            [("finish", "سفید")],
            [V("PS-WL-12W-3000", 1_850_000, 10, "12w", "3000k"), V("PS-WL-12W-6500", 1_850_000, 8, "12w", "6500k")]),
        new(wallLights, "namanoor", "چراغ دیواری فضای باز IP44", "outdoor-wall-light-ip44",
            "چراغ دیواری ضدآب برای حیاط، بالکن و نما.", ProductStatus.Published,
            [("base", "e27"), ("ip", "IP44"), ("finish", "مشکی")],
            [V("NN-WL-OUT-IP44", 2_400_000, 6)]),
        new(wallLights, "mazinoor", "چراغ دیواری کلاسیک تک‌شعله طلایی", "classic-wall-light-single-gold",
            "دیوارکوب کلاسیک هماهنگ با لوسترهای طلایی.", ProductStatus.Published,
            [("base", "e14"), ("finish", "طلایی")],
            [V("MZ-WL-CL1-GLD", 1_250_000, 9)]),
    ];
}
