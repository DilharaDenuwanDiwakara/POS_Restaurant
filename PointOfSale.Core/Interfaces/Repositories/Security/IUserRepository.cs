using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Security;

namespace PointOfSale.Core.Interfaces.Security
{
    public interface IUserRepository
    {
        Task<int> CreateAsync(User user);
        Task<IEnumerable<User>> GetAllAsync();
        Task UpdateAsync(User user);
        Task DeleteAsync(int id);

        Task<User> GetByUsernameAsync(string username);

        Task<PinAuthResult> GetUserByPinAsync(string pinCode);

        Task<HashSet<string>> GetPermissionsAsync(int userId);
        Task ChangePasswordAsync(int userId, string newPasswordHash);

        Task<IEnumerable<Roles>> GetAllRolesAsync();
        Task<IEnumerable<PermissionNode>> GetAllPermissionsAsync();
        Task<List<int>> GetRolePermissionIdsAsync(int roleId);
        Task SaveRolePermissionsAsync(int roleId, List<int> grantedPermissionIds);

        // User-Specific Settings
        Task<IEnumerable<int>> GetUserPermissionIdsAsync(int userId);
        Task<List<PermissionNode>> GetUserPermissionOverridesAsync(int userId);
        Task SaveUserPermissionsAsync(int userId, List<PermissionNode> userOverrides, int modifiedByUserId);

    }
}
