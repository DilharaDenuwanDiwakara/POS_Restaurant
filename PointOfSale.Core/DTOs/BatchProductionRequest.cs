namespace PointOfSale.Core.DTOs
{
    public class BatchProductionRequest
    {
        public int OutputProductId { get; set; }
        public decimal ProducedQty { get; set; }
        public int BranchId { get; set; }
        public int LocationId { get; set; }
        public int UserId { get; set; }
    }
}
