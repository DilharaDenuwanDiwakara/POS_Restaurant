using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    /// <summary>
    /// Defines persistence operations for internal stock issue transactions.
    /// </summary>
    public interface IInternalIssueRepository
    {
        /// <summary>
        /// Creates a new internal stock issue and returns the generated issue details.
        /// </summary>
        /// <param name="dto">The internal issue data to save.</param>
        /// <returns>The generated internal issue identifier and issue number.</returns>
        Task<InternalIssueSaveResultDto> CreateInternalIssueAsync(InternalIssueSaveDto dto);
    }
}
