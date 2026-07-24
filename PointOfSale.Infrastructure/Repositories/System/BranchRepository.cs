using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Infrastructure.Repositories.System
{
    public class BranchRepository : BaseRepository, IBranchRepository
    {

        public BranchRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        public async Task<IEnumerable<Branch>> GetAllAsync()
        {
            var branches = new List<Branch>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[System].[uspGetAllBranches]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                branches.Add(MapBranch(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting the branches.", ex);
            }
            return branches;
        }

        private Branch MapBranch(IDataRecord record)
        {
            return new Branch
            {
                Id = GetValue<int>(record, "Id"),
                Name = GetValue<string>(record, "BranchName"),
                Address = GetValue<string>(record, "Address"),
                IsMainBranch = GetValue<bool>(record, "IsMainBranch"),
                IsActive = GetValue<bool>(record, "IsActive")
            };
        }
    }
}
