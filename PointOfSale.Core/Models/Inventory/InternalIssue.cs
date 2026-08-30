using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Inventory
{
    /// <summary>
    /// Represents an internal stock issue document header.
    /// </summary>
    public class InternalIssue
    {
        /// <summary>
        /// Gets or sets the primary key of the internal issue.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the unique issue number.
        /// </summary>
        public string IssueNumber { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the stock issue occurred.
        /// </summary>
        public DateTime IssueDate { get; set; }

        /// <summary>
        /// Gets or sets the stock issue type.
        /// </summary>
        public string IssueType { get; set; }

        /// <summary>
        /// Gets or sets the station associated with the stock issue.
        /// </summary>
        public int StationId { get; set; }

        /// <summary>
        /// Gets or sets the total value of all issue lines.
        /// </summary>
        public decimal TotalValue { get; set; }

        /// <summary>
        /// Gets or sets the accounting journal entry created for this stock issue.
        /// </summary>
        public int? JournalEntryId { get; set; }

        /// <summary>
        /// Gets or sets the target account (e.g. Wastage Expense or Staff Receivable) debited for
        /// Wastage/Staff Recovery issues. Null for Consumable issues.
        /// </summary>
        public int? TargetAccountId { get; set; }

        /// <summary>
        /// Gets or sets optional remarks for the issue.
        /// </summary>
        public string Remarks { get; set; }

        /// <summary>
        /// Gets or sets the user identifier that created this issue.
        /// </summary>
        public int CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the date and time when this issue was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the issue line items.
        /// </summary>
        public List<InternalIssueLine> Lines { get; set; } = new List<InternalIssueLine>();
    }
}
