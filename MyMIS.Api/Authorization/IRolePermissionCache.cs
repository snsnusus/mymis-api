using MyMIS.Api.Models;

namespace MyMIS.Api.Authorization;

// The "shape" a role-permission cache must have. RolePermissionCache is the
// real one; tests pass a Moq fake with the same shape.
public interface IRolePermissionCache
{
  Task<IReadOnlyDictionary<string, PermissionScope>> GetAsync(int roleId);

  void Invalidate(int roleId);
}