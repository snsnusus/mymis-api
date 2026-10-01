using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class Phone
{
  [Required, MaxLength(2)]
  public string CountryCode { get; set; } = string.Empty;

  [Required, MaxLength(16)]
  public string Number { get; set; } = string.Empty;
}