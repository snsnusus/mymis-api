using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using MyMIS.Api.Options;

namespace MyMIS.Api.Services;

public class S3UploadService(IAmazonS3 s3Client, IOptions<S3Options> options)
{
  private readonly IAmazonS3 _s3Client = s3Client;
  private readonly string _bucketName = options.Value.BucketName;

  private static readonly string[] AllowedContentTypes =
  [
    "image/jpeg",
    "image/png"
  ];

  public async Task<string> UploadAvatarAsync(int employeeId, IFormFile file)
  {
    if (!AllowedContentTypes.Contains(file.ContentType))
    {
      throw new InvalidOperationException(
          $"Unsupported file type: {file.ContentType}. Only JPEG and PNG images are allowed.");
    }

    var extension = Path.GetExtension(file.FileName);
    var key = $"avatars/{employeeId}{extension}";

    using var stream = file.OpenReadStream();

    var request = new PutObjectRequest
    {
      BucketName = _bucketName,
      Key = key,
      InputStream = stream,
      ContentType = file.ContentType
    };

    await _s3Client.PutObjectAsync(request);

    return key;
  }

  public string GetPresignedUrl(string key, int expiryMinutes = 15)
  {
    var request = new GetPreSignedUrlRequest
    {
      BucketName = _bucketName,
      Key = key,
      Verb = HttpVerb.GET,
      Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
    };

    return _s3Client.GetPreSignedURL(request);
  }
}