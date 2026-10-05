using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmployeeAvatarStyleUpdateDto
{
  // Nullable + [Required]: a missing value becomes null, which [Required]
  // rejects with a 400, instead of silently defaulting to the first enum member.
  [Required]
  [EnumDataType(typeof(AvatarStyle))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public AvatarStyle? AvatarStyle { get; set; }
}