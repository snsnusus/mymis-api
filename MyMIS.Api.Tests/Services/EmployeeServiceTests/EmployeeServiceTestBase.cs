using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Moq;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Options;
using MyMIS.Api.Services;
using MyMIS.Api.Tests.TestData;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class EmployeeServiceTestBase : IDisposable
{
  protected readonly AppDbContext _context;
  protected readonly EmployeeService _service;
  protected readonly DateTime now = DateTime.UtcNow;

  protected EmployeeServiceTestBase()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;


    _context = new AppDbContext(options);

    var hobbyService = new HobbyService(_context);

    var s3Options = MSOptions.Create(new S3Options
    {
      AccessKey = "test-access-key",
      SecretKey = "test-secret-key",
      Region = "ap-southeast-1",
      BucketName = "test-bucket"
    });
    var mockS3Client = new Mock<IAmazonS3>();
    mockS3Client
        .Setup(client => client.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
        .Returns((GetPreSignedUrlRequest request) => $"https://fake-presigned-url.test/{request.Key}");
    var s3UploadService = new S3UploadService(mockS3Client.Object, s3Options);
    var codeCounter = 0;
    var mockCodeGenerator = new Mock<IEmployeeCodeGenerator>();
    mockCodeGenerator
        .Setup(generator => generator.GenerateAsync())
        .ReturnsAsync(() => $"MYMIS-TEST-{++codeCounter:D5}");

    _service = new EmployeeService(_context, hobbyService, s3UploadService, mockCodeGenerator.Object);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  protected static Employee NewEmployee(string firstName, string lastName, string employeeCode) => new()
  {
    FirstName = firstName,
    LastName = lastName,
    Gender = "MALE",
    MaritalStatus = "SINGLE",
    EmployeeCode = employeeCode,
    Username = employeeCode.ToLowerInvariant(),
    PasswordHash = "irrelevant-for-this-test",
  };

  protected static EmergencyContactCreateDto NewContactDto(bool isPrimary = false) => new()
  {
    FirstName = "Jose",
    LastName = "Reyes",
    Relationship = EmergencyContactRelationship.Spouse,
    Phone = new PhoneDto { CountryCode = "PH", Number = "+639171234567" },
    IsPrimary = isPrimary,
  };

  protected static EmployeeAddressCreateDto NewAddressDto(AddressType type, int barangayId, bool isPrimary = false) => new()
  {
    Type = type,
    Address = new AddressDto
    {
      AddressLine1 = "123 Mabini St.",
      BarangayId = barangayId,
      PostalCode = "1105",
    },
    IsPrimary = isPrimary,
  };

  protected static EmployeePhoneCreateDto NewPhoneDto(string number, bool isPrimary = false) => new()
  {
    Ownership = ContactOwnership.Personal,
    Phone = new PhoneDto { CountryCode = "PH", Number = number },
    IsPrimary = isPrimary,
  };

  protected static EmployeeEmailCreateDto NewEmailDto(
    string email,
    ContactOwnership ownership = ContactOwnership.Personal,
    bool isPrimary = false
  ) => new()
  {
    Ownership = ownership,
    Email = email,
    IsPrimary = isPrimary,
  };

  // Five employees, inserted out of order.
  // Sorted by last name: Bautista, Cruz, Garcia, Reyes, Santos.
  protected async Task SeedFiveEmployeesAsync()
  {
    _context.Employees.AddRange(
        NewEmployee("Maria", "Santos", "EMP-005"),
        NewEmployee("Ana", "Cruz", "EMP-002"),
        NewEmployee("Dina", "Garcia", "EMP-003"),
        NewEmployee("Carlo", "Bautista", "EMP-001"),
        NewEmployee("Ben", "Reyes", "EMP-004"));
    await _context.SaveChangesAsync();
  }

  protected async Task<Barangay> SeedBarangayAsync()
  {
    var barangay = new Barangay
    {
      Name = "Bagong Pag-asa",
      City = new City { Name = "Quezon City", Region = new Region { Name = "NCR" } },
    };
    _context.Barangays.Add(barangay);
    await _context.SaveChangesAsync();
    return barangay;
  }

}