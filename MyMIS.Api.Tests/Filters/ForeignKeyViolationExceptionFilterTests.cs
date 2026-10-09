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

// These cover the cases the filter must NOT touch. The case it handles (a real delete
// blocked by a real foreign key) is covered by the integration tests, because it needs
// a real DbUpdateException that carries a real Deleted entry.
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
  public void OnException_ForeignKeyViolationWithoutADeletedEntry_IsLeftUntouched()
  {
    // Arrange: no entries at all, the same as an insert or update that points at a missing row.
    // That is bad input, not "still in use", so it must stay as it is.
    var exception = new DbUpdateException(
      "Could not save.",
      NewPostgresException(PostgresErrorCodes.ForeignKeyViolation, "FK_Barangays_Cities_CityId"));
    var context = NewContext(exception);

    // Act
    NewFilter().OnException(context);

    // Assert
    Assert.Null(context.Result);
    Assert.False(context.ExceptionHandled);
  }
}