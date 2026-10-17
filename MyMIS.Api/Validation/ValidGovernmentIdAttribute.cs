using System.ComponentModel.DataAnnotations;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Validation;

public enum GovernmentIdType
{
  Sss,
  PhilHealth,
  PagIbig,
  Tin,
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidGovernmentIdAttribute(GovernmentIdType type) : ValidationAttribute
{
  public GovernmentIdType Type { get; } = type;

  public override bool IsValid(object? value)
  {
    // Not provided is fine: these fields are optional.
    if (value is null) return true;
    if (value is not string raw) return false;

    var digits = GovernmentIdRules.Normalize(raw);
    if (digits is null) return true;   // blank counts as "not provided"

    return Type switch
    {
      GovernmentIdType.Sss => GovernmentIdRules.IsValidSss(digits),
      GovernmentIdType.PhilHealth => GovernmentIdRules.IsValidPhilHealth(digits),
      GovernmentIdType.PagIbig => GovernmentIdRules.IsValidPagIbig(digits),
      GovernmentIdType.Tin => GovernmentIdRules.IsValidTin(digits),
      _ => false,
    };
  }

  public override string FormatErrorMessage(string name) => Type switch
  {
    GovernmentIdType.Sss => "SSS number must be 10 digits.",
    GovernmentIdType.PhilHealth => "PhilHealth number must be 12 digits.",
    GovernmentIdType.PagIbig => "Pag-IBIG number must be 12 digits.",
    GovernmentIdType.Tin => "TIN must be 9 or 12 digits.",
    _ => $"{name} is not valid.",
  };
}