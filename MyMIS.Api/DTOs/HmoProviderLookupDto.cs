namespace MyMIS.Api.DTOs;

// One provider option in the employee HMO provider dropdown, with its active plans.
public class HmoProviderLookupDto
{
  public int Id { get; set; }

  public string Name { get; set; } = string.Empty;

  // The portal uses these as the min/max of the effective and end date pickers.
  public DateOnly ContractStartDate { get; set; }

  public DateOnly ContractEndDate { get; set; }

  public List<HmoPlanLookupDto> Plans { get; set; } = [];
}