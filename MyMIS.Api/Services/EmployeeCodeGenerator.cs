using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Services;

public class EmployeeCodeGenerator(AppDbContext context) : IEmployeeCodeGenerator
{
  private const string NextValueSql =
    "SELECT nextval('\"" + EmployeeCodeFormat.SequenceName + "\"') AS \"Value\"";

  private readonly AppDbContext _context = context;

  public async Task<string> GenerateAsync()
  {
    var number = await _context.Database
      .SqlQueryRaw<long>(NextValueSql)
      .SingleAsync();

    var year = EmployeeCodeFormat.GetManilaYear(DateTimeOffset.UtcNow);

    return EmployeeCodeFormat.Format(year, number);
  }
}