using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Moq;
using MyMIS.Api.Options;
using MyMIS.Api.Services;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace MyMIS.Api.Tests.Services;

public class S3UploadServiceTests
{
  private readonly Mock<IAmazonS3> _s3 = new();
  private readonly S3UploadService _service;

  // Whatever the service sent to the (fake) S3 client, captured so tests can inspect it.
  private PutObjectRequest? _putRequest;
  private GetPreSignedUrlRequest? _presignRequest;

  public S3UploadServiceTests()
  {
    // The service calls PutObjectAsync(request) without a token, but Moq expression trees
    // can't omit optional arguments, so the setup names both parameters.
    _s3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .Callback<PutObjectRequest, CancellationToken>((request, _) => _putRequest = request)
      .ReturnsAsync(new PutObjectResponse());

    _s3.Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
      .Callback<GetPreSignedUrlRequest>(request => _presignRequest = request)
      .Returns("https://signed.example/test");

    var options = MsOptions.Create(new S3Options
    {
      AccessKey = "test-access-key",
      SecretKey = "test-secret-key",
      Region = "ap-southeast-1",
      BucketName = "test-bucket",
    });

    _service = new S3UploadService(_s3.Object, options);
  }

  // A real FormFile over an in-memory stream. Headers must be set before ContentType,
  // because ContentType is stored in the headers.
  private static FormFile NewFile(string fileName, string contentType)
  {
    var stream = new MemoryStream(new byte[16]);
    return new FormFile(stream, 0, stream.Length, "file", fileName)
    {
      Headers = new HeaderDictionary(),
      ContentType = contentType,
    };
  }

  // ---------------- UploadAvatarAsync ----------------

  [Fact]
  public async Task UploadAvatarAsync_PngFile_UploadsToTheEmployeeKeyAndReturnsTheKey()
  {
    // Act
    var key = await _service.UploadAvatarAsync(5, NewFile("photo.png", "image/png"));

    // Assert
    Assert.Equal("avatars/5.png", key);

    Assert.NotNull(_putRequest);
    Assert.Equal("test-bucket", _putRequest.BucketName);
    Assert.Equal("avatars/5.png", _putRequest.Key);
    Assert.Equal("image/png", _putRequest.ContentType);
  }

  [Fact]
  public async Task UploadAvatarAsync_JpegFile_KeepsTheJpgExtension()
  {
    // Act
    var key = await _service.UploadAvatarAsync(7, NewFile("me.jpg", "image/jpeg"));

    // Assert
    Assert.Equal("avatars/7.jpg", key);
    Assert.NotNull(_putRequest);
    Assert.Equal("image/jpeg", _putRequest.ContentType);
  }

  [Fact]
  public async Task UploadAvatarAsync_SameEmployeeTwice_UsesTheSameKeySoTheOldPhotoIsOverwritten()
  {
    // Act
    var first = await _service.UploadAvatarAsync(5, NewFile("old.png", "image/png"));
    var second = await _service.UploadAvatarAsync(5, NewFile("new.png", "image/png"));

    // Assert: a deterministic key means a re-upload replaces the object instead of orphaning it
    Assert.Equal(first, second);
  }

  [Theory]
  [InlineData("application/pdf")]
  [InlineData("image/gif")]
  [InlineData("text/plain")]
  public async Task UploadAvatarAsync_UnsupportedContentType_ThrowsAndNeverCallsS3(string contentType)
  {
    // Act
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(
      () => _service.UploadAvatarAsync(5, NewFile("file.bin", contentType)));

    // Assert
    Assert.Contains(contentType, exception.Message);
    _s3.Verify(
      c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  // ---------------- GetPresignedUrl ----------------

  [Fact]
  public void GetPresignedUrl_DefaultExpiry_SignsAGetRequestValidFor15Minutes()
  {
    // Arrange
    var before = DateTime.UtcNow;

    // Act
    var url = _service.GetPresignedUrl("avatars/5.png");

    // Assert
    var after = DateTime.UtcNow;

    Assert.Equal("https://signed.example/test", url);
    Assert.NotNull(_presignRequest);
    Assert.Equal("test-bucket", _presignRequest.BucketName);
    Assert.Equal("avatars/5.png", _presignRequest.Key);
    Assert.Equal(HttpVerb.GET, _presignRequest.Verb);

    // Convert.ToDateTime accepts the SDK property whether it is DateTime or DateTime?
    var expires = Convert.ToDateTime(_presignRequest.Expires);
    Assert.InRange(expires, before.AddMinutes(15), after.AddMinutes(15));
  }

  [Fact]
  public void GetPresignedUrl_CustomExpiry_UsesTheGivenNumberOfMinutes()
  {
    // Arrange
    var before = DateTime.UtcNow;

    // Act
    _service.GetPresignedUrl("avatars/5.png", expiryMinutes: 60);

    // Assert
    var after = DateTime.UtcNow;

    Assert.NotNull(_presignRequest);
    var expires = Convert.ToDateTime(_presignRequest.Expires);
    Assert.InRange(expires, before.AddMinutes(60), after.AddMinutes(60));
  }
}