using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class RolePermissionResponseDto
{
  public string Permission { get; set; } = string.Empty;

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public PermissionScope Scope { get; set; }
}