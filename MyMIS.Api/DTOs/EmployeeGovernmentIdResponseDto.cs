namespace MyMIS.Api.DTOs;

public class EmployeeGovernmentIdResponseDto
{
  public string? SssNumber { get; set; }

  public string? PhilHealthNumber { get; set; }

  public string? PagIbigNumber { get; set; }

  public string? TinNumber { get; set; }

  public DateTime? UpdatedAt { get; set; }

  public string? UpdatedByName { get; set; }
}