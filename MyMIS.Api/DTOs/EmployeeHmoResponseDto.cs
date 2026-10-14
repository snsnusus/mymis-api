namespace MyMIS.Api.DTOs;

public class EmployeeHmoResponseDto
{
  public int Id { get; set; }

  public HmoRefDto Provider { get; set; } = null!;

  public HmoRefDto Plan { get; set; } = null!;

  public string MemberNumber { get; set; } = string.Empty;

  public DateOnly EffectiveDate { get; set; }

  public DateOnly? EndDate { get; set; }

  public List<EmployeeHmoDependentResponseDto> Dependents { get; set; } = [];
}