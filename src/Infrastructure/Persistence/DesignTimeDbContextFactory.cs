using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shop.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef`. Reads ConnectionStrings__Default from the environment; the fallback is a
/// credential-free LocalDB string so migrations can be scaffolded without any secret.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShopDbContext>
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=ShopMvp_Design;Trusted_Connection=True;TrustServerCertificate=True";

    public ShopDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? LocalDbFallback;
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connectionString).Options;
        return new ShopDbContext(options);
    }
}
