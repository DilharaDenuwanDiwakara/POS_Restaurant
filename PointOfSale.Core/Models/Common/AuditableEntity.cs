using System;

namespace PointOfSale.Core.Models.Common
{
    public abstract class AuditableEntity<T> : BaseEntity<T>
    {
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
