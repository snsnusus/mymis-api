namespace MyMIS.Api.IntegrationTests.Infrastructure;

public static class TestPermissions
{
  // Every permission the migrations seed. When you add a new permission policy, add it here too.
  public static readonly string[] All =
  [
    "employees.create", "employees.update", "positions.create", "positions.update",
    "locations.manage", "offices.manage", "hmo.manage",
  ];
}