using Microsoft.AspNetCore.Identity;
using Shop.Application.Common;
using Shop.Domain.Security;
using Shop.Infrastructure.Identity;

namespace Shop.Infrastructure.Accounts;

/// <summary>Creating admin accounts outside the web API (the <c>create-admin</c> command and tests).</summary>
public static class AdminAccounts
{
    public static async Task<Result> CreateAdminAsync(UserManager<ApplicationUser> users, string email, string password, TimeProvider clock)
    {
        email = email.Trim();
        if (email.Length is 0 or > 256 || !email.Contains('@') || email.Contains(' '))
        {
            return Result.Failure(Error.Validation(new Dictionary<string, string[]> { ["email"] = ["Enter a valid e-mail address."] }));
        }

        if (AdminPasswordRules.Weakness(password, email) is { } weakness)
        {
            return Result.Failure(Error.Validation(new Dictionary<string, string[]> { ["password"] = [weakness] }));
        }

        if (await users.FindByEmailAsync(email) is not null)
        {
            return Result.Failure(Error.Conflict("email_taken", "An account with this e-mail already exists."));
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            // Codes only: descriptions can echo input.
            return Result.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["password"] = created.Errors.Select(e => e.Code).ToArray(),
            }));
        }

        var role = await users.AddToRoleAsync(user, Roles.Admin);
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            return Result.Failure(Error.Conflict("role_missing", "The Admin role does not exist yet; start the API once so it is created."));
        }

        return Result.Success();
    }
}
