using Microsoft.Extensions.Logging;
using Shop.Application.Translations;
using Shop.Domain.Common;

namespace Shop.UnitTests;

public class TranslateTests
{
    private sealed class FakeSource(Dictionary<string, string> edited) : ITranslationSource
    {
        public string? Lookup(string key, string culture) => edited.TryGetValue(key, out var text) ? text : null;
    }

    private sealed class CountingLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add($"{logLevel}: {formatter(state, exception)}");
    }

    [Fact]
    public void Msg_RoundTripsKeyAndArguments_IncludingSeparatorsInValues()
    {
        var encoded = Msg.Of("api.validation.max-length", ("max", 30), ("field", "a|b=c é"));

        Assert.True(Msg.TryParse(encoded, out var key, out var args));
        Assert.Equal("api.validation.max-length", key);
        Assert.Equal([("max", "30"), ("field", "a|b=c é")], args);
    }

    [Theory]
    [InlineData("سبد خرید خالی است.")]
    [InlineData("Hello world")]
    [InlineData("")]
    public void Msg_FinishedTextIsNotAKey(string text)
    {
        Assert.False(Msg.IsEncoded(text));
    }

    [Fact]
    public void Resolve_UsesTheEditedTextThenTheShippedDefault()
    {
        var resolver = new TranslationResolver(new FakeSource(new() { ["api.error.unexpected"] = "ویرایش‌شده" }), null);

        Assert.Equal("ویرایش‌شده", resolver.Resolve("api.error.unexpected"));
        Assert.Equal(TranslationManifest.Defaults["api.error.unexpected"], new TranslationResolver(null, null).Resolve("api.error.unexpected"));
    }

    [Fact]
    public void Get_SubstitutesNamedPlaceholders_AndLeavesUnknownOnesAlone()
    {
        var resolver = new TranslationResolver(new FakeSource(new() { ["test.key"] = "حداکثر {max} نویسه برای {field} و {other}" }), null);

        Assert.Equal("حداکثر 30 نویسه برای نام و {other}", resolver.Get("test.key", ("max", 30), ("field", "نام")));
    }

    [Fact]
    public void Resolve_PassesFinishedTextThrough()
    {
        Assert.Equal("متن آماده", new TranslationResolver(null, null).Resolve("متن آماده"));
    }

    [Fact]
    public void AMissingKey_GivesTheGenericText_NeverTheKey_AndIsLoggedOncePerKey()
    {
        var logger = new CountingLogger();
        var resolver = new TranslationResolver(null, logger);

        var first = resolver.Get("no.such-key");
        var second = resolver.Get("no.such-key");
        resolver.Get("another.missing-key");

        Assert.Equal(TranslationManifest.Defaults[TranslationResolver.UnexpectedKey], first);
        Assert.Equal(first, second);
        Assert.DoesNotContain("no.such-key", first);
        Assert.Equal(2, logger.Messages.Count);
        Assert.All(logger.Messages, m => Assert.StartsWith("Warning", m));
    }

    [Fact]
    public void PlaceholdersOf_ListsNamedPlaceholders()
    {
        Assert.Equal(new[] { "count", "max" }.Order(), TranslationResolver.PlaceholdersOf("{count} از {max} و {count}").Order());
    }
}
