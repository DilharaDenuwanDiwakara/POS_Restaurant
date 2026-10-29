namespace PointOfSale.Core.Models.System
{
    public class BankBINMapping
    {
        public int Id { get; set; }
        public int BankId { get; set; }
        public string BINPrefix { get; set; }
        private string _cardType;
        public string CardType { get => _cardType; set => _cardType = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant(); }
        private string _cardBrand;
        public string CardBrand { get => _cardBrand; set => _cardBrand = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant(); }
        public bool IsActive { get; set; } = true;
    }
}
