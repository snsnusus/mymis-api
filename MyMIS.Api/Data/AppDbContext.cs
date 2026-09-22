using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Models;

namespace MyMIS.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeHobby> EmployeeHobbies => Set<EmployeeHobby>();
    public DbSet<EmployeePersonalDetail> EmployeePersonalDetails => Set<EmployeePersonalDetail>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Hobby> Hobbies => Set<Hobby>();
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<EmployeePermission> EmployeePermissions { get; set; }
    public DbSet<Position> Positions => Set<Position>();

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

        // 1:1 — Employee <-> EmployeePersonalDetail
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.PersonalDetail)
            .WithOne(pd => pd.Employee)
            .HasForeignKey<EmployeePersonalDetail>(pd => pd.EmployeeId);

        // Many-to-many — Employee <-> Hobby, via the explicit EmployeeHobby join entity
        modelBuilder.Entity<EmployeeHobby>()
            .HasKey(eh => new { eh.EmployeeId, eh.HobbyId }); // composite PK — the pair together is the identity

        modelBuilder.Entity<EmployeeHobby>()
            .HasOne(eh => eh.Employee)
            .WithMany(e => e.EmployeeHobbies)
            .HasForeignKey(eh => eh.EmployeeId);

        modelBuilder.Entity<EmployeeHobby>()
            .HasOne(eh => eh.Hobby)
            .WithMany(h => h.EmployeeHobbies)
            .HasForeignKey(eh => eh.HobbyId);

        modelBuilder.Entity<EmployeePermission>()
            .HasKey(ep => new { ep.EmployeeId, ep.PermissionId });

        modelBuilder.Entity<EmployeePermission>()
            .HasOne(ep => ep.Employee)
            .WithMany(e => e.EmployeePermissions)
            .HasForeignKey(ep => ep.EmployeeId);

        modelBuilder.Entity<EmployeePermission>()
            .HasOne(ep => ep.Permission)
            .WithMany()
            .HasForeignKey(ep => ep.PermissionId);

        modelBuilder.Entity<Permission>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<Position>()
            .HasOne(p => p.Department)
            .WithMany()
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Position>()
            .HasIndex(p => new { p.DepartmentId, p.Slug }).IsUnique();

        modelBuilder.Entity<Hobby>()
            .HasIndex(h => h.NormalizedName)
            .IsUnique();
    }
}