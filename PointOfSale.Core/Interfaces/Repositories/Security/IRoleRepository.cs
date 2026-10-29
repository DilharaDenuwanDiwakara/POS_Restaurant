using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Security;

namespace PointOfSale.Core.Interfaces.Security
{
    public interface IRoleRepository
    {
        Task<List<Role>> GetAllRolesAsync();
        Task<int> SaveRoleAsync(Role role);
        Task DeleteRoleAsync(int roleId);
        Task<List<PermissionNode>> GetAllPermissionsAsync();
        Task<List<int>> GetRolePermissionIdsAsync(int roleId);
        Task SaveRolePermissionsAsync(int roleId, List<int> permissionIds);
    }
}
