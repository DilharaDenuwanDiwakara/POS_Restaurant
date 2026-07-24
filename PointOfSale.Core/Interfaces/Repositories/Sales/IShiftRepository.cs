using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface IShiftRepository
    {
        Task<ShiftDto> GetActiveShiftAsync(int userId);
        Task<TillShiftStatusDto> CheckActiveShiftAsync(string machineName, int userId);
        Task<IEnumerable<ShiftDto>> GetShiftsForReconciliationAsync(int branchId);
        Task OpenShiftAsync(ShiftDto shift);
        Task<ShiftReconciliationDto> GetShiftReconciliationAsync(int shiftId);
        Task<ShiftReconciliationDto> CloseShiftAsync(int shiftId, int userId, decimal physicalCashDrop);
        Task<ShiftReconciliationDto> CloseShiftAndGenerateZReportAsync(int shiftId, int userId, decimal physicalCash);
        DataSet GetZReportDataSet(int shiftId);
        Task PostShiftToLedgerAsync(int shiftId, int userId, string managerNotes);
    }
}
