namespace PointOfSale.Core.Models.Inventory
{
    /// <summary>
    /// Represents a single product line issued on an internal stock issue.
    /// </summary>
    public class InternalIssueLine
    {
        /// <summary>
        /// Gets or sets the primary key of the internal issue line.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the parent internal issue identifier.
        /// </summary>
        public int InternalIssueId { get; set; }

        /// <summary>
        /// Gets or sets the issued product identifier. Null when the line is a Menu Item.
        /// </summary>
        public int? ProductId { get; set; }

        /// <summary>
        /// Gets or sets the issued menu item variant identifier. Null when the line is a Consumable Product.
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
