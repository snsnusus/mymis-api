using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MyMIS.Api.Helpers;

public static class DbUpdateExceptionExtensions
{
  // Returns the name of the unique index that was violated (e.g. "IX_Offices_NormalizedName"),
  // or null if this DbUpdateException was caused by something else.
  public static string? GetUniqueViolationConstraint(this DbUpdateException ex)
  {
    if (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
    {
      return pg.ConstraintName;
    }

    return null;
  }
}