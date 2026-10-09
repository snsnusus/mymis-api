using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Helpers;
using Npgsql;

namespace MyMIS.Api.Tests.Helpers;

public class DbUpdateExceptionExtensionsTests
{
  private static PostgresException NewPostgresException(string sqlState, string? constraintName) =>
    new(
      messageText: "test error",
      severity: "ERROR",
      invariantSeverity: "ERROR",
      sqlState: sqlState,
      constraintName: constraintName);

  [Fact]
  public void GetUniqueViolationConstraint_UniqueViolation_ReturnsTheConstraintName()
  {
    // Arrange
    var exception = new DbUpdateException(
      "Could not save.",
      NewPostgresException(PostgresErrorCodes.UniqueViolation, "IX_Offices_NormalizedName"));

    // Act
    var constraint = exception.GetUniqueViolationConstraint();

    // Assert
    Assert.Equal("IX_Offices_NormalizedName", constraint);
  }

  [Fact]
  public void GetUniqueViolationConstraint_OtherPostgresError_ReturnsNull()
  {
    // Arrange
    var exception = new DbUpdateException(
      "Could not save.",
      NewPostgresException(PostgresErrorCodes.ForeignKeyViolation, "FK_Barangays_Cities_CityId"));

    // Act + Assert
    Assert.Null(exception.GetUniqueViolationConstraint());
  }

  [Fact]
  public void GetUniqueViolationConstraint_InnerExceptionIsNotPostgres_ReturnsNull()
  {
    // Arrange
    var exception = new DbUpdateException("Could not save.", new Exception("not postgres"));

    // Act + Assert
    Assert.Null(exception.GetUniqueViolationConstraint());
  }

  [Fact]
  public void GetUniqueViolationConstraint_NoInnerException_ReturnsNull()
  {
    // Arrange
    var exception = new DbUpdateException("Could not save.");

    // Act + Assert
    Assert.Null(exception.GetUniqueViolationConstraint());
  }
}