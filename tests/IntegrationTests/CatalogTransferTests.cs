using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog.Transfer;
using Shop.Domain.Security;
using Shop.Infrastructure.Identity;

namespace Shop.IntegrationTests;

public sealed record AdminCategoryBody(int Id, string Slug);

public sealed record XferAttrValueBody(int AttributeId, int? VariantId, int? OptionId, string? Text);

public sealed record XferProductBody(int Id, string Slug, string Name, string Status, AdminVariantBody[] Variants, XferAttrValueBody[] Attributes);

public sealed record ImportErrorBody(int Row, string Column, string Message);

public sealed record ImportRowBody(int Row, string ProductSlug, string Sku, string Action);

public sealed record ImportSummaryBody(int Rows, int ProductsToCreate, int ProductsToUpdate, int VariantsToCreate, int VariantsToUpdate, int Unchanged);

public sealed record ImportPreviewBody(bool CanApply, ImportSummaryBody Summary, ImportRowBody[] Rows, ImportErrorBody[] Errors, bool ErrorsTruncated);

public sealed record ImportAppliedBody(ImportSummaryBody Summary);

/// <summary>Products as a CSV / Excel table (docs/16): export, dry run, apply in one transaction, per-row Persian errors, malformed files.</summary>
public sealed class CatalogTransferTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private const string Base = "/api/v1/admin/catalog";
    private static readonly string[] Header =
        ["product_slug", "product_name", "category", "brand", "status", "description", "path", "sku", "price", "compare_at_price", "stock", "variant_active", "sort_order", "image_ids"];

    private static byte[] Csv(string[] header, params string[][] rows) =>
        CsvTable.Write(header, rows.Select(r => (IReadOnlyList<string>)r));

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, byte[] file, string fileName)
    {
        var csrf = await CsrfTokenAsync(client);
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(part, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add(csrf.HeaderName, csrf.Token);
        return await client.SendAsync(request);
    }

    private async Task<ImportPreviewBody> PreviewAsync(HttpClient admin, byte[] file, string name = "products.csv")
    {
        var response = await UploadAsync(admin, $"{Base}/import/preview", file, name);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<Envelope<ImportPreviewBody>>(ApiJson))!.Result!;
    }

    private Task<HttpResponseMessage> ApplyRawAsync(HttpClient admin, byte[] file, string name = "products.csv") =>
        UploadAsync(admin, $"{Base}/import/apply", file, name);

    private async Task<ImportAppliedBody> ApplyAsync(HttpClient admin, byte[] file, string name = "products.csv")
    {
        var response = await ApplyRawAsync(admin, file, name);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<Envelope<ImportAppliedBody>>(ApiJson))!.Result!;
    }

    private async Task<(string CategoryPath, string CategorySlug, string BrandName)> ReferencesAsync(HttpClient admin)
    {
        var slug = Unique("imp-cat");
        var category = await CreateCategoryAsync(admin, slug);
        var brandSlug = Unique("imp-brand");
        await CreateBrandAsync(admin, brandSlug);
        _ = category;
        return ($"/category/{slug}", slug, "برند " + brandSlug);
    }

    private static string[] Row(string slug, string name, string category, string sku, string price, string stock = "5", string status = "published", string brand = "", string compare = "", string path = "") =>
        [slug, name, category, brand, status, "توضیح", path, sku, price, compare, stock, "1", "0", ""];

    private static readonly string[] HeaderWithSeo =
        ["product_slug", "product_name", "category", "brand", "status", "description", "seo_title", "seo_description", "seo_noindex", "path", "sku", "price", "compare_at_price", "stock", "variant_active", "sort_order", "image_ids"];

    private static string[] RowWithSeo(string slug, string name, string category, string sku, string price, string seoTitle = "", string seoDescription = "", string seoNoIndex = "", string status = "published", string stock = "5") =>
        [slug, name, category, "", status, "توضیح", seoTitle, seoDescription, seoNoIndex, "", sku, price, "", stock, "1", "0", ""];

    private async Task<XferProductBody> ProductBySlugAsync(HttpClient admin, string slug)
    {
        var list = await GetOkAsync<PagedBody<AdminListItemBody>>(admin, $"/api/v1/admin/products?q={slug}");
        var item = list.Items.Single(i => i.Slug == slug);
        return await GetOkAsync<XferProductBody>(admin, $"/api/v1/admin/products/{item.Id}");
    }

    private sealed record PagedBody<T>(T[] Items, int Total);

    private sealed record AdminListItemBody(int Id, string Slug);

    private Task<bool> ProductExistsAsync(string slug) => Fixture.WithDbAsync(db => db.Products.AnyAsync(p => p.Slug == slug));

    /// <summary>A user holding exactly these permissions (direct claims, no role), logged in. Mirrors RoutesTests.LoginWithAsync.</summary>
    private async Task<HttpClient> LoginWithAsync(params string[] permissions)
    {
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, CreatedAtUtc = DateTime.UtcNow };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            foreach (var permission in permissions)
            {
                Assert.True((await users.AddClaimAsync(user, new Claim("permission", permission))).Succeeded);
            }
        }

        var client = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, "/api/v1/auth/login", new { username = email, password })).StatusCode);
        return client;
    }

    private async Task<AdminSeoBody> SeoOfAsync(HttpClient admin, int productId) =>
        await GetOkAsync<AdminSeoBody>(admin, $"/api/v1/admin/seo/product/{productId}");

    // ---- access ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task OnlyCatalogManage_MayExportAndImport()
    {
        var anon = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"{Base}/export")).StatusCode);

        var admin = await LoginAdminAsync();
        // A role without Catalog.Import/Export.
        var stepUp = Fixture.AdminPassword;
        var role = await SendOkAsync<StaffRoleBody>(admin, HttpMethod.Post, "/api/v1/admin/staff/roles",
            new { name = "NoCatalog" + Guid.NewGuid().ToString("N")[..6], title = "بدون محصول", permissions = new[] { Permissions.OrdersView }, currentPassword = stepUp });
        var username = "nocat-" + Guid.NewGuid().ToString("N")[..8];
        var password = TestData.NewPassword() + "-Zz9";
        await SendOkAsync<StaffUserBody>(admin, HttpMethod.Post, "/api/v1/admin/staff",
            new { username, displayName = "x", email = (string?)null, password, roles = new[] { role.Name }, currentPassword = stepUp });
        var limited = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(limited, "/api/v1/auth/login", new { username, password })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync($"{Base}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(limited, $"{Base}/import/preview", Csv(Header), "a.csv")).StatusCode);

        // No CSRF token: refused before the file is read.
        using var content = new MultipartFormDataContent { { new ByteArrayContent(Csv(Header)), "file", "a.csv" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"{Base}/import/preview", content)).StatusCode);
    }

    // ---- export -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Export_WritesEveryVariantWithItsPricesStockAttributesCategoryAndPath_InCsvAndExcel()
    {
        var admin = await LoginAdminAsync();
        var (_, categorySlug, brandName) = await ReferencesAsync(admin);
        var category = await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories");
        var attributes = await AttributesAsync(admin);
        var slug = Unique("exp");
        var product = await CreateProductAsync(admin, category.Single(c => c.Slug == categorySlug).Id, slug, variants: [(120_000, 7), (150_000, 3)]);
        await SetAttributesAsync(admin, product.Id,
            Opt(attributes, "base", "e27"),
            Opt(attributes, "wattage", attributes["wattage"].Options[0].Slug, product.Variants[0].Id),
            Opt(attributes, "wattage", attributes["wattage"].Options[1].Slug, product.Variants[1].Id));

        var csv = await admin.GetAsync($"{Base}/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.Contains("products-", csv.Content.Headers.ContentDisposition!.FileName);
        var table = CsvTable.Read(await csv.Content.ReadAsByteArrayAsync());
        // cost_price sits right after compare_at_price; seo_title/seo_description/seo_noindex sit right after description.
        var exportedFixedHeader = Header.Take(6).Concat(["seo_title", "seo_description", "seo_noindex"]).Concat(Header.Skip(6).Take(4)).Append("cost_price").Concat(Header.Skip(10)).ToArray();
        Assert.Equal(exportedFixedHeader, table.Header.Take(exportedFixedHeader.Length));
        Assert.Contains("spec:base", table.Header);
        Assert.Contains("axis:wattage", table.Header);

        var mine = table.Rows.Where(r => r.Cells[0] == slug).ToList();
        Assert.Equal(2, mine.Count);
        var col = table.Header.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i);
        Assert.Equal(["120000", "150000"], mine.Select(r => r.Cells[col["price"]]).Order());
        Assert.Equal(["3", "7"], mine.Select(r => r.Cells[col["stock"]]).Order());
        Assert.Equal($"/category/{categorySlug}", mine[0].Cells[col["category"]]);
        Assert.Equal($"/product/{slug}", mine[0].Cells[col["path"]]);
        Assert.Equal("E27", mine[0].Cells[col["spec:base"]]);
        Assert.All(mine, r => Assert.False(string.IsNullOrEmpty(r.Cells[col["axis:wattage"]])));
        _ = brandName;

        // The Excel file carries the same table.
        var xlsx = await admin.GetAsync($"{Base}/export?format=xlsx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);
        var excel = TableCodec.Read(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()), "x.xlsx");
        Assert.Equal(table.Header, excel.Header);
        Assert.Equal(table.Rows.Count, excel.Rows.Count);
    }

    [Fact]
    public async Task Export_ThenPreviewingTheSameFile_ChangesNothing()
    {
        var admin = await LoginAdminAsync();
        var (_, categorySlug, _) = await ReferencesAsync(admin);
        var categories = await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories");
        await CreateProductAsync(admin, categories.Single(c => c.Slug == categorySlug).Id, Unique("rt"), variants: [(10_000, 1), (20_000, 2)]);

        foreach (var (format, name) in new[] { ("csv", "p.csv"), ("xlsx", "p.xlsx") })
        {
            var file = await (await admin.GetAsync($"{Base}/export?format={format}")).Content.ReadAsByteArrayAsync();
            var preview = await PreviewAsync(admin, file, name);
            Assert.True(preview.CanApply, string.Join('\n', preview.Errors.Select(e => $"{e.Row} {e.Column}: {e.Message}")));
            Assert.Equal(0, preview.Summary.ProductsToCreate);
            Assert.Equal(0, preview.Summary.ProductsToUpdate);
            Assert.Equal(0, preview.Summary.VariantsToCreate + preview.Summary.VariantsToUpdate);
            Assert.Equal(preview.Summary.Rows, preview.Summary.Unchanged);
        }
    }

    [Fact]
    public async Task Export_ProtectsAgainstFormulaInjection_AndTheImportRestoresTheValue()
    {
        var admin = await LoginAdminAsync();
        var (_, categorySlug, _) = await ReferencesAsync(admin);
        var categories = await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories");
        var slug = Unique("inj");
        await CreateProductAsync(admin, categories.Single(c => c.Slug == categorySlug).Id, slug, name: "=HYPERLINK(\"http://evil\")");
        var csv = await (await admin.GetAsync($"{Base}/export?format=csv")).Content.ReadAsByteArrayAsync();
        var table = CsvTable.Read(csv);
        var name = table.Rows.Single(r => r.Cells[0] == slug).Cells[1];
        Assert.StartsWith("'=", name); // a spreadsheet shows text, never runs it
        // Only this product's rows: the apostrophe is taken off again, so the same name means nothing to update.
        var header = table.Header.ToArray();
        var preview = await PreviewAsync(admin, CsvTable.Write(header, table.Rows.Where(r => r.Cells[0] == slug).Select(r => (IReadOnlyList<string>)r.Cells)));
        Assert.True(preview.CanApply, string.Join('\n', preview.Errors.Select(e => $"{e.Row} {e.Column}: {e.Message}")));
        Assert.Equal((0, 0, 1), (preview.Summary.ProductsToUpdate, preview.Summary.VariantsToUpdate, preview.Summary.Unchanged));
    }

    // ---- dry run and apply -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Preview_WritesNothing_AndApplyCreatesProductsVariantsAttributesAndPaths_InOneStep()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, brandName) = await ReferencesAsync(admin);
        var attributes = await AttributesAsync(admin);
        var wattage = attributes["wattage"].Options[0];
        var slugA = Unique("new-a");
        var slugB = Unique("new-b");
        var header = Header.Concat(["spec:base", "axis:wattage", "spec:lumen"]).ToArray();
        var file = Csv(header,
            [.. Row(slugA, "لامپ آزمایشی الف", categoryPath, slugA + "-1", "۱۲۰٬۰۰۰", stock: "۸", brand: brandName, compare: "150000"), "e27", wattage.Value, "۱۰۵۰"],
            [.. Row(slugA, "", "", slugA + "-2", "130000", stock: "2"), "", attributes["wattage"].Options[1].Value, ""],
            [.. Row(slugB, "لامپ آزمایشی ب", categoryPath, slugB + "-1", "99000", status: "draft", path: $"/lamps/{slugB}"), "e14", "", ""]);

        var preview = await PreviewAsync(admin, file);
        Assert.True(preview.CanApply, string.Join('\n', preview.Errors.Select(e => $"{e.Row} {e.Column}: {e.Message}")));
        Assert.Equal((3, 2, 0, 3, 0, 0), (preview.Summary.Rows, preview.Summary.ProductsToCreate, preview.Summary.ProductsToUpdate, preview.Summary.VariantsToCreate, preview.Summary.VariantsToUpdate, preview.Summary.Unchanged));
        Assert.Equal(["create-product", "create-variant", "create-product"], preview.Rows.OrderBy(r => r.Row).Select(r => r.Action));
        Assert.False(await ProductExistsAsync(slugA)); // a dry run writes nothing
        Assert.False(await ProductExistsAsync(slugB));

        var applied = await ApplyAsync(admin, file);
        Assert.Equal(2, applied.Summary.ProductsToCreate);

        var a = await ProductBySlugAsync(admin, slugA);
        Assert.Equal("لامپ آزمایشی الف", a.Name);
        Assert.Equal("published", a.Status);
        Assert.Equal([120_000L, 130_000L], a.Variants.Select(v => v.Price).Order());
        Assert.Equal([2, 8], a.Variants.Select(v => v.Stock).Order());
        Assert.Equal(150_000L, a.Variants.Single(v => v.Price == 120_000).CompareAtPrice);
        var b = await ProductBySlugAsync(admin, slugB);
        Assert.Equal("draft", b.Status);

        // attribute values: base is a spec of the product; wattage is per variant; lumen (text) is a spec
        var baseValues = a.Attributes.Where(x => x.AttributeId == attributes["base"].Id).ToList();
        Assert.Single(baseValues);
        Assert.Null(baseValues[0].VariantId);
        Assert.Equal(2, a.Attributes.Count(x => x.AttributeId == attributes["wattage"].Id && x.VariantId is not null));
        Assert.Equal("1050", a.Attributes.Single(x => x.AttributeId == attributes["lumen"].Id).Text?.Replace("۱۰۵۰", "1050"));

        // the custom path is live, the default one for the other product too
        var anon = NewClient();
        Assert.Equal("product", (await ResolveKindAsync(anon, $"/lamps/{slugB}")));
        Assert.Equal("product", (await ResolveKindAsync(anon, $"/product/{slugA}")));

        // applying the same file again changes nothing
        var again = await PreviewAsync(admin, file);
        Assert.True(again.CanApply, string.Join('\n', again.Errors.Select(e => $"{e.Row} {e.Column}: {e.Message}")));
        Assert.Equal((0, 0, 0, 0, 3), (again.Summary.ProductsToCreate, again.Summary.ProductsToUpdate, again.Summary.VariantsToCreate, again.Summary.VariantsToUpdate, again.Summary.Unchanged));
    }

    private sealed record ResolveBody(string Kind);

    private async Task<string?> ResolveKindAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync($"/api/v1/catalog/resolve?path={Uri.EscapeDataString(path)}");
        return (await response.Content.ReadFromJsonAsync<Envelope<ResolveBody>>(ApiJson))?.Result?.Kind;
    }

    [Fact]
    public async Task Apply_UpdatesExistingVariants_AddsNewOnes_WritesStockAdjustments_AndLeavesOthersAlone()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, categorySlug, _) = await ReferencesAsync(admin);
        var categories = await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories");
        var slug = Unique("upd");
        var product = await CreateProductAsync(admin, categories.Single(c => c.Slug == categorySlug).Id, slug, variants: [(100_000, 10), (200_000, 20)]);
        var skuA = product.Variants[0].Sku;
        var skuB = product.Variants[1].Sku;
        var other = await CreateProductAsync(admin, categories.Single(c => c.Slug == categorySlug).Id, Unique("untouched"));

        // Only the first variant is in the file (price, stock, deactivated), plus a new variant; the product gets a new name and status.
        var first = Row(slug, "نام تازه", categoryPath, skuA, "110000", stock: "4", status: "archived");
        first[11] = "0"; // variant_active
        first[12] = "3"; // sort_order
        var file = Csv(Header, first, Row(slug, "", "", slug + "-new", "50000", stock: "9"));
        var preview = await PreviewAsync(admin, file);
        Assert.True(preview.CanApply, string.Join('\n', preview.Errors.Select(e => $"{e.Row} {e.Column}: {e.Message}")));
        Assert.Equal((0, 1, 1, 1), (preview.Summary.ProductsToCreate, preview.Summary.ProductsToUpdate, preview.Summary.VariantsToUpdate, preview.Summary.VariantsToCreate));
        await ApplyAsync(admin, file);

        var after = await ProductBySlugAsync(admin, slug);
        Assert.Equal("نام تازه", after.Name);
        Assert.Equal("archived", after.Status);
        var a = after.Variants.Single(v => v.Sku == skuA);
        Assert.Equal((110_000L, 4, false, 3), (a.Price, a.Stock, a.IsActive, a.SortOrder));
        var b = after.Variants.Single(v => v.Sku == skuB);
        Assert.Equal((200_000L, 20), (b.Price, b.Stock)); // not in the file: untouched, not deleted
        Assert.Equal(9, after.Variants.Single(v => v.Sku == slug + "-new").Stock);

        // the stock change is in the adjustment history, marked as an import
        var adjustments = await Fixture.WithDbAsync(db => db.StockAdjustments.Where(x => x.VariantId == a.Id).Select(x => new { x.Delta, x.StockAfter, x.Source }).ToListAsync());
        Assert.Contains(adjustments, x => x is { Delta: -6, StockAfter: 4, Source: "import" });
        Assert.Equal("published", (await ProductBySlugAsync(admin, other.Slug)).Status);
    }

    // ---- errors -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task EveryProblemIsReportedPerRowInPersian_AndOneBadRowMeansNothingIsApplied()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, categorySlug, _) = await ReferencesAsync(admin);
        var categories = await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories");
        var existing = await CreateProductAsync(admin, categories.Single(c => c.Slug == categorySlug).Id, Unique("ex"));
        var good = Unique("good");
        var header = Header.Concat(["spec:base"]).ToArray();
        string[] Line(string[] row, string spec = "") => [.. row, spec];

        var file = Csv(header,
            Line(Row(good, "درست", categoryPath, good + "-1", "1000")),                                          // row 2: fine
            Line(Row("Bad Slug", "x", categoryPath, "ok-sku-1", "1000")),                                         // row 3: slug
            Line(Row(Unique("e"), "x", categoryPath, "bad sku!", "1000")),                                        // row 4: sku
            Line(Row(Unique("e"), "x", categoryPath, "ok-sku-2", "abc")),                                         // row 5: price
            Line(Row(Unique("e"), "x", categoryPath, "ok-sku-3", "1000", compare: "500")),                        // row 6: compare below price
            Line(Row(Unique("e"), "x", categoryPath, "ok-sku-4", "1000", stock: "-3")),                           // row 7: stock
            Line(Row(Unique("e"), "x", "/category/nope-" + good, "ok-sku-5", "1000")),                            // row 8: unknown category
            Line(Row(Unique("e"), "x", categoryPath, "ok-sku-6", "1000", brand: "برند ناموجود")),                 // row 9: unknown brand
            Line(Row(Unique("e"), "x", categoryPath, existing.Variants[0].Sku, "1000")),                          // row 10: SKU of another product
            Line(Row(Unique("e"), "x", categoryPath, good + "-1", "1000")),                                       // row 11: SKU twice in the file
            Line(Row(Unique("e"), "x", categoryPath, "ok-sku-7", "1000", status: "weird")),                       // row 12: status
            Line(Row(Unique("e"), "x", categoryPath, "ok-sku-8", "1000"), "no-such-option"),                      // row 13: unknown option
            Line(Row(Unique("e"), "", categoryPath, "ok-sku-9", "1000")),                                         // row 14: name missing
            Line(Row(Unique("e"), "x", "", "ok-sku-10", "1000")),                                                 // row 15: category missing
            Line([.. Row(Unique("e"), "x", categoryPath, "ok-sku-11", "1000")[..13], "http://example.com/a.jpg"]),     // row 16: image URL
            Line([.. Row(existing.Slug, "", "", "ok-sku-12", "1000", path: "/somewhere/else")]));                     // row 17: path of an existing product

        var preview = await PreviewAsync(admin, file);
        Assert.False(preview.CanApply);
        var errors = preview.Errors.Select(e => (e.Row, e.Column)).ToHashSet();
        foreach (var expected in new[]
        {
            (3, "product_slug"), (4, "sku"), (5, "price"), (6, "compare_at_price"), (7, "stock"), (8, "category"), (9, "brand"), (10, "sku"),
            (11, "sku"), (12, "status"), (13, "spec:base"), (14, "product_name"), (15, "category"), (16, "image_ids"), (17, "path"),
        })
        {
            Assert.True(errors.Contains(expected), $"expected an error at row {expected.Item1}, column {expected.Item2}; got: {string.Join(", ", errors)}");
        }

        Assert.DoesNotContain(preview.Errors, e => e.Row == 2); // the good row has no error
        Assert.All(preview.Errors, e => Assert.Matches("[؀-ۿ]", e.Message)); // Persian text, never a key or an exception message

        // Apply refuses, and not even the good row was written.
        var apply = await ApplyRawAsync(admin, file);
        await AssertProblemAsync(apply, 400);
        Assert.False(await ProductExistsAsync(good));
    }

    [Fact]
    public async Task ProductLevelValues_MustAgreeAcrossTheRowsOfOneProduct()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, _) = await ReferencesAsync(admin);
        var slug = Unique("agree");
        var file = Csv(Header,
            Row(slug, "نام یک", categoryPath, slug + "-1", "1000"),
            Row(slug, "نام دو", categoryPath, slug + "-2", "1000"));
        var preview = await PreviewAsync(admin, file);
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Errors, e => e.Row == 3 && e.Column == "product_name");
    }

    [Fact]
    public async Task AnAttributeCannotBeBothAProductSpecAndAVariantAxis()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, _) = await ReferencesAsync(admin);
        var attributes = await AttributesAsync(admin);
        var slug = Unique("both");
        var header = Header.Concat(["spec:wattage", "axis:wattage"]).ToArray();
        var value = attributes["wattage"].Options[0].Value;
        var preview = await PreviewAsync(admin, Csv(header, [.. Row(slug, "x", categoryPath, slug + "-1", "1000"), value, value]));
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Errors, e => e.Column.StartsWith("spec:wattage") && e.Message.Contains("wattage"));
    }

    [Fact]
    public async Task HeaderProblems_UnknownMissingAndDuplicateColumns_AreReportedAtRowZero()
    {
        var admin = await LoginAdminAsync();
        var unknown = await PreviewAsync(admin, Csv(["product_slug", "product_name", "sku", "price", "colour"], ["a", "b", "c", "100", "red"]));
        Assert.Contains(unknown.Errors, e => e is { Row: 0, Column: "colour" });
        var missing = await PreviewAsync(admin, Csv(["product_slug", "sku"], ["a", "b"]));
        Assert.Contains(missing.Errors, e => e is { Row: 0, Column: "product_name" });
        Assert.Contains(missing.Errors, e => e is { Row: 0, Column: "price" });
        var duplicate = await PreviewAsync(admin, Csv(["product_slug", "product_name", "sku", "price", "sku"], ["a", "b", "c", "100", "d"]));
        Assert.Contains(duplicate.Errors, e => e is { Row: 0, Column: "sku" });
        Assert.False(missing.CanApply || unknown.CanApply || duplicate.CanApply);
    }

    [Fact]
    public async Task ImageIds_OnlyReorderAProductsOwnImages_NeverFetchAnything()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, categorySlug, _) = await ReferencesAsync(admin);
        var categories = await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories");
        var product = await CreateProductAsync(admin, categories.Single(c => c.Slug == categorySlug).Id, Unique("img"));
        var row = Row(product.Slug, "", "", product.Variants[0].Sku, "100000");

        // A new product cannot take images; an unknown id is refused; a URL is refused with the explanation.
        var fresh = Unique("img-new");
        var forNew = await PreviewAsync(admin, Csv(Header, [.. Row(fresh, "x", categoryPath, fresh + "-1", "1000")[..13], "5"]));
        Assert.Contains(forNew.Errors, e => e.Column == "image_ids");
        var foreign = await PreviewAsync(admin, Csv(Header, [.. row[..13], "999999"]));
        Assert.Contains(foreign.Errors, e => e.Column == "image_ids");
        var url = await PreviewAsync(admin, Csv(Header, [.. row[..13], "https://example.com/x.jpg"]));
        Assert.Contains(url.Errors, e => e.Column == "image_ids" && e.Message.Contains("دریافت نمی‌شود"));
    }

    // ---- malformed files ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MalformedFiles_AreRefusedWithAPersianFileMessage_NeverAServerError()
    {
        var admin = await LoginAdminAsync();
        var cases = new (string Name, byte[] Bytes)[]
        {
            ("a.csv", []),
            ("a.csv", [0xFF, 0xFE, 0x00, 0x61]),
            ("a.csv", Encoding.UTF8.GetBytes("a,b\n\"unterminated")),
            ("a.xlsx", Encoding.UTF8.GetBytes("this is not a zip")),
            ("a.xlsx", "PK\u0003\u0004garbage-after-the-signature"u8.ToArray()),
            ("a.xls", [1, 2, 3]),
            ("a.csv", XlsxTable.Write(["a"], [["1"]], new HashSet<int>(), "s")),
            ("a.csv", Encoding.UTF8.GetBytes(string.Join(',', Enumerable.Range(0, 200)) + "\n1")),
        };
        foreach (var (name, bytes) in cases)
        {
            var preview = await UploadAsync(admin, $"{Base}/import/preview", bytes, name);
            var problem = await AssertProblemAsync(preview, 400);
            Assert.True(problem.Errors?.ContainsKey("file") == true, $"{name}: no file error");
            Assert.Matches("[؀-ۿ]", problem.Errors!["file"][0]);
            var apply = await UploadAsync(admin, $"{Base}/import/apply", bytes, name);
            await AssertProblemAsync(apply, 400);
        }
    }

    [Fact]
    public async Task ATooLargeUpload_IsRefused()
    {
        var admin = await LoginAdminAsync();
        var big = new byte[TableLimits.MaxFileBytes + 200_000];
        Array.Fill(big, (byte)'a');
        HttpResponseMessage? response = null;
        try
        {
            response = await UploadAsync(admin, $"{Base}/import/preview", big, "big.csv");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            return; // the server closed the connection on the oversized body: refused
        }

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, $"got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ExcelUpload_AppliesLikeCsv_AndAuditsTheTransfer()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, _) = await ReferencesAsync(admin);
        var slug = Unique("xl");
        var xlsx = XlsxTable.Write(Header, [Row(slug, "از اکسل", categoryPath, slug + "-1", "250000", stock: "12")], new HashSet<int> { 8, 9, 10, 12 }, "products");
        await ApplyAsync(admin, xlsx, "catalog.xlsx");
        var product = await ProductBySlugAsync(admin, slug);
        Assert.Equal(("از اکسل", 250_000L, 12), (product.Name, product.Variants[0].Price, product.Variants[0].Stock));

        var log = await Fixture.WithDbAsync(db => db.CatalogTransferLogs.Where(l => l.FileName == "catalog.xlsx").SingleAsync());
        Assert.Equal(("import", 1, 1, 1), (log.Kind, log.Rows, log.ProductsCreated, log.VariantsCreated));
        await admin.GetAsync($"{Base}/export?format=csv");
        Assert.True(await Fixture.WithDbAsync(db => db.CatalogTransferLogs.AnyAsync(l => l.Kind == "export")));
    }

    // cost_price has its own column (right after compare_at_price), used only in these tests so the shared Header/Row
    // helpers above (and every test built on them) stay untouched.
    private static readonly string[] HeaderWithCost =
        ["product_slug", "product_name", "category", "brand", "status", "description", "path", "sku", "price", "compare_at_price", "cost_price", "stock", "variant_active", "sort_order", "image_ids"];

    private static string[] RowWithCost(string slug, string name, string category, string sku, string price, string costPrice = "", string stock = "5", string status = "published") =>
        [slug, name, category, "", status, "توضیح", "", sku, price, "", costPrice, stock, "1", "0", ""];

    [Fact]
    public async Task CostPrice_EmptyCellLeavesItUnchanged_AGivenValueUpdatesIt_AndAnInvalidOneIsARowError()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, categorySlug, _) = await ReferencesAsync(admin);
        var categories = await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories");
        var slug = Unique("cost");
        var product = await CreateProductAsync(admin, categories.Single(c => c.Slug == categorySlug).Id, slug, variants: [(100_000, 5)]);
        var sku = product.Variants[0].Sku;

        // Creates with a cost price.
        var createFile = CsvTable.Write(HeaderWithCost, [RowWithCost(slug, "نام", categoryPath, sku, "100000", "60000")]);
        await ApplyAsync(admin, createFile);
        Assert.Equal(60_000L, (await ProductBySlugAsync(admin, slug)).Variants.Single().CostPrice);

        // An empty cost_price cell: the preview reports no change, and applying it really leaves the value alone.
        var emptyFile = CsvTable.Write(HeaderWithCost, [RowWithCost(slug, "نام", categoryPath, sku, "100000")]);
        var unchangedPreview = await PreviewAsync(admin, emptyFile);
        Assert.True(unchangedPreview.CanApply);
        Assert.Equal(0, unchangedPreview.Summary.VariantsToUpdate);
        await ApplyAsync(admin, emptyFile);
        Assert.Equal(60_000L, (await ProductBySlugAsync(admin, slug)).Variants.Single().CostPrice);

        // A given value updates it.
        var updateFile = CsvTable.Write(HeaderWithCost, [RowWithCost(slug, "نام", categoryPath, sku, "100000", "70000")]);
        var updatedPreview = await PreviewAsync(admin, updateFile);
        Assert.Equal(1, updatedPreview.Summary.VariantsToUpdate);
        await ApplyAsync(admin, updateFile);
        Assert.Equal(70_000L, (await ProductBySlugAsync(admin, slug)).Variants.Single().CostPrice);

        // An invalid (negative) cost price is a row error; nothing is written.
        var badFile = CsvTable.Write(HeaderWithCost, [RowWithCost(slug, "نام", categoryPath, sku, "100000", "-1")]);
        var badPreview = await PreviewAsync(admin, badFile);
        Assert.False(badPreview.CanApply);
        Assert.Contains(badPreview.Errors, e => e.Column == "cost_price");
        Assert.Equal(70_000L, (await ProductBySlugAsync(admin, slug)).Variants.Single().CostPrice); // unchanged
    }

    // ---- G2: SEO columns -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Seo_EmptyIsNoChange_DashClears_AndAGivenValueSets()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, _) = await ReferencesAsync(admin);
        var slug = Unique("seo-imp");
        var sku = slug + "-0";

        // Creates with SEO title/description/noindex set.
        await ApplyAsync(admin, CsvTable.Write(HeaderWithSeo, [RowWithSeo(slug, "نام", categoryPath, sku, "100000", "عنوان سئو", "توضیح سئو", "1")]));
        var product = await ProductBySlugAsync(admin, slug);
        var seo = await SeoOfAsync(admin, product.Id);
        Assert.Equal("عنوان سئو", seo.Seo.MetaTitle);
        Assert.Equal("توضیح سئو", seo.Seo.MetaDescription);
        Assert.True(seo.Seo.NoIndex);

        // Empty cells: no change.
        await ApplyAsync(admin, CsvTable.Write(HeaderWithSeo, [RowWithSeo(slug, "نام", categoryPath, sku, "100000")]));
        seo = await SeoOfAsync(admin, product.Id);
        Assert.Equal("عنوان سئو", seo.Seo.MetaTitle);
        Assert.Equal("توضیح سئو", seo.Seo.MetaDescription);
        Assert.True(seo.Seo.NoIndex);

        // A single "-" clears title/description and turns noindex back off.
        await ApplyAsync(admin, CsvTable.Write(HeaderWithSeo, [RowWithSeo(slug, "نام", categoryPath, sku, "100000", "-", "-", "-")]));
        seo = await SeoOfAsync(admin, product.Id);
        Assert.Null(seo.Seo.MetaTitle);
        Assert.Null(seo.Seo.MetaDescription);
        Assert.False(seo.Seo.NoIndex);
    }

    [Fact]
    public async Task Seo_TooLongTitleOrDescription_IsARowError()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, _) = await ReferencesAsync(admin);
        var slug = Unique("seo-long");
        var longTitle = new string('ا', 121);

        var preview = await PreviewAsync(admin, CsvTable.Write(HeaderWithSeo, [RowWithSeo(slug, "نام", categoryPath, slug + "-0", "100000", longTitle)]));
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Errors, e => e.Column == "seo_title");
    }

    // ---- G2: template download --------------------------------------------------------------------------------------------

    [Fact]
    public async Task Template_HasHeadersHintsAndOneExampleRow_InCsvAndXlsx()
    {
        var admin = await LoginAdminAsync();

        var csv = await admin.GetAsync($"{Base}/template?format=csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Contains("products-template", csv.Content.Headers.ContentDisposition!.FileName);
        var table = CsvTable.Read(await csv.Content.ReadAsByteArrayAsync());
        Assert.Contains("product_slug", table.Header);
        Assert.Contains("seo_title", table.Header);
        Assert.Equal(2, table.Rows.Count); // the hint row, then one example row
        var col = table.Header.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i);
        Assert.False(string.IsNullOrEmpty(table.Rows[0].Cells[col["product_slug"]])); // a Persian hint, not empty
        Assert.False(Shop.Domain.Catalog.Slug.IsValid(table.Rows[0].Cells[col["product_slug"]])); // it is a hint, not a usable slug
        Assert.True(Shop.Domain.Catalog.Slug.IsValid(table.Rows[1].Cells[col["product_slug"]])); // the example row is a usable row

        var xlsx = await admin.GetAsync($"{Base}/template?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        var excel = TableCodec.Read(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()), "t.xlsx");
        Assert.Equal(table.Header, excel.Header);
        Assert.Equal(2, excel.Rows.Count);
    }

    // ---- G2: round trip ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task RoundTrip_ExportThenImportUnchanged_Is0Changes(string format)
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, categorySlug, _) = await ReferencesAsync(admin);
        var category = (await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories")).Single(c => c.Slug == categorySlug);
        var slug = Unique("rt");
        await CreateProductAsync(admin, category.Id, slug, variants: [(120_000, 7)]);

        var exported = await admin.GetAsync($"{Base}/export?format={format}");
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        var bytes = await exported.Content.ReadAsByteArrayAsync();

        var preview = await PreviewAsync(admin, bytes, $"roundtrip.{format}");
        Assert.True(preview.CanApply, string.Join(", ", preview.Errors.Select(e => e.Message)));
        Assert.Equal(0, preview.Summary.ProductsToCreate);
        Assert.Equal(0, preview.Summary.ProductsToUpdate);
        Assert.Equal(0, preview.Summary.VariantsToCreate);
        Assert.Equal(0, preview.Summary.VariantsToUpdate);

        var applied = await ApplyAsync(admin, bytes, $"roundtrip.{format}");
        Assert.Equal(0, applied.Summary.ProductsToCreate);
        Assert.Equal(0, applied.Summary.ProductsToUpdate);
        Assert.Equal(0, applied.Summary.VariantsToCreate);
        Assert.Equal(0, applied.Summary.VariantsToUpdate);
    }

    // ---- G2: permission gating ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task ImportOnly_CannotCreate_CannotUpdate_CannotPublish()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, _) = await ReferencesAsync(admin);
        var slug = Unique("perm-i");
        await CreateProductAsync(admin, (await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories")).First(c => $"/category/{c.Slug}" == categoryPath).Id, slug, variants: [(100_000, 5)]);
        var product = await ProductBySlugAsync(admin, slug);

        var importer = await LoginWithAsync(Permissions.CatalogImport);

        // Cannot create.
        var createRow = Row(Unique("perm-i-new"), "نام", categoryPath, Unique("perm-i-new") + "-0", "100000");
        var createPreview = await PreviewAsync(importer, Csv(Header, createRow));
        Assert.False(createPreview.CanApply);
        Assert.Contains(createPreview.Errors, e => e.Message.Contains("Catalog.Create"));

        // Cannot update an existing field.
        var updateRow = Row(slug, "نام تغییریافته", categoryPath, product.Variants[0].Sku, "100000");
        var updatePreview = await PreviewAsync(importer, Csv(Header, updateRow));
        Assert.False(updatePreview.CanApply);
        Assert.Contains(updatePreview.Errors, e => e.Message.Contains("Catalog.Update"));

        // Cannot change status (to published or anything else).
        var statusRow = Row(slug, product.Name, categoryPath, product.Variants[0].Sku, "100000", status: "archived");
        var statusPreview = await PreviewAsync(importer, Csv(Header, statusRow));
        Assert.False(statusPreview.CanApply);
        Assert.Contains(statusPreview.Errors, e => e.Message.Contains("Catalog.Update"));

        // A true no-op (same values) needs no permission beyond Import and reports 0 changes.
        var sameRow = Row(slug, product.Name, categoryPath, product.Variants[0].Sku, "100000");
        var samePreview = await PreviewAsync(importer, Csv(Header, sameRow));
        Assert.True(samePreview.CanApply, string.Join(", ", samePreview.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task ImportAndUpdate_CanUpdateAndPublish_ButNotCreate()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, _) = await ReferencesAsync(admin);
        var categoryId = (await GetOkAsync<AdminCategoryBody[]>(admin, "/api/v1/admin/categories")).First(c => $"/category/{c.Slug}" == categoryPath).Id;
        var slug = Unique("perm-u");
        await CreateProductAsync(admin, categoryId, slug, variants: [(100_000, 5)], status: "draft");
        var product = await ProductBySlugAsync(admin, slug);

        var updater = await LoginWithAsync(Permissions.CatalogImport, Permissions.CatalogUpdate);

        // Can update and publish an existing product.
        var publishRow = Row(slug, "نام", categoryPath, product.Variants[0].Sku, "100000", status: "published");
        var applied = await ApplyAsync(updater, Csv(Header, publishRow));
        Assert.Equal(1, applied.Summary.ProductsToUpdate);
        Assert.Equal("published", (await ProductBySlugAsync(admin, slug)).Status);

        // Cannot create a new product.
        var newSlug = Unique("perm-u-new");
        var createRow = Row(newSlug, "نام", categoryPath, newSlug + "-0", "100000");
        var createPreview = await PreviewAsync(updater, Csv(Header, createRow));
        Assert.False(createPreview.CanApply);
        Assert.Contains(createPreview.Errors, e => e.Message.Contains("Catalog.Create"));
    }

    [Fact]
    public async Task ImportAndCreate_CanCreateDrafts_ButPublishingOnCreateAlsoNeedsUpdate()
    {
        var admin = await LoginAdminAsync();
        var (categoryPath, _, _) = await ReferencesAsync(admin);
        var creator = await LoginWithAsync(Permissions.CatalogImport, Permissions.CatalogCreate);

        // Can create a draft.
        var draftSlug = Unique("perm-c-draft");
        var draftRow = Row(draftSlug, "نام", categoryPath, draftSlug + "-0", "100000", status: "draft");
        var applied = await ApplyAsync(creator, Csv(Header, draftRow));
        Assert.Equal(1, applied.Summary.ProductsToCreate);

        // Creating with status=published also needs Catalog.Update.
        var publishedSlug = Unique("perm-c-pub");
        var publishedRow = Row(publishedSlug, "نام", categoryPath, publishedSlug + "-0", "100000", status: "published");
        var publishedPreview = await PreviewAsync(creator, Csv(Header, publishedRow));
        Assert.False(publishedPreview.CanApply);
        Assert.Contains(publishedPreview.Errors, e => e.Message.Contains("Catalog.Update"));
        Assert.False(await ProductExistsAsync(publishedSlug)); // nothing written
    }
}
