using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MyMIS.Api.DTOs;

namespace MyMIS.Api.Helpers;

public static class BulkRowParser
{
  public const int MaxRows = 1000;

  public static string? GetRowCountError(int rowCount)
  {
    if (rowCount == 0)
    {
      return "The payload must contain at least one row.";
    }

    if (rowCount > MaxRows)
    {
      return $"A single upload is limited to {MaxRows} rows.";
    }

    return null; // null = the count is fine
  }

  public static bool TryParse<T>(
      JsonElement raw,
      int rowNumber,
      [NotNullWhen(true)] out T? item,
      [NotNullWhen(false)] out BulkRowErrorDto? error)
      where T : class
  {
    item = null;
    error = null;

    // e.g. a stray number or string in the array: [ {...}, 42, {...} ]
    if (raw.ValueKind != JsonValueKind.Object)
    {
      error = new BulkRowErrorDto
      {
        Row = rowNumber,
        Data = raw,
        Errors = ["Row must be a JSON object."],
      };
      return false;
    }

    // Stage A: convert the raw JSON into the typed DTO.
    T? parsed;
    try
    {
      parsed = raw.Deserialize<T>(JsonSerializerOptions.Web);
    }
    catch (JsonException ex)
    {
      // e.g. "regionId": "abc" — the value can't become an int.
      error = new BulkRowErrorDto
      {
        Row = rowNumber,
        Data = raw,
        Errors = [$"Invalid value at '{ex.Path}'."],
      };
      return false;
    }

    if (parsed is null)
    {
      error = new BulkRowErrorDto
      {
        Row = rowNumber,
        Data = raw,
        Errors = ["Row could not be read."],
      };
      return false;
    }

    // Stage B: run the [Required]/[MaxLength] attributes on the DTO.
    var results = new List<ValidationResult>();
    var isValid = Validator.TryValidateObject(
        parsed,
        new ValidationContext(parsed),
        results,
        validateAllProperties: true);

    if (!isValid)
    {
      error = new BulkRowErrorDto
      {
        Row = rowNumber,
        Data = raw,
        Errors = results.Select(r => r.ErrorMessage ?? "Invalid value.").ToList(),
      };
      return false;
    }

    item = parsed;
    return true;
  }
}