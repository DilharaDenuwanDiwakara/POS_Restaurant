using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class WastageReasonRepository : BaseRepository, IWastageReasonRepository
    {
        public WastageReasonRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        #region Public Methods

        public async Task<int> CreateAsync(WastageReason wastageReason)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspInsertWastageReason]"))
                {
                    AddReasonParameters(command, wastageReason);

                    var reasonId = command.Parameters.Add("@Id", SqlDbType.Int);
                    reasonId.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();

                    await command.ExecuteNonQueryAsync();

                    return (int)reasonId.Value;
                }
            }
            catch (SqlException ex)
            {
                // 2627/2601 = Unique Index/Constraint violation (Duplicate Name)
                // 50000 = Custom user-defined error from SP
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occurred while creating the wastage reason.", ex);
            }
        }

        public async Task<IEnumerable<WastageReason>> GetAllAsync()
        {
            var reasons = new List<WastageReason>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetAllWastageReasons]"))
                {
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            reasons.Add(MapWastageReason(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving the wastage reasons.", ex);
            }

            return reasons;
        }

        public async Task UpdateAsync(WastageReason wastageReason)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspUpdateWastageReason]"))
                {
                    AddReasonIdParameter(command, wastageReason.Id);
                    AddReasonParameters(command, wastageReason);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                // Handle duplicate name on update if necessary
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException($"A database error occurred while updating wastage reason with ID {wastageReason.Id}.", ex);
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspDeleteWastageReason]"))
                {
                    AddReasonIdParameter(command, id);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                // 547 is foreign key constraint (if this reason is used in actual wastage records)
                if (ex.Number == 547)
                {
                    throw new InvalidOperationException("Cannot delete this reason because it is currently in use.", ex);
                }

                throw new InvalidOperationException($"A database error occurred while deleting wastage reason with ID {id}.", ex);
            }
        }

        #endregion

        #region Private Methods

        private void AddReasonParameters(SqlCommand command, WastageReason wastageReason)
        {
            // Assuming the column in DB is 'Reason' and max length is 100
            command.Parameters.Add("@Reason", SqlDbType.NVarChar, 100).Value = wastageReason.Reason;
        }

        private void AddReasonIdParameter(SqlCommand command, int id)
        {
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        }

        private WastageReason MapWastageReason(IDataRecord record)
        {
            return new WastageReason
            {
                Id = GetValue<int>(record, "Id"),
                Reason = GetValue<string>(record, "Reason")
            };
        }

        #endregion
    }
}
