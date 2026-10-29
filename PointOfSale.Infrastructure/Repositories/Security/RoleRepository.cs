using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Models.Security;

namespace PointOfSale.Infrastructure.Repositories.Security
{
    public class RoleRepository : BaseRepository, IRoleRepository
    {
        public RoleRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        public async Task<List<Role>> GetAllRolesAsync()
        {
            try
            {
                var roles = new List<Role>();
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, RoleName FROM [Auth].[Roles] ORDER BY RoleName, Id";
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                            roles.Add(new Role { Id = reader.GetInt32(0), Name = reader.GetString(1) });
                    }
                }
                return roles;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Unable to load roles.", ex);
            }
        }

        // Returns the generated ID on insert, or the existing ID on update.
        public async Task<int> SaveRoleAsync(Role role)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            var name = role.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
                throw new ValidationException("Enter a role name of 1 to 100 characters.");
            if (role.Id < 0) throw new ValidationException("The role ID is invalid.");

            try
            {
                var savedId = role.Id;
                await InTransactionAsync(async (connection, transaction) =>
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.Parameters.Add("@RoleId", SqlDbType.Int).Value = role.Id;
                        command.Parameters.Add("@RoleName", SqlDbType.NVarChar, 100).Value = name;
                        command.CommandText = "SELECT TOP (1) Id FROM [Auth].[Roles] WITH (UPDLOCK, HOLDLOCK) WHERE RoleName = @RoleName AND Id <> @RoleId";
                        if (await command.ExecuteScalarAsync() != null)
                            throw new ValidationException("A role with this name already exists.");

                        if (role.Id == 0)
                        {
                            command.CommandText = "INSERT INTO [Auth].[Roles] (RoleName) VALUES (@RoleName); SELECT CAST(SCOPE_IDENTITY() AS int);";
                            savedId = Convert.ToInt32(await command.ExecuteScalarAsync());
                        }
                        else
                        {
                            command.CommandText = "UPDATE [Auth].[Roles] SET RoleName = @RoleName WHERE Id = @RoleId; SELECT @@ROWCOUNT;";
                            if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
                                throw new ValidationException("This role no longer exists. Reload the roles list.");
                        }
                    }
                });
                return savedId;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                throw new ValidationException("A role with this name already exists.", ex);
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Unable to save the role.", ex);
            }
        }

        public async Task DeleteRoleAsync(int roleId)
        {
            if (roleId <= 0) throw new ValidationException("Select a saved role to delete.");
            try
            {
                await InTransactionAsync(async (connection, transaction) =>
                {
                    await EnsureRoleExistsAsync(connection, transaction, roleId);
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
                        // Keep the validation and deletion in one serializable transaction.
                        command.CommandText = "SELECT TOP (1) Id FROM [Auth].[User] WITH (UPDLOCK, HOLDLOCK) WHERE RoleId = @RoleId AND IsActive = 1";
                        if (await command.ExecuteScalarAsync() != null)
                            throw new ValidationException("This role is assigned to active users. Assign those users another role before deleting it.");

                        command.CommandText = "DELETE FROM [Auth].[RolePermission] WHERE RoleId = @RoleId; DELETE FROM [Auth].[Roles] WHERE Id = @RoleId;";
                        await command.ExecuteNonQueryAsync();
                    }
                });
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                throw new ValidationException("This role is still referenced by users or other records and cannot be deleted.", ex);
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Unable to delete the role.", ex);
            }
        }

        public async Task<List<PermissionNode>> GetAllPermissionsAsync()
        {
            try
            {
                var permissions = new List<PermissionNode>();
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Module, PermissionKey, Description FROM [Auth].[Permission] ORDER BY Module, PermissionKey";
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
                                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                            });
                        }
                    }
                }
                return permissions;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Unable to load the permission catalog.", ex);
            }
        }

        public async Task<List<int>> GetRolePermissionIdsAsync(int roleId)
        {
            try
            {
                var ids = new List<int>();
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    // RolePermission is a join table: the presence of a row grants access.
                    command.CommandText = "SELECT PermissionId FROM [Auth].[RolePermission] WHERE RoleId = @RoleId";
                    command.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
                    }
                }
                return ids;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Unable to load role permissions.", ex);
            }
        }

        public async Task SaveRolePermissionsAsync(int roleId, List<int> permissionIds)
        {
            if (roleId <= 0) throw new ValidationException("Save the role before assigning permissions.");
            if (permissionIds == null) throw new ArgumentNullException(nameof(permissionIds));
            var ids = permissionIds.Distinct().ToList();
            // The deployed permission catalog starts at ID 0 (ACCESS_DASHBOARD).
            if (ids.Any(id => id < 0)) throw new ValidationException("Permission IDs cannot be negative.");

            try
            {
                await InTransactionAsync(async (connection, transaction) =>
                {
                    await EnsureRoleExistsAsync(connection, transaction, roleId);
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
                        command.CommandText = "DELETE FROM [Auth].[RolePermission] WHERE RoleId = @RoleId";
                        await command.ExecuteNonQueryAsync();

                        command.CommandText = "INSERT INTO [Auth].[RolePermission] (RoleId, PermissionId) VALUES (@RoleId, @PermissionId)";
                        var permissionId = command.Parameters.Add("@PermissionId", SqlDbType.Int);
                        foreach (var id in ids)
                        {
                            permissionId.Value = id;
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                });
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Unable to save role permissions. No changes were committed.", ex);
            }
        }

        private static async Task EnsureRoleExistsAsync(SqlConnection connection, SqlTransaction transaction, int roleId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT Id FROM [Auth].[Roles] WITH (UPDLOCK, HOLDLOCK) WHERE Id = @RoleId";
                command.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
                if (await command.ExecuteScalarAsync() == null)
                    throw new ValidationException("This role no longer exists. Reload the roles list.");
            }
        }

        private async Task InTransactionAsync(Func<SqlConnection, SqlTransaction, Task> action)
        {
            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                // .NET Framework SqlTransaction exposes synchronous begin/commit/rollback only.
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        await action(connection, transaction);
                        transaction.Commit();
                    }
                    catch
                    {
                        // Disposing the transaction rolls back if the connection is already broken.
                        try
                        {
                            if (transaction.Connection != null) transaction.Rollback();
                        }
                        catch (Exception rollbackError) when (rollbackError is SqlException || rollbackError is InvalidOperationException)
                        {
                            Debug.WriteLine($"Role transaction rollback failed: {rollbackError.Message}");
                        }
                        throw;
                    }
                }
            }
        }
    }
}
