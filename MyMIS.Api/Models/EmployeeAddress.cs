namespace MyMIS.Api.Models;

public class EmployeeAddress
{
  public int Id { get; set; }

  public int EmployeeId { get; set; }

  public AddressType Type { get; set; }

  public Address Address { get; set; } = null!;

  public bool IsPrimary { get; set; }

  public bool ShowInProfile { get; set; } = true;

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public DateTime? DeletedAt { get; set; }
}