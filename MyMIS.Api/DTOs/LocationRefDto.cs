namespace MyMIS.Api.Dtos;

// A minimal { id, name } reference to one level of the location chain.
public class LocationRefDto
{
  public int Id { get; set; }

  public string Name { get; set; } = string.Empty;
}