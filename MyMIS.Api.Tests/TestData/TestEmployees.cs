using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.TestData;

// Minimal valid Employee for seeding tests. Only required fields are set.
internal static class TestEmployees
{
  public static Employee New(string firstName, string lastName, string employeeCode) => new()
  {
    FirstName = firstName,
    LastName = lastName,
    Gender = "MALE",
    MaritalStatus = "SINGLE",
    EmployeeCode = employeeCode,
    Username = employeeCode.ToLowerInvariant(),
    PasswordHash = "irrelevant-for-this-test",
  };
}