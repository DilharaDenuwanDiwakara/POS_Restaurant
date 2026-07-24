using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class BrandRepository : BaseRepository, IBrandRepository
    {
        public BrandRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<int> CreateAsync(Brand brand)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspInsertBrand]"))
                {
                    AddBrandParameters(command, brand);

                    var brandId = command.Parameters.Add("@BrandId", SqlDbType.Int);
                    brandId.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();

                    await command.ExecuteNonQueryAsync();

                    return (int)brandId.Value;
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the brand.", ex);
            }
        }
        public async Task<IEnumerable<Brand>> GetAllAsync()
        {
            var brands = new List<Brand>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetAllBrands]"))
                {
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            brands.Add(MapBrand(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving the brands.", ex);
            }
            return brands;
        }
        public async Task UpdateAsync(Brand brand)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspUpdateBrand]"))
                {
                    AddBrandIdParameter(command, brand.BrandId);
                    AddBrandParameters(command, brand);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating brand with ID {brand.BrandId}.", ex);
            }
        }
        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspDeleteBrand]"))
                {
                    AddBrandIdParameter(command, id);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting brand with ID {id}.", ex);
            }
        }
        #endregion

        #region Private Methods
        private void AddBrandParameters(SqlCommand command, Brand brand)
        {
            command.Parameters.Add("@BrandName", SqlDbType.NVarChar, 50).Value = brand.BrandName;
        }
        private void AddBrandIdParameter(SqlCommand command, int brandId)
        {
            command.Parameters.Add("@BrandId", SqlDbType.Int).Value = brandId;
        }
        private Brand MapBrand(IDataRecord record)
        {
            return new Brand
            {
                BrandId = GetValue<int>(record, "BrandId"),
                BrandName = GetValue<string>(record, "BrandName"),
                CreatedDate = GetValue<DateTime>(record, "CreatedDate")
            };
        }
        #endregion
    }
}
