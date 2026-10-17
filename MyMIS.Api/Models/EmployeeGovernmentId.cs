namespace MyMIS.Api.Models;

public class EmployeeGovernmentId
{
  public int Id { get; set; }

  public int EmployeeId { get; set; }

  public Employee Employee { get; set; } = null!;

  // Stored as digits only (no dashes). Format for display in the backend,
  // same as you did for phone numbers.
  public string? SssNumber { get; set; }

  public string? PhilHealthNumber { get; set; }

  public string? PagIbigNumber { get; set; }

  public string? TinNumber { get; set; }

  public int UpdatedById { get; set; }

  public Employee UpdatedBy { get; set; } = null!;

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public DateTime? DeletedAt { get; set; }

}