using System.Data;
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
        /// Creates a new internal stock issue and returns the generated issue details. The stored
        /// procedure performs recipe explosion for Menu Item lines internally when a VariantId is supplied.
        /// </summary>
        /// <param name="dto">The internal issue data to save.</param>
        /// <returns>The generated internal issue identifier and issue number.</returns>
        Task<InternalIssueSaveResultDto> CreateInternalIssueAsync(InternalIssueSaveDto dto);

        /// <summary>
        /// Gets the print-ready voucher data for a saved internal stock issue.
        /// </summary>
        /// <param name="internalIssueId">The internal issue identifier returned by <see cref="CreateInternalIssueAsync"/>.</param>
        /// <returns>A data table matching the Internal Issue Voucher report's data source.</returns>
        Task<DataTable> GetInternalIssueVoucherAsync(int internalIssueId);
    }
}
