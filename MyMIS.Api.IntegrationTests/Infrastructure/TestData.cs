using MyMIS.Api.Models;

namespace MyMIS.Api.IntegrationTests.Infrastructure;

// Small helpers that insert reference data straight into the database.
// Every call uses unique names, because all tests share one database.
public static class TestData
{
  public static string Unique() => Guid.NewGuid().ToString("N")[..8];

  public static Task<Department> SeedDepartmentAsync(this ApiFactory factory) =>
    factory.QueryDbAsync(async db =>
    {
      var suffix = Unique();
      var department = new Department { Name = $"Department {suffix}", Slug = $"D{suffix[..6]}", Status = "Active" };

      db.Departments.Add(department);
      await db.SaveChangesAsync();
      return department;
    });

  public static Task<Position> SeedPositionAsync(this ApiFactory factory, Department department, string? slug = null) =>
    factory.QueryDbAsync(async db =>
    {
      var position = new Position
      {
        Title = "Associate",
        Slug = slug ?? $"P{Unique()}",
        Description = "A position description.",
        SortOrder = 1,
        IsActive = true,
        IsApprover = false,
        DepartmentId = department.Id,
      };

      db.Positions.Add(position);
      await db.SaveChangesAsync();
      return position;
    });

  public static Task<Region> SeedRegionAsync(this ApiFactory factory) =>
    factory.QueryDbAsync(async db =>
    {
      var region = new Region { Name = $"Region {Unique()}" };

      db.Regions.Add(region);
      await db.SaveChangesAsync();
      return region;
    });

  public static Task<City> SeedCityAsync(this ApiFactory factory, Region region, string? name = null) =>
    factory.QueryDbAsync(async db =>
    {
      var city = new City { Name = name ?? $"City {Unique()}", RegionId = region.Id };

      db.Cities.Add(city);
      await db.SaveChangesAsync();
      return city;
    });

  public static Task<Barangay> SeedBarangayAsync(this ApiFactory factory, City city) =>
    factory.QueryDbAsync(async db =>
    {
      var barangay = new Barangay { Name = $"Barangay {Unique()}", CityId = city.Id };

      db.Barangays.Add(barangay);
      await db.SaveChangesAsync();
      return barangay;
    });

  // An emergency contact whose address uses the given barangay, so that barangay is "in use".
  public static Task SeedEmergencyContactInBarangayAsync(this ApiFactory factory, Employee employee, Barangay barangay) =>
    factory.ExecuteDbAsync(async db =>
    {
      db.EmergencyContacts.Add(new EmergencyContact
      {
        EmployeeId = employee.Id,
        FirstName = "Ana",
        LastName = "Santos",
        Relationship = EmergencyContactRelationship.Parent,
        Phone = new Phone { CountryCode = "PH", Number = "+639171234567" },
        Address = new Address { AddressLine1 = "123 Mabini St.", BarangayId = barangay.Id, PostalCode = "1105" },
        IsPrimary = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
      });

      await db.SaveChangesAsync();
    });
}