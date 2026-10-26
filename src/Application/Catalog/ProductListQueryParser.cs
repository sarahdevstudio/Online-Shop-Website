using System.Globalization;
using Shop.Application.Common;
using Shop.Domain.Catalog;

namespace Shop.Application.Catalog;

/// <summary>
/// Turns the listing query string into a <see cref="ProductListQuery"/>. List values may be comma-separated
/// or repeated (<c>wattage=9w,12w</c> == <c>wattage=9w&amp;wattage=12w</c>). Keys that aren't reserved are attribute
/// codes; ones that can't be a code (utm_source, ...) are ignored.
/// </summary>
public static class ProductListQueryParser
{
    public static Result<ProductListQuery> Parse(IEnumerable<(string Key, IReadOnlyList<string?> Values)> query)
    {
        var errors = new Dictionary<string, string[]>();
        var attributes = new Dictionary<string, IReadOnlyList<string>>();
        var result = new ProductListQuery();

        foreach (var (rawKey, values) in query)
        {
            var key = rawKey.ToLowerInvariant();
            var single = values.LastOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
            switch (key)
            {
                case "category":
                    result = result with { Category = single?.ToLowerInvariant() };
                    break;
                case "q":
                    if (single is { Length: > ProductListQuery.QMaxLength })
                    {
                        errors["q"] = [$"حداکثر {ProductListQuery.QMaxLength} نویسه."];
                    }

                    result = result with { Q = single };
                    break;
                case "brand":
                    result = result with { Brands = SplitList(values) };
                    break;
                case "minprice":
                    result = result with { MinPrice = ParsePrice("minPrice", single, errors) };
                    break;
                case "maxprice":
                    result = result with { MaxPrice = ParsePrice("maxPrice", single, errors) };
                    break;
                case "instock":
                    if (single is null or "false" or "0")
                    {
                        result = result with { InStock = false };
                    }
                    else if (single is "true" or "1")
                    {
                        result = result with { InStock = true };
                    }
                    else
                    {
                        errors["inStock"] = ["مقدار باید «بله» یا «خیر» باشد."];
                    }

                    break;
                case "sort":
                    ProductSort? sort = single switch
                    {
                        null or "newest" => ProductSort.Newest,
                        "price-asc" => ProductSort.PriceAsc,
                        "price-desc" => ProductSort.PriceDesc,
                        _ => null,
                    };
                    if (sort is null)
                    {
                        errors["sort"] = ["ترتیب نمایش نامعتبر است."];
                    }
                    else
                    {
                        result = result with { Sort = sort.Value };
                    }

                    break;
                case "page":
                    result = result with { Page = ParseInt("page", single, 1, int.MaxValue, 1, errors) };
                    break;
                case "pagesize":
                    result = result with
                    {
                        PageSize = ParseInt("pageSize", single, 1, ProductListQuery.MaxPageSize, ProductListQuery.DefaultPageSize, errors),
                    };
                    break;
                default:
                    if (AttributeDefinition.IsValidCode(key))
                    {
                        attributes[key] = SplitList(values);
                    }

                    break;
            }
        }

        if (result is { MinPrice: { } min, MaxPrice: { } max } && min > max)
        {
            errors["minPrice"] = ["حداقل قیمت نباید از حداکثر قیمت بیشتر باشد."];
        }

        return errors.Count > 0
            ? Error.Validation(errors)
            : result with { Attributes = attributes };
    }

    private static List<string> SplitList(IReadOnlyList<string?> values) =>
        values.SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(v => v.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static long? ParsePrice(string name, string? value, Dictionary<string, string[]> errors)
    {
        if (value is null)
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var price))
        {
            return price;
        }

        errors[name] = ["Must be a whole, non-negative Toman amount."];
        return null;
    }

    private static int ParseInt(string name, string? value, int min, int max, int fallback, Dictionary<string, string[]> errors)
    {
        if (value is null)
        {
            return fallback;
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max)
        {
            return number;
        }

        errors[name] = [$"Must be a whole number from {min} to {max}."];
        return fallback;
    }
}
