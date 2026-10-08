using System.ComponentModel;

namespace PointOfSale.Core.DTOs
{
    public class SubRecipeLineDto : INotifyPropertyChanged
    {
        private decimal _quantity;
        private decimal _standardCost;

        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int UnitMeasureId { get; set; }
        public string UnitMeasureName { get; set; }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                    OnPropertyChanged(nameof(LineCost));
                }
            }
        }

        public decimal StandardCost
        {
            get => _standardCost;
            set
            {
                if (_standardCost != value)
                {
                    _standardCost = value;
                    OnPropertyChanged(nameof(StandardCost));
                    OnPropertyChanged(nameof(LineCost));
                }
            }
        }

        public decimal LineCost => Quantity * StandardCost;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
