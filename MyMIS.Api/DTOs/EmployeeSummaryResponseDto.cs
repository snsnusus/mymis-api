namespace MyMIS.Api.DTOs;

public class EmployeeSummaryResponseDto
{
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string MiddleName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Suffix { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string? DepartmentName { get; set; }

    public string? AvatarUrl { get; set; }

    public string? PositionTitle { get; set; }
}