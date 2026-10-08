using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface IBatchProductionService
    {
        Task<bool> ProcessBatchAsync(BatchProductionRequest request);
        Task<IList<BatchProductionHistoryDto>> GetHistoryAsync(DateTime fromDate, DateTime toDate);
    }
}
