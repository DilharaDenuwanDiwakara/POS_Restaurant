using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;

namespace PointOfSale.Core.Services
{
    public class BatchProductionService : IBatchProductionService
    {
        private readonly IBatchProductionRepository _batchProductionRepository;

        public BatchProductionService(IBatchProductionRepository batchProductionRepository)
        {
            _batchProductionRepository = batchProductionRepository ?? throw new ArgumentNullException(nameof(batchProductionRepository));
        }

        public Task<bool> ProcessBatchAsync(BatchProductionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.OutputProductId <= 0)
                throw new ArgumentException("Select a valid item to produce.", nameof(request.OutputProductId));

            if (request.ProducedQty <= 0m)
                throw new ArgumentException("Produced quantity must be greater than zero.", nameof(request.ProducedQty));

            if (request.BranchId <= 0)
                throw new ArgumentException("A valid branch is required.", nameof(request.BranchId));

            if (request.LocationId <= 0)
                throw new ArgumentException("Select a valid production location.", nameof(request.LocationId));

            if (request.UserId <= 0)
                throw new ArgumentException("A valid user is required.", nameof(request.UserId));

            return _batchProductionRepository.ProcessBatchAsync(request);
        }

        public Task<IList<BatchProductionHistoryDto>> GetHistoryAsync(DateTime fromDate, DateTime toDate)
        {
            if (fromDate > toDate)
            {
                throw new ArgumentException("From date cannot be later than To date.", nameof(fromDate));
            }

            return _batchProductionRepository.GetHistoryAsync(fromDate, toDate);
        }
    }
}
