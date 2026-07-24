using System.Collections.Generic;
using PointOfSale.Core.Models.Security;

namespace PointOfSale.Core.Services
{
    public class UserSessionService : IUserSessionService
    {
        public User CurrentUser { get; private set; }
        private HashSet<string> _permissions = new HashSet<string>();

        public int UserId { get; private set; }
        public int BranchId { get; private set; }
        public string BranchName { get; private set; }
        public string FullName { get; private set; }
        public string Username { get; private set; }
        public int RoleId { get; private set; }
        public string RoleName { get; private set; }
        public int? CurrentShiftId { get; private set; }

        public void SetCurrentUser(User user)
        {
            CurrentUser = user;

            UserId = user.UserId;
            BranchId = user.BranchId;
            BranchName = user.BranchName;
            Username = user.Username;
            FullName = user.FullName;
            RoleId = user.Role;      // Assuming user.Role is now an 'int'
            RoleName = user.RoleName;
        }

        public void SetCurrentShift(int? shiftId)
        {
            CurrentShiftId = shiftId;
        }

        public void ClearSession()
        {
            CurrentUser = null;
            UserId = 0;
            BranchId = 0;
            Username = string.Empty;
            RoleId = 0;
            RoleName = string.Empty;
            CurrentShiftId = null;
            _permissions.Clear();
        }

        public void SetPermissions(HashSet<string> permissions)
        {
            _permissions = permissions ?? new HashSet<string>();
        }

        public bool HasPermission(string permissionKey)
        {
            // Admins usually bypass checks, or you can require explicit permission
            if (RoleId == 1) return true;

            return _permissions.Contains(permissionKey);
        }
    }
}
