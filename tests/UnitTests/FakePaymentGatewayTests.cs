using Shop.Application.Payments;
using Shop.Infrastructure.Payments;

namespace Shop.UnitTests;

public class FakePaymentGatewayTests
{
    private static PaymentInitiationRequest Request(decimal amount = 150_000m) =>
        new("ORDER-1", amount, "IRR", "https://shop.test/payment/return");

    [Fact]
    public async Task Initiate_ThenVerify_IsPendingUntilCompleted()
    {
        var gateway = new FakePaymentGateway();

        var initiation = await gateway.InitiateAsync(Request(), CancellationToken.None);
        var pending = await gateway.VerifyAsync(initiation.ProviderReference, CancellationToken.None);

        Assert.Equal(PaymentVerificationStatus.Pending, pending.Status);
        Assert.Equal(150_000m, pending.Amount);
        Assert.StartsWith("https://shop.test/payment/return", initiation.RedirectUrl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, PaymentVerificationStatus.Succeeded)]
    [InlineData(false, PaymentVerificationStatus.Failed)]
    public async Task Complete_SettlesPayment(bool succeeded, PaymentVerificationStatus expected)
    {
        var gateway = new FakePaymentGateway();
        var initiation = await gateway.InitiateAsync(Request(), CancellationToken.None);

        gateway.Complete(initiation.ProviderReference, succeeded);
        var verified = await gateway.VerifyAsync(initiation.ProviderReference, CancellationToken.None);

        Assert.Equal(expected, verified.Status);
    }

    [Fact]
    public async Task Verify_UnknownReference_IsFailed_NeverSucceeded()
    {
        var gateway = new FakePaymentGateway();

        var result = await gateway.VerifyAsync("client-claims-success", CancellationToken.None);

        Assert.Equal(PaymentVerificationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Initiate_NonPositiveAmount_Throws()
    {
        var gateway = new FakePaymentGateway();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => gateway.InitiateAsync(Request(0m), CancellationToken.None));
    }
}
