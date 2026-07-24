namespace PointOfSale.Core.Models.System
{
    public class Branch
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string TaxRegNo { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }
    }
}
