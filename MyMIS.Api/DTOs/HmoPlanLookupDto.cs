namespace MyMIS.Api.DTOs;

public class HmoPlanLookupDto
{
  public int Id { get; set; }

  public string Name { get; set; } = string.Empty;

  public bool AllowDependents { get; set; }
}