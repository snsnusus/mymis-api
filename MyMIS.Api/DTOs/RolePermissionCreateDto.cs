using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class RolePermissionCreateDto
{
  [Required]
  [MaxLength(100)]
  public string Permission { get; set; } = string.Empty;

  [Required]
  [EnumDataType(typeof(PermissionScope))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public PermissionScope? Scope { get; set; }
}