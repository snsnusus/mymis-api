using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using PhoneNumbers;

namespace MyMIS.Api.Helpers;

public static class PhoneFormatter
{
  private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

  public static Phone? TryNormalize(PhoneDto dto)
  {
    var region = dto.CountryCode.Trim().ToUpperInvariant();

    try
    {
      var parsed = Util.Parse(dto.Number.Trim(), region);

      if (!Util.IsValidNumberForRegion(parsed, region))
      {
        return null;
      }

      return new Phone
      {
        CountryCode = region,
        Number = Util.Format(parsed, PhoneNumberFormat.E164),
      };
    }
    catch (NumberParseException)
    {
      return null;
    }
  }

  // Model → response. Derives every display format from the two stored fields.
  public static PhoneResponseDto ToResponse(Phone phone)
  {
    var parsed = Util.Parse(phone.Number, phone.CountryCode);

    var dialCode = $"+{parsed.CountryCode}";                                  // "+63"
    var international = Util.Format(parsed, PhoneNumberFormat.INTERNATIONAL); // "+63 936 123 1234"
    var national = Util.Format(parsed, PhoneNumberFormat.NATIONAL);           // "0936 123 1234"

    return new PhoneResponseDto
    {
      CountryCode = phone.CountryCode,
      DialCode = dialCode,
      International = phone.Number,
      Local = new string(national.Where(char.IsDigit).ToArray()),
      Formatted = $"({dialCode}){international[dialCode.Length..]}",
    };
  }

  public static PhoneLineType DetectLineType(Phone phone)
  {
    var parsed = Util.Parse(phone.Number, phone.CountryCode);
    var numberType = Util.GetNumberType(parsed);

    return numberType switch
    {
      PhoneNumberType.FIXED_LINE => PhoneLineType.Landline,
      // when in doubt, treat it as mobile, so the uniqueness rule applies
      _ => PhoneLineType.Mobile,
    };
  }
}