using PointOfSale.Core.Models.Purchasing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PointOfSale.Core.DTOs;

namespace PointOfSale.UI.ViewModels.Purchasing
{
    public class SupplierReturnModel : BaseViewModel
    {
        public SupplierReturnModel(
            int supplierReturnId,
            string returnNumber,
            DateTime returnDate,
            string invoiceNo,
            string invoiceDate,
            string supplierName,
            string returnedBy,
            decimal netAmount,
            IEnumerable<SupplierReturnFlatDto> lines)
        {
            SupplierReturnId = supplierReturnId;
            ReturnNumber = returnNumber;
            ReturnDate = returnDate;
            InvoiceNo = invoiceNo;
            InvoiceDate = invoiceDate;
            SupplierName = supplierName;
            ReturnedBy = returnedBy;
            NetAmount = netAmount;

            ReturnLines = new ObservableCollection<SupplierReturnFlatDto>(lines ?? new List<SupplierReturnFlatDto>());
        }

        public int SupplierReturnId { get; }
        public string ReturnNumber { get; }
        public DateTime ReturnDate { get; }
        public string InvoiceNo { get; }
        public string InvoiceDate { get; }
        public string SupplierName { get; }
        public string ReturnedBy { get; }
        public decimal NetAmount { get; }

        public ObservableCollection<SupplierReturnFlatDto> ReturnLines { get; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
    }
}
