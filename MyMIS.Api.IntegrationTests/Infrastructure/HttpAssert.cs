using System.Net;

namespace MyMIS.Api.IntegrationTests.Infrastructure;

public static class HttpAssert
{
  // Includes the response body in the failure message, so a wrong status explains itself.
  public static async Task StatusAsync(HttpStatusCode expected, HttpResponseMessage response)
  {
    Assert.True(
      response.StatusCode == expected,
      $"Expected {(int)expected} but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
  }
}