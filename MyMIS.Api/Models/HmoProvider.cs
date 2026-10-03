using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class HmoProvider
{
  public int Id { get; set; }

  [Required, MaxLength(20)]
  public string Code { get; set; } = string.Empty;

  [Required, MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(150)]
  public string? AccountManagerName { get; set; }

  [MaxLength(50)]
  public string? Hotline { get; set; }

  [MaxLength(254)]
  public string? SupportEmail { get; set; }

  [MaxLength(500)]
  public string? WebsiteUrl { get; set; }

  public DateOnly ContractStartDate { get; set; }
  public DateOnly ContractEndDate { get; set; }

  public bool IsActive { get; set; } = true;

  public DateTime CreatedAt { get; set; }
  public DateTime UpdatedAt { get; set; }

  public List<HmoPlan> Plans { get; set; } = [];
}