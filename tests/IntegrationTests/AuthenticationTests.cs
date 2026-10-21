using System.Net;
using System.Net.Http.Json;

namespace Shop.IntegrationTests;

public sealed class AuthenticationTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Register_ValidData_CreatesAccount_AndSignsTheUserIn()
    {
        var client = NewClient();
        var email = TestData.NewEmail();

        var response = await PostAsync(client, "/api/v1/auth/register",
            new { email, password = TestData.NewPassword(), fullName = "Sara Ahmadi", phone = "09121234567" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = await ReadAsync<AccountBody>(response);
        Assert.Equal(email, account.Email);
        Assert.Equal(["Customer"], account.Roles);

        // The auth cookie from registration works without a separate login.
        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(account.Id, (await ReadAsync<AccountBody>(me)).Id);
    }

    [Fact]
    public async Task Register_SetsHardenedAuthCookie()
    {
        var client = NewClient();

        var response = await PostAsync(client, "/api/v1/auth/register",
            new { email = TestData.NewEmail(), password = TestData.NewPassword(), fullName = "Sara" });

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("shop_auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict_EvenWithDifferentCasing()
    {
        var (_, email, _) = await RegisterCustomerAsync();

        var response = await PostAsync(NewClient(), "/api/v1/auth/register",
            new { email = email.ToUpperInvariant(), password = TestData.NewPassword(), fullName = "Someone Else" });

        await AssertProblemAsync(response, 409, "email_taken");
    }

    [Fact]
    public async Task Register_ConcurrentRequestsForSameEmail_CreateExactlyOneAccount()
    {
        var email = TestData.NewEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            PostAsync(NewClient(), "/api/v1/auth/register",
                new { email, password = TestData.NewPassword(), fullName = "Racer" })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var count = await Fixture.WithDbAsync(db => Task.FromResult(db.Users.Count(u => u.Email == email)));
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("not-an-email", "Valid-Password-123", "Email")]
    [InlineData("", "Valid-Password-123", "Email")]
    [InlineData("ok@example.com", "short", "Password")]
    [InlineData("ok@example.com", "", "Password")]
    public async Task Register_InvalidInput_ReturnsValidationProblem(string email, string password, string field)
    {
        var response = await PostAsync(NewClient(), "/api/v1/auth/register", new { email, password, fullName = "Sara" });

        var problem = await AssertProblemAsync(response, 400);
        Assert.Contains(field, problem.Errors!.Keys);
    }

    [Fact]
    public async Task Register_MissingFullName_ReturnsValidationProblem()
    {
        var response = await PostAsync(NewClient(), "/api/v1/auth/register",
            new { email = TestData.NewEmail(), password = TestData.NewPassword(), fullName = "" });

        var problem = await AssertProblemAsync(response, 400);
        Assert.Contains("FullName", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAccount_AndAuthenticatesLaterRequests()
    {
        var (_, email, password) = await RegisterCustomerAsync();
        var client = NewClient();

        var response = await PostAsync(client, "/api/v1/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, (await ReadAsync<AccountBody>(response)).Email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var (_, email, password) = await RegisterCustomerAsync();

        var response = await PostAsync(NewClient(), "/api/v1/auth/login", new { email = email.ToUpperInvariant(), password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_And_UnknownUser_AreIndistinguishable()
    {
        var (_, email, _) = await RegisterCustomerAsync();

        var wrongPassword = await PostAsync(NewClient(), "/api/v1/auth/login", new { email, password = TestData.NewPassword() });
        var unknownUser = await PostAsync(NewClient(), "/api/v1/auth/login", new { email = TestData.NewEmail(), password = TestData.NewPassword() });

        await AssertProblemAsync(wrongPassword, 401, "invalid_credentials");
        await AssertProblemAsync(unknownUser, 401, "invalid_credentials");
        Assert.False(wrongPassword.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.StartsWith("shop_auth=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Login_AfterRepeatedFailures_LocksTheAccountEvenForTheCorrectPassword()
    {
        var (_, email, password) = await RegisterCustomerAsync();

        for (var i = 0; i < 5; i++)
        {
            await PostAsync(NewClient(), "/api/v1/auth/login", new { email, password = TestData.NewPassword() });
        }

        var response = await PostAsync(NewClient(), "/api/v1/auth/login", new { email, password });

        await AssertProblemAsync(response, 401, "invalid_credentials");
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var (client, _, _) = await RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        var logout = await PostAsync(client, "/api/v1/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AssertProblemAsync(await client.GetAsync("/api/v1/auth/me"), 401);
    }

    [Fact]
    public async Task Logout_WhenAnonymous_IsUnauthorized()
    {
        var response = await PostAsync(NewClient(), "/api/v1/auth/logout");

        await AssertProblemAsync(response, 401);
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/customer/profile")]
    [InlineData("/api/v1/admin/ping")]
    public async Task ProtectedEndpoints_RejectAnonymousCallers(string url)
    {
        var response = await NewClient().GetAsync(url);

        await AssertProblemAsync(response, 401);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task UnsafeRequest_WithoutAntiforgeryToken_IsRejected()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestData.NewEmail(), password = TestData.NewPassword() }, Json);

        await AssertProblemAsync(response, 400, "antiforgery_invalid");
    }

    [Fact]
    public async Task UnsafeRequest_WithTokenFromAnotherSession_IsRejected()
    {
        var other = NewClient();
        var foreignToken = await other.GetFromJsonAsync<CsrfBody>("/api/v1/auth/csrf", Json);
        var client = NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = TestData.NewEmail(), password = TestData.NewPassword() }, options: Json),
        };
        request.Headers.Add("X-XSRF-TOKEN", foreignToken!.Token);

        var response = await client.SendAsync(request);

        await AssertProblemAsync(response, 400, "antiforgery_invalid");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails404()
    {
        var response = await NewClient().GetAsync("/api/v1/does-not-exist");

        await AssertProblemAsync(response, 404);
    }
}
