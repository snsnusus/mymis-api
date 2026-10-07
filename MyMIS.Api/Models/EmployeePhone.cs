namespace MyMIS.Api.Models;

public class EmployeePhone
{
  public int Id { get; set; }

  public int EmployeeId { get; set; }

  public ContactOwnership Ownership { get; set; }

  public PhoneLineType LineType { get; set; }

  public Phone Phone { get; set; } = null!;

  public bool IsPrimary { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public DateTime? DeletedAt { get; set; }
}