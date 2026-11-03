using Microsoft.AspNetCore.Identity;
using Shop.Domain.Common;

namespace Shop.Infrastructure.Identity;

/// <summary>Identity's error texts are English by default and reach users through registration and password changes.</summary>
public sealed class PersianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Describe(nameof(DefaultError), "api.identity.default");
    public override IdentityError ConcurrencyFailure() => Describe(nameof(ConcurrencyFailure), "api.identity.concurrency-failure");
    public override IdentityError PasswordMismatch() => Describe(nameof(PasswordMismatch), "api.identity.password-mismatch");
    public override IdentityError InvalidToken() => Describe(nameof(InvalidToken), "api.identity.invalid-token");
    public override IdentityError InvalidUserName(string? userName) => Describe(nameof(InvalidUserName), "api.identity.invalid-user-name");
    public override IdentityError InvalidEmail(string? email) => Describe(nameof(InvalidEmail), "api.identity.invalid-email");
    public override IdentityError DuplicateUserName(string userName) => Describe(nameof(DuplicateUserName), "api.identity.duplicate-user-name");
    public override IdentityError DuplicateEmail(string email) => Describe(nameof(DuplicateEmail), "api.identity.duplicate-email");
    public override IdentityError PasswordTooShort(int length) => Describe(nameof(PasswordTooShort), Msg.Of("api.identity.password-too-short", ("length", length)));
    public override IdentityError PasswordRequiresNonAlphanumeric() => Describe(nameof(PasswordRequiresNonAlphanumeric), "api.identity.password-requires-non-alphanumeric");
    public override IdentityError PasswordRequiresDigit() => Describe(nameof(PasswordRequiresDigit), "api.identity.password-requires-digit");
    public override IdentityError PasswordRequiresLower() => Describe(nameof(PasswordRequiresLower), "api.identity.password-requires-lower");
    public override IdentityError PasswordRequiresUpper() => Describe(nameof(PasswordRequiresUpper), "api.identity.password-requires-upper");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Describe(nameof(PasswordRequiresUniqueChars), Msg.Of("api.identity.password-requires-unique-chars", ("uniqueChars", uniqueChars)));
    public override IdentityError UserAlreadyHasPassword() => Describe(nameof(UserAlreadyHasPassword), "api.identity.user-already-has-password");
    public override IdentityError UserAlreadyInRole(string role) => Describe(nameof(UserAlreadyInRole), "api.identity.user-already-in-role");
    public override IdentityError UserNotInRole(string role) => Describe(nameof(UserNotInRole), "api.identity.user-not-in-role");
    public override IdentityError InvalidRoleName(string? role) => Describe(nameof(InvalidRoleName), "api.identity.invalid-role-name");
    public override IdentityError DuplicateRoleName(string role) => Describe(nameof(DuplicateRoleName), "api.identity.duplicate-role-name");
    public override IdentityError RecoveryCodeRedemptionFailed() => Describe(nameof(RecoveryCodeRedemptionFailed), "api.identity.recovery-code-redemption-failed");
    public override IdentityError LoginAlreadyAssociated() => Describe(nameof(LoginAlreadyAssociated), "api.identity.login-already-associated");

    private static IdentityError Describe(string code, string description) => new() { Code = code, Description = description };
}
