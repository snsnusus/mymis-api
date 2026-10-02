using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class HmoPlanCoverageGroupDto
{
  [Required, MaxLength(100)]
  public string Category { get; set; } = string.Empty;

  [Required, MinLength(1)]
  public List<HmoPlanCoverageItemDto> Items { get; set; } = [];
}