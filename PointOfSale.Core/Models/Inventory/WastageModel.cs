using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Inventory
{
    public class WastageModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isLoadingLineItems;
        private bool _hasLoadedLineItems;

        public long Id { get; set; }
        public string WastageNumber { get; set; }
        public DateTime WastageDate { get; set; }
        public string LocationName { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal TotalAmount { get; set; }
        public ObservableCollection<WastageLineModel> WastageLines { get; } = new ObservableCollection<WastageLineModel>();

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

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
