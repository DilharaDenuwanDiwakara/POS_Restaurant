using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface IDiscountRepository
    {
        Task<IEnumerable<DiscountDefinition>> GetAllAsync();
        Task<int> CreateAsync(DiscountDefinition discount);
        Task UpdateAsync(DiscountDefinition discount);
        Task<DiscountValidationResult> ValidateBillDiscountCodeAsync(string code, int branchId, decimal subTotal);
        Task RedeemDiscountForSaleAsync(long salesId, int discountId, string code, int branchId, decimal subTotal, decimal discountAmount, int usedBy);
        Task<IEnumerable<DiscountDefinition>> GetActiveAutoDiscountsAsync(int branchId, decimal subTotal);
    }
}

