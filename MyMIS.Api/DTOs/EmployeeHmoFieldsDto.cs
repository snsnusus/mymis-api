using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public abstract class EmployeeHmoFieldsDto : IValidatableObject
{
  [Required]
  public int? HmoPlanId { get; set; }

  [Required, MaxLength(50)]
  public string MemberNumber { get; set; } = string.Empty;

  [Required]
  public DateOnly? EffectiveDate { get; set; }

  public DateOnly? EndDate { get; set; }

  public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    if (EffectiveDate is not null &&
      EndDate is not null &&
      EndDate <= EffectiveDate)
    {
      yield return new ValidationResult(
          "End date must be after the effective date.",
          [nameof(EndDate)]);
    }
  }
}