using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Purchasing
{
    public class GoodsReceiveNoteLine : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
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

        public long GoodsPurchaseNoteLineId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal QuantityOrdered { get; set; }
        public decimal OrderedPrice { get; set; }
        public bool IsTaxApplicable { get; set; }

        private decimal _quantityReceived;
        private string _quantityReceivedInput;
        public decimal QuantityReceived
        {
            get => _quantityReceived;
            set
            {
                value = NormalizeQuantityReceived(value);

                if (SetProperty(ref _quantityReceived, value))
                {
                    RefreshQuantityReceivedInput();
                    OnPropertyChanged(nameof(LineTotal));
                    OnPropertyChanged(nameof(HasQuantityDiscrepancy));
                }
            }
        }

        public string QuantityReceivedInput
        {
            get => _quantityReceivedInput ?? FormatQuantityReceived(QuantityReceived);
            set => SetProperty(ref _quantityReceivedInput, value);
        }

        public void CommitQuantityReceivedInput()
        {
            var text = QuantityReceivedInput?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                QuantityReceived = 0m;
                RefreshQuantityReceivedInput();
                return;
            }

            var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var normalizedText = text
                .Replace(".", decimalSeparator)
                .Replace(",", decimalSeparator);

            if (normalizedText == decimalSeparator)
            {
                normalizedText = "0" + decimalSeparator;
            }
            else if (normalizedText.EndsWith(decimalSeparator, StringComparison.Ordinal))
            {
                normalizedText += "0";
            }

            if (decimal.TryParse(normalizedText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedValue))
            {
                QuantityReceived = parsedValue;
                RefreshQuantityReceivedInput();
            }
            else
            {
                RefreshQuantityReceivedInput();
            }
        }

        public void SetStoredQuantityReceived(decimal value)
        {
            value = Math.Max(0m, value);

            if (SetProperty(ref _quantityReceived, value, nameof(QuantityReceived)))
            {
                RefreshQuantityReceivedInput();
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(HasQuantityDiscrepancy));
            }
        }

        private decimal NormalizeQuantityReceived(decimal value)
        {
            if (value > QuantityOrdered)
            {
                return QuantityOrdered;
            }

            if (value < 0)
            {
                return 0m;
            }

            return value;
        }

        private static string FormatQuantityReceived(decimal value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private void RefreshQuantityReceivedInput()
        {
            _quantityReceivedInput = FormatQuantityReceived(QuantityReceived);
            OnPropertyChanged(nameof(QuantityReceivedInput));
        }

        public int UnitMeasureId { get; set; }
        public string UnitMeasure { get; set; }
        public decimal BaseQuantityReceived { get; set; }
        public decimal BaseUnitCost { get; set; }

        private DateTime? _expiryDate;
        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set => SetProperty(ref _expiryDate, value);
        }

        // Copied from the master Product record when a line is loaded.
        // Drives per-row ExpiryDate cell locking in the DataGrid — never saved back to the GRN.
        private bool _trackExpiry;
        public bool TrackExpiry
        {
            get => _trackExpiry;
            set => SetProperty(ref _trackExpiry, value);
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set { SetProperty(ref _unitPrice, value); OnPropertyChanged(nameof(LineTotal)); }
        }

        private decimal _lineDiscount;
        public decimal LineDiscount
        {
            get => _lineDiscount;
            set { SetProperty(ref _lineDiscount, value); OnPropertyChanged(nameof(LineTotal)); }
        }

        private decimal _taxAmount;
        public decimal TaxAmount
        {
            get => _taxAmount;
            set { SetProperty(ref _taxAmount, value); OnPropertyChanged(nameof(LineTotal)); }
        }

        // Line total is shown before tax; tax is summarized at GRN header level.
        public decimal LineTotal => Math.Round((QuantityReceived * UnitPrice) - LineDiscount, 2);
        public bool HasQuantityDiscrepancy => QuantityOrdered > 0m && QuantityOrdered != QuantityReceived;

    }
}
