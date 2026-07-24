namespace PointOfSale.Core.DTOs
{
    public class AccountType
    {
        public int AccountTypeId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string NormalBalance { get; set; } // 'D' or 'C'
        public bool IsActive { get; set; }
    }
}
