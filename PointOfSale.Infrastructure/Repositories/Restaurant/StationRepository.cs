using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class StationRepository : BaseRepository, IStationRepository
    {
        public StationRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<int> CreateAsync(Station station)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspInsertStation]"))
                    {
                        AddStationParameters(command, station);

                        var id = command.Parameters.Add("@Id", SqlDbType.Int);
                        id.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)id.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the station.", ex);
            }
        }
        public async Task<IEnumerable<Station>> GetAllAsync(int branchId)
        {
            var stations = new List<Station>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspGetAllStations]"))
                    {
                        await connection.OpenAsync();

                        command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                stations.Add(MapStation(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                // Log error here
                throw new InvalidOperationException("Database error loading stations.", ex);
            }

            return stations;
        }
        public async Task UpdateAsync(Station station)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspUpdateStation]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = station.Id;
                        AddStationParameters(command, station);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating station with ID {station.Id}.", ex);
            }
        }

        #region Private Method
        private void AddStationParameters(SqlCommand command, Station station)
        {
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = station.BranchId;
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = station.Name;

            object printerIpValue = string.IsNullOrEmpty(station.PrinterIP) ? (object)DBNull.Value : station.PrinterIP;
            command.Parameters.Add("@PrinterIP", SqlDbType.NVarChar, 50).Value = printerIpValue;

            object printerNameValue = string.IsNullOrEmpty(station.PrinterName) ? (object)DBNull.Value : station.PrinterName;
            command.Parameters.Add("@PrinterName", SqlDbType.NVarChar, 100).Value = printerNameValue;
        }
        private Station MapStation(IDataRecord record)
        {
            return new Station
            {
                Id = GetValue<int>(record, "Id"),
                BranchId = GetValue<int>(record, "BranchId"),
                Name = GetValue<string>(record, "StationName"),
                PrinterIP = GetValue<string>(record, "PrinterIP"),
                PrinterName = GetValue<string>(record, "PrinterName")
            };
        }
        #endregion
    }
}
