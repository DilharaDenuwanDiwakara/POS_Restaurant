namespace PointOfSale.Core.DTOs
{
    public class PaymentAccountDto
    {
        public int Id { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }

        public string DisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AccountCode))
                    return AccountName;

                return $"{AccountCode} - {AccountName}";
            }
        }
    }
}
