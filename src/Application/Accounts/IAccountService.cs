using Shop.Application.Common;

namespace Shop.Application.Accounts;

public interface IAccountService
{
    /// <summary>Creates the identity user, assigns the Customer role, creates the profile, and signs the user in.</summary>
    Task<Result<AccountDto>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct);

    Task<Result<AccountDto>> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>Signs out and revokes every session of the user (the security stamp changes), not just this browser's cookie.</summary>
    Task LogoutAsync(CancellationToken ct);

    /// <summary>
    /// Changes the password after checking the current one, revokes all other sessions and keeps the caller signed in on a
    /// fresh cookie. Admins need at least 12 characters.
    /// </summary>
    Task<Result> ChangePasswordAsync(Guid userId, Admin.ChangePasswordRequest request, CancellationToken ct);

    Task<Result<AccountDto>> GetAccountAsync(Guid userId, CancellationToken ct);

    Task<Result<CustomerProfileDto>> GetCustomerProfileAsync(Guid userId, CancellationToken ct);
}
