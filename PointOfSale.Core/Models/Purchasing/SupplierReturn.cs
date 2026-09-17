using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Models.Purchasing
{
    public class SupplierReturn : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isLoadingLineItems;
        private bool _hasLoadedLineItems;

        public event PropertyChangedEventHandler PropertyChanged;

        public int BranchId { get; set; }
        public int LocationId { get; set; }
        public int SupplierReturnId { get; set; }
        public string ReturnNumber { get; set; }
        public string SupplierName { get; set; }
        public string OriginalInvoiceNumbers { get; set; }
        public string OriginalInvoiceDates { get; set; }
        public int SupplierId { get; set; }
        public string ReturnedBy { get; set; }
        public string Note { get; set; } = null;
        public DateTime ReturnDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal NetAmount { get; set; }
        public string Status { get; set; }
        public string ApproverRemark { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }
        public int CreatedBy { get; set; }

        // Used while creating/saving supplier returns.
        public List<SupplierReturnLine> Lines { get; set; } = new List<SupplierReturnLine>();

        // Used by the history row-details grid.
        public ObservableCollection<SupplierReturnLineDto> LineItems { get; } = new ObservableCollection<SupplierReturnLineDto>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsLoadingLineItems
        {
            get => _isLoadingLineItems;
            set => SetProperty(ref _isLoadingLineItems, value);
        }

        public bool HasLoadedLineItems
        {
            get => _hasLoadedLineItems;
            set => SetProperty(ref _hasLoadedLineItems, value);
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
