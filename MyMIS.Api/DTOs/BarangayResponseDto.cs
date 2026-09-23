namespace MyMIS.Api.DTOs;

public class BarangayResponseDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string? PsgcCode { get; set; }
  public string? ZipCode { get; set; }
  public int CityId { get; set; }
  public string CityName { get; set; } = string.Empty;
}