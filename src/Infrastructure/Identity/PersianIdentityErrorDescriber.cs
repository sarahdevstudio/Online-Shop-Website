using Microsoft.AspNetCore.Identity;

namespace Shop.Infrastructure.Identity;

/// <summary>Identity's error texts are English by default and reach users through registration and password changes.</summary>
public sealed class PersianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Describe(nameof(DefaultError), "خطای ناشناخته‌ای رخ داد.");
    public override IdentityError ConcurrencyFailure() => Describe(nameof(ConcurrencyFailure), "اطلاعات هم‌زمان تغییر کرد. دوباره تلاش کنید.");
    public override IdentityError PasswordMismatch() => Describe(nameof(PasswordMismatch), "رمز عبور درست نیست.");
    public override IdentityError InvalidToken() => Describe(nameof(InvalidToken), "توکن نامعتبر است.");
    public override IdentityError InvalidUserName(string? userName) => Describe(nameof(InvalidUserName), "نام کاربری نامعتبر است.");
    public override IdentityError InvalidEmail(string? email) => Describe(nameof(InvalidEmail), "ایمیل نامعتبر است.");
    public override IdentityError DuplicateUserName(string userName) => Describe(nameof(DuplicateUserName), "این نام کاربری قبلاً ثبت شده است.");
    public override IdentityError DuplicateEmail(string email) => Describe(nameof(DuplicateEmail), "این ایمیل قبلاً ثبت شده است.");
    public override IdentityError PasswordTooShort(int length) => Describe(nameof(PasswordTooShort), $"رمز عبور باید حداقل {length} نویسه باشد.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Describe(nameof(PasswordRequiresNonAlphanumeric), "رمز عبور باید حداقل یک نویسهٔ غیرحرفی و غیرعددی داشته باشد.");
    public override IdentityError PasswordRequiresDigit() => Describe(nameof(PasswordRequiresDigit), "رمز عبور باید حداقل یک عدد داشته باشد.");
    public override IdentityError PasswordRequiresLower() => Describe(nameof(PasswordRequiresLower), "رمز عبور باید حداقل یک حرف کوچک انگلیسی داشته باشد.");
    public override IdentityError PasswordRequiresUpper() => Describe(nameof(PasswordRequiresUpper), "رمز عبور باید حداقل یک حرف بزرگ انگلیسی داشته باشد.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Describe(nameof(PasswordRequiresUniqueChars), $"رمز عبور باید حداقل {uniqueChars} نویسهٔ متفاوت داشته باشد.");
    public override IdentityError UserAlreadyHasPassword() => Describe(nameof(UserAlreadyHasPassword), "این حساب از قبل رمز عبور دارد.");
    public override IdentityError UserAlreadyInRole(string role) => Describe(nameof(UserAlreadyInRole), "کاربر از قبل این نقش را دارد.");
    public override IdentityError UserNotInRole(string role) => Describe(nameof(UserNotInRole), "کاربر این نقش را ندارد.");
    public override IdentityError InvalidRoleName(string? role) => Describe(nameof(InvalidRoleName), "نام نقش نامعتبر است.");
    public override IdentityError DuplicateRoleName(string role) => Describe(nameof(DuplicateRoleName), "این نقش قبلاً ثبت شده است.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Describe(nameof(RecoveryCodeRedemptionFailed), "کد بازیابی پذیرفته نشد.");
    public override IdentityError LoginAlreadyAssociated() => Describe(nameof(LoginAlreadyAssociated), "این ورود از قبل به حساب دیگری وصل است.");

    private static IdentityError Describe(string code, string description) => new() { Code = code, Description = description };
}
