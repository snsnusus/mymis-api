namespace MyMIS.Api.DTOs;

public class PhoneResponseDto
{
  public string CountryCode { get; set; } = string.Empty;   // "PH"

  public string DialCode { get; set; } = string.Empty;      // "+63"

  public string International { get; set; } = string.Empty; // "+639361231234"

  public string Local { get; set; } = string.Empty;         // "09361231234"

  public string Formatted { get; set; } = string.Empty;     // "(+63) 936 123 1234"
}