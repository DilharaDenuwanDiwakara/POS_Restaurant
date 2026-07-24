namespace PointOfSale.Core.DTOs
{
    public class RegisterDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string RegisterCode { get; set; }
        public string RegisterName { get; set; }
    }
}
