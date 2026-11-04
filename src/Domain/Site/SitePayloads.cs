using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shop.Domain.Site;

/// <summary>The closed sets of values the site configuration accepts (the validator and the storefront share these names).</summary>
public static class SiteValues
{
    public static readonly IReadOnlyList<string> Fonts = ["vazirmatn", "estedad", "shabnam", "sahel"];
    public static readonly IReadOnlyList<string> Radii = ["none", "sm", "md", "lg"];
    public static readonly IReadOnlyList<string> BlockTypes = ["hero", "categoryTiles", "productRow", "text", "banner"];
    public static readonly IReadOnlyList<string> ProductSources = ["newest", "discounted", "category", "handpicked"];
    public static readonly IReadOnlyList<string> BannerHeights = ["sm", "md", "lg"];
    public static readonly IReadOnlyList<string> AnnouncementStyles = ["primary", "accent", "neutral"];
    public static readonly IReadOnlyList<string> MediaPurposes = ["logo", "favicon", "hero", "banner", "og"];

    public const int MaxBlocks = 20;

    /// <summary>What a link can point to besides a typed URL (the same names as the route table's target types).</summary>
    public static readonly IReadOnlyList<string> LinkEntityTypes = ["category", "product", "contentPage", "blogPost"];
}

/// <summary>
/// A link to an entity of the shop instead of a typed URL. It is stored as (type, id) and turned into the entity's CURRENT
/// path when the document is compiled, so a path change moves the link without a redirect hop; a deleted entity hides it.
/// </summary>
public sealed record LinkEntity
{
    public string Type { get; init; } = string.Empty;
    public int Id { get; init; }

    /// <summary>The target's name when the link was chosen, kept so the editor can still name it after the target is deleted. Never shown on the site.</summary>
    public string? Label { get; init; }
}

// ---- theme ----

public sealed record ThemeLogo
{
    public int MediaId { get; init; }
    public string Alt { get; init; } = string.Empty;
}

public sealed record ThemeFavicon
{
    public int MediaId { get; init; }
}

public sealed record ThemePayload
{
    public string Primary { get; init; } = string.Empty;
    public string Secondary { get; init; } = string.Empty;
    public string Accent { get; init; } = string.Empty;
    public string Background { get; init; } = string.Empty;
    public string Surface { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Success { get; init; } = string.Empty;
    public string Warning { get; init; } = string.Empty;
    public string Danger { get; init; } = string.Empty;
    public string Font { get; init; } = string.Empty;
    public string Radius { get; init; } = string.Empty;
    public ThemeLogo? Logo { get; init; }
    public ThemeFavicon? Favicon { get; init; }
}

// ---- home ----

public sealed record HeroSlide
{
    public int? MediaId { get; init; }
    public string Alt { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string LinkLabel { get; init; } = string.Empty;
    public string? Link { get; init; }
    public LinkEntity? Entity { get; init; }
}

public sealed record HeroData
{
    public int AutoplaySeconds { get; init; }
    public List<HeroSlide> Slides { get; init; } = [];
}

public sealed record CategoryTileItem
{
    public int CategoryId { get; init; }
    public string? Label { get; init; }
}

public sealed record CategoryTilesData
{
    public List<CategoryTileItem> Items { get; init; } = [];
}

public sealed record ProductRowData
{
    public string Title { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public List<int> ProductIds { get; init; } = [];
    public int Limit { get; init; }
}

public sealed record TextBlockData
{
    public string Heading { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

public sealed record BannerData
{
    public int MediaId { get; init; }
    public string Alt { get; init; } = string.Empty;
    public string? Link { get; init; }
    public LinkEntity? Entity { get; init; }
    public string Height { get; init; } = string.Empty;
}

/// <summary>One block of the home page. <see cref="Data"/> is the typed record that matches <see cref="Type"/> (null for an unknown type).</summary>
public sealed record HomeBlock
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public object? Data { get; init; }

    public static Type? DataTypeFor(string type) => type switch
    {
        "hero" => typeof(HeroData),
        "categoryTiles" => typeof(CategoryTilesData),
        "productRow" => typeof(ProductRowData),
        "text" => typeof(TextBlockData),
        "banner" => typeof(BannerData),
        _ => null,
    };
}

public sealed record HomePayload
{
    public List<HomeBlock> Blocks { get; init; } = [];
}

// ---- chrome ----

public sealed record AnnouncementBar
{
    public bool Enabled { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Link { get; init; }
    public LinkEntity? Entity { get; init; }
    public string Style { get; init; } = "primary";
    public bool Dismissible { get; init; } = true;
}

public sealed record MenuChild
{
    public string Label { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
    public LinkEntity? Entity { get; init; }
}

public sealed record MenuItem
{
    public string Label { get; init; } = string.Empty;
    public string? Link { get; init; }
    public LinkEntity? Entity { get; init; }
    public bool AllCategories { get; init; }
    public List<MenuChild> Children { get; init; } = [];
}

public sealed record FooterLink
{
    public string Label { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
    public LinkEntity? Entity { get; init; }
}

public sealed record FooterColumn
{
    public string Title { get; init; } = string.Empty;
    public List<FooterLink> Links { get; init; } = [];
}

public sealed record FooterContent
{
    public string About { get; init; } = string.Empty;
    public List<FooterColumn> Columns { get; init; } = [];
}

/// <summary>Social links. Input may be a handle or a URL; what is stored is the normalized https URL (or null).</summary>
public sealed record SocialLinks
{
    public string? Telegram { get; init; }
    public string? Whatsapp { get; init; }
    public string? Aparat { get; init; }
    public string? Twitter { get; init; }
    public string? Pinterest { get; init; }
    public string? Youtube { get; init; }
    public string? Linkedin { get; init; }
}

public sealed record ChromePayload
{
    public AnnouncementBar Announcement { get; init; } = new();
    public List<MenuItem> HeaderMenu { get; init; } = [];
    public FooterContent Footer { get; init; } = new();
    public SocialLinks Social { get; init; } = new();
}

// ---- seo ----

public sealed record SeoPayload
{
    public string SiteName { get; init; } = string.Empty;
    public string TitleTemplate { get; init; } = string.Empty;
    public string HomeTitle { get; init; } = string.Empty;
    public string DefaultDescription { get; init; } = string.Empty;
    public int? DefaultOgMediaId { get; init; }
}

// ---- trust badges (identifiers only) ----

public sealed record EnamadBadge
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

public sealed record SamandehiBadge
{
    public string Id { get; init; } = string.Empty;
    public string VerifyCode { get; init; } = string.Empty;
    public string LogoCode { get; init; } = string.Empty;
}

public sealed record BadgesPayload
{
    public EnamadBadge? Enamad { get; init; }
    public SamandehiBadge? Samandehi { get; init; }
}

// ---- JSON ----

/// <summary>Reads and writes the typed payloads. No dynamic JSON: every kind maps to one record.</summary>
public static class SiteJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.Converters.Add(new HomeBlockConverter());
        return options;
    }

    public static Type PayloadType(SiteKind kind) => kind switch
    {
        SiteKind.Theme => typeof(ThemePayload),
        SiteKind.Home => typeof(HomePayload),
        SiteKind.Chrome => typeof(ChromePayload),
        SiteKind.Seo => typeof(SeoPayload),
        SiteKind.Badges => typeof(BadgesPayload),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Serialize(object payload) => JsonSerializer.Serialize(payload, payload.GetType(), Options);

    /// <summary>The typed payload, or null when the JSON does not fit the record (wrong types, not an object).</summary>
    public static object? TryDeserialize(SiteKind kind, string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, PayloadType(kind), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static object? TryDeserialize(SiteKind kind, JsonElement element) => TryDeserialize(kind, element.GetRawText());

    private sealed class HomeBlockConverter : JsonConverter<HomeBlock>
    {
        public override HomeBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("A block must be an object.");
            }

            var id = Guid.Empty;
            if (root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
            {
                Guid.TryParse(idElement.GetString(), out id);
            }

            var type = root.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString() ?? string.Empty
                : string.Empty;
            var enabled = !root.TryGetProperty("enabled", out var enabledElement) || enabledElement.ValueKind != JsonValueKind.False;

            object? data = null;
            if (HomeBlock.DataTypeFor(type) is { } dataType)
            {
                data = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Deserialize(dataElement.GetRawText(), dataType, options)
                    : Activator.CreateInstance(dataType);
            }

            return new HomeBlock { Id = id, Type = type, Enabled = enabled, Data = data };
        }

        public override void Write(Utf8JsonWriter writer, HomeBlock value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("id", value.Id);
            writer.WriteString("type", value.Type);
            writer.WriteBoolean("enabled", value.Enabled);
            writer.WritePropertyName("data");
            if (value.Data is null)
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            else
            {
                JsonSerializer.Serialize(writer, value.Data, value.Data.GetType(), options);
            }

            writer.WriteEndObject();
        }
    }
}
