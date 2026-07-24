using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Infrastructure.Repositories.Purchasing
{
    public class SupplierRepository : BaseRepository, ISupplierRepository
    {
        public SupplierRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<IEnumerable<SupplierDocument>> GetDocumentsBySupplierIdAsync(int supplierId)
        {
            var documents = new List<SupplierDocument>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetSupplierDocuments]"))
                {
                    command.Parameters.AddWithValue("@SupplierId", supplierId);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            documents.Add(MapSupplierDocument(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while retrieving documents for supplier {supplierId}.", ex);
            }

            return documents;
        }

        public async Task<long> AddDocumentAsync(SupplierDocument document)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspInsertSupplierDocument]"))
                {
                    command.Parameters.Add("@SupplierId", SqlDbType.Int).Value = document.SupplierId;
                    command.Parameters.Add("@DocumentName", SqlDbType.NVarChar, 255).Value = document.DocumentName;
                    command.Parameters.Add("@DocumentUrl", SqlDbType.NVarChar, 1000).Value = document.DocumentUrl;
                    command.Parameters.Add("@UploadedBy", SqlDbType.Int).Value = document.UploadedBy;

                    var outputId = command.Parameters.Add("@DocumentId", SqlDbType.BigInt);
                    outputId.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    return (long)outputId.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while adding the supplier document.", ex);
            }
        }

        public async Task DeleteDocumentAsync(long documentId)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspDeleteSupplierDocument]"))
                {
                    command.Parameters.AddWithValue("@DocumentId", documentId);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting supplier document {documentId}.", ex);
            }
        }

        public async Task<int> CreateAsync(Supplier supplier)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[Purchasing].[uspInsertSupplier]";

                AddSupplierParameters(command, supplier);
                AddContactsParameter(command, supplier.Contacts);

                command.Parameters.AddWithValue("@CreatedBy", supplier.CreatedBy);

                var outputParam = new SqlParameter("@SupplierId", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };
                command.Parameters.Add(outputParam);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return (int)command.Parameters["@SupplierId"].Value;
            }
        }

        public async Task<IEnumerable<Supplier>> GetAllAsync()
        {
            var suppliers = new List<Supplier>();
            var contacts = new List<SupplierContact>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetAllSuppliers]"))
                {
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            suppliers.Add(MapSupplier(reader));
                        }

                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                contacts.Add(MapSupplierContact(reader));
                            }
                        }
                    }

                    var contactsBySupplierId = contacts
                        .GroupBy(contact => contact.SupplierId)
                        .ToDictionary(group => group.Key, group => group.ToList());

                    foreach (var supplier in suppliers)
                    {
                        supplier.Contacts = contactsBySupplierId.TryGetValue(supplier.SupplierId, out var supplierContacts)
                            ? supplierContacts
                            : new List<SupplierContact>();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving suppliers.", ex);
            }

            return suppliers;
        }

        public async Task<Supplier> GetByIdAsync(int supplierId)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();

                    Supplier supplier = null;
                    const string sql = @"
                    SELECT
                        Id AS SupplierId,
                        Code AS SupplierCode,
                        Name AS SupplierName,
                        TaxRegistrationNumber,
                        BusinessRegistrationNumber,
                        Address,
                        DefaultPaymentMethod,
                        BankName,
                        BankBranch,
                        AccountNumber,
                        AccountName,
                        IsCredit,
                        CreditLimit,
                        CreditPeriodDays,
                        IsActive,
                        CreatedBy,
                        UpdatedBy
                    FROM [Purchasing].[Supplier]
                    WHERE Id = @SupplierId;";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@SupplierId", SqlDbType.Int).Value = supplierId;

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                supplier = MapSupplier(reader);
                            }
                        }
                    }

                    if (supplier == null)
                    {
                        return null;
                    }

                    supplier.Contacts = await GetContactsBySupplierIdAsync(connection, supplier.SupplierId);
                    return supplier;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while retrieving supplier {supplierId}.", ex);
            }
        }

        public async Task UpdateAsync(Supplier supplier)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[Purchasing].[uspUpdateSupplier]";

                // 1. Core Identifiers
                command.Parameters.AddWithValue("@SupplierId", supplier.SupplierId);
                command.Parameters.AddWithValue("@UpdatedBy", (object)supplier.UpdatedBy ?? DBNull.Value);

                // 2. Add the Master Data
                AddSupplierParameters(command, supplier);

                // 3. Add the Child Data (TVP)
                AddContactsParameter(command, supplier.Contacts);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }
        #endregion

        #region Private Methods
        private void AddSupplierParameters(SqlCommand command, Supplier supplier)
        {
            AddParameter(command, "@SupplierName", SqlDbType.NVarChar, supplier.SupplierName, 100);
            AddParameter(command, "@TaxRegistrationNumber", SqlDbType.NVarChar, supplier.TaxRegistrationNumber, 50);
            AddParameter(command, "@BusinessRegistrationNumber", SqlDbType.NVarChar, supplier.BusinessRegistrationNumber, 50);
            AddParameter(command, "@Address", SqlDbType.NVarChar, supplier.Address, 255);

            AddParameter(command, "@DefaultPaymentMethod", SqlDbType.NVarChar, supplier.DefaultPaymentMethod?.ToString(), 50);
            AddParameter(command, "@BankName", SqlDbType.NVarChar, supplier.BankName, 100);
            AddParameter(command, "@BankBranch", SqlDbType.NVarChar, supplier.BankBranch, 50);
            AddParameter(command, "@AccountName", SqlDbType.NVarChar, supplier.AccountName, 100);
            AddParameter(command, "@AccountNumber", SqlDbType.NVarChar, supplier.AccountNumber, 50);

            AddParameter(command, "@IsCredit", SqlDbType.Bit, supplier.IsCredit);
            AddParameter(command, "@CreditPeriodDays", SqlDbType.Int, supplier.CreditPeriodDays);
            AddParameter(command, "@CreditLimit", SqlDbType.Decimal, supplier.CreditLimit, precision: 18, scale: 2);
            AddParameter(command, "@IsActive", SqlDbType.Bit, supplier.IsActive);
        }

        private void AddContactsParameter(SqlCommand command, IEnumerable<SupplierContact> contacts)
        {
            var table = new DataTable();

            // MUST INCLUDE ID FOR THE MERGE STATEMENT!
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("ContactName", typeof(string));
            table.Columns.Add("PhoneNumber", typeof(string));
            table.Columns.Add("IsWhatsApp", typeof(bool));
            table.Columns.Add("EmailAddress", typeof(string));
            table.Columns.Add("IsPrimary", typeof(bool));
            table.Columns.Add("IsActive", typeof(bool));

            if (contacts != null)
            {
                foreach (var contact in contacts)
                {
                    table.Rows.Add(
                        contact.Id, // Ensure your ViewModel leaves this at 0 for new contacts
                        contact.ContactName,
                        string.IsNullOrWhiteSpace(contact.PhoneNumber) ? (object)DBNull.Value : contact.PhoneNumber,
                        contact.IsWhatsApp,
                        string.IsNullOrWhiteSpace(contact.EmailAddress) ? (object)DBNull.Value : contact.EmailAddress,
                        contact.IsPrimary,
                        contact.IsActive
                    );
                }
            }

            var param = command.Parameters.AddWithValue("@SupplierContacts", table);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "Purchasing.tvpSupplierContact";
        }

        private async Task<List<SupplierContact>> GetContactsBySupplierIdAsync(SqlConnection connection, int supplierId)
        {
            var contacts = new List<SupplierContact>();

            const string sql = @"
                SELECT
                    Id,
                    SupplierId,
                    ContactName,
                    PhoneNumber,
                    IsWhatsApp,
                    EmailAddress,
                    IsPrimary,
                    IsActive
                FROM [Purchasing].[SupplierContact]
                WHERE SupplierId = @SupplierId
                ORDER BY IsPrimary DESC, Id;";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@SupplierId", SqlDbType.Int).Value = supplierId;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        contacts.Add(MapSupplierContact(reader));
                    }
                }
            }

            return contacts;
        }

        private Supplier MapSupplier(IDataRecord record)
        {
            return new Supplier
            {
                SupplierId = GetValue<int>(record, "SupplierId"),
                SupplierCode = GetValue<string>(record, "SupplierCode"),
                SupplierName = GetValue<string>(record, "SupplierName"),

                TaxRegistrationNumber = GetValue<string>(record, "TaxRegistrationNumber"),
                BusinessRegistrationNumber = GetValue<string>(record, "BusinessRegistrationNumber"),
                Address = GetValue<string>(record, "Address"),
                ContactsCount = GetOptionalValue<int>(record, "ContactsCount"),
                ContactDetailsTooltip = GetOptionalValue<string>(record, "ContactDetailsTooltip") ?? string.Empty,

                IsCredit = GetValue<bool>(record, "IsCredit"),
                CreditPeriodDays = GetNullableValue<int>(record, "CreditPeriodDays"),
                CreditLimit = GetNullableValue<decimal>(record, "CreditLimit"),
                DefaultPaymentMethod = Enum.TryParse(GetOptionalValue<string>(record, "DefaultPaymentMethod"), out SupplierPaymentMethod method)
                    ? (SupplierPaymentMethod?)method
                    : null,

                BankName = GetValue<string>(record, "BankName"),
                BankBranch = GetValue<string>(record, "BankBranch"),
                AccountName = GetValue<string>(record, "AccountName"),
                AccountNumber = GetValue<string>(record, "AccountNumber"),

                IsActive = GetValue<bool>(record, "IsActive"),
                CreatedBy = GetValue<int>(record, "CreatedBy"),
                UpdatedBy = GetNullableValue<int>(record, "UpdatedBy")
            };
        }

        private T GetOptionalValue<T>(IDataRecord record, string columnName)
        {
            if (!HasColumn(record, columnName))
            {
                return default(T);
            }

            var value = record[columnName];
            if (value == DBNull.Value || value == null)
            {
                return default(T);
            }

            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType);

            return (T)Convert.ChangeType(value, underlyingType ?? targetType);
        }

        private bool HasColumn(IDataRecord record, string columnName)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private SupplierContact MapSupplierContact(IDataRecord record)
        {
            return new SupplierContact
            {
                Id = GetValue<int>(record, "Id"),
                SupplierId = GetValue<int>(record, "SupplierId"),
                ContactName = GetValue<string>(record, "ContactName"),
                PhoneNumber = GetValue<string>(record, "PhoneNumber"),
                IsWhatsApp = GetValue<bool>(record, "IsWhatsApp"),
                EmailAddress = GetValue<string>(record, "EmailAddress"),
                IsPrimary = GetValue<bool>(record, "IsPrimary"),
                IsActive = GetValue<bool>(record, "IsActive")
            };
        }

        private SupplierDocument MapSupplierDocument(IDataRecord record)
        {
            return new SupplierDocument
            {
                Id = GetValue<long>(record, "Id"),
                SupplierId = GetValue<int>(record, "SupplierId"),
                DocumentName = GetValue<string>(record, "DocumentName"),
                DocumentUrl = GetValue<string>(record, "DocumentUrl"),
                UploadedDate = GetValue<DateTime>(record, "UploadedDate"),
                UploadedBy = GetValue<int>(record, "UploadedBy")
            };
        }

        private T? GetNullableValue<T>(IDataRecord record, string columnName) where T : struct
        {
            var value = record[columnName];
            if (value == DBNull.Value || value == null)
            {
                return null;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }

        private void AddParameter(
            SqlCommand cmd,
            string name,
            SqlDbType dbType,
            object value,
            int? size = null,
            byte? precision = null,
            byte? scale = null)
        {
            var param = size.HasValue ? cmd.Parameters.Add(name, dbType, size.Value) : cmd.Parameters.Add(name, dbType);

            if (precision.HasValue)
            {
                param.Precision = precision.Value;
            }

            if (scale.HasValue)
            {
                param.Scale = scale.Value;
            }

            if (value == null)
            {
                param.Value = DBNull.Value;
            }
            else if (value is string str && string.IsNullOrWhiteSpace(str))
            {
                param.Value = DBNull.Value;
            }
            else
            {
                param.Value = value;
            }
        }
        #endregion
    }
}
