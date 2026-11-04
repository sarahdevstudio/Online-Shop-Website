using Microsoft.AspNetCore.Identity;

namespace Shop.Infrastructure.Identity;

/// <summary>Identity principal for both customers and admins. Roles decide what the user is.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Shown in the staff list and the audit; null for customers and accounts created before this field existed.</summary>
    public string? DisplayName { get; set; }

    /// <summary>A deactivated account cannot sign in and its sessions are revoked; the account and its audit trail stay.</summary>
    public bool IsActive { get; set; } = true;
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string name) : base(name)
    {
    }

    /// <summary>The Persian name shown in the roles editor; the built-in roles have none (the admin panel names them).</summary>
    public string? Title { get; set; }
}
