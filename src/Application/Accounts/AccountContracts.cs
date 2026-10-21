namespace Shop.Application.Accounts;

public sealed record RegisterCustomerRequest(string Email, string Password, string FullName, string? Phone);

public sealed record LoginRequest(string Email, string Password);

public sealed record AccountDto(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

public sealed record CustomerProfileDto(Guid CustomerId, string Email, string FullName, string? Phone);
