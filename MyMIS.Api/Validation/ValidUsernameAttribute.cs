using System.ComponentModel.DataAnnotations;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidUsernameAttribute : ValidationAttribute
{
  public ValidUsernameAttribute() : base(UsernameRules.Description) { }

  public override bool IsValid(object? value)
  {
    // Missing/blank is [Required]'s job — don't double-report it.
    if (value is null) return true;
    if (value is not string raw) return false;
    if (string.IsNullOrWhiteSpace(raw)) return true;

    return UsernameRules.IsValid(UsernameRules.Normalize(raw));
  }
}