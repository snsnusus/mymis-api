namespace MyMIS.Api.DTOs;

// Deliberately tiny: just enough to render one row in an autocomplete
// dropdown (name, position, small avatar). Not the same type as
// EmployeeSummaryResponseDto, because the list table and the dropdown
// will grow in different directions.
public class EmployeeLookupDto
{
  public int Id { get; set; }

  public string FirstName { get; set; } = string.Empty;

  public string LastName { get; set; } = string.Empty;

  public string? PositionTitle { get; set; }

  public int? DepartmentId { get; set; }

  public string? AvatarUrl { get; set; }
}