using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class HmoPlanSummaryDto
{
  public int Id { get; set; }

  public int HmoProviderId { get; set; }

  public string Name { get; set; } = string.Empty;

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoPlanTier Tier { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoRoomType RoomType { get; set; }

  public decimal MaximumBenefitLimit { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoPremiumFrequency PremiumFrequency { get; set; }

  public decimal PremiumCost { get; set; }

  public bool AllowDependents { get; set; }

  public bool IsActive { get; set; }
}