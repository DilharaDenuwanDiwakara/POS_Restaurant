using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Common;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Infrastructure.Repositories.System
{
    public class CompanyRepository : BaseRepository, ICompanyRepository
    {
        public CompanyRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<Company> GetAsync()
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                            SELECT TOP (1)
                                [Id],
                                [TradingName],
                                [LegalName],
                                [BusinessRegistrationNumber],
                                [TaxRegistrationNumber],
                                [AddressLine1],
                                [AddressLine2],
                                [ContactNumber],
                                [Email],
                                [Website],
                                [BaseCurrency],
                                [ReceiptFooterText],
                                [DocumentTerms],
                                [CompanyLogo]
                            FROM [System].[Company]
                            ORDER BY [Id] DESC;";

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapCompany(reader);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(BuildDatabaseErrorMessage("selecting", ex), ex);
            }

            return null;
        }

        public async Task<int> CreateAsync(Company company)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                            INSERT INTO [System].[Company]
                            (
                                [TradingName],
                                [LegalName],
                                [BusinessRegistrationNumber],
                                [TaxRegistrationNumber],
                                [AddressLine1],
                                [AddressLine2],
                                [ContactNumber],
                                [Email],
                                [Website],
                                [BaseCurrency],
                                [ReceiptFooterText],
                                [DocumentTerms],
                                [CompanyLogo],
                                [CreatedBy]
                            )
                            VALUES
                            (
                                @TradingName,
                                @LegalName,
                                @BusinessRegistrationNumber,
                                @TaxRegistrationNumber,
                                @AddressLine1,
                                @AddressLine2,
                                @ContactNumber,
                                @Email,
                                @Website,
                                @BaseCurrency,
                                @ReceiptFooterText,
                                @DocumentTerms,
                                @CompanyLogo,
                                @CreatedBy
                            );
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    AddCompanyParameters(command, company);

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(BuildDatabaseErrorMessage("creating", ex), ex);
            }
        }

        public async Task UpdateAsync(Company company)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        UPDATE [System].[Company]
                        SET
                            [TradingName] = @TradingName,
                            [LegalName] = @LegalName,
                            [BusinessRegistrationNumber] = @BusinessRegistrationNumber,
                            [TaxRegistrationNumber] = @TaxRegistrationNumber,
                            [AddressLine1] = @AddressLine1,
                            [AddressLine2] = @AddressLine2,
                            [ContactNumber] = @ContactNumber,
                            [Email] = @Email,
                            [Website] = @Website,
                            [BaseCurrency] = @BaseCurrency,
                            [ReceiptFooterText] = @ReceiptFooterText,
                            [DocumentTerms] = @DocumentTerms,
                            [CompanyLogo] = @CompanyLogo
                        WHERE [Id] = @Id;";

                    command.Parameters.Add("@Id", SqlDbType.Int).Value = company.Id;
                    AddCompanyParameters(command, company);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(BuildDatabaseErrorMessage("updating", ex), ex);
            }
        }

        private static string BuildDatabaseErrorMessage(string operation, SqlException ex)
        {
            var baseMessage = $"A database error occurred while {operation} company details.";

            if (ex.Number == 207 || ex.Message.IndexOf("Invalid column name", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return $"{baseMessage} The [System].[Company] schema is outdated. Run Database/System/CompanyDetails_Migration.sql. SQL Server: {ex.Message}";
            }

            if (ex.Number == 208 || ex.Message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return $"{baseMessage} The [System].[Company] table was not found. Verify the database deployment. SQL Server: {ex.Message}";
            }

            return $"{baseMessage} SQL Server: {ex.Message}";
        }

        private static void AddCompanyParameters(SqlCommand command, Company company)
        {
            if (company.CompanyLogo != null && company.CompanyLogo.LongLength > FileUploadConstraints.MaxFileSizeBytes)
            {
                throw new InvalidOperationException(FileUploadConstraints.BuildFileTooLargeMessage("Company logo"));
            }

            command.Parameters.Add("@TradingName", SqlDbType.NVarChar, 150).Value = (object)company.TradingName ?? DBNull.Value;
            command.Parameters.Add("@LegalName", SqlDbType.NVarChar, 150).Value = (object)company.LegalName ?? DBNull.Value;
            command.Parameters.Add("@BusinessRegistrationNumber", SqlDbType.NVarChar, 50).Value = (object)company.BusinessRegistrationNumber ?? DBNull.Value;
            command.Parameters.Add("@TaxRegistrationNumber", SqlDbType.NVarChar, 50).Value = (object)company.TaxRegistrationNumber ?? DBNull.Value;
            command.Parameters.Add("@AddressLine1", SqlDbType.NVarChar, 150).Value = (object)company.AddressLine1 ?? DBNull.Value;
            command.Parameters.Add("@AddressLine2", SqlDbType.NVarChar, 150).Value = (object)company.AddressLine2 ?? DBNull.Value;
            command.Parameters.Add("@ContactNumber", SqlDbType.NVarChar, 50).Value = (object)company.ContactNumber ?? DBNull.Value;
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = (object)company.Email ?? DBNull.Value;
            command.Parameters.Add("@Website", SqlDbType.NVarChar, 100).Value = (object)company.Website ?? DBNull.Value;
            command.Parameters.Add("@BaseCurrency", SqlDbType.NVarChar, 10).Value = (object)company.BaseCurrency ?? DBNull.Value;
            command.Parameters.Add("@ReceiptFooterText", SqlDbType.NVarChar, 250).Value = (object)company.ReceiptFooterText ?? DBNull.Value;
            command.Parameters.Add("@DocumentTerms", SqlDbType.NVarChar, 500).Value = (object)company.DocumentTerms ?? DBNull.Value;
            command.Parameters.Add("@CompanyLogo", SqlDbType.VarBinary, -1).Value = (object)company.CompanyLogo ?? DBNull.Value;
            command.Parameters.Add("CreatedBy", SqlDbType.Int).Value = company.CreatedBy;
        }

        private Company MapCompany(IDataRecord record)
        {
            return new Company
            {
                Id = GetValue<int>(record, "Id"),
                TradingName = GetValue<string>(record, "TradingName"),
                LegalName = GetValue<string>(record, "LegalName"),
                BusinessRegistrationNumber = GetValue<string>(record, "BusinessRegistrationNumber"),
                TaxRegistrationNumber = GetValue<string>(record, "TaxRegistrationNumber"),
                AddressLine1 = GetValue<string>(record, "AddressLine1"),
                AddressLine2 = GetValue<string>(record, "AddressLine2"),
                ContactNumber = GetValue<string>(record, "ContactNumber"),
                Email = GetValue<string>(record, "Email"),
                Website = GetValue<string>(record, "Website"),
                BaseCurrency = GetValue<string>(record, "BaseCurrency"),
                ReceiptFooterText = GetValue<string>(record, "ReceiptFooterText"),
                DocumentTerms = GetValue<string>(record, "DocumentTerms"),
                CompanyLogo = record["CompanyLogo"] == DBNull.Value ? null : (byte[])record["CompanyLogo"]
            };
        }
    }
}
