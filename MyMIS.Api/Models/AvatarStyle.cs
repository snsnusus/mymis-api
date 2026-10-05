using System.Text.Json.Serialization;

namespace MyMIS.Api.Models;

public enum AvatarStyle
{
  // JSON names match DiceBear's style names (@dicebear/styles/<name>.json),
  // so the portal can use the value directly.
  [JsonStringEnumMemberName("avataaars")]
  Avataaars,

  [JsonStringEnumMemberName("bottts")]
  Bottts,

  [JsonStringEnumMemberName("constellation")]
  Constellation,
}