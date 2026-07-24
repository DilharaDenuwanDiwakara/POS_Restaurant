using System.Collections.Generic;
using System.Linq;

namespace PointOfSale.Core.Exception
{
    public class ProductImportResult
    {
        public bool HasErrors => Errors.Any();
        public List<ProductImportError> Errors { get; } = new List<ProductImportError>();
    }
}
