using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class MealPeriodRepository : BaseRepository, IMealPeriodRepository
    {
        public MealPeriodRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<int> CreateAsync(MealPeriod mealPeriod)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspInsertMealPeriod]"))
                    {
                        AddMealPeriodParameters(command, mealPeriod);

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
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the meal period.", ex);
            }
        }

        public async Task<IEnumerable<MealPeriod>> GetAllAsync()
        {
            var mealPeriods = new List<MealPeriod>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspGetAllMealPeriods]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                mealPeriods.Add(MapMealPeriod(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Database error loading meal periods.", ex);
            }

            return mealPeriods;
        }

        public async Task UpdateAsync(MealPeriod mealPeriod)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspUpdateMealPeriod]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = mealPeriod.Id;
                        AddMealPeriodParameters(command, mealPeriod);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating meal period with ID {mealPeriod.Id}.", ex);
            }
        }

        private void AddMealPeriodParameters(SqlCommand command, MealPeriod mealPeriod)
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = mealPeriod.Name;
            command.Parameters.Add("@DefaultStartTime", SqlDbType.Time).Value = mealPeriod.DefaultStartTime;
            command.Parameters.Add("@DefaultEndTime", SqlDbType.Time).Value = mealPeriod.DefaultEndTime;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = mealPeriod.IsActive;
        }

        private MealPeriod MapMealPeriod(IDataRecord record)
        {
            return new MealPeriod
            {
                Id = GetValue<int>(record, "Id"),
                Name = GetValue<string>(record, "Name"),
                DefaultStartTime = GetValue<TimeSpan>(record, "DefaultStartTime"),
                DefaultEndTime = GetValue<TimeSpan>(record, "DefaultEndTime"),
                IsActive = GetValue<bool>(record, "IsActive")
            };
        }
    }
}
