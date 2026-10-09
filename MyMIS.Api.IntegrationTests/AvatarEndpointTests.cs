using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Moq;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;

namespace MyMIS.Api.IntegrationTests;

// The avatar endpoints through real HTTP. S3 is the fake from ApiFactory, so nothing leaves the machine,
// and the Lambda is played by the test, which sends the same PATCH the real Lambda sends.
[Collection(IntegrationCollection.Name)]
public class AvatarEndpointTests(ApiFactory factory)
{
  // An id that no row has
  private const int Missing = 999_999;

  // Secrets that must all be refused. They are built from the real one, so each is wrong in one specific way.
  public static TheoryData<string> WrongSecrets => new()
  {
    "wrong-secret",
    ApiFactory.CallbackSecret + "x",              // the right secret plus one character
    ApiFactory.CallbackSecret[..^1],              // the right secret minus its last character
    ApiFactory.CallbackSecret.ToUpperInvariant(), // the right letters in the wrong case
  };

  // ---------------- helpers ----------------

  private static MultipartFormDataContent NewUpload(
    string contentType, string fileName, int length = 32, string field = "file")
  {
    var content = new MultipartFormDataContent();

    var file = new ByteArrayContent(new byte[length]);
    file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

    content.Add(file, field, fileName);
    return content;
  }

  // The same request the Lambda sends. A null secret means "send no header at all".
  private static HttpRequestMessage NewCallback(int employeeId, string? secret, string? thumbnailKey = null)
  {
    var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Employees/{employeeId}/avatar-thumbnail")
    {
      Content = JsonContent.Create(new { thumbnailKey = thumbnailKey ?? $"avatars-thumbnails/{employeeId}.png" }),
    };

    if (secret is not null)
    {
      request.Headers.Add("X-Internal-Secret", secret);
    }

    return request;
  }

  private async Task<HttpStatusCode> SendCallbackAsync(int employeeId, string? secret)
  {
    using var client = factory.CreateApiClient();
    using var request = NewCallback(employeeId, secret);
    using var response = await client.SendAsync(request);
    return response.StatusCode;
  }

  private Task<Employee> SavedEmployeeAsync(int id) =>
    factory.QueryDbAsync(db => db.Employees.AsNoTracking().SingleAsync(e => e.Id == id));

  // The fake S3 is shared by every test, but each key contains a unique employee id, so
  // checking for one exact key is safe.
  private void VerifyUploaded(string key, string contentType) =>
    factory.S3.Verify(
      c => c.PutObjectAsync(
        It.Is<PutObjectRequest>(r => r.Key == key && r.ContentType == contentType),
        It.IsAny<CancellationToken>()),
      Times.Once);

  private void VerifyNeverUploaded(string key) =>
    factory.S3.Verify(
      c => c.PutObjectAsync(It.Is<PutObjectRequest>(r => r.Key == key), It.IsAny<CancellationToken>()),
      Times.Never);

  // ---------------- the Lambda callback: PATCH /api/Employees/{id}/avatar-thumbnail ----------------

  [Fact]
  public async Task Callback_WithTheRightSecret_Returns204AndSavesTheThumbnailKey()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();

    // Act: no login at all, only the secret
    var status = await SendCallbackAsync(employee.Id, ApiFactory.CallbackSecret);

    // Assert
    Assert.Equal(HttpStatusCode.NoContent, status);
    var saved = await SavedEmployeeAsync(employee.Id);
    Assert.Equal($"avatars-thumbnails/{employee.Id}.png", saved.AvatarThumbnailUrl);
  }

  [Fact]
  public async Task Callback_WithoutTheSecretHeader_Returns401AndChangesNothing()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();

    // Act
    var status = await SendCallbackAsync(employee.Id, secret: null);

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, status);
    Assert.Null((await SavedEmployeeAsync(employee.Id)).AvatarThumbnailUrl);
  }

  [Theory]
  [MemberData(nameof(WrongSecrets))]
  public async Task Callback_WithAWrongSecret_Returns401AndChangesNothing(string wrongSecret)
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();

    // Act
    var status = await SendCallbackAsync(employee.Id, wrongSecret);

    // Assert
    Assert.True(
      status == HttpStatusCode.Unauthorized,
      $"A callback with the secret '{wrongSecret}' returned {(int)status}.");
    Assert.Null((await SavedEmployeeAsync(employee.Id)).AvatarThumbnailUrl);
  }

  [Fact]
  public async Task Callback_WithALoginButNoSecret_Returns401()
  {
    // Arrange: even a SuperAdmin's token doesn't open this endpoint. Only the secret does.
    var employee = await factory.SeedEmployeeAsync();
    var superAdmin = await factory.GetClientAsync(Role.SuperAdmin);

    // Act
    using var request = NewCallback(employee.Id, secret: null);
    using var response = await superAdmin.SendAsync(request);

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Callback_WithTheRightSecretForAnEmployeeThatDoesNotExist_Returns404()
  {
    // Act
    var status = await SendCallbackAsync(Missing, ApiFactory.CallbackSecret);

    // Assert
    Assert.Equal(HttpStatusCode.NotFound, status);
  }

  [Fact]
  public async Task Callback_WithAWrongSecretForAnEmployeeThatDoesNotExist_Returns401NotNotFound()
  {
    // Act: the secret is checked first, so a caller without it can't use 404 vs 401 to find out which ids exist
    var status = await SendCallbackAsync(Missing, "wrong-secret");

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, status);
  }

  // ---------------- own avatar: POST /api/Employees/me/avatar ----------------

  [Fact]
  public async Task UploadMyAvatar_WithAPng_Returns200WithASignedUrlAndSavesTheKey()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    using var response = await client.PostAsync("/api/Employees/me/avatar", NewUpload("image/png", "photo.png"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    var key = $"avatars/{employee.Id}.png";

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal($"https://fake-presigned-url.test/{key}", json.RootElement.GetProperty("avatarUrl").GetString());

    Assert.Equal(key, (await SavedEmployeeAsync(employee.Id)).AvatarUrl);
    VerifyUploaded(key, "image/png");
  }

  [Fact]
  public async Task UploadMyAvatar_WithAJpeg_StoresItUnderAJpgKey()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    using var response = await client.PostAsync("/api/Employees/me/avatar", NewUpload("image/jpeg", "photo.jpg"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    var key = $"avatars/{employee.Id}.jpg";

    Assert.Equal(key, (await SavedEmployeeAsync(employee.Id)).AvatarUrl);
    VerifyUploaded(key, "image/jpeg");
  }

  [Theory]
  [InlineData("image/gif", "photo.gif")]
  [InlineData("application/pdf", "document.pdf")]
  public async Task UploadMyAvatar_WithAnUnsupportedType_Returns400AndUploadsNothing(string contentType, string fileName)
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    using var response = await client.PostAsync("/api/Employees/me/avatar", NewUpload(contentType, fileName));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.BadRequest, response);
    Assert.Contains(contentType, await response.Content.ReadAsStringAsync());

    Assert.Null((await SavedEmployeeAsync(employee.Id)).AvatarUrl);
    VerifyNeverUploaded($"avatars/{employee.Id}.png");
  }

  [Fact]
  public async Task UploadMyAvatar_WithAnEmptyFile_Returns400()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    using var response = await client.PostAsync("/api/Employees/me/avatar", NewUpload("image/png", "empty.png", length: 0));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.BadRequest, response);
    Assert.Null((await SavedEmployeeAsync(employee.Id)).AvatarUrl);
  }

  [Fact]
  public async Task UploadMyAvatar_WithTheFileInTheWrongFormField_Returns400()
  {
    // Arrange: the endpoint reads a form field named "file"
    var employee = await factory.SeedEmployeeAsync();
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    using var response = await client.PostAsync(
      "/api/Employees/me/avatar", NewUpload("image/png", "photo.png", field: "photo"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.BadRequest, response);
  }

  [Fact]
  public async Task UploadMyAvatar_WithoutAToken_Returns401()
  {
    // Arrange
    using var client = factory.CreateApiClient();

    // Act
    using var response = await client.PostAsync("/api/Employees/me/avatar", NewUpload("image/png", "photo.png"));

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ---------------- someone else's avatar: POST /api/Employees/{id}/avatar ----------------

  [Fact]
  public async Task UploadAvatarForAnEmployee_WithoutAnyPermission_Returns403AndUploadsNothing()
  {
    // Arrange
    var target = await factory.SeedEmployeeAsync();
    var client = await factory.GetClientAsync(Role.User);

    // Act
    using var response = await client.PostAsync($"/api/Employees/{target.Id}/avatar", NewUpload("image/png", "photo.png"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.Forbidden, response);
    VerifyNeverUploaded($"avatars/{target.Id}.png");
  }

  [Fact]
  public async Task UploadAvatarForAnEmployee_WithOnlyAnUnrelatedPermission_Returns403()
  {
    // Arrange: positions.create is a real permission, but not one this endpoint accepts
    var target = await factory.SeedEmployeeAsync();
    var client = await factory.GetClientAsync(Role.User, "positions.create");

    // Act
    using var response = await client.PostAsync($"/api/Employees/{target.Id}/avatar", NewUpload("image/png", "photo.png"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.Forbidden, response);
  }

  [Fact]
  public async Task UploadAvatarForAnEmployee_WithEmployeesUpdate_StoresItOnTheTargetNotTheCaller()
  {
    // Arrange
    var target = await factory.SeedEmployeeAsync();
    var client = await factory.GetClientAsync(Role.User, "employees.update");

    // Act
    using var response = await client.PostAsync($"/api/Employees/{target.Id}/avatar", NewUpload("image/png", "photo.png"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    var key = $"avatars/{target.Id}.png";

    Assert.Equal(key, (await SavedEmployeeAsync(target.Id)).AvatarUrl);
    VerifyUploaded(key, "image/png");
  }

  [Fact]
  public async Task UploadAvatarForAnEmployee_WithEmployeesCreateAlone_IsAlsoAllowed()
  {
    // Arrange: the controller accepts update OR create, which an attribute alone couldn't express
    var target = await factory.SeedEmployeeAsync();
    var client = await factory.GetClientAsync(Role.User, "employees.create");

    // Act
    using var response = await client.PostAsync($"/api/Employees/{target.Id}/avatar", NewUpload("image/png", "photo.png"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
  }

  [Fact]
  public async Task UploadAvatarForAnEmployee_AsSuperAdmin_IsAllowed()
  {
    // Arrange
    var target = await factory.SeedEmployeeAsync();
    var client = await factory.GetClientAsync(Role.SuperAdmin);

    // Act
    using var response = await client.PostAsync($"/api/Employees/{target.Id}/avatar", NewUpload("image/png", "photo.png"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
  }

  [Fact]
  public async Task UploadAvatarForAnEmployee_WhoDoesNotExist_Returns404AndUploadsNothing()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.User, "employees.update");

    // Act
    using var response = await client.PostAsync($"/api/Employees/{Missing}/avatar", NewUpload("image/png", "photo.png"));

    // Assert: the employee is looked up before the upload, so S3 is never touched
    await HttpAssert.StatusAsync(HttpStatusCode.NotFound, response);
    VerifyNeverUploaded($"avatars/{Missing}.png");
  }

  [Fact]
  public async Task UploadAvatarForAnEmployee_WhoDoesNotExist_WithoutPermission_Returns403NotNotFound()
  {
    // Arrange: permission is checked before existence on this route, so the answer doesn't reveal which ids exist
    var client = await factory.GetClientAsync(Role.User);

    // Act
    using var response = await client.PostAsync($"/api/Employees/{Missing}/avatar", NewUpload("image/png", "photo.png"));

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.Forbidden, response);
  }

  // ---------------- the whole pipeline, with the Lambda played by the test ----------------

  [Fact]
  public async Task Avatar_UploadThenCallbackThenRead_ReturnsSignedUrlsForBothImages()
  {
    // Arrange: an employee uploads a photo
    var employee = await factory.SeedEmployeeAsync();
    using var client = await factory.CreateClientForAsync(employee);

    using var upload = await client.PostAsync("/api/Employees/me/avatar", NewUpload("image/png", "photo.png"));
    await HttpAssert.StatusAsync(HttpStatusCode.OK, upload);

    // Act: the Lambda reports the thumbnail it made, then the employee reads their record
    var callback = await SendCallbackAsync(employee.Id, ApiFactory.CallbackSecret);
    Assert.Equal(HttpStatusCode.NoContent, callback);

    using var read = await client.GetAsync($"/api/Employees/{employee.Id}");

    // Assert: both stored keys come back as signed URLs
    await HttpAssert.StatusAsync(HttpStatusCode.OK, read);
    using var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync());

    Assert.Equal(
      $"https://fake-presigned-url.test/avatars/{employee.Id}.png",
      json.RootElement.GetProperty("avatarUrl").GetString());
    Assert.Equal(
      $"https://fake-presigned-url.test/avatars-thumbnails/{employee.Id}.png",
      json.RootElement.GetProperty("avatarThumbnailUrl").GetString());
  }
}