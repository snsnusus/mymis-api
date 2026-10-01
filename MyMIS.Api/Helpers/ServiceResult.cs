namespace MyMIS.Api.Helpers;

public enum ServiceErrorType
{
  NotFound,
  Validation,
}

// The outcome of a service operation: either a value, or an error type + message.
public class ServiceResult<T>
{
  public T? Value { get; private init; }

  public ServiceErrorType? ErrorType { get; private init; }

  public string? ErrorMessage { get; private init; }

  public bool IsSuccess => ErrorType is null;

  public static ServiceResult<T> Success(T value) =>
      new() { Value = value };

  public static ServiceResult<T> NotFound(string message) =>
      new() { ErrorType = ServiceErrorType.NotFound, ErrorMessage = message };

  public static ServiceResult<T> Invalid(string message) =>
      new() { ErrorType = ServiceErrorType.Validation, ErrorMessage = message };
}