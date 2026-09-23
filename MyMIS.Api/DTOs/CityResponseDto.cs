namespace MyMIS.Api.DTOs;

public class CityResponseDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string? PsgcCode { get; set; }
  public int RegionId { get; set; }
  public string RegionName { get; set; } = string.Empty;
}