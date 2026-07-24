using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class LocationRepository : BaseRepository, ILocationRepository
    {
        public LocationRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        public async Task<IEnumerable<Location>> GetByBranchIdAsync(int branchId)
        {
            var locations = new List<Location>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Inventory].[uspGetLocationsByBranchId]"))
            {
                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        locations.Add(new Location
                        {
                            Id = GetValue<int>(reader, "Id"),
                            BranchId = GetValue<int>(reader, "BranchId"),
                            Name = GetValue<string>(reader, "Name")
                        });
                    }
                }
            }

            return locations;
        }
    }
}
