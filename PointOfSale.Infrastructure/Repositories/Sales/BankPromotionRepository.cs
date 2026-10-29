using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Infrastructure.Repositories.System;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class BankPromotionRepository : BaseRepository, IBankPromotionRepository
    {
        private readonly IBINMappingRepository _bins;

        public BankPromotionRepository(DatabaseConnection connection) : this(connection, new BINMappingRepository(connection)) { }

        public BankPromotionRepository(DatabaseConnection connection, IBINMappingRepository bins) : base(connection)
        {
            _bins = bins ?? throw new ArgumentNullException(nameof(bins));
        }

        public async Task<IEnumerable<BankPromotionDto>> GetAllAsync()
        {
            const string sql = @"
SELECT p.Id, p.BankId, b.BankName, p.PromoName, p.DiscountPercentage, p.IsActive,
       v.Id AS VariantId, v.ItemCode,
       CASE WHEN v.Name = 'STANDARD' THEN m.Name ELSE m.Name + ' - ' + v.Name END AS VariantName, p.ValidFrom, p.ValidTo, p.RequiredCardType
FROM [Sales].[BankPromotion] p
JOIN [System].[Bank] b ON b.Id = p.BankId
LEFT JOIN [Sales].[BankPromoExcludedVariant] e ON e.BankPromotionId = p.Id
LEFT JOIN [Restaurant].[Variant] v ON v.Id = e.VariantId
LEFT JOIN [Restaurant].[MenuItem] m ON m.Id = v.MenuItemId
ORDER BY p.Id DESC, v.Id;";
            var result = new Dictionary<int, BankPromotionDto>();
            using (var connection = GetConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetInt32(0);
                        if (!result.TryGetValue(id, out var promotion))
                        {
                            promotion = new BankPromotionDto
                            {
                                Id = id, BankId = reader.GetInt32(1), BankName = reader.GetString(2),
                                PromoName = reader.GetString(3), DiscountPercentage = reader.GetDecimal(4), IsActive = reader.GetBoolean(5),
                                ValidFrom = reader.GetDateTime(9), ValidTo = reader.GetDateTime(10), RequiredCardType = reader.GetString(11)
                            };
                            result.Add(id, promotion);
                        }
                        if (!reader.IsDBNull(6))
                            promotion.ExcludedVariants.Add(new Variant
                            {
                                Id = reader.GetInt32(6), ItemCode = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                Name = reader.IsDBNull(8) ? "Variant #" + reader.GetInt32(6) : reader.GetString(8)
                            });
                    }
                }
            }
            return result.Values.OrderByDescending(p => p.Id).ToList();
        }

        public Task<IEnumerable<BankBINMapping>> GetBinsAsync(int bankId) => _bins.GetByBankIdAsync(bankId);
        public Task<int> AddBinAsync(BankBINMapping bin) => _bins.InsertAsync(bin);
        public Task UpdateBinAsync(BankBINMapping bin) => _bins.UpdateAsync(bin);
        public Task DeleteBinAsync(int id, int bankId) => _bins.DeleteAsync(id, bankId);

        public async Task<int> SaveAsync(BankPromotion promotion, IEnumerable<int> excludedVariantIds)
        {
            if (promotion == null) throw new ArgumentNullException(nameof(promotion));
            if (excludedVariantIds == null) throw new ArgumentNullException(nameof(excludedVariantIds));
            var variants = excludedVariantIds.ToList();
            var requiredCardType = new[] { "ALL", "CREDIT", "DEBIT" }.FirstOrDefault(t =>
                string.Equals(t, promotion.RequiredCardType?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (requiredCardType == null) throw new ArgumentException("Required card type must be All, Credit or Debit.");
            if (promotion.Id < 0 || promotion.BankId <= 0 || string.IsNullOrWhiteSpace(promotion.PromoName) || promotion.PromoName.Trim().Length > 200)
                throw new ArgumentException("Select a bank and enter a promotion name of up to 200 characters.");
            if (promotion.DiscountPercentage <= 0 || promotion.DiscountPercentage > 100 || decimal.Round(promotion.DiscountPercentage, 2) != promotion.DiscountPercentage)
                throw new ArgumentException("Discount must be greater than 0 and at most 100, with up to two decimal places.");
            if (promotion.ValidFrom < SqlDateTime.MinValue.Value ||
                promotion.ValidTo > SqlDateTime.MaxValue.Value || promotion.ValidTo <= promotion.ValidFrom)
                throw new ArgumentException("Valid To must be strictly later than Valid From, within the SQL DATETIME range (1753-9999).");
            // SQL DATETIME rounds to approximately 3ms. Validate the stored values too.
            if (new SqlDateTime(promotion.ValidTo).Value <= new SqlDateTime(promotion.ValidFrom).Value)
                throw new ArgumentException("The validity interval is too short for SQL DATETIME precision.");
            if (variants.Distinct().Count() != variants.Count || variants.Any(v => v <= 0))
                throw new ArgumentException("Excluded variants must be unique and valid.");

            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int id;
                        using (var command = new SqlCommand(@"
IF @Id = 0
BEGIN
    INSERT INTO [Sales].[BankPromotion] (BankId, PromoName, DiscountPercentage, IsActive, ValidFrom, ValidTo, RequiredCardType)
    OUTPUT INSERTED.Id VALUES (@BankId, @PromoName, @DiscountPercentage, @IsActive, @ValidFrom, @ValidTo, @RequiredCardType);
END
ELSE
BEGIN
    UPDATE [Sales].[BankPromotion] SET BankId=@BankId, PromoName=@PromoName,
        DiscountPercentage=@DiscountPercentage, IsActive=@IsActive, ValidFrom=@ValidFrom, ValidTo=@ValidTo, RequiredCardType=@RequiredCardType
    OUTPUT INSERTED.Id WHERE Id=@Id;
END;", connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.Int).Value = promotion.Id;
                            command.Parameters.Add("@BankId", SqlDbType.Int).Value = promotion.BankId;
                            command.Parameters.Add("@PromoName", SqlDbType.NVarChar, 200).Value = promotion.PromoName.Trim();
                            var percentage = command.Parameters.Add("@DiscountPercentage", SqlDbType.Decimal);
                            percentage.Precision = 5;
                            percentage.Scale = 2;
                            percentage.Value = promotion.DiscountPercentage;
                            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = promotion.IsActive;
                            command.Parameters.Add("@ValidFrom", SqlDbType.DateTime).Value = promotion.ValidFrom;
                            command.Parameters.Add("@ValidTo", SqlDbType.DateTime).Value = promotion.ValidTo;
                            command.Parameters.Add("@RequiredCardType", SqlDbType.VarChar, 20).Value = requiredCardType;
                            var savedId = await command.ExecuteScalarAsync();
                            if (savedId == null || savedId == DBNull.Value)
                                throw new InvalidOperationException("The promotion no longer exists. Clear the form to create a new one.");
                            id = Convert.ToInt32(savedId);
                        }

                        using (var command = new SqlCommand(@"
DELETE FROM [Sales].[BankPromoExcludedVariant] WHERE BankPromotionId=@Id;", connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                            await command.ExecuteNonQueryAsync();
                        }
                        foreach (var variantId in variants)
                        {
                            using (var command = new SqlCommand("INSERT INTO [Sales].[BankPromoExcludedVariant] (BankPromotionId,VariantId) VALUES (@Id,@VariantId);", connection, transaction))
                            {
                                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                                command.Parameters.Add("@VariantId", SqlDbType.Int).Value = variantId;
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                        transaction.Commit();
                        return id;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
