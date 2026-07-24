namespace PointOfSale.Core.Exception
{
    public class ProductImportError
    {
        public int RowNumber { get; set; }
        public string ProductCode { get; set; }
        public string Error { get; set; }
    }
}
