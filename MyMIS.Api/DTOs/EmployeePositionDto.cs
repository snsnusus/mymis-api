namespace MyMIS.Api.DTOs;

public class EmployeePositionDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public bool IsApprover { get; set; }
}