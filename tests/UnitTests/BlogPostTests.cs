using Shop.Domain.Blog;

namespace Shop.UnitTests;

public class BlogPostTests
{
    private static readonly DateTime T0 = new(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc);

    private static BlogPost NewPost(bool publish = false) =>
        BlogPost.Create("led-vs-halogen", "ال‌ای‌دی یا هالوژن", "مقایسهٔ کوتاه", "متن مقاله", publish, null, T0);

    [Fact]
    public void Create_Draft_HasNoPublishDate()
    {
        var post = NewPost();

        Assert.False(post.IsPublished);
        Assert.Null(post.PublishedAtUtc);
        Assert.Equal(T0, post.CreatedAtUtc);
    }

    [Fact]
    public void Publishing_SetsTheDateOnce_AndKeepsItWhenRepublished()
    {
        var post = NewPost();
        var t1 = T0.AddDays(1);
        post.Apply(post.Title, post.Excerpt, post.Body, publish: true, null, t1);
        Assert.Equal(t1, post.PublishedAtUtc);

        post.Apply(post.Title, post.Excerpt, post.Body, publish: false, null, t1.AddDays(1));
        Assert.False(post.IsPublished);
        Assert.Equal(t1, post.PublishedAtUtc);

        post.Apply(post.Title, post.Excerpt, post.Body, publish: true, null, t1.AddDays(2));
        Assert.Equal(t1, post.PublishedAtUtc); // the original publication date stays
    }

    [Fact]
    public void Create_RejectsAnInvalidSlug()
    {
        Assert.Throws<ArgumentException>(() => BlogPost.Create("Bad Slug", "عنوان", "خلاصه", "متن", false, null, T0));
    }

    [Fact]
    public void Apply_NormalizesArabicLettersAndWhitespace()
    {
        var post = NewPost();
        post.Apply("  كتاب   ي ", "خلاصه", "متن ي", false, null, T0);

        Assert.Equal("کتاب ی", post.Title);
        Assert.Equal("متن ی", post.Body);
    }

    [Theory]
    [InlineData("led-bulb", "عنوان خوب", "خلاصه", "متن", false)]
    [InlineData(null, "عنوان خوب", "خلاصه", "متن", false)] // update: the slug is not re-checked
    [InlineData("Bad_Slug", "عنوان خوب", "خلاصه", "متن", true)]
    [InlineData("led-bulb", "ا", "خلاصه", "متن", true)]
    [InlineData("led-bulb", "عنوان خوب", "", "متن", true)]
    [InlineData("led-bulb", "عنوان خوب", "خلاصه", "  ", true)]
    public void Validate_ReportsEachBadField(string? slug, string title, string excerpt, string body, bool hasErrors)
    {
        var errors = BlogPost.Validate(slug, title, excerpt, body);

        Assert.Equal(hasErrors, errors.Count > 0);
    }

    [Fact]
    public void Validate_TooLongFields_AreNamedAndPersian()
    {
        var errors = BlogPost.Validate("ok", new string('ا', BlogPost.TitleMaxLength + 1), new string('ا', BlogPost.ExcerptMaxLength + 1), new string('ا', BlogPost.BodyMaxLength + 1));

        Assert.Equal(["title", "excerpt", "body"], errors.Keys.OrderBy(k => k == "title" ? 0 : k == "excerpt" ? 1 : 2));
        Assert.All(errors.Values.SelectMany(v => v), m => Assert.DoesNotContain(m, c => char.IsAsciiLetter(c)));
    }
}
