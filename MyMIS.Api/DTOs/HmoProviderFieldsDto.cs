using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public abstract class HmoProviderFieldsDto : IValidatableObject
{
  [Required, MaxLength(20)]
  public string Code { get; set; } = string.Empty;

  [Required, MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(150)]
  public string? AccountManagerName { get; set; }

  [MaxLength(50)]
  public string? Hotline { get; set; }

  [MaxLength(254), EmailAddress]
  public string? SupportEmail { get; set; }

  [MaxLength(500), Url]
  public string? WebsiteUrl { get; set; }

  [Required]
  public DateOnly? ContractStartDate { get; set; }

  [Required]
  public DateOnly? ContractEndDate { get; set; }

  public bool IsActive { get; set; } = true;

  public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    if (ContractStartDate is not null &&
        ContractEndDate is not null &&
        ContractEndDate <= ContractStartDate)
    {
      yield return new ValidationResult(
          "Contract end date must be after the start date.",
          [nameof(ContractEndDate)]);
    }
  }
}