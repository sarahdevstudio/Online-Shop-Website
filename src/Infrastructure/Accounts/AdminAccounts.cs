using Microsoft.AspNetCore.Identity;
using Shop.Application.Common;
using Shop.Domain.Security;
using Shop.Infrastructure.Identity;

namespace Shop.Infrastructure.Accounts;

/// <summary>Creating admin accounts outside the web API (the <c>create-admin</c> command and tests).</summary>
public static class AdminAccounts
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 64;

    /// <summary>Letters, digits and <c>. _ - @</c>: what Identity accepts by default, and safe to show and type.</summary>
    public static bool IsValidUsername(string? username) =>
        username is { Length: >= UsernameMinLength and <= UsernameMaxLength } && username.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '@');

    /// <summary>Admins sign in with the username; the e-mail is optional contact information.</summary>
    public static async Task<Result> CreateAdminAsync(
        UserManager<ApplicationUser> users, string username, string password, TimeProvider clock, string? email = null)
    {
        username = username.Trim();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (!IsValidUsername(username))
        {
            return Result.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["username"] = [$"نام کاربری باید {UsernameMinLength} تا {UsernameMaxLength} نویسه و فقط شامل حروف انگلیسی، عدد و . _ - @ باشد."],
            }));
        }

        if (email is not null && (email.Length > 256 || !email.Contains('@') || email.Contains(' ')))
        {
            return Result.Failure(Error.Validation(new Dictionary<string, string[]> { ["email"] = ["ایمیل معتبر وارد کنید."] }));
        }

        if (AdminPasswordRules.Weakness(password, username) is { } weakness)
        {
            return Result.Failure(Error.Validation(new Dictionary<string, string[]> { ["password"] = [weakness] }));
        }

        if (await users.FindByNameAsync(username) is not null)
        {
            return Result.Failure(Error.Conflict("username_taken", "حسابی با این نام کاربری از قبل وجود دارد."));
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = email,
            EmailConfirmed = email is not null,
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
            return Result.Failure(Error.Conflict("role_missing", "نقش مدیر هنوز ساخته نشده است؛ یک‌بار API را اجرا کنید تا ساخته شود."));
        }

        return Result.Success();
    }
}
