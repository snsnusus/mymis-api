namespace MyMIS.Api.DTOs;

// Full shape for GET /api/Departments/{id}, and the result of Create/Update.
public class DepartmentResponseDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Slug { get; set; } = string.Empty;
  public string? Description { get; set; }
  public string? CostCenterCode { get; set; }
  public string? CoverImageUrl { get; set; }
  public string Status { get; set; } = string.Empty;
  public DepartmentContactDto? PrimaryContact { get; set; }
  public DepartmentContactDto? SecondaryContact { get; set; }
  public int EmployeeCount { get; set; }
}