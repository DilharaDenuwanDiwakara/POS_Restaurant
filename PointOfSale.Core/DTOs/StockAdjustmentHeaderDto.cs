using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.DTOs
{
    public class StockAdjustmentHeaderDto : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public long Id { get; set; }
        public string AdjustmentNumber { get; set; }
        public DateTime AdjustDate { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public int UserId { get; set; }
        public string CreatedByName { get; set; }
        public decimal TotalAmount { get; set; }
        public ObservableCollection<StockAdjustmentLineDto> Lines { get; } = new ObservableCollection<StockAdjustmentLineDto>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
