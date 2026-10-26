using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Shop.Application.Accounts;
using Shop.Application.Admin;
using Shop.Application.Common;
using Shop.Domain.Customers;
using Shop.Domain.Security;
using Shop.Infrastructure.Identity;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Accounts;

internal sealed class AccountService(
    ShopDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ICurrentUser currentUser,
    LoginThrottle throttle,
    TimeProvider clock) : IAccountService
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("invalid_credentials", "نام کاربری یا رمز عبور درست نیست.");

    private static readonly Error EmailTaken =
        Error.Conflict("email_taken", "حسابی با این ایمیل از قبل وجود دارد.");

    public async Task<Result<AccountDto>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var created = await userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                return MapCreateFailure(created);
            }

            var roleResult = await userManager.AddToRoleAsync(user, Roles.Customer);
            if (!roleResult.Succeeded)
            {
                // The Customer role is seeded at startup; a failure here is a deployment fault, not user error.
                throw new InvalidOperationException(
                    "Could not assign the Customer role: " + string.Join(", ", roleResult.Errors.Select(e => e.Code)));
            }

            db.Customers.Add(Customer.Create(user.Id, request.FullName, request.Phone, user.CreatedAtUtc));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two concurrent registrations for the same email: the database constraint is the final arbiter.
            return EmailTaken;
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return new AccountDto(user.Id, email, email, request.FullName.Trim(), [Roles.Customer]);
    }

    public async Task<Result<AccountDto>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();

        // Per-account attempt limit (on top of the per-IP limiter and the 15-minute lockout after 5 failures). It counts
        // every attempt for any username string, so it reveals nothing about whether the account exists.
        if (!throttle.TryAcquire(username))
        {
            return Error.TooManyRequests("rate_limited", "تعداد تلاش‌های ورود زیاد است. یک دقیقه بعد دوباره امتحان کنید.");
        }

        var result = await signInManager.PasswordSignInAsync(username, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            // Wrong password, unknown user and lockout all look the same to the caller.
            return InvalidCredentials;
        }

        var user = await userManager.FindByNameAsync(username);
        return user is null ? InvalidCredentials : await BuildAccountAsync(user, ct);
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        // A new security stamp makes every cookie issued to this user invalid on its next request (checked on each request),
        // so a copied cookie dies with the logout instead of living until it expires.
        if (currentUser.UserId is { } id && await userManager.FindByIdAsync(id.ToString()) is { } user)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }

        await signInManager.SignOutAsync();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure(Error.NotFound("account_not_found", "حساب کاربری پیدا نشد."));
        }

        if (await userManager.IsInRoleAsync(user, Roles.Admin) && AdminPasswordRules.Weakness(request.NewPassword, user.UserName) is { } weakness)
        {
            return Result.Failure(Error.Validation(new Dictionary<string, string[]> { ["newPassword"] = [weakness] }));
        }

        if (await userManager.IsLockedOutAsync(user) || !await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            await userManager.AccessFailedAsync(user); // guessing the current password here counts towards the lockout
            return Result.Failure(Error.Validation(new Dictionary<string, string[]> { ["currentPassword"] = ["رمز عبور فعلی درست نیست."] }));
        }

        var changed = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded)
        {
            return Result.Failure(Error.Validation(new Dictionary<string, string[]> { ["newPassword"] = changed.Errors.Select(e => e.Description).ToArray() }));
        }

        // ChangePassword rotated the security stamp, so every session (this one too) is now invalid; keep the caller
        // signed in on a fresh cookie.
        await userManager.ResetAccessFailedCountAsync(user);
        await signInManager.RefreshSignInAsync(user);
        return Result.Success();
    }

    public async Task<Result<AccountDto>> GetAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Error.NotFound("account_not_found", "حساب کاربری پیدا نشد.")
            : await BuildAccountAsync(user, ct);
    }

    public async Task<Result<CustomerProfileDto>> GetCustomerProfileAsync(Guid userId, CancellationToken ct)
    {
        var profile = await db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Join(db.Users, c => c.UserId, u => u.Id, (c, u) => new CustomerProfileDto(c.Id, u.Email!, c.FullName, c.Phone))
            .SingleOrDefaultAsync(ct);

        return profile is null
            ? Error.NotFound("customer_not_found", "پروفایل مشتری پیدا نشد.")
            : profile;
    }

    private async Task<Result<AccountDto>> BuildAccountAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var displayName = await db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .Select(c => c.FullName)
            .SingleOrDefaultAsync(ct) ?? user.UserName!;

        return new AccountDto(user.Id, user.UserName!, user.Email, displayName, roles.ToArray());
    }

    private static Result<AccountDto> MapCreateFailure(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return EmailTaken;
        }

        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return Error.Validation(errors);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
