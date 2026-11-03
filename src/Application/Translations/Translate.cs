using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shop.Domain.Common;
using Shop.Domain.Translations;

namespace Shop.Application.Translations;

/// <summary>Where edited texts come from (the database cache). Null means "no edited text": the shipped default applies.</summary>
public interface ITranslationSource
{
    string? Lookup(string key, string culture);
}

/// <summary>
/// Looks a key up: the culture's edited text, then the text shipped in the assembly, then the generic "unexpected" text. It never
/// returns a raw key and never throws; every miss is logged once per key (operator log, English).
/// </summary>
public sealed partial class TranslationResolver(ITranslationSource? source, ILogger? logger, string? culture = null)
{
    public const string UnexpectedKey = "api.error.unexpected";

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}")]
    private static partial Regex Placeholder();

    private readonly ConcurrentDictionary<string, byte> _missed = new(StringComparer.Ordinal);
    private readonly string _culture = string.IsNullOrWhiteSpace(culture) ? Translation.DefaultCulture : culture;

    public string Culture => _culture;

    public string Get(string key, params (string Name, object? Value)[] args) =>
        Format(Template(key), args.Select(a => (a.Name, Convert.ToString(a.Value, CultureInfo.InvariantCulture) ?? string.Empty)));

    /// <summary>Turns an encoded message (<see cref="Msg"/>) into text; text that is already finished (not a key) passes through unchanged.</summary>
    public string Resolve(string? message)
    {
        if (message is null)
        {
            return Get(UnexpectedKey);
        }

        return Msg.TryParse(message, out var key, out var args) ? Format(Template(key), args.Select(a => (a.Name, Nested(a.Value)))) : message;
    }

    /// <summary>An argument that is itself a known key (a field label, a contrast pair name) is translated too; any other value is kept.</summary>
    private string Nested(string value) =>
        Msg.IsEncoded(value) && !value.Contains('|') && Known(value) ? Template(value) : value;

    private bool Known(string key) => source?.Lookup(key, _culture) is { Length: > 0 } || TranslationManifest.Defaults.ContainsKey(key);

    public string Template(string key)
    {
        if (source?.Lookup(key, _culture) is { Length: > 0 } edited)
        {
            return edited;
        }

        if (TranslationManifest.Defaults.TryGetValue(key, out var shipped))
        {
            return shipped;
        }

        if (_missed.TryAdd(key, 0))
        {
            logger?.LogWarning("Translation key {Key} is missing for culture {Culture}; the generic text is used", key, _culture);
        }

        return TranslationManifest.Defaults.TryGetValue(UnexpectedKey, out var generic) ? generic : "خطا";
    }

    private static string Format(string template, IEnumerable<(string Name, string Value)> args)
    {
        var values = args.GroupBy(a => a.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
        return values.Count == 0
            ? template
            : Placeholder().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
    }

    /// <summary>The named placeholders of a text, for the editor's "same placeholders" rule.</summary>
    public static IReadOnlySet<string> PlaceholdersOf(string text) =>
        Placeholder().Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Key → current text for every key that starts with one of <paramref name="allowed"/> (and with <paramref name="prefix"/> when
    /// given). The caller names the allowed prefixes, so a client can never read an area it is not meant to see.
    /// </summary>
    public IReadOnlyDictionary<string, string> Bundle(IReadOnlyCollection<string> allowed, string? prefix)
    {
        var wanted = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim();
        return TranslationManifest.Defaults.Keys
            .Where(k => allowed.Any(a => k.StartsWith(a, StringComparison.Ordinal)) && (wanted is null || k.StartsWith(wanted, StringComparison.Ordinal)))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToDictionary(k => k, Template, StringComparer.Ordinal);
    }
}

/// <summary>
/// The one place user-facing text is produced: <c>Translate.Get("shop.cart.empty")</c>,
/// <c>Translate.Get("api.validation.max-length", ("max", 30))</c>. Wired to the database cache at startup; without that only the
/// shipped texts are used (tools, unit tests).
/// </summary>
public static class Translate
{
    private static volatile TranslationResolver _current = new(null, null);

    public static TranslationResolver Current => _current;

    public static void Configure(ITranslationSource? source, ILogger? logger, string? culture = null) =>
        _current = new TranslationResolver(source, logger, culture);

    public static string Get(string key, params (string Name, object? Value)[] args) => _current.Get(key, args);

    public static string Resolve(string? message) => _current.Resolve(message);

    public static IReadOnlySet<string> PlaceholdersOf(string text) => TranslationResolver.PlaceholdersOf(text);
}
