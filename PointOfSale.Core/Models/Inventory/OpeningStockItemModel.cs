using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Inventory
{
    public class OpeningStockItemModel : INotifyPropertyChanged
    {
        private decimal _openingQuantity;
        private decimal _unitCost;
        private decimal _currentStock;

        public event PropertyChangedEventHandler PropertyChanged;

        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string DefaultUOM { get; set; }

        public decimal CurrentStock
        {
            get => _currentStock;
            set
            {
                if (_currentStock == value) return;
                _currentStock = value;
                OnPropertyChanged();
            }
        }

        public decimal OpeningQuantity
        {
            get => _openingQuantity;
            set
            {
                if (_openingQuantity == value) return;
                _openingQuantity = value;
                OnPropertyChanged();
            }
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                if (_unitCost == value) return;
                _unitCost = value;
                OnPropertyChanged();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
