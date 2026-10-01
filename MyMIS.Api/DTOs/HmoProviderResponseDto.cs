namespace MyMIS.Api.DTOs;

public class HmoProviderResponseDto
{
  public int Id { get; set; }

  public string Code { get; set; } = string.Empty;

  public string Name { get; set; } = string.Empty;

  public string? AccountManagerName { get; set; }

  public string? Hotline { get; set; }

  public string? SupportEmail { get; set; }

  public string? WebsiteUrl { get; set; }

  public DateOnly ContractStartDate { get; set; }

  public DateOnly ContractEndDate { get; set; }

  public bool IsActive { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }
}