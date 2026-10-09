using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Filters;

// Turns "this row can't be deleted because other rows still point at it" (a database
// foreign-key violation on a delete) into a 409 instead of a 500. Every other exception
// passes through untouched.
public class ForeignKeyViolationExceptionFilter(ILogger<ForeignKeyViolationExceptionFilter> logger) : IExceptionFilter
{
  public const string InUseMessage = "This record is still in use by other records, so it cannot be deleted.";

  private readonly ILogger<ForeignKeyViolationExceptionFilter> _logger = logger;

  public void OnException(ExceptionContext context)
  {
    if (context.Exception is not DbUpdateException dbException)
    {
      return;
    }

    var constraint = dbException.GetForeignKeyViolationConstraint();
    if (constraint is null)
    {
      return;
    }

    // Postgres uses the same error code when an INSERT or UPDATE points at a row that doesn't
    // exist. That is bad input rather than a conflict, so it is deliberately left alone here.
    if (!dbException.Entries.Any(entry => entry.State == EntityState.Deleted))
    {
      return;
    }

    _logger.LogInformation("Delete blocked by foreign key {Constraint}.", constraint);

    var problem = new ProblemDetails
    {
      Status = StatusCodes.Status409Conflict,
      Title = "This record is still in use.",
      Detail = InUseMessage,
    };

    // Same { message } convention as the unique-violation 409, so clients read it one way.
    problem.Extensions["message"] = InUseMessage;

    context.Result = new ConflictObjectResult(problem);
    context.ExceptionHandled = true;
  }
}