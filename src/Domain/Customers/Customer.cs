namespace Shop.Domain.Customers;

/// <summary>
/// Storefront customer profile. Credentials live in the identity user; this row holds business data
/// and is linked 1:1 by <see cref="UserId"/>.
/// </summary>
public sealed class Customer
{
    public const int FullNameMaxLength = 200;
    public const int PhoneMaxLength = 32;

    private Customer()
    {
        FullName = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string FullName { get; private set; }
    public string? Phone { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Customer Create(Guid userId, string fullName, string? phone, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAtUtc = utcNow,
        };
        customer.UpdateProfile(fullName, phone);
        return customer;
    }

    public void UpdateProfile(string fullName, string? phone)
    {
        var name = fullName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > FullNameMaxLength)
        {
            throw new ArgumentException($"Full name is required and must be at most {FullNameMaxLength} characters.", nameof(fullName));
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (normalizedPhone is { Length: > PhoneMaxLength })
        {
            throw new ArgumentException($"Phone must be at most {PhoneMaxLength} characters.", nameof(phone));
        }

        FullName = name;
        Phone = normalizedPhone;
    }
}
