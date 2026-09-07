using System;
using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    /// <summary>
    /// Carries all data required to save an internal stock issue.
    /// </summary>
    public class InternalIssueSaveDto
    {
        /// <summary>
        /// Gets or sets the issue date.
        /// </summary>
        public DateTime IssueDate { get; set; }

        /// <summary>
        /// Gets or sets the type of internal issue.
        /// </summary>
        public string IssueType { get; set; }

        /// <summary>
        /// Gets or sets the station issuing or consuming the stock.
        /// </summary>
        public int StationId { get; set; }

        /// <summary>
        /// Gets or sets the branch where the internal issue is recorded.
        /// </summary>
        public int BranchId { get; set; }

        /// <summary>
        /// Gets or sets the inventory location stock should be deducted from.
        /// </summary>
        public int LocationId { get; set; }

        /// <summary>
        /// Gets or sets the total issue value.
        /// </summary>
        public decimal TotalValue { get; set; }

        /// <summary>
        /// Gets or sets optional remarks for the transaction.
        /// </summary>
        public string Remarks { get; set; }

        /// <summary>
        /// Gets or sets the user identifier creating the transaction.
        /// </summary>
        public int CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the target account to debit for the internal issue.
        /// </summary>
        public int? TargetAccountId { get; set; }

        /// <summary>
        /// Gets or sets the products issued in this transaction.
        /// </summary>
        public List<InternalIssueLineDto> Lines { get; set; } = new List<InternalIssueLineDto>();
    }

    /// <summary>
    /// Carries the generated values returned after saving an internal stock issue.
    /// </summary>
    public class InternalIssueSaveResultDto
    {
        /// <summary>
        /// Gets or sets the generated internal issue identifier.
        /// </summary>
        public int InternalIssueId { get; set; }

        /// <summary>
        /// Gets or sets the generated internal issue number.
        /// </summary>
        public string IssueNumber { get; set; }
    }

    /// <summary>
    /// Carries product line data required to save an internal stock issue.
    /// </summary>
    public class InternalIssueLineDto
    {
        /// <summary>
        /// Gets or sets the issued product identifier. Populated for a Consumable Product line;
        /// left null when the line is a Menu Item (see <see cref="VariantId"/>).
        /// </summary>
        public int? ProductId { get; set; }

        /// <summary>
        /// Gets or sets the issued menu item variant identifier. Populated for a Menu Item line so the
        /// stored procedure can explode the recipe internally; left null for Consumable Product lines.
        /// </summary>
        public int? VariantId { get; set; }

        /// <summary>
        /// Gets or sets the issued quantity.
        /// </summary>
        public decimal Qty { get; set; }

        /// <summary>
        /// Gets or sets the unit cost at the time of issue.
        /// </summary>
        public decimal UnitCost { get; set; }

        /// <summary>
        /// Gets or sets the total value for this line.
        /// </summary>
        public decimal LineTotal { get; set; }
    }
}
