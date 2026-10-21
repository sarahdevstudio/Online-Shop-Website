using System.Collections.Concurrent;
using Shop.Application.Payments;

namespace Shop.Infrastructure.Payments;

/// <summary>
/// Deterministic in-memory gateway for development and automated tests only (never registered in
/// production). Payments stay Pending until a test or developer calls <see cref="Complete"/>.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, (PaymentVerificationStatus Status, decimal Amount)> _payments = new();

    public Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Amount must be positive.");
        }

        var providerReference = $"fake_{Guid.NewGuid():N}";
        _payments[providerReference] = (PaymentVerificationStatus.Pending, request.Amount);
        var redirect = $"{request.ReturnUrl}?providerReference={providerReference}";
        return Task.FromResult(new PaymentInitiationResult(providerReference, redirect));
    }

    public Task<PaymentVerificationResult> VerifyAsync(string providerReference, CancellationToken ct)
    {
        var result = _payments.TryGetValue(providerReference, out var payment)
            ? new PaymentVerificationResult(providerReference, payment.Status, payment.Amount)
            : new PaymentVerificationResult(providerReference, PaymentVerificationStatus.Failed, 0m);
        return Task.FromResult(result);
    }

    /// <summary>Test hook: settle a pending payment as succeeded or failed.</summary>
    public void Complete(string providerReference, bool succeeded)
    {
        if (!_payments.TryGetValue(providerReference, out var payment))
        {
            throw new KeyNotFoundException("Unknown fake payment reference.");
        }

        _payments[providerReference] = (succeeded ? PaymentVerificationStatus.Succeeded : PaymentVerificationStatus.Failed, payment.Amount);
    }
}
