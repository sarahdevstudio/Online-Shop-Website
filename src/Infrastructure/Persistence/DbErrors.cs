using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Shop.Infrastructure.Persistence;

internal static class DbErrors
{
    /// <summary>True when the save failed on the named unique index (the database is the final arbiter of uniqueness).</summary>
    public static bool IsUniqueViolation(DbUpdateException ex, string indexName) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 } sql
        && sql.Message.Contains(indexName, StringComparison.Ordinal);
}
