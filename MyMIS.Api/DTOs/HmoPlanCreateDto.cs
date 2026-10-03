using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class HmoPlanCreateDto : HmoPlanFieldsDto
{
  [Required]
  public int? HmoProviderId { get; set; }
}