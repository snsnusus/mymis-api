using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Filters;

// Turns a database unique-index violation thrown from any controller action into a 409,
// instead of letting it surface as a 500. Every other exception passes through untouched.
public class UniqueViolationExceptionFilter(ILogger<UniqueViolationExceptionFilter> logger) : IExceptionFilter
{
  private const string GenericMessage = "This conflicts with an existing record.";

  private readonly ILogger<UniqueViolationExceptionFilter> _logger = logger;

  public void OnException(ExceptionContext context)
  {
    if (context.Exception is not DbUpdateException dbException)
    {
      return;
    }

    var constraint = dbException.GetUniqueViolationConstraint();
    if (constraint is null)
    {
      return;
    }

    if (!UniqueConstraints.ByName.TryGetValue(constraint, out var entry))
    {
      _logger.LogWarning(
        "Unique constraint {Constraint} has no entry in UniqueConstraints; returning a generic 409.",
        constraint);

      entry = (string.Empty, GenericMessage);
    }

    var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
    {
      [entry.Field] = [entry.Message],
    })
    {
      Status = StatusCodes.Status409Conflict,
      Title = "A record with this value already exists.",
    };

    // Lets clients that read `message` (the { message } convention) show the same text.
    problem.Extensions["message"] = entry.Message;

    context.Result = new ConflictObjectResult(problem);
    context.ExceptionHandled = true;
  }
}