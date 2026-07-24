using System;
using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public class Account
    {
        public int Id { get; set; }
        public int AccountTypeId { get; set; }
        public int? ParentAccountId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsHeader { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<Account> SubAccounts { get; set; } = new List<Account>();
        public AccountType AccountType { get; set; }
    }
}
