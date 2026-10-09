using MyMIS.Api.Exceptions;

namespace MyMIS.Api.Helpers;

// Maps each unique index in the database to the request field it belongs to and the
// message a client should see when a save violates it. Used by
// UniqueViolationExceptionFilter to turn a database unique violation into a 409.
// When you add a unique index, add its entry here too.
public static class UniqueConstraints
{
  public static IReadOnlyDictionary<string, (string Field, string Message)> ByName { get; } =
    new Dictionary<string, (string Field, string Message)>
    {
      // Employees
      ["IX_Employees_Username"] = ("Username", DuplicateUsernameException.DefaultMessage),
      ["IX_EmployeeEmails_Email"] = ("Email", "This email address is already in use."),
      ["IX_EmployeePhones_Phone_Number_Mobile"] = ("Phone", "This mobile number is already in use."),

      // Organization
      ["IX_Offices_NormalizedName"] = ("Name", "An office with this name already exists."),
      ["IX_Positions_DepartmentId_Slug"] = ("Slug", "This department already has a position with this slug."),

      // HMO
      ["IX_HmoProviders_Code"] = ("Code", "An HMO provider with this code already exists."),
      ["IX_HmoProviders_NormalizedName"] = ("Name", "An HMO provider with this name already exists."),
      ["IX_HmoPlans_HmoProviderId_NormalizedName"] = ("Name", "This HMO provider already has a plan with this name."),

      // Locations
      ["IX_Cities_RegionId_Name"] = ("Name", "A city with this name already exists in this region."),
      ["IX_Barangays_CityId_Name"] = ("Name", "A barangay with this name already exists in this city."),

      // One primary per employee
      ["IX_EmergencyContacts_EmployeeId_Primary"] = ("IsPrimary", "The primary emergency contact changed while saving. Please try again."),
      ["IX_EmployeeAddresses_EmployeeId_Primary"] = ("IsPrimary", "The primary address changed while saving. Please try again."),
      ["IX_EmployeePhones_EmployeeId_Primary"] = ("IsPrimary", "The primary phone number changed while saving. Please try again."),
      ["IX_EmployeeEmails_EmployeeId_Primary"] = ("IsPrimary", "The primary email changed while saving. Please try again."),
    };
}