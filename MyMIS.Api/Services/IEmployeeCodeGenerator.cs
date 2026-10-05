namespace MyMIS.Api.Services;

public interface IEmployeeCodeGenerator
{
  Task<string> GenerateAsync();
}