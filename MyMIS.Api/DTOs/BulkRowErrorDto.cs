using System.Text.Json;

namespace MyMIS.Api.DTOs;

public class BulkRowErrorDto
{
  // 1-based, so it matches how a person counts rows in the text area.
  public int Row { get; set; }

  public List<string> Errors { get; set; } = [];

  // The original row, echoed back so the portal can show exactly what was skipped.
  public JsonElement Data { get; set; }
}