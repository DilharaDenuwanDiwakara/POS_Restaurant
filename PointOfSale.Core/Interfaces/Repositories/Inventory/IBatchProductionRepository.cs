using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IBatchProductionRepository
    {
        /// <summary>
        /// Processes a semi-finished goods batch production transaction.
        /// </summary>
        Task<bool> ProcessBatchAsync(BatchProductionRequest request);
        Task<IList<BatchProductionHistoryDto>> GetHistoryAsync(DateTime fromDate, DateTime toDate);
    }
}
