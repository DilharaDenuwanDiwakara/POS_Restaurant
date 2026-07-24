using System.Collections.Generic;
using PointOfSale.Core.Models.Security;

namespace PointOfSale.Core.Services
{
    public interface IUserSessionService
    {
        int UserId { get; }
        int BranchId { get; }
        string Username { get; }
        int RoleId { get; }
        string RoleName { get; }
        int? CurrentShiftId { get; }

        User CurrentUser { get; }

        void SetCurrentUser(User user);
        void SetCurrentShift(int? shiftId);
        void ClearSession();
        bool HasPermission(string permissionKey);
        void SetPermissions(HashSet<string> permissions);
    }
}
