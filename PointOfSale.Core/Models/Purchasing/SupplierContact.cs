namespace PointOfSale.Core.Models.Purchasing
{
    public class SupplierContact
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string ContactName { get; set; }
        public string Designation { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsWhatsApp { get; set; }
        public string EmailAddress { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
    }
}
