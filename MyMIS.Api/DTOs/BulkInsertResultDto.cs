namespace MyMIS.Api.DTOs;

public class BulkInsertResultDto
{
  public int Total { get; set; }
  public int Inserted { get; set; }

  // Computed properties: no setter, derived from the values above.
  public int Failed => Total - Inserted;
  public string Message => $"Inserted {Inserted} out of {Total} successfully.";

  public List<BulkRowErrorDto> Errors { get; set; } = [];
}