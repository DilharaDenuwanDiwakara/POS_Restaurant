using System.Threading.Tasks;
using PointOfSale.Core.Models.Security;

namespace PointOfSale.Core.Services
{
    public interface IAuthService
    {
        Task<User> LoginAsync(string username, string password);
    }
}
