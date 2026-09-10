namespace MyMIS.Api.DTOs;

public class DepartmentResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    // Flattened, human-readable — not raw FK ints, same principle as EmployeeResponseDto.DepartmentName
    public string? PrimaryContactName { get; set; }
    public string? SecondaryContactName { get; set; }
    public int EmployeeCount { get; set; }
}