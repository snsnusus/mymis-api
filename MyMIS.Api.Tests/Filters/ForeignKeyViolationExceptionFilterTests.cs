using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyMIS.Api.Filters;
using Npgsql;

namespace MyMIS.Api.Tests.Filters;

// The unit tests cover the 400 path and the cases it must ignore, 
// while the 409 path needs a real delete
public class ForeignKeyViolationExceptionFilterTests
{
  private static ExceptionContext NewContext(Exception exception) =>
    new(
      new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
      new List<IFilterMetadata>())
    {
      Exception = exception,
    };

  private static PostgresException NewPostgresException(string sqlState, string? constraintName) =>
    new(
      messageText: "test error",
      severity: "ERROR",
      invariantSeverity: "ERROR",
      sqlState: sqlState,
      constraintName: constraintName);

  private static ForeignKeyViolationExceptionFilter NewFilter() =>
    new(NullLogger<ForeignKeyViolationExceptionFilter>.Instance);

  [Fact]
  public void OnException_NotADbUpdateException_IsLeftUntouched()
  {
    // Arrange
    var context = NewContext(new InvalidOperationException("boom"));

    // Act
    NewFilter().OnException(context);

    // Assert
    Assert.Null(context.Result);
    Assert.False(context.ExceptionHandled);
  }

  [Fact]
  public void OnException_UniqueViolation_IsLeftForTheOtherFilter()
  {
    // Arrange
    var exception = new DbUpdateException(
      "Could not save.",
      NewPostgresException(PostgresErrorCodes.UniqueViolation, "IX_Offices_NormalizedName"));
    var context = NewContext(exception);

    // Act
    NewFilter().OnException(context);

    // Assert
    Assert.Null(context.Result);
    Assert.False(context.ExceptionHandled);
  }

  [Fact]
  public void OnException_ForeignKeyViolationThatIsNotADelete_Returns400()
  {
    // Arrange: no deleted entry, the same as an insert or update that points at a missing row
    var exception = new DbUpdateException(
      "Could not save.",
      NewPostgresException(PostgresErrorCodes.ForeignKeyViolation, "FK_Barangays_Cities_CityId"));
    var context = NewContext(exception);

    // Act
    NewFilter().OnException(context);

    // Assert
    var result = Assert.IsType<BadRequestObjectResult>(context.Result);
    Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    Assert.True(context.ExceptionHandled);

    var problem = Assert.IsType<ProblemDetails>(result.Value);
    Assert.Equal(400, problem.Status);
    Assert.Equal(
      ForeignKeyViolationExceptionFilter.MissingReferenceMessage,
      (string?)problem.Extensions["message"]);
  }
}