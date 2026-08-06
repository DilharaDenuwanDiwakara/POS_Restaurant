using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Models.Security;

namespace PointOfSale.Infrastructure.Repositories.Security
{
    public class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Methods
        #region Public Methods
        public async Task<int> CreateAsync(User user)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Auth].[uspInsertUser]"))
                    {
                        AddUserParameters(command, user);

                        var userId = command.Parameters.Add("@UserId", SqlDbType.Int);
                        userId.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)userId.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while creating the user.", ex);
            }

        }
        public async Task<IEnumerable<User>> GetAllAsync()
        {
            var users = new List<User>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Auth].[uspGetAllUsers]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                users.Add(MapUser(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting the user.", ex);
            }
            return users;
        }
        public async Task<User> GetByUsernameAsync(string username)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Auth].[uspAuthenticateUser]"))
                    {
                        command.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return MapUser(reader);
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while fetching user {username}.", ex);
            }
            return null;
        }

        public async Task<PinAuthResult> GetUserByPinAsync(string pinCode)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = "SELECT TOP 1 Id, FullName FROM [Auth].[User] WHERE PinCode = @PinCode AND IsActive = 1";
                    command.Parameters.Add("@PinCode", SqlDbType.NVarChar, 20).Value = pinCode;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new PinAuthResult
                            {
                                UserId = GetValue<int>(reader, "Id"),
                                FullName = GetValue<string>(reader, "FullName")
                            };
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while verifying the PIN.", ex);
            }
            return null;
        }

        public async Task<HashSet<string>> GetPermissionsAsync(int userId)
        {
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Auth].[uspGetUserPermissions]"))
                {
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // We only need the Key (e.g., "PRODUCT_ADD")
                            permissions.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error
                Debug.WriteLine($"Error fetching permissions: {ex.Message}");
            }

            return permissions;
        }
        public async Task UpdateAsync(User user)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Auth].[uspUpdateUser]"))
                    {
                        command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;

                        AddUserParameters(command, user);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating user with ID {user.UserId}.", ex);
            }

        }
        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Auth].[uspDeleteUser]"))
                    {
                        command.Parameters.Add("@UserId", SqlDbType.Int).Value = id;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting user with ID {id}.", ex);
            }
        }

        public async Task ChangePasswordAsync(int userId, string newPasswordHash)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Auth].[uspChangePassword]"))
                    {
                        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                        command.Parameters.Add("@NewPasswordHash", SqlDbType.NVarChar, 255).Value = newPasswordHash;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                // Handle the specific error we raised in the SP
                // SQL Server RAISERROR with severity 16 comes as a SqlException
                if (ex.Message.Contains("The current password provided is incorrect"))
                {
                    throw new InvalidOperationException("The current password you entered is incorrect.");
                }

                throw new InvalidOperationException($"A database error occurred while changing the password for user ID {userId}.", ex);
            }
        }

        public async Task<IEnumerable<Roles>> GetAllRolesAsync()
        {
            var roles = new List<Roles>();
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "SELECT Id, RoleName FROM [Auth].[Roles] ORDER BY RoleName"))
                {
                    command.CommandType = CommandType.Text; // Using direct SQL for simple reference data is fine, or use an SP

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            roles.Add(new Roles
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Error fetching roles.", ex);
            }
            return roles;
        }

        public async Task<IEnumerable<PermissionNode>> GetAllPermissionsAsync()
        {
            var permissions = new List<PermissionNode>();
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "SELECT Id, Module, PermissionKey, Description FROM [Auth].[Permission] ORDER BY Module, PermissionKey"))
                {
                    command.CommandType = CommandType.Text;

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            permissions.Add(new PermissionNode
                            {
                                PermissionId = reader.GetInt32(0),
                                Module = reader.GetString(1),
                                PermissionKey = reader.GetString(2),
                                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                IsGranted = false // Default to false
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Error fetching system permissions.", ex);
            }
            return permissions;
        }

        public async Task<IEnumerable<int>> GetUserPermissionIdsAsync(int userId)
        {
            var ids = new List<int>();
            try
            {
                using (var connection = GetConnection())
                // Ensure you select only permissions where IsGranted = 1
                using (var command = CreateCommand(connection, "SELECT PermissionId FROM [Auth].[UserPermission] WHERE UserId = @UserId AND IsGranted = 1"))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            ids.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching user permissions: {ex.Message}");
            }
            return ids;
        }

        public async Task SaveUserPermissionsAsync(int userId, List<int> grantedPermissionIds)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. Clear existing user overrides
                            using (var deleteCmd = connection.CreateCommand())
                            {
                                deleteCmd.Transaction = transaction;
                                deleteCmd.CommandText = "DELETE FROM [Auth].[UserPermission] WHERE UserId = @UserId";
                                deleteCmd.Parameters.AddWithValue("@UserId", userId);
                                await deleteCmd.ExecuteNonQueryAsync();
                            }

                            // 2. Insert new granted permissions
                            if (grantedPermissionIds != null && grantedPermissionIds.Any())
                            {
                                using (var insertCmd = connection.CreateCommand())
                                {
                                    insertCmd.Transaction = transaction;
                                    insertCmd.CommandText = "INSERT INTO [Auth].[UserPermission] (UserId, PermissionId, IsGranted) VALUES (@UserId, @PermissionId, 1)";

                                    // Add parameters once
                                    var pUserId = insertCmd.Parameters.AddWithValue("@UserId", userId);
                                    var pPermId = insertCmd.Parameters.Add("@PermissionId", SqlDbType.Int);

                                    foreach (var permId in grantedPermissionIds)
                                    {
                                        pPermId.Value = permId;
                                        await insertCmd.ExecuteNonQueryAsync();
                                    }
                                }
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error saving permissions for user {userId}.", ex);
            }
        }
        #endregion

        #region Private Methods
        private void AddUserParameters(SqlCommand command, User user)
        {
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = user.BranchId;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = user.FullName;
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = user.Username;
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255).Value = user.PasswordHash;
            // Column is [Auth].[User].[PinCode]; left NULL when no PIN is assigned.
            command.Parameters.Add("@PinCode", SqlDbType.NVarChar, 10).Value =
                string.IsNullOrWhiteSpace(user.Pin) ? (object)DBNull.Value : user.Pin.Trim();
            command.Parameters.Add("@RoleId", SqlDbType.Int).Value = (int)user.Role;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = user.IsActive;
        }
        private User MapUser(IDataRecord record)
        {
            return new User
            {
                UserId = GetValue<int>(record, "Id"),
                FullName = GetValue<string>(record, "FullName"),
                Username = GetValue<string>(record, "Username"),
                PasswordHash = GetValue<string>(record, "PasswordHash"),
                // Guarded with HasColumn so user list/login keep working even before
                // uspGetAllUsers/uspAuthenticateUser are updated to return PinCode.
                Pin = HasColumn(record, "PinCode") ? GetValue<string>(record, "PinCode") : null,
                Role = GetValue<int>(record, "RoleId"),
                RoleName = GetValue<string>(record, "RoleName"),
                IsActive = GetValue<bool>(record, "IsActive"),
                BranchId = GetValue<int>(record, "DefaultBranchId"),
                BranchName = GetValue<string>(record, "BranchName")

            };
        }

        private static bool HasColumn(IDataRecord record, string columnName)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
        #endregion
    }
}
