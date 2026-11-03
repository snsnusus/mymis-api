using Amazon.S3.Model;
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
  public DbSet<EmployeePersonalDetail> EmployeePersonalDetails => Set<EmployeePersonalDetail>();
  public DbSet<EmployeeAddress> EmployeeAddresses => Set<EmployeeAddress>();
  public DbSet<EmployeeEmail> EmployeeEmails => Set<EmployeeEmail>();
  public DbSet<EmployeeGovernmentId> EmployeeGovernmentIds => Set<EmployeeGovernmentId>();
  public DbSet<EmployeeHobby> EmployeeHobbies => Set<EmployeeHobby>();
  public DbSet<EmployeeHmo> EmployeeHmos => Set<EmployeeHmo>();
  public DbSet<EmployeeHmoDependent> EmployeeHmoDependents => Set<EmployeeHmoDependent>();
  public DbSet<EmployeePhone> EmployeePhones => Set<EmployeePhone>();
  public DbSet<HmoPlanCoverage> HmoPlanCoverages => Set<HmoPlanCoverage>();
  public DbSet<HmoPlan> HmoPlans => Set<HmoPlan>();
  public DbSet<HmoProvider> HmoProviders => Set<HmoProvider>();
  public DbSet<Hobby> Hobbies => Set<Hobby>();
  public DbSet<Office> Offices => Set<Office>();
  public DbSet<Permission> Permissions => Set<Permission>();
  public DbSet<Position> Positions => Set<Position>();
  public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
  public DbSet<Region> Regions => Set<Region>();
  public DbSet<Role> Roles => Set<Role>();
  public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Department>(entity =>
    {
      entity.HasOne(d => d.PrimaryContact)
        .WithMany()
        .HasForeignKey(d => d.PrimaryContactId)
        .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(d => d.SecondaryContact)
        .WithMany()
        .HasForeignKey(d => d.SecondaryContactId)
        .OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<Employee>(entity =>
    {
      entity.HasQueryFilter(e => e.DeletedAt == null);

      entity.Property(e => e.AvatarStyle)
        .HasConversion<string>()
        .HasMaxLength(20);

      entity.HasIndex(e => e.Username, "IX_Employees_Username")
        .IsUnique()
        .HasFilter("\"DeletedAt\" IS NULL");

      entity.HasIndex(e => e.EmployeeCode)
        .IsUnique();

      entity.HasOne(e => e.PersonalDetail)
        .WithOne(pd => pd.Employee)
        .HasForeignKey<EmployeePersonalDetail>(pd => pd.EmployeeId);

      entity.HasOne(e => e.Position)
        .WithMany()
        .HasForeignKey(e => e.PositionId)
        .OnDelete(DeleteBehavior.SetNull);

      entity.Property(e => e.EmployeeType)
        .HasConversion<string>()
        .HasMaxLength(20);

      entity.Property(e => e.EmploymentStatus)
        .HasConversion<string>()
        .HasMaxLength(20);

      entity.HasOne(e => e.Role)
        .WithMany()
        .HasForeignKey(e => e.RoleId)
        .OnDelete(DeleteBehavior.Restrict);
    });

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

    modelBuilder.Entity<EmployeeHobby>(entity =>
    {
      entity.HasKey(eh => new { eh.EmployeeId, eh.HobbyId });

      entity.HasOne(eh => eh.Employee)
        .WithMany(e => e.EmployeeHobbies)
        .HasForeignKey(eh => eh.EmployeeId);

      entity.HasOne(eh => eh.Hobby)
        .WithMany(h => h.EmployeeHobbies)
        .HasForeignKey(eh => eh.HobbyId);
    });

    modelBuilder.Entity<Permission>()
      .HasIndex(p => p.Name)
      .IsUnique();

    modelBuilder.Entity<Position>(entity =>
    {
      entity.HasOne(p => p.Department)
        .WithMany()
        .HasForeignKey(p => p.DepartmentId)
        .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(p => new { p.DepartmentId, p.Slug }).IsUnique();
    });

    modelBuilder.Entity<Hobby>()
      .HasIndex(h => h.NormalizedName)
      .IsUnique();

    modelBuilder.Entity<City>(entity =>
    {
      entity.HasOne(c => c.Region)
        .WithMany()
        .HasForeignKey(c => c.RegionId)
        .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(c => new { c.RegionId, c.Name })
        .IsUnique();
    });

    modelBuilder.Entity<Barangay>(entity =>
    {
      entity.HasOne(b => b.City)
        .WithMany()
        .HasForeignKey(b => b.CityId)
        .OnDelete(DeleteBehavior.Restrict);

      entity.HasIndex(b => new { b.CityId, b.Name })
        .IsUnique();
    });

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

    modelBuilder.Entity<EmployeeHmo>(entity =>
    {
      entity.HasOne<Employee>()
        .WithMany(e => e.HmoEnrollments)
        .HasForeignKey(h => h.EmployeeId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(h => h.HmoProvider)
        .WithMany()
        .HasForeignKey(h => h.HmoProviderId)
        .OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(h => h.HmoPlan)
        .WithMany()
        .HasForeignKey(h => h.HmoPlanId)
        .OnDelete(DeleteBehavior.Restrict);

      entity.Property(h => h.MemberNumber)
        .HasMaxLength(50);

      entity.HasIndex(h => h.EmployeeId, "IX_EmployeeHmos_EmployeeId");

      entity.HasIndex(h => h.EmployeeId, "IX_EmployeeHmos_EmployeeId_Active")
        .IsUnique()
        .HasFilter("\"DeletedAt\" IS NULL");

      entity.HasIndex(h => new { h.HmoProviderId, h.MemberNumber }, "IX_EmployeeHmos_HmoProviderId_MemberNumber")
        .IsUnique()
        .HasFilter("\"DeletedAt\" IS NULL");

      entity.HasQueryFilter(h => h.DeletedAt == null);
    });

    modelBuilder.Entity<EmployeeHmoDependent>(entity =>
    {
      entity.HasOne<EmployeeHmo>()
        .WithMany(h => h.Dependents)
        .HasForeignKey(d => d.EmployeeHmoId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(d => d.HmoPlan)
        .WithMany()
        .HasForeignKey(d => d.HmoPlanId)
        .OnDelete(DeleteBehavior.Restrict);

      entity.Property(d => d.FirstName).HasMaxLength(100);

      entity.Property(d => d.MiddleName).HasMaxLength(100);

      entity.Property(d => d.LastName).HasMaxLength(100);

      entity.Property(d => d.Suffix).HasMaxLength(20);

      entity.Property(d => d.MemberNumber).HasMaxLength(50);

      entity.Property(d => d.Relationship).HasConversion<string>().HasMaxLength(20);

      entity.HasQueryFilter(d => d.DeletedAt == null);
    });

    modelBuilder.Entity<EmployeeGovernmentId>(entity =>
    {
      entity.HasQueryFilter(g => g.DeletedAt == null);

      // 1:1 — the dependent side (this table) holds the FK.
      entity.HasOne(g => g.Employee)
        .WithOne(e => e.GovernmentId)
        .HasForeignKey<EmployeeGovernmentId>(g => g.EmployeeId)
        .OnDelete(DeleteBehavior.Cascade);

      // One partial unique index per number: uniqueness only among active rows.
      entity.HasIndex(g => g.SssNumber).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
      entity.HasIndex(g => g.PhilHealthNumber).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
      entity.HasIndex(g => g.PagIbigNumber).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
      entity.HasIndex(g => g.TinNumber).IsUnique().HasFilter("\"DeletedAt\" IS NULL");

      entity.HasOne(g => g.UpdatedBy)
        .WithMany()
        .HasForeignKey(g => g.UpdatedById)
        .OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<Role>()
      .HasIndex(r => r.NormalizedName)
      .IsUnique();

    modelBuilder.Entity<RolePermission>(entity =>
    {
      entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

      entity.HasOne(rp => rp.Role)
        .WithMany(r => r.RolePermissions)
        .HasForeignKey(rp => rp.RoleId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(rp => rp.Permission)
        .WithMany()
        .HasForeignKey(rp => rp.PermissionId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.Property(rp => rp.Scope)
        .HasConversion<string>()
        .HasMaxLength(20);
    });


  }
}