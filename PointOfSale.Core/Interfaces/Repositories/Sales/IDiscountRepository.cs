using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface IDiscountRepository
    {
        Task<IEnumerable<DiscountDefinitionDto>> GetAllAsync();
        Task<int> CreateAsync(DiscountDefinitionDto discount);
        Task UpdateAsync(DiscountDefinitionDto discount);
        Task<DiscountValidationResult> ValidateBillDiscountCodeAsync(string code, int branchId, decimal subTotal);
        Task RedeemDiscountForSaleAsync(long salesId, int discountId, string code, int branchId, decimal subTotal, decimal discountAmount, int usedBy);
        Task<IEnumerable<DiscountDefinitionDto>> GetActiveAutoDiscountsAsync(int branchId, decimal subTotal);
    }
}

