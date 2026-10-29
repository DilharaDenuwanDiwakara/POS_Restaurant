using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface IBankPromotionRepository
    {
        Task<IEnumerable<BankPromotionDto>> GetAllAsync();
        Task<int> SaveAsync(BankPromotion promotion, IEnumerable<int> excludedVariantIds);
        Task<IEnumerable<BankBINMapping>> GetBinsAsync(int bankId);
        Task<int> AddBinAsync(BankBINMapping bin);
        Task UpdateBinAsync(BankBINMapping bin);
        Task DeleteBinAsync(int id, int bankId);
    }
}
