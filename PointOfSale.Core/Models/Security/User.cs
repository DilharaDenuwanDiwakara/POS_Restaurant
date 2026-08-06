using System;

namespace PointOfSale.Core.Models.Security
{
    public class User
    {
        public int UserId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Pin { get; set; }
        public int Role { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastLogin { get; set; }

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName))
                    return "?";

                // Logic A: Just the first letter (e.g., "Kasun Perera" -> "K")
                return FullName.Substring(0, 1).ToUpper();

                // Logic B: (Optional) First letter of First & Last Name (e.g., "Kasun Perera" -> "KP")
                var parts = FullName.Trim().Split(' ');
                if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpper();
                return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
            }
        }
    }
}
