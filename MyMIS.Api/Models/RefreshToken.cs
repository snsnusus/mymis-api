namespace MyMIS.Api.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public required string TokenHash { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}