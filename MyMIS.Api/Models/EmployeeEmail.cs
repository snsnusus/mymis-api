namespace MyMIS.Api.Models;

public class EmployeeEmail
{
  public int Id { get; set; }

  public int EmployeeId { get; set; }

  public ContactOwnership Ownership { get; set; }

  public string Email { get; set; } = string.Empty;

  public bool IsPrimary { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public DateTime? DeletedAt { get; set; }
}