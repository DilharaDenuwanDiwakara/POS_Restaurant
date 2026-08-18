using System;

namespace PointOfSale.Core.DTOs
{
    public class ServedOrderDto
    {
        public long OpenAccountId { get; set; }
        public long OrderId { get; set; }
        public string TableName { get; set; }
        public string OrderNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerContactNumber { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }

        public string ItemCountText => ItemCount == 1 ? "1 Item" : $"{ItemCount} Items";

        public string CustomerSummaryText
        {
            get
            {
                var contactNumber = CustomerContactNumber?.Trim();
                var customerName = CustomerName?.Trim();

                if (!string.IsNullOrWhiteSpace(contactNumber) && !string.IsNullOrWhiteSpace(customerName))
                    return $"{contactNumber} - {customerName}";

                if (!string.IsNullOrWhiteSpace(contactNumber))
                    return contactNumber;

                if (!string.IsNullOrWhiteSpace(customerName))
                    return customerName;

                return TableName;
            }
        }
    }
}
