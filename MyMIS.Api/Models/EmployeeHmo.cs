namespace MyMIS.Api.Models;

public class EmployeeHmo
{
  public int Id { get; set; }

  public int EmployeeId { get; set; }

  public int HmoProviderId { get; set; }

  public HmoProvider HmoProvider { get; set; } = null!;

  public int HmoPlanId { get; set; }

  public HmoPlan HmoPlan { get; set; } = null!;

  public string MemberNumber { get; set; } = string.Empty;

  public DateOnly EffectiveDate { get; set; }

  public DateOnly? EndDate { get; set; }

  public ICollection<EmployeeHmoDependent> Dependents { get; set; } = [];

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public DateTime? DeletedAt { get; set; }
}