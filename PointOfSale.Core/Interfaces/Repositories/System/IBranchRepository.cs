using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Core.Interfaces.Repositories.System
{
    public interface IBranchRepository
    {
        Task<IEnumerable<Branch>> GetAllAsync();
    }
}
