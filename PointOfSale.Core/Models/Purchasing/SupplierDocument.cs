using System;

namespace PointOfSale.Core.Models.Purchasing
{
    public class SupplierDocument
    {
        public long Id { get; set; }
        public int SupplierId { get; set; }
        public string DocumentName { get; set; }
        public string DocumentUrl { get; set; }
        public string SecureDocumentUrl { get; set; }
        public string LocalFilePath { get; set; }
        public DateTime UploadedDate { get; set; }
        public int UploadedBy { get; set; }
    }
}
