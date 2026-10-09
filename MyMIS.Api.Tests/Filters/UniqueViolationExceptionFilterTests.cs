using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyMIS.Api.Filters;
using MyMIS.Api.Helpers;
using Npgsql;

namespace MyMIS.Api.Tests.Filters;

public class UniqueViolationExceptionFilterTests
{
  // ---------------- helpers ----------------

  // The context MVC hands to a filter when an action throws.
  private static ExceptionContext NewContext(Exception exception) =>
    new(
      new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
      new List<IFilterMetadata>())
    {
      Exception = exception,
    };

  // Npgsql's own exception type, built by hand: the only fields the filter reads are
  // SqlState (what kind of error) and ConstraintName (which index).
  private static PostgresException NewPostgresException(string sqlState, string? constraintName) =>
    new(
      messageText: "test error",
      severity: "ERROR",
      invariantSeverity: "ERROR",
      sqlState: sqlState,
      constraintName: constraintName);

  private static DbUpdateException NewUniqueViolation(string? constraintName) =>
    new("Could not save.", NewPostgresException(PostgresErrorCodes.UniqueViolation, constraintName));

  private static UniqueViolationExceptionFilter NewFilter(
    ILogger<UniqueViolationExceptionFilter>? logger = null) =>
    new(logger ?? NullLogger<UniqueViolationExceptionFilter>.Instance);

  // ---------------- exceptions the filter must ignore ----------------

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
  public void OnException_DbUpdateExceptionWithoutPostgresInnerException_IsLeftUntouched()
  {
    // Arrange
    var context = NewContext(new DbUpdateException("Could not save.", new Exception("not postgres")));

    // Act
    NewFilter().OnException(context);

    // Assert
    Assert.Null(context.Result);
    Assert.False(context.ExceptionHandled);
  }

  [Fact]
  public void OnException_PostgresErrorThatIsNotAUniqueViolation_IsLeftUntouched()
  {
    // Arrange: a foreign key violation (e.g. a nonexistent cityId) must stay a 500
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

  [Fact]
  public void OnException_UniqueViolationWithoutAConstraintName_IsLeftUntouched()
  {
    // Arrange
    var context = NewContext(NewUniqueViolation(constraintName: null));

    // Act
    NewFilter().OnException(context);

    // Assert
    Assert.Null(context.Result);
    Assert.False(context.ExceptionHandled);
  }

  // ---------------- unique violations it must turn into a 409 ----------------

  [Theory]
  [InlineData("IX_Offices_NormalizedName")]
  [InlineData("IX_Employees_Username")]
  [InlineData("IX_EmployeeEmails_EmployeeId_Primary")]
  public void OnException_KnownUniqueConstraint_Returns409WithItsFieldAndMessage(string constraint)
  {
    // Arrange: the expected field and message come from the dictionary itself,
    // so this test proves the wiring without repeating every sentence
    var expected = UniqueConstraints.ByName[constraint];
    var context = NewContext(NewUniqueViolation(constraint));

    // Act
    NewFilter().OnException(context);

    // Assert
    var conflict = Assert.IsType<ConflictObjectResult>(context.Result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.True(context.ExceptionHandled);

    var problem = Assert.IsType<ValidationProblemDetails>(conflict.Value);
    Assert.Equal(StatusCodes.Status409Conflict, problem.Status);

    var error = Assert.Single(problem.Errors);
    Assert.Equal(expected.Field, error.Key);
    Assert.Equal(expected.Message, Assert.Single(error.Value));

    // Clients that read { message } get the same text
    Assert.Equal(expected.Message, (string?)problem.Extensions["message"]);
  }

  [Fact]
  public void OnException_UnknownUniqueConstraint_Returns409WithGenericMessageAndLogsAWarning()
  {
    // Arrange
    var logger = new Mock<ILogger<UniqueViolationExceptionFilter>>();
    var context = NewContext(NewUniqueViolation("IX_Something_NotInTheDictionary"));

    // Act
    NewFilter(logger.Object).OnException(context);

    // Assert: still a 409, with the generic text and no field name
    var conflict = Assert.IsType<ConflictObjectResult>(context.Result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.True(context.ExceptionHandled);

    var problem = Assert.IsType<ValidationProblemDetails>(conflict.Value);
    var error = Assert.Single(problem.Errors);
    Assert.Equal(string.Empty, error.Key);
    Assert.Equal("This conflicts with an existing record.", Assert.Single(error.Value));

    // The warning is the reminder to add the missing dictionary entry
    logger.Verify(
      l => l.Log(
        LogLevel.Warning,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception?>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
      Times.Once);
  }
}