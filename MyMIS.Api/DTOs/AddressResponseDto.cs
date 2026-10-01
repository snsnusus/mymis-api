namespace MyMIS.Api.Dtos;

// Response shape: the full location chain, each level as { id, name }.
public class AddressResponseDto
{
  public string AddressLine1 { get; set; } = string.Empty;

  public string? AddressLine2 { get; set; }

  public string PostalCode { get; set; } = string.Empty;

  public LocationRefDto Region { get; set; } = null!;

  public LocationRefDto City { get; set; } = null!;

  public LocationRefDto Barangay { get; set; } = null!;
}