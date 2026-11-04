using Shop.Application.Search;

namespace Shop.UnitTests;

public class EditDistanceTests
{
    [Theory]
    [InlineData("lamp", "lamp", 0)]
    [InlineData("lamp", "lap", 1)]        // a missing letter
    [InlineData("lamp", "lampp", 1)]      // an extra letter
    [InlineData("lamp", "lemp", 1)]       // a wrong letter
    [InlineData("lamp", "lmap", 1)]       // two letters swapped counts as one typo
    [InlineData("لامپ", "لمپ", 1)]
    [InlineData("لامپ", "لاپم", 1)]
    [InlineData("پنل", "پنل", 0)]
    public void CountsSingleTypos(string a, string b, int expected) => Assert.Equal(expected, EditDistance.Osa(a, b, 3));

    [Fact]
    public void StopsEarlyOnceTheLimitIsCertainlyExceeded()
    {
        Assert.Equal(3, EditDistance.Osa("lamp", "chandelier", 2)); // max + 1
        Assert.Equal(2, EditDistance.Osa("a", "abcdefgh", 1));      // the length difference alone is enough
        Assert.Equal(0, EditDistance.Osa(string.Empty, string.Empty, 1));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(12, 2)]
    public void ShortWordsAllowNoTypos_LongWordsAllowMore(int length, int allowed) => Assert.Equal(allowed, EditDistance.Allowed(length));
}
