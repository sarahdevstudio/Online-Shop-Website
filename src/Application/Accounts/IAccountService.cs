using Shop.Application.Common;

namespace Shop.Application.Accounts;

public interface IAccountService
{
    /// <summary>Creates the identity user, assigns the Customer role, creates the profile, and signs the user in.</summary>
    Task<Result<AccountDto>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct);

    Task<Result<AccountDto>> LoginAsync(LoginRequest request, CancellationToken ct);

    Task LogoutAsync(CancellationToken ct);

    Task<Result<AccountDto>> GetAccountAsync(Guid userId, CancellationToken ct);

    Task<Result<CustomerProfileDto>> GetCustomerProfileAsync(Guid userId, CancellationToken ct);
}
