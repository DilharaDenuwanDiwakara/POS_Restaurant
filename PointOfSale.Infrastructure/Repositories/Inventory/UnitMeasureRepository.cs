using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class UnitMeasureRepository : BaseRepository, IUnitMeasureRepository
    {
        public UnitMeasureRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<int> CreateAsync(UnitMeasure unitMeasure)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspInsertUnitMeasure]"))
                    {
                        AddUnitMeasureParameters(command, unitMeasure);

                        var unitMeasureId = command.Parameters.Add("@UnitMeasureId", SqlDbType.Int);
                        unitMeasureId.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)unitMeasureId.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the unit measure.", ex);
            }
        }

        public async Task<IEnumerable<UnitMeasure>> GetAllAsync()
        {
            var unitMeasure = new List<UnitMeasure>();
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspGetAllUnitMeasure]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                unitMeasure.Add(MapUnitMeasure(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting the unit measure.", ex);
            }
            return unitMeasure;
        }
        public async Task UpdateAsync(UnitMeasure unitMeasure)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspUpdateUnitMeasure]"))
                    {
                        command.Parameters.Add("@UnitMeasureId", SqlDbType.Int).Value = unitMeasure.UnitMeasureId;
                        AddUnitMeasureParameters(command, unitMeasure);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating unit measure with ID {unitMeasure.UnitMeasureId}.", ex);
            }
        }
        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspDeleteUnitMeasure]"))
                    {
                        command.Parameters.Add("@UnitMeasureId", SqlDbType.Int).Value = id;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting unit measure with ID {id}.", ex);
            }
        }
        #endregion

        #region Private Methods
        private void AddUnitMeasureParameters(SqlCommand command, UnitMeasure unitMeasure)
        {
            command.Parameters.Add("@UnitMeasureCode", SqlDbType.NVarChar, 10).Value = unitMeasure.Code;
            command.Parameters.Add("@Name", SqlDbType.NVarChar).Value = unitMeasure.UnitMeasureName;
        }
        private UnitMeasure MapUnitMeasure(IDataRecord record)
        {
            return new UnitMeasure
            {
                UnitMeasureId = GetValue<int>(record, "Id"),
                Code = GetValue<string>(record, "Code"),
                UnitMeasureName = GetValue<string>(record, "Name")
            };
        }
        #endregion
    }
}
