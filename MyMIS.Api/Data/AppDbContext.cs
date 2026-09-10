using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Models;

namespace MyMIS.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Resolve the two-FK-to-Employee ambiguity on Department
        modelBuilder.Entity<Department>()
            .HasOne(d => d.PrimaryContact)
            .WithMany()
            .HasForeignKey(d => d.PrimaryContactId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Department>()
            .HasOne(d => d.SecondaryContact)
            .WithMany()
            .HasForeignKey(d => d.SecondaryContactId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employee>()
            .HasQueryFilter(e => e.DeletedAt == null);

        modelBuilder.Entity<Employee>()
            .Property(e => e.Role)
            .HasConversion<string>()
            .HasDefaultValue(Role.User);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.TokenHash)
            .IsUnique();
    }
}