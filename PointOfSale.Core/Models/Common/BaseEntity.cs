namespace PointOfSale.Core.Models.Common
{
    public abstract class BaseEntity<T>
    {
        public T Id { get; set; }
    }
}
