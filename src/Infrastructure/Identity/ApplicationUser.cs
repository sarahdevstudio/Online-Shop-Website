using Microsoft.AspNetCore.Identity;

namespace Shop.Infrastructure.Identity;

/// <summary>Identity principal for both customers and admins. Roles decide what the user is.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string name) : base(name)
    {
    }
}
