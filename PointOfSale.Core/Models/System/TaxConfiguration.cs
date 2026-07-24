using System;

namespace PointOfSale.Core.Models.System
{
    public class TaxConfiguration
    {
        public int Id { get; set; }
        public string TaxCode { get; set; }
        public string TaxName { get; set; }
        public decimal Rate { get; set; }
        public int CalculationOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsInclusive { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string AccountCode { get; set; }
        public int? LastModifiedBy { get; set; }
    }
}
