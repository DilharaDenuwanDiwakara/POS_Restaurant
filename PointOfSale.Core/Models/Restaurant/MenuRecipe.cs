using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Restaurant
{
    public class MenuRecipe : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // 2. The Helper Method (Boilerplate)
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private decimal _quantity;
        private decimal _costPerUnit;
        private string _unitName;

        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UnitMeasureId { get; set; }
        public string ProductName { get; set; }
        public string UnitName
        {
            get => _unitName;
            set
            {
                if (_unitName != value)
                {
                    _unitName = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal CostPerUnit
        {
            get => _costPerUnit;
            set
            {
                if (_costPerUnit != value)
                {
                    _costPerUnit = value;
                    OnPropertyChanged();              // Update self
                    OnPropertyChanged(nameof(TotalCost)); // Update calculation
                }
            }
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalCost));
                }
            }
        }

        // Calculated Property
        public decimal TotalCost => Quantity * CostPerUnit;
    }
}
