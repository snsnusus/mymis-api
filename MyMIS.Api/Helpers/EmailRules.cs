using System.Net.Mail;

namespace MyMIS.Api.Helpers;

public static class EmailRules
{
  public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

  // Expects an already-normalized value.
  public static bool IsValid(string normalizedEmail)
  {
    // Not parseable as an email at all, e.g. "jdoe", "@@", "".
    if (!MailAddress.TryCreate(normalizedEmail, out var parsed))
    {
      return false;
    }

    // Parsed, but with extras like a display name ("Juan <juan@x.com>").
    if (parsed.Address != normalizedEmail)
    {
      return false;
    }

    // Domain must look real: has a dot, not at either end ("jdoe@gmail" fails).
    var host = parsed.Host;
    return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.');
  }
}