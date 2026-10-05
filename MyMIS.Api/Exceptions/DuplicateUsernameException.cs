namespace MyMIS.Api.Exceptions;

public class DuplicateUsernameException(Exception? innerException = null)
  : Exception(DefaultMessage, innerException)
{
  public const string DefaultMessage = "That username is already taken.";
}