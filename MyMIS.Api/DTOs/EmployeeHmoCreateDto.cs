using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class EmployeeHmoCreateDto : EmployeeHmoFieldsDto
{
  // Optional. An empty list (or omitted) means no dependents.
  public List<EmployeeHmoDependentCreateDto> Dependents { get; set; } = [];

  public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    foreach (var result in base.Validate(validationContext))
    {
      yield return result;
    }

    var dependents = Dependents ?? [];

    var seenMemberNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < dependents.Count; index++)
    {
      var memberNumber = dependents[index].MemberNumber?.Trim();
      if (string.IsNullOrEmpty(memberNumber))
      {
        continue; // optional field; blanks never clash
      }

      if (!seenMemberNumbers.Add(memberNumber))
      {
        yield return new ValidationResult(
            "Another dependent in this request already uses this member number.",
            [$"{nameof(Dependents)}[{index}].{nameof(EmployeeHmoDependentCreateDto.MemberNumber)}"]);
      }
    }
  }
}