using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Purchasing
{
    public class SupplierReturnLine : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int _productId;
        private string _productName;
        private int _returnReasonId;
        private string _returnReasonDescription;
        private string _unitMeasure;
        private decimal _quantity;
        private decimal _unitPrice; // This will hold the CostPrice from the batch
        private long _batchId; // Crucial for multi-cost tracking
        private long _goodsReceiveNoteLineId;
        private decimal _originalGrnQuantity;
        private decimal _originalGrnLineDiscount;
        private decimal _originalGrnTaxAmount;
        private decimal _lineSubTotal;
        private decimal _lineDiscount;
        private decimal _taxAmount;
        private decimal _lineTotal;

        public int SupplierReturnLineId { get; set; }
        public int SupplierReturnId { get; set; }

        public int ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        public int ReturnReasonId
        {
            get => _returnReasonId;
            set => SetProperty(ref _returnReasonId, value);
        }

        public string ReturnReasonDescription
        {
            get => _returnReasonDescription;
            set => SetProperty(ref _returnReasonDescription, value);
        }

        public string UnitMeasure
        {
            get => _unitMeasure;
            set => SetProperty(ref _unitMeasure, value);
        }

        public decimal BatchCostPrice { get; set; } // The actual cost price from the batch

        public long GoodsReceiveNoteLineId
        {
            get => _goodsReceiveNoteLineId;
            set => SetProperty(ref _goodsReceiveNoteLineId, value);
        }

        public decimal OriginalGrnQuantity
        {
            get => _originalGrnQuantity;
            set
            {
                if (SetProperty(ref _originalGrnQuantity, value))
                {
                    CalculateLineTotal();
                }
            }
        }

        public decimal OriginalGrnLineDiscount
        {
            get => _originalGrnLineDiscount;
            set
            {
                if (SetProperty(ref _originalGrnLineDiscount, value))
                {
                    CalculateLineTotal();
                }
            }
        }

        public decimal OriginalGrnTaxAmount
        {
            get => _originalGrnTaxAmount;
            set
            {
                if (SetProperty(ref _originalGrnTaxAmount, value))
                {
                    CalculateLineTotal();
                }
            }
        }

        public long BatchId
        {
            get => _batchId;
            set => SetProperty(ref _batchId, value);
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    CalculateLineTotal();
                }
            }
        }

        // Renamed 'QuantityReceived' in XAML to 'Quantity' here for consistency
        public decimal QuantityReceived => Quantity;

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    // UnitPrice is the Cost Price for a supplier return
                    BatchCostPrice = value;
                    CalculateLineTotal();
                }
            }
        }

        public decimal LineSubTotal
        {
            get => _lineSubTotal;
            set => SetProperty(ref _lineSubTotal, value);
        }

        public decimal LineDiscount
        {
            get => _lineDiscount;
            set => SetProperty(ref _lineDiscount, value);
        }

        public decimal TaxAmount
        {
            get => _taxAmount;
            set => SetProperty(ref _taxAmount, value);
        }

        public decimal LineTotal
        {
            get => _lineTotal;
            set => SetProperty(ref _lineTotal, value);
        }

        private void CalculateLineTotal()
        {
            var discountPerUnit = OriginalGrnQuantity > 0 ? OriginalGrnLineDiscount / OriginalGrnQuantity : 0m;
            var taxPerUnit = OriginalGrnQuantity > 0 ? OriginalGrnTaxAmount / OriginalGrnQuantity : 0m;

            LineSubTotal = Quantity * UnitPrice;
            LineDiscount = Quantity * discountPerUnit;
            TaxAmount = Quantity * taxPerUnit;
            LineTotal = LineSubTotal - LineDiscount + TaxAmount;
        }

        public void SetOriginalGrnValues(OriginalGrnReturnLine originalLine)
        {
            if (originalLine == null)
            {
                return;
            }

            GoodsReceiveNoteLineId = originalLine.GoodsReceiveNoteLineId;
            OriginalGrnQuantity = originalLine.OriginalQuantity;
            OriginalGrnLineDiscount = originalLine.LineDiscount;
            OriginalGrnTaxAmount = originalLine.TaxAmount;
            UnitPrice = originalLine.UnitPrice;
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
