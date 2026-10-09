using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Filters;

// Turns a database foreign-key violation thrown from any controller action into a clean response
// instead of a 500. Postgres uses the same error for two different mistakes, so the filter tells them apart:
//   - a delete blocked because other rows still point at the row  -> 409 "still in use"
//   - an insert or update that points at a row that doesn't exist -> 400 "related record missing"
// Every other exception passes through untouched.
public class ForeignKeyViolationExceptionFilter(ILogger<ForeignKeyViolationExceptionFilter> logger) : IExceptionFilter
{
  public const string InUseMessage = "This record is still in use by other records, so it cannot be deleted.";

  public const string MissingReferenceMessage =
    "A related record you selected does not exist. It may have been deleted, so please refresh and try again.";

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

    // The entities that failed to save tell the two cases apart, without reading Postgres'
    // error text (which can change with the server's language).
    var isDelete = dbException.Entries.Any(entry => entry.State == EntityState.Deleted);

    if (isDelete)
    {
      _logger.LogInformation("Delete blocked by foreign key {Constraint}.", constraint);

      context.Result = new ConflictObjectResult(
        NewProblem(StatusCodes.Status409Conflict, "This record is still in use.", InUseMessage));
    }
    else
    {
      // A warning, not information: if a server-built reference ever breaks, this line shows which one.
      _logger.LogWarning(
        "Save rejected: foreign key {Constraint} points at a row that does not exist.", constraint);

      context.Result = new BadRequestObjectResult(
        NewProblem(StatusCodes.Status400BadRequest, "A related record does not exist.", MissingReferenceMessage));
    }

    context.ExceptionHandled = true;
  }

  private static ProblemDetails NewProblem(int status, string title, string message)
  {
    var problem = new ProblemDetails { Status = status, Title = title, Detail = message };

    // Same { message } convention as the unique-violation 409, so clients read it one way.
    problem.Extensions["message"] = message;
    return problem;
  }
}