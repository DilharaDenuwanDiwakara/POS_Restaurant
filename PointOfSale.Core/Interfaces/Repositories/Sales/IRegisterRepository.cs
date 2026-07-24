using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface IRegisterRepository
    {
        Task<RegisterDto> GetRegisterByIdAsync(int registerId);
        Task<int> RegisterTerminalAsync(int branchId, string registerCode, string registerName, string machineName, int authorizedByUserId);
    }
}
