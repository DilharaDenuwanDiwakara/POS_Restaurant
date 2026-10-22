using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public sealed class AccountInquiryResponseDto
    {
        public AccountInquirySummaryDto Summary { get; set; } = new AccountInquirySummaryDto();
        public List<AccountInquiryLineDto> TransactionLines { get; set; } = new List<AccountInquiryLineDto>();
    }
}
