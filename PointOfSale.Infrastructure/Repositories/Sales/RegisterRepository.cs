using System;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class RegisterRepository : BaseRepository, IRegisterRepository
    {
        public RegisterRepository(DatabaseConnection dbConnection) : base(dbConnection)
        {
        }

        public async Task<RegisterDto> GetRegisterByIdAsync(int registerId)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = @"
                    SELECT TOP (1)
                        pr.[Id],
                        pr.[BranchId],
                        pr.[RegisterCode],
                        pr.[RegisterName]
                    FROM [System].[POSRegister] pr
                    WHERE pr.[Id] = @RegisterId
                      AND pr.[IsActive] = 1;";
                command.Parameters.Add("@RegisterId", SqlDbType.Int).Value = registerId;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new RegisterDto
                        {
                            Id = GetValue<int>(reader, "Id"),
                            BranchId = GetValue<int>(reader, "BranchId"),
                            RegisterCode = GetValue<string>(reader, "RegisterCode"),
                            RegisterName = GetValue<string>(reader, "RegisterName")
                        };
                    }
                }
            }

            return null;
        }

        public async Task<int> RegisterTerminalAsync(int branchId, string registerCode, string registerName, string machineName, int authorizedByUserId)
        {
            if (branchId <= 0)
            {
                throw new ArgumentException("Branch is required.", nameof(branchId));
            }

            if (string.IsNullOrWhiteSpace(registerCode))
            {
                throw new ArgumentException("Register code is required.", nameof(registerCode));
            }

            if (string.IsNullOrWhiteSpace(registerName))
            {
                throw new ArgumentException("Register name is required.", nameof(registerName));
            }

            if (string.IsNullOrWhiteSpace(machineName))
            {
                throw new ArgumentException("Machine name is required.", nameof(machineName));
            }

            if (authorizedByUserId <= 0)
            {
                throw new ArgumentException("Authorizing admin user is required.", nameof(authorizedByUserId));
            }

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[System].[uspRegisterNewTerminal]"))
            {
                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                command.Parameters.Add("@RegisterCode", SqlDbType.NVarChar, 50).Value = registerCode.Trim();
                command.Parameters.Add("@RegisterName", SqlDbType.NVarChar, 100).Value = registerName.Trim();
                command.Parameters.Add("@MachineName", SqlDbType.NVarChar, 100).Value = machineName.Trim();
                command.Parameters.Add("@AuthorizedByUserId", SqlDbType.Int).Value = authorizedByUserId;

                await connection.OpenAsync();

                var result = await command.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
        }
    }
}
