using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Purchasing
{
    public class GoodsPurchaseNoteLine : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private decimal _unitPrice;
        private decimal _quantityOrdered;
        private decimal _lineDiscount;
        private decimal _taxAmount;

        public long GoodsPurchaseNoteLineId { get; set; }
        public long GoodsPurchaseNoteId { get; set; }
        public int ProductId { get; set; }


        public string ProductName { get; set; }
        public string UnitMeasure { get; set; }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (_unitPrice != value)
                {
                    _unitPrice = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal QuantityOrdered
        {
            get => _quantityOrdered;
            set
            {
                if (_quantityOrdered != value)
                {
                    _quantityOrdered = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(QuantityOrder));
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal QuantityOrder
        {
            get => QuantityOrdered;
            set => QuantityOrdered = value;
        }

        public decimal QuantityReceived { get; set; }
        public decimal AlreadyReceivedQuantity { get; set; }
        public decimal LastGrnCostPrice { get; set; }
        public bool IsTaxApplicable { get; set; }

        // Fetched from [Inventory].[Product] via the SP JOIN — carried so the GRN ViewModel
        // can lock the ExpiryDate cell without a second database round-trip.
        public bool TrackExpiry { get; set; }

        public decimal LineDiscount
        {
            get => _lineDiscount;
            set
            {
                if (_lineDiscount != value)
                {
                    _lineDiscount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal TaxAmount
        {
            get => _taxAmount;
            set
            {
                if (_taxAmount != value)
                {
                    _taxAmount = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal LineTotal => Math.Round((QuantityOrdered * UnitPrice) - LineDiscount, 2);
    }
}

