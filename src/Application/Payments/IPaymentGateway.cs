namespace Shop.Application.Payments;

public enum PaymentVerificationStatus
{
    Pending,
    Succeeded,
    Failed,
}

public sealed record PaymentInitiationRequest(string PaymentReference, decimal Amount, string CurrencyCode, string ReturnUrl);

public sealed record PaymentInitiationResult(string ProviderReference, string RedirectUrl);

public sealed record PaymentVerificationResult(string ProviderReference, PaymentVerificationStatus Status, decimal Amount);

/// <summary>
/// Port for online payment providers. The order domain depends only on this contract; a real provider
/// is an adapter added later. Verification must always be a server-to-provider call, never client-reported.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken ct);

    Task<PaymentVerificationResult> VerifyAsync(string providerReference, CancellationToken ct);
}
