using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
  public DbSet<Barangay> Barangays => Set<Barangay>();
  public DbSet<City> Cities => Set<City>();
  public DbSet<Department> Departments => Set<Department>();
  public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
  public DbSet<Employee> Employees => Set<Employee>();
  public DbSet<EmployeePermission> EmployeePermissions { get; set; }
  public DbSet<EmployeePersonalDetail> EmployeePersonalDetails => Set<EmployeePersonalDetail>();
  public DbSet<EmployeeAddress> EmployeeAddresses => Set<EmployeeAddress>();
  public DbSet<EmployeeEmail> EmployeeEmails => Set<EmployeeEmail>();
  public DbSet<EmployeeHobby> EmployeeHobbies => Set<EmployeeHobby>();
  public DbSet<EmployeePhone> EmployeePhones => Set<EmployeePhone>();
  public DbSet<HmoPlanCoverage> HmoPlanCoverages => Set<HmoPlanCoverage>();
  public DbSet<HmoPlan> HmoPlans => Set<HmoPlan>();
  public DbSet<HmoProvider> HmoProviders => Set<HmoProvider>();
  public DbSet<Hobby> Hobbies => Set<Hobby>();
  public DbSet<Office> Offices => Set<Office>();
  public DbSet<Permission> Permissions { get; set; }
  public DbSet<Position> Positions => Set<Position>();
  public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
  public DbSet<Region> Regions => Set<Region>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

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

    modelBuilder.Entity<Employee>()
      .Property(e => e.AvatarStyle)
      .HasConversion<string>()
      .HasMaxLength(20);

    modelBuilder.Entity<Employee>()
    .HasIndex(e => e.Username)
    .IsUnique();

    modelBuilder.Entity<Employee>()
      .HasIndex(e => e.EmployeeCode)
      .IsUnique();

    modelBuilder.Entity<Employee>()
      .HasOne(e => e.PersonalDetail)
      .WithOne(pd => pd.Employee)
      .HasForeignKey<EmployeePersonalDetail>(pd => pd.EmployeeId);

    modelBuilder.Entity<Employee>()
      .HasOne(e => e.Position)
      .WithMany()
      .HasForeignKey(e => e.PositionId)
      .OnDelete(DeleteBehavior.SetNull);

    modelBuilder.Entity<Employee>()
      .Property(e => e.EmployeeType)
      .HasConversion<string>()
      .HasMaxLength(20);

    modelBuilder.Entity<Employee>()
      .Property(e => e.EmploymentStatus)
      .HasConversion<string>()
      .HasMaxLength(20);

    modelBuilder.Entity<EmployeeAddress>(entity =>
      {
        entity.HasOne<Employee>()
          .WithMany(e => e.Addresses)
          .HasForeignKey(a => a.EmployeeId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.Property(a => a.Type)
          .HasConversion<string>()
          .HasMaxLength(20);

        entity.OwnsOne(a => a.Address, address =>
          {
            address.HasOne(x => x.Barangay)
              .WithMany()
              .HasForeignKey(x => x.BarangayId)
              .OnDelete(DeleteBehavior.Restrict);
          });
        entity.Navigation(a => a.Address).IsRequired();

        entity.HasIndex(a => a.EmployeeId, "IX_EmployeeAddresses_EmployeeId");

        entity.HasIndex(a => a.EmployeeId, "IX_EmployeeAddresses_EmployeeId_Primary")
          .IsUnique()
          .HasFilter("\"IsPrimary\" = true AND \"DeletedAt\" IS NULL");

        entity.HasQueryFilter(a => a.DeletedAt == null);
      });

    modelBuilder.Entity<EmployeePhone>(entity =>
    {
      entity.HasOne<Employee>()
        .WithMany(e => e.Phones)
        .HasForeignKey(ep => ep.EmployeeId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.Property(ep => ep.LineType)
        .HasConversion<string>()
        .HasMaxLength(20);

      entity.Property(ep => ep.Ownership)
        .HasConversion<string>()
        .HasMaxLength(20);

      entity.OwnsOne(ep => ep.Phone, phone =>
      {
        phone.HasIndex(p => p.Number, "IX_EmployeePhones_Phone_Number_Mobile")
          .IsUnique()
          .HasFilter("\"LineType\" = 'Mobile' AND \"DeletedAt\" IS NULL");
      });
      entity.Navigation(ep => ep.Phone).IsRequired();

      entity.HasIndex(ep => ep.EmployeeId, "IX_EmployeePhones_EmployeeId");

      entity.HasIndex(ep => ep.EmployeeId, "IX_EmployeePhones_EmployeeId_Primary")
        .IsUnique()
        .HasFilter("\"IsPrimary\" = true AND \"DeletedAt\" IS NULL");

      entity.HasQueryFilter(ep => ep.DeletedAt == null);
    });

    modelBuilder.Entity<EmployeeEmail>(entity =>
    {
      entity.HasOne<Employee>()
        .WithMany(e => e.Emails)
        .HasForeignKey(ee => ee.EmployeeId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.Property(ee => ee.Ownership)
        .HasConversion<string>()
        .HasMaxLength(20);

      entity.Property(ee => ee.Email)
        .IsRequired()
        .HasMaxLength(254);

      entity.HasIndex(ee => ee.EmployeeId, "IX_EmployeeEmails_EmployeeId");

      entity.HasIndex(ee => ee.EmployeeId, "IX_EmployeeEmails_EmployeeId_Primary")
        .IsUnique()
        .HasFilter("\"IsPrimary\" = true AND \"DeletedAt\" IS NULL");

      entity.HasIndex(ee => ee.Email, "IX_EmployeeEmails_Email")
        .IsUnique()
        .HasFilter("\"DeletedAt\" IS NULL");

      entity.HasQueryFilter(ee => ee.DeletedAt == null);
    });

    modelBuilder.Entity<RefreshToken>()
      .HasIndex(r => r.TokenHash)
      .IsUnique();

    modelBuilder.Entity<EmployeeHobby>()
      .HasKey(eh => new { eh.EmployeeId, eh.HobbyId });

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

    modelBuilder.Entity<City>()
      .HasOne(c => c.Region)
      .WithMany()
      .HasForeignKey(c => c.RegionId)
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<City>()
      .HasIndex(c => new { c.RegionId, c.Name })
      .IsUnique();

    modelBuilder.Entity<Barangay>()
      .HasOne(b => b.City)
      .WithMany()
      .HasForeignKey(b => b.CityId)
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<Barangay>()
      .HasIndex(b => new { b.CityId, b.Name })
      .IsUnique();

    modelBuilder.Entity<Office>()
      .HasIndex(o => o.NormalizedName)
      .IsUnique();

    modelBuilder.Entity<EmergencyContact>(entity =>
      {
        entity.HasOne<Employee>()
          .WithMany(e => e.EmergencyContacts)
          .HasForeignKey(c => c.EmployeeId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.Property(c => c.Relationship)
          .HasConversion<string>()
          .HasMaxLength(20);

        entity.OwnsOne(c => c.Phone);
        entity.Navigation(c => c.Phone).IsRequired();

        entity.OwnsOne(c => c.Address, address =>
          {
            address.HasOne(a => a.Barangay)
              .WithMany()
              .HasForeignKey(a => a.BarangayId)
              .OnDelete(DeleteBehavior.Restrict);
          });

        entity.HasIndex(c => c.EmployeeId, "IX_EmergencyContacts_EmployeeId");

        entity.HasIndex(c => c.EmployeeId, "IX_EmergencyContacts_EmployeeId_Primary")
          .IsUnique()
          .HasFilter("\"IsPrimary\" = true AND \"DeletedAt\" IS NULL");

        entity.HasQueryFilter(c => c.DeletedAt == null);
      });

    modelBuilder.Entity<HmoProvider>(entity =>
      {
        entity.HasIndex(p => p.Code).IsUnique();
        entity.HasIndex(p => p.NormalizedName).IsUnique();
      });

    modelBuilder.Entity<HmoPlan>(entity =>
      {
        entity.HasOne(p => p.HmoProvider)
              .WithMany(provider => provider.Plans)
              .HasForeignKey(p => p.HmoProviderId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(p => new { p.HmoProviderId, p.NormalizedName }).IsUnique();

        entity.Property(p => p.Tier).HasConversion<string>().HasMaxLength(20);
        entity.Property(p => p.RoomType).HasConversion<string>().HasMaxLength(20);
        entity.Property(p => p.PremiumFrequency).HasConversion<string>().HasMaxLength(20);

        entity.Property(p => p.MaximumBenefitLimit).HasPrecision(12, 2);
        entity.Property(p => p.PremiumCost).HasPrecision(12, 2);
        entity.Property(p => p.PecLimit).HasPrecision(12, 2);
        entity.Property(p => p.DependentPremiumCost).HasPrecision(12, 2);
        entity.Property(p => p.EmployerSubsidyPercentage).HasPrecision(5, 2);
        entity.Property(p => p.DependentSubsidyPercentage).HasPrecision(5, 2);
      });

    modelBuilder.Entity<HmoPlanCoverage>(entity =>
      {
        entity.HasOne(c => c.HmoPlan)
          .WithMany(p => p.Coverages)
          .HasForeignKey(c => c.HmoPlanId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.Property(c => c.LimitAmount).HasPrecision(12, 2);
      });

    modelBuilder.HasSequence<long>(EmployeeCodeFormat.SequenceName)
      .StartsAt(1)
      .IncrementsBy(1);
  }
}