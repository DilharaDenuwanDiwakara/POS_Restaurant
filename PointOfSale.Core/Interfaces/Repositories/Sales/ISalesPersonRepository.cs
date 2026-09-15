using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface ISalesPersonRepository
    {
        Task<IEnumerable<SalesPerson>> GetAllAsync();
        Task<int> CreateAsync(SalesPerson salesPerson);
        Task UpdateAsync(SalesPerson salesPerson);
    }
}
