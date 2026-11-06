namespace MyMIS.Api.DTOs;

public class RoleSummaryResponseDto
{
  public int Id { get; set; }

  public string Name { get; set; } = string.Empty;

  public string? Description { get; set; }

  public bool IsSystem { get; set; }

  public bool IsCustom { get; set; }

  public int EmployeeCount { get; set; }
}