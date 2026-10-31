using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyMIS.Api.Data;
using MyMIS.Api.Models;

namespace MyMIS.Api.Authorization;

// Answers "what can role X do, and where?" without hitting the database
// on every request.
//
// Registered as a SINGLETON: one instance for the whole app, shared by every
// request, like a module-level Map in a Node process.
public class RolePermissionCache(IMemoryCache cache, IServiceScopeFactory scopeFactory) : IRolePermissionCache
{
  // Safety net: even if an invalidation is ever missed, an entry refreshes
  // itself after 10 minutes. Step 7 calls Invalidate() whenever a role changes.
  private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

  // One cache entry per role, e.g. "role-permissions:3".
  private static string Key(int roleId) => $"role-permissions:{roleId}";

  // Returns permission name → scope for the role, e.g.
  // { "employees.read": Company, "employees.update": Department }.
  // An unknown role id returns an empty dictionary (so: no permissions).
  public async Task<IReadOnlyDictionary<string, PermissionScope>> GetAsync(int roleId)
  {
    // TryGetValue returns true/false AND writes the cached value into `cached`
    // through `out`, the same "Try" pattern as int.TryParse.
    if (cache.TryGetValue(Key(roleId), out IReadOnlyDictionary<string, PermissionScope>? cached)
        && cached is not null)
    {
      return cached;
    }

    // AppDbContext is SCOPED (one per HTTP request), but this class is a
    // SINGLETON that lives forever. A singleton must never hold a scoped
    // service directly (a "captive dependency": it would keep one DbContext
    // alive for the app's whole life). So we create a short-lived scope,
    // borrow a DbContext from it, and dispose both when the method ends.
    using var scope = scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var role = await db.Roles
        .AsNoTracking()
        .Where(r => r.Id == roleId)
        .Select(r => new { r.IsSystem })
        .FirstOrDefaultAsync();

    IReadOnlyDictionary<string, PermissionScope> permissions;

    if (role is null)
    {
      permissions = new Dictionary<string, PermissionScope>();
    }
    else if (role.IsSystem)
    {
      // Decision 1a: a system role (SuperAdmin) holds EVERY permission,
      // company-wide, including ones added by future migrations.
      permissions = await db.Permissions
          .AsNoTracking()
          .ToDictionaryAsync(p => p.Name, _ => PermissionScope.Company);
    }
    else
    {
      // Select first, so the SQL query fetches just the name and scope.
      // (Reading rp.Permission.Name inside ToDictionaryAsync would run in C#
      // AFTER the query, where Permission was never loaded → null.)
      permissions = await db.RolePermissions
          .AsNoTracking()
          .Where(rp => rp.RoleId == roleId)
          .Select(rp => new { rp.Permission.Name, rp.Scope })
          .ToDictionaryAsync(x => x.Name, x => x.Scope);
    }

    cache.Set(Key(roleId), permissions, Lifetime);
    return permissions;
  }

  // Step 7 calls this after a role's permissions change, so the next request
  // reloads from the database instead of waiting up to 10 minutes.
  public void Invalidate(int roleId) => cache.Remove(Key(roleId));
}