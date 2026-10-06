using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;


public class PhoneDto
{
  [Required]
  [RegularExpression(@"^[A-Za-z]{2}$", ErrorMessage = "CountryCode must be a 2-letter ISO country code.")]
  public string CountryCode { get; set; } = string.Empty;

  [Required]
  [RegularExpression(@"^\+[1-9]\d{6,14}$", ErrorMessage = "Number must be in E.164 format, e.g. +639171234567.")]
  public string Number { get; set; } = string.Empty;
}