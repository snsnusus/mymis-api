namespace MyMIS.Api.DTOs;

// Lean shape for GET /api/Departments (the list cards).
public class DepartmentSummaryResponseDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Slug { get; set; } = string.Empty;
  public string? Description { get; set; }
  public string Status { get; set; } = string.Empty;
  public string? CoverImageUrl { get; set; }
  public string? PrimaryContactName { get; set; }
  public string? SecondaryContactName { get; set; }
  public int EmployeeCount { get; set; }
}