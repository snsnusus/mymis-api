using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmployeeHmoDependentCreateDto : IValidatableObject
{
  [Required, MaxLength(100)]
  public string FirstName { get; set; } = string.Empty;

  [MaxLength(100)]
  public string? MiddleName { get; set; }

  [Required, MaxLength(100)]
  public string LastName { get; set; } = string.Empty;

  [MaxLength(20)]
  public string? Suffix { get; set; }

  [Required, EnumDataType(typeof(HmoDependentRelationship))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoDependentRelationship? Relationship { get; set; }

  public int? HmoPlanId { get; set; }

  [MaxLength(50)]
  public string? MemberNumber { get; set; }

  [Required]
  public DateOnly? Birthdate { get; set; }

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (Birthdate > today)
    {
      yield return new ValidationResult(
          "Birthdate cannot be in the future.",
          [nameof(Birthdate)]);
    }
  }
}