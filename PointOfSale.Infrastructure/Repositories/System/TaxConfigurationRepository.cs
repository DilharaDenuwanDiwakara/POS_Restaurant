using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Infrastructure.Repositories.System
{
    public class TaxConfigurationRepository : BaseRepository, ITaxConfigurationRepository
    {
        public TaxConfigurationRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        public async Task<IEnumerable<TaxConfiguration>> GetAllAsync()
        {
            var items = new List<TaxConfiguration>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT
                                Id,
                                TaxCode,
                                TaxName,
                                Rate,
                                CalculationOrder,
                                IsActive,
                                IsInclusive,
                                EffectiveDate,
                                AccountCode,
                                UpdatedBy
                            FROM [System].[TaxConfiguration]
                            ORDER BY CalculationOrder, TaxName;";

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                items.Add(MapTaxConfiguration(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while selecting tax configurations.", ex);
            }

            return items;
        }

        public async Task<int> CreateAsync(TaxConfiguration taxConfiguration)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.Text;
                        command.CommandText = @"
                            INSERT INTO [System].[TaxConfiguration]
                            (
                                TaxCode,
                                TaxName,
                                Rate,
                                CalculationOrder,
                                IsActive,
                                IsInclusive,
                                EffectiveDate,
                                AccountCode,
                                CreatedBy,
                                CreatedAt
                            )
                            VALUES
                            (
                                @TaxCode,
                                @TaxName,
                                @Rate,
                                @CalculationOrder,
                                @IsActive,
                                @IsInclusive,
                                @EffectiveDate,
                                @AccountCode,
                                @ModifiedBy,
                                SYSDATETIME()
                            );
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        AddTaxParameters(command, taxConfiguration);

                        await connection.OpenAsync();
                        var result = await command.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while creating a tax configuration.", ex);
            }
        }

        public async Task UpdateAsync(TaxConfiguration taxConfiguration)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.Text;
                        command.CommandText = @"
                            UPDATE [System].[TaxConfiguration]
                            SET
                                TaxCode = @TaxCode,
                                TaxName = @TaxName,
                                Rate = @Rate,
                                CalculationOrder = @CalculationOrder,
                                IsActive = @IsActive,
                                IsInclusive = @IsInclusive,
                                EffectiveDate = @EffectiveDate,
                                AccountCode = @AccountCode,
                                UpdatedBy = @ModifiedBy,
                                UpdatedAt = SYSDATETIME()
                            WHERE Id = @Id;";

                        command.Parameters.Add("@Id", SqlDbType.Int).Value = taxConfiguration.Id;
                        AddTaxParameters(command, taxConfiguration);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating tax configuration with ID {taxConfiguration.Id}.", ex);
            }
        }

        private static void AddTaxParameters(SqlCommand command, TaxConfiguration taxConfiguration)
        {
            command.Parameters.Add("@TaxCode", SqlDbType.NVarChar, 20).Value = taxConfiguration.TaxCode ?? string.Empty;
            command.Parameters.Add("@TaxName", SqlDbType.NVarChar, 100).Value = taxConfiguration.TaxName ?? string.Empty;
            command.Parameters.Add("@Rate", SqlDbType.Decimal).Value = taxConfiguration.Rate;
            command.Parameters["@Rate"].Precision = 18;
            command.Parameters["@Rate"].Scale = 2;
            command.Parameters.Add("@CalculationOrder", SqlDbType.Int).Value = taxConfiguration.CalculationOrder;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = taxConfiguration.IsActive;
            command.Parameters.Add("@IsInclusive", SqlDbType.Bit).Value = taxConfiguration.IsInclusive;
            command.Parameters.Add("@EffectiveDate", SqlDbType.DateTime).Value = taxConfiguration.EffectiveDate;
            command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 50).Value =
                string.IsNullOrWhiteSpace(taxConfiguration.AccountCode)
                    ? (object)DBNull.Value
                    : taxConfiguration.AccountCode;
            command.Parameters.Add("@ModifiedBy", SqlDbType.Int).Value = (object)taxConfiguration.LastModifiedBy ?? DBNull.Value;
        }

        private TaxConfiguration MapTaxConfiguration(IDataRecord record)
        {
            return new TaxConfiguration
            {
                Id = GetValue<int>(record, "Id"),
                TaxCode = GetValue<string>(record, "TaxCode"),
                TaxName = GetValue<string>(record, "TaxName"),
                Rate = GetValue<decimal>(record, "Rate"),
                CalculationOrder = GetValue<int>(record, "CalculationOrder"),
                IsActive = GetValue<bool>(record, "IsActive"),
                IsInclusive = GetValue<bool>(record, "IsInclusive"),
                EffectiveDate = GetValue<DateTime>(record, "EffectiveDate"),
                AccountCode = GetValue<string>(record, "AccountCode"),
                LastModifiedBy = GetValue<int?>(record, "UpdatedBy")
            };
        }
    }
}
