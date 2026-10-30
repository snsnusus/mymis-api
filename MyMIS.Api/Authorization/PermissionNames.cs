namespace MyMIS.Api.Authorization;

// The catalog of every permission string in the system.
// A static class can't be instantiated (no `new PermissionNames()`);
// it's just a container for constants, like a frozen object in JS.
public static class PermissionNames
{
  // Nested static class = a namespace-like grouping per resource.
  // Usage: PermissionNames.Employees.Read
  public static class Employees
  {
    // `const` = a compile-time constant. The compiler copies the value
    // into every place that uses it. Attributes like
    // [Authorize(Policy = PermissionNames.Employees.Read)] ONLY accept
    // compile-time constants, which is why these must be `const`.
    public const string Read = "employees.read";
    public const string Create = "employees.create";
    public const string Update = "employees.update";
    public const string Delete = "employees.delete";
    public const string PrivateRead = "employees.private.read";
    public const string UpdateWork = "employees.update.work";
  }

  public static class Departments
  {
    public const string Read = "departments.read";
    public const string Create = "departments.create";
    public const string Update = "departments.update";
    public const string Delete = "departments.delete";
  }

  public static class Positions
  {
    public const string Read = "positions.read";
    public const string Create = "positions.create";
    public const string Update = "positions.update";
    public const string Delete = "positions.delete";
  }

  public static class Offices
  {
    public const string Read = "offices.read";
    public const string Create = "offices.create";
    public const string Update = "offices.update";
    public const string Delete = "offices.delete";
  }

  public static class Locations
  {
    public const string Read = "locations.read";
    public const string Create = "locations.create";
    public const string Update = "locations.update";
    public const string Delete = "locations.delete";
  }

  public static class Hmo
  {
    public const string Read = "hmo.read";
    public const string Create = "hmo.create";
    public const string Update = "hmo.update";
    public const string Delete = "hmo.delete";
  }

  public static class Roles
  {
    public const string Read = "roles.read";
    public const string Create = "roles.create";
    public const string Update = "roles.update";
    public const string Delete = "roles.delete";
    public const string Assign = "roles.assign";   // set an employee's role (Step 8)
  }

  // Every permission in one list, for seeding (Step 3) and for
  // registering policies in a loop (Step 5).
  // This can't be `const`: only primitives and strings can be const,
  // not collections. `static readonly` means "set once when the class
  // is first used, never reassigned".
  // `[ ... ]` is a collection expression (C# 12), the same idea as a JS array literal.
  public static readonly IReadOnlyList<string> All =
  [
    Employees.Read,
    Employees.Create,
    Employees.Update,
    Employees.Delete,
    Employees.PrivateRead,
    Employees.UpdateWork,
    Departments.Read,
    Departments.Create,
    Departments.Update,
    Departments.Delete,
    Positions.Read,
    Positions.Create,
    Positions.Update,
    Positions.Delete,
    Offices.Read,
    Offices.Create,
    Offices.Update,
    Offices.Delete,
    Locations.Read,
    Locations.Create,
    Locations.Update,
    Locations.Delete,
    Hmo.Read,
    Hmo.Create,
    Hmo.Update,
    Hmo.Delete,
    Roles.Read,
    Roles.Create,
    Roles.Update,
    Roles.Delete,
    Roles.Assign,
  ];
}