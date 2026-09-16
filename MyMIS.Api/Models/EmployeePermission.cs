namespace MyMIS.Api.Models;

public class EmployeePermission
{
    public int EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    public int PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}