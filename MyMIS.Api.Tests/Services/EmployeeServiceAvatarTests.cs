using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using MyMIS.Api.Data;
using MyMIS.Api.Models;
using MyMIS.Api.Options;
using MyMIS.Api.Services;
using MyMIS.Api.Tests.TestData;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace MyMIS.Api.Tests.Services;

public class EmployeeServiceAvatarTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly EmployeeService _service;
  private readonly Mock<IAmazonS3> _s3 = new();
  private readonly Employee _juan;

  public EmployeeServiceAvatarTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);

    _s3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PutObjectResponse());

    _s3.Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
      .Returns((GetPreSignedUrlRequest request) => $"https://fake-presigned-url.test/{request.Key}");

    var s3Options = MsOptions.Create(new S3Options
    {
      AccessKey = "test-access-key",
      SecretKey = "test-secret-key",
      Region = "ap-southeast-1",
      BucketName = "test-bucket",
    });

    var s3UploadService = new S3UploadService(_s3.Object, s3Options);
    var hobbyService = new HobbyService(_context);
    var codeGenerator = new Mock<IEmployeeCodeGenerator>();

    _service = new EmployeeService(_context, hobbyService, s3UploadService, codeGenerator.Object);

    _juan = TestEmployees.New("Juan", "Cruz", "EMP-001");
    _context.Employees.Add(_juan);
    _context.SaveChanges();
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // ---------------- helpers ----------------

  private static FormFile NewFile(string fileName, string contentType)
  {
    var stream = new MemoryStream(new byte[16]);
    return new FormFile(stream, 0, stream.Length, "file", fileName)
    {
      Headers = new HeaderDictionary(),
      ContentType = contentType,
    };
  }

  private Task<Employee> SavedEmployeeAsync() =>
    _context.Employees.AsNoTracking().FirstAsync(e => e.Id == _juan.Id);

  private void VerifyUploadCalls(Times times) =>
    _s3.Verify(
      c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
      times);

  // ---------------- UpdateAvatarAsync ----------------

  [Fact]
  public async Task UpdateAvatarAsync_EmployeeDoesNotExist_ReturnsNullAndNeverCallsS3()
  {
    // Act
    var result = await _service.UpdateAvatarAsync(9999, NewFile("photo.png", "image/png"));

    // Assert
    Assert.Null(result);
    VerifyUploadCalls(Times.Never());
  }

  [Fact]
  public async Task UpdateAvatarAsync_ValidImage_SavesTheKeyAndReturnsAPresignedUrl()
  {
    // Act
    var url = await _service.UpdateAvatarAsync(_juan.Id, NewFile("photo.png", "image/png"));

    // Assert: the database stores the key, the caller gets a signed URL for it
    var expectedKey = $"avatars/{_juan.Id}.png";
    Assert.Equal($"https://fake-presigned-url.test/{expectedKey}", url);

    var saved = await SavedEmployeeAsync();
    Assert.Equal(expectedKey, saved.AvatarUrl);
    VerifyUploadCalls(Times.Once());
  }

  [Fact]
  public async Task UpdateAvatarAsync_UnsupportedFileType_ThrowsAndLeavesTheEmployeeUnchanged()
  {
    // Arrange
    _juan.AvatarUrl = "avatars/previous.png";
    await _context.SaveChangesAsync();

    // Act + Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
      () => _service.UpdateAvatarAsync(_juan.Id, NewFile("doc.pdf", "application/pdf")));

    var saved = await SavedEmployeeAsync();
    Assert.Equal("avatars/previous.png", saved.AvatarUrl);
    VerifyUploadCalls(Times.Never());
  }

  [Fact]
  public async Task UpdateAvatarAsync_S3UploadFails_DoesNotSaveTheKey()
  {
    // Arrange: S3 is down. Upload happens before the save, so the database must stay untouched
    // (an orphaned S3 file is harmless; a row pointing at a file that was never written is not).
    _s3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new AmazonS3Exception("upload failed"));

    // Act + Assert
    await Assert.ThrowsAsync<AmazonS3Exception>(
      () => _service.UpdateAvatarAsync(_juan.Id, NewFile("photo.png", "image/png")));

    var saved = await SavedEmployeeAsync();
    Assert.Null(saved.AvatarUrl);
  }

  [Fact]
  public async Task UpdateAvatarAsync_ReuploadWithAnotherExtension_StoresTheNewKey()
  {
    // Arrange
    await _service.UpdateAvatarAsync(_juan.Id, NewFile("photo.png", "image/png"));

    // Act: same employee, now a JPEG
    await _service.UpdateAvatarAsync(_juan.Id, NewFile("photo.jpg", "image/jpeg"));

    // Assert: the stored key follows the new extension. (The old avatars/{id}.png object is not
    // deleted by this code, so a changed extension leaves it orphaned in S3.)
    var saved = await SavedEmployeeAsync();
    Assert.Equal($"avatars/{_juan.Id}.jpg", saved.AvatarUrl);
  }

  // ---------------- UpdateAvatarThumbnailAsync ----------------

  [Fact]
  public async Task UpdateAvatarThumbnailAsync_EmployeeDoesNotExist_ReturnsFalse()
  {
    Assert.False(await _service.UpdateAvatarThumbnailAsync(9999, "avatars-thumbnails/9999.png"));
  }

  [Fact]
  public async Task UpdateAvatarThumbnailAsync_ExistingEmployee_SavesTheThumbnailKeyAndKeepsTheAvatar()
  {
    // Arrange
    _juan.AvatarUrl = $"avatars/{_juan.Id}.png";
    await _context.SaveChangesAsync();

    // Act
    var updated = await _service.UpdateAvatarThumbnailAsync(_juan.Id, $"avatars-thumbnails/{_juan.Id}.png");

    // Assert
    Assert.True(updated);
    var saved = await SavedEmployeeAsync();
    Assert.Equal($"avatars-thumbnails/{_juan.Id}.png", saved.AvatarThumbnailUrl);
    Assert.Equal($"avatars/{_juan.Id}.png", saved.AvatarUrl);
  }

  // ---------------- reading: stored keys become signed URLs ----------------

  [Fact]
  public async Task GetByIdAsync_EmployeeWithAvatarKeys_ReturnsSignedUrlsForBoth()
  {
    // Arrange
    _juan.AvatarUrl = "avatars/1.png";
    _juan.AvatarThumbnailUrl = "avatars-thumbnails/1.png";
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(_juan.Id, includeEmergencyContacts: false);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("https://fake-presigned-url.test/avatars/1.png", result.AvatarUrl);
    Assert.Equal("https://fake-presigned-url.test/avatars-thumbnails/1.png", result.AvatarThumbnailUrl);
  }

  [Fact]
  public async Task GetByIdAsync_EmployeeWithoutAvatar_ReturnsNullUrls()
  {
    // Act
    var result = await _service.GetByIdAsync(_juan.Id, includeEmergencyContacts: false);

    // Assert: no key means no URL, and no signing call is needed
    Assert.NotNull(result);
    Assert.Null(result.AvatarUrl);
    Assert.Null(result.AvatarThumbnailUrl);
  }
}