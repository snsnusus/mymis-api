namespace MyMIS.Api.Options;

public class S3Options
{
  public required string AccessKey { get; init; }
  public required string SecretKey { get; init; }
  public required string Region { get; init; }
  public required string BucketName { get; init; }
}