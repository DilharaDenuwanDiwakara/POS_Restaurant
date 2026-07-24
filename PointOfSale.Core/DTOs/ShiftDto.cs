using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.DTOs
{
    public class ShiftDto : INotifyPropertyChanged
    {
        private int _id;
        private int _userId;
        private int _branchId;
        private int? _tillId;
        private string _tillName;
        private string _cashierName;
        private DateTime _startTime;
        private DateTime? _endTime;
        private decimal _startingFloat;
        private string _status;

        public int Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public int UserId
        {
            get => _userId;
            set => SetField(ref _userId, value);
        }

        public int BranchId
        {
            get => _branchId;
            set => SetField(ref _branchId, value);
        }

        public int? TillId
        {
            get => _tillId;
            set => SetField(ref _tillId, value);
        }

        public string TillName
        {
            get => _tillName;
            set => SetField(ref _tillName, value);
        }

        public string CashierName
        {
            get => _cashierName;
            set => SetField(ref _cashierName, value);
        }

        public DateTime StartTime
        {
            get => _startTime;
            set => SetField(ref _startTime, value);
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set => SetField(ref _endTime, value);
        }

        public decimal StartingFloat
        {
            get => _startingFloat;
            set => SetField(ref _startingFloat, value);
        }

        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
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
