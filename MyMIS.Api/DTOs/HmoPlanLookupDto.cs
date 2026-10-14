namespace MyMIS.Api.DTOs;

// One plan option in the employee HMO plan dropdown.
public class HmoPlanLookupDto
{
  public int Id { get; set; }

  public string Name { get; set; } = string.Empty;

  // Lets the portal filter the dependents' plan selects without another request.
  public bool AllowDependents { get; set; }
}