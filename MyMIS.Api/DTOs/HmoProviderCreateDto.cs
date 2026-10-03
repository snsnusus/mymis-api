using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class HmoProviderCreateDto : HmoProviderFieldsDto
{
  public List<HmoPlanFieldsDto> Plans { get; set; } = [];

  public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    foreach (var result in base.Validate(validationContext))
    {
      yield return result;
    }

    var plans = Plans ?? [];
    var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < plans.Count; index++)
    {
      var name = plans[index].Name.Trim();
      if (name.Length > 0 && !seenNames.Add(name))
      {
        yield return new ValidationResult(
            "Another plan in this request already uses this name.",
            [$"{nameof(Plans)}[{index}].{nameof(HmoPlanFieldsDto.Name)}"]);
      }
    }
  }
}