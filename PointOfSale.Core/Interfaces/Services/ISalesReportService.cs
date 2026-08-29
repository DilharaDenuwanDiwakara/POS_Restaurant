using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface ISalesReportService
    {
        IEnumerable<SalesReportTypeDto> GetAvailableReportTypes();
        Task<DataTable> GenerateReportAsync(string reportTypeKey, SalesReportRequestDto request);
    }
}
