using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.DTOs
{
    public class ShiftReconciliationDto : INotifyPropertyChanged
    {
        private int _id;
        private int _shiftId;
        private decimal _systemCash;
        private decimal _physicalCash;
        private decimal _variance;
        private string _notes;
        private decimal _expectedCardTotal;
        private decimal _expectedCreditTotal;

        public int Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public int ShiftId
        {
            get => _shiftId;
            set => SetField(ref _shiftId, value);
        }

        public decimal SystemCash
        {
            get => _systemCash;
            set => SetField(ref _systemCash, value);
        }

        public decimal PhysicalCash
        {
            get => _physicalCash;
            set => SetField(ref _physicalCash, value);
        }

        public decimal Variance
        {
            get => _variance;
            set => SetField(ref _variance, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetField(ref _notes, value);
        }

        public decimal ExpectedCardTotal
        {
            get => _expectedCardTotal;
            set => SetField(ref _expectedCardTotal, value);
        }

        public decimal ExpectedCreditTotal
        {
            get => _expectedCreditTotal;
            set => SetField(ref _expectedCreditTotal, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
