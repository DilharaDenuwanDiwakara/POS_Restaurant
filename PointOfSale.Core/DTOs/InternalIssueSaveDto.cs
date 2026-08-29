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
        /// Gets or sets the wastage expense account to debit when the issue type is Wastage.
        /// Debit/Credit accounts are otherwise resolved automatically from category-level GL mappings.
        /// </summary>
        public int? WastageAccountId { get; set; }

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
        /// Gets or sets the issued product identifier.
        /// </summary>
        public int ProductId { get; set; }

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

    /// <summary>
    /// Carries a raw product ingredient returned from a menu recipe for internal issue explosion.
    /// Quantities are returned in the product's stock/base unit so they can be passed directly
    /// to Inventory.InternalIssueLineType.
    /// </summary>
    public class RecipeIngredientDto
    {
        /// <summary>
        /// Gets or sets the finished menu item identifier, when available.
        /// </summary>
        public int MenuItemId { get; set; }

        /// <summary>
        /// Gets or sets the finished menu variant identifier.
        /// </summary>
        public int VariantId { get; set; }

        /// <summary>
        /// Gets or sets the raw product ingredient identifier.
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Gets or sets the raw product ingredient display name.
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Gets or sets the ingredient quantity per one menu item, converted to product stock/base unit.
        /// </summary>
        public decimal QuantityPerItem { get; set; }

        /// <summary>
        /// Gets or sets the current product stock/base unit cost.
        /// </summary>
        public decimal UnitCost { get; set; }

        /// <summary>
        /// Gets or sets the product stock/base unit identifier.
        /// </summary>
        public int UnitMeasureId { get; set; }

        /// <summary>
        /// Gets or sets the product stock/base unit code or name.
        /// </summary>
        public string UnitMeasureName { get; set; }
    }
}
