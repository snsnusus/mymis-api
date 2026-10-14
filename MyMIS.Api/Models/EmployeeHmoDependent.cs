namespace MyMIS.Api.Models;

public class EmployeeHmoDependent
{
  public int Id { get; set; }

  public int EmployeeHmoId { get; set; }

  public string FirstName { get; set; } = string.Empty;

  public string? MiddleName { get; set; }

  public string LastName { get; set; } = string.Empty;

  public string? Suffix { get; set; }

  public DateOnly Birthdate { get; set; }

  public HmoDependentRelationship Relationship { get; set; }

  public int HmoPlanId { get; set; }

  public HmoPlan HmoPlan { get; set; } = null!;

  public string? MemberNumber { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public DateTime? DeletedAt { get; set; }
}