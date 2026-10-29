using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Infrastructure.Repositories.System
{
    public class BINMappingRepository : BaseRepository, IBINMappingRepository
    {
        public BINMappingRepository(DatabaseConnection connection) : base(connection) { }

        public async Task<IEnumerable<BankBINMapping>> GetByBankIdAsync(int bankId)
        {
            var result = new List<BankBINMapping>();
            using (var connection = GetConnection())
            using (var command = new SqlCommand("SELECT Id, BINPrefix, IsActive, CardType, CardBrand FROM [System].[BankBINMapping] WHERE BankId=@BankId ORDER BY BINPrefix;", connection))
            {
                command.Parameters.Add("@BankId", SqlDbType.Int).Value = bankId;
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync())
                        result.Add(new BankBINMapping { Id = reader.GetInt32(0), BankId = bankId, BINPrefix = reader.GetString(1), IsActive = reader.GetBoolean(2), CardType = reader.IsDBNull(3) ? null : reader.GetString(3), CardBrand = reader.IsDBNull(4) ? null : reader.GetString(4) });
            }
            return result;
        }

        public Task<int> InsertAsync(BankBINMapping bin) => SaveBinAsync(bin, false);

        public async Task UpdateAsync(BankBINMapping bin)
        {
            await SaveBinAsync(bin, true);
        }

        public async Task DeleteAsync(int id, int bankId)
        {
            if (id <= 0 || bankId <= 0) throw new ArgumentException("Select a saved BIN and its bank.");
            using (var connection = GetConnection())
            using (var command = new SqlCommand("DELETE FROM [System].[BankBINMapping] WHERE Id=@Id AND BankId=@BankId;", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@BankId", SqlDbType.Int).Value = bankId;
                await connection.OpenAsync();
                if (await command.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("The BIN no longer exists for this bank. Reload the bank's BINs.");
            }
        }

        private async Task<int> SaveBinAsync(BankBINMapping bin, bool update)
        {
            if (bin == null) throw new ArgumentNullException(nameof(bin));
            if ((update && bin.Id <= 0) || bin.BankId <= 0 || bin.BINPrefix == null || bin.BINPrefix.Length != 6 || bin.BINPrefix.Any(c => c < '0' || c > '9'))
                throw new ArgumentException("Select a bank and enter exactly six digits for the BIN prefix.");
            string cardType = null;
            if (!string.IsNullOrWhiteSpace(bin.CardType))
            {
                cardType = new[] { "CREDIT", "DEBIT" }.FirstOrDefault(t => string.Equals(t, bin.CardType.Trim(), StringComparison.OrdinalIgnoreCase));
                if (cardType == null) throw new ArgumentException("BIN card type must be Credit or Debit, or empty for an unclassified BIN.");
            }
            var cardBrand = string.IsNullOrWhiteSpace(bin.CardBrand) ? null : bin.CardBrand.Trim().ToUpperInvariant();
            if (cardBrand?.Length > 30) throw new ArgumentException("Card brand must be at most 30 characters.");
            var sql = update
                ? @"UPDATE [System].[BankBINMapping] SET BINPrefix=@BIN, IsActive=@Active, CardType=@CardType, CardBrand=@CardBrand
OUTPUT INSERTED.Id WHERE Id=@Id AND BankId=@BankId;"
                : @"INSERT INTO [System].[BankBINMapping] (BankId,BINPrefix,IsActive,CardType,CardBrand)
OUTPUT INSERTED.Id VALUES (@BankId,@BIN,@Active,@CardType,@CardBrand);";
            using (var connection = GetConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                if (update) command.Parameters.Add("@Id", SqlDbType.Int).Value = bin.Id;
                command.Parameters.Add("@BankId", SqlDbType.Int).Value = bin.BankId;
                command.Parameters.Add("@BIN", SqlDbType.VarChar, 6).Value = bin.BINPrefix;
                command.Parameters.Add("@Active", SqlDbType.Bit).Value = bin.IsActive;
                command.Parameters.Add("@CardType", SqlDbType.VarChar, 20).Value = (object)cardType ?? DBNull.Value;
                command.Parameters.Add("@CardBrand", SqlDbType.VarChar, 30).Value = (object)cardBrand ?? DBNull.Value;
                await connection.OpenAsync();
                try
                {
                    var savedId = await command.ExecuteScalarAsync();
                    if (savedId == null || savedId == DBNull.Value)
                        throw new InvalidOperationException("The BIN no longer exists for this bank. Cancel editing and reload the bank's BINs.");
                    return Convert.ToInt32(savedId);
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                { throw new InvalidOperationException("That BIN already exists for this bank or is active for another bank.", ex); }
            }
        }

    }
}
