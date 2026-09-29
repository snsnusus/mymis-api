namespace MyMIS.Api.DTOs;

public class OfficeResponseDto
{
  public int Id { get; set; }

  public string Name { get; set; } = string.Empty;

  public string City { get; set; } = string.Empty;

  public string CountryCode { get; set; } = string.Empty;

  public string? Address { get; set; }
}