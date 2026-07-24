using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Core.Interfaces.Repositories.System
{
    public interface ITaxConfigurationRepository
    {
        Task<IEnumerable<TaxConfiguration>> GetAllAsync();
        Task<int> CreateAsync(TaxConfiguration taxConfiguration);
        Task UpdateAsync(TaxConfiguration taxConfiguration);
    }
}
