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

  // Returns the name of the foreign key that was violated (e.g. "FK_Cities_Regions_RegionId"),
  // or null if this DbUpdateException was caused by something else.
  public static string? GetForeignKeyViolationConstraint(this DbUpdateException ex)
  {
    if (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } pg)
    {
      return pg.ConstraintName;
    }

    return null;
  }
}