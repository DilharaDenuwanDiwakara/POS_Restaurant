using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Inventory
{
    public class ProductBatch : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private long _batchId;
        public long BatchId
        {
            get => _batchId;
            set => SetProperty(ref _batchId, value);
        }

        private int _supplierId;
        public int SupplierId
        {
            get => _supplierId;
            set => SetProperty(ref _supplierId, value);
        }

        private string _supplierName;
        public string SupplierName
        {
            get => _supplierName;
            set => SetProperty(ref _supplierName, value);
        }

        private int _productId;
        public int ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }
        private string _productName;
        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        private decimal _availableQuantity;
        public decimal AvailableQuantity
        {
            get => _availableQuantity;
            set => SetProperty(ref _availableQuantity, value);
        }

        private decimal _unitCost;
        public decimal UnitCost
        {
            get => _unitCost;
            set => SetProperty(ref _unitCost, value);
        }

        private DateTime _receivedDate;
        public DateTime ReceivedDate
        {
            get => _receivedDate;
            set => SetProperty(ref _receivedDate, value);
        }

        public DateTime? ExpiryDate { get; set; }


        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

    }
}
