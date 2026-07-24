using System.Threading.Tasks;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Core.Interfaces.Repositories.System
{
    public interface ICompanyRepository
    {
        Task<Company> GetAsync();
        Task<int> CreateAsync(Company company);
        Task UpdateAsync(Company company);
    }
}
