namespace PointOfSale.Core.DTOs
{
    public class ProvisioningYieldDetailModel
    {
        public string OutputProductName { get; set; }
        public string Unit { get; set; }
        public decimal OutputQty { get; set; }
        public decimal CostAllocationPercentage { get; set; }
        public decimal CalculatedUnitCost { get; set; }
        public bool IsWastage { get; set; }
    }
}
