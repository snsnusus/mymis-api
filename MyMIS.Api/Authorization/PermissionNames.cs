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
    public const string WorkDetailsUpdate = "employees.work-details.update";
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

  public static class Roles
  {
    public const string Read = "roles.read";
    public const string Create = "roles.create";
    public const string Update = "roles.update";
    public const string Delete = "roles.delete";
    public const string Assign = "roles.assign";   // set an employee's role (Step 8)
  }

  public static class HmoProviders
  {
    public const string Read = "hmo-providers.read";
    public const string Create = "hmo-providers.create";
    public const string Update = "hmo-providers.update";
    public const string Delete = "hmo-providers.delete";
  }

  public static class HmoPlans
  {
    public const string Read = "hmo-plans.read";
    public const string Create = "hmo-plans.create";
    public const string Update = "hmo-plans.update";
    public const string Delete = "hmo-plans.delete";
  }

  // Every permission in one list, for seeding and for registering policies in a loop.
  // This can't be `const`: only primitives and strings can be const, not collections.
  // `static readonly` means "set once when the class is first used, never reassigned".
  // `[ ... ]` is a collection expression (C# 12), the same idea as a JS array literal.
  public static readonly IReadOnlyList<string> All =
  [
    Employees.Read,
    Employees.Create,
    Employees.Update,
    Employees.Delete,
    Employees.WorkDetailsUpdate,
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
    Roles.Read,
    Roles.Create,
    Roles.Update,
    Roles.Delete,
    Roles.Assign,
    HmoProviders.Read,
    HmoProviders.Create,
    HmoProviders.Update,
    HmoProviders.Delete,
    HmoPlans.Read,
    HmoPlans.Create,
    HmoPlans.Update,
    HmoPlans.Delete,
  ];

  // Permissions that can never be granted with Department scope (decision 7):
  // onboarding, creating departments, managing roles, and every delete.
  // The Roles API rejects them with Department scope, which is why the
  // endpoints behind them need no gate 2.
  //
  // A HashSet answers "is X in here?" in one step, like a JS Set.
  // IReadOnlySet lets callers check membership but not add or remove.
  public static readonly IReadOnlySet<string> CompanyOnly = new HashSet<string>
  {
    Employees.Create,
    Employees.Delete,
    Departments.Create,
    Departments.Delete,
    Positions.Delete,
    Offices.Delete,
    Locations.Delete,
    HmoProviders.Delete,
    HmoPlans.Delete,
    Roles.Read,
    Roles.Create,
    Roles.Update,
    Roles.Delete,
    Roles.Assign,
  };
}