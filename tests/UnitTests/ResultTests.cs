using Shop.Application.Common;

namespace Shop.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_ExposesValue()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_ExposesError_AndRefusesValue()
    {
        Result<int> result = Error.NotFound("missing", "Not here.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ValidationError_CarriesFieldErrors()
    {
        var error = Error.Validation(new Dictionary<string, string[]> { ["email"] = ["bad"] });

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(["bad"], error.ValidationErrors!["email"]);
    }
}
