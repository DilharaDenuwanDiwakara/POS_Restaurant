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

        private decimal _qty;
        private decimal _baseUnitCost;
        private decimal _wastagePercentage;
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

        public decimal BaseUnitCost
        {
            get => _baseUnitCost;
            set
            {
                if (_baseUnitCost != value)
                {
                    _baseUnitCost = value;
                    OnCostingChanged();
                }
            }
        }

        public decimal WastagePercentage
        {
            get => _wastagePercentage;
            set
            {
                if (_wastagePercentage != value)
                {
                    _wastagePercentage = value;
                    OnCostingChanged();
                }
            }
        }

        public decimal YieldPercentage
        {
            get
            {
                if (WastagePercentage <= 0m)
                {
                    return 100m;
                }

                return 100m - WastagePercentage;
            }
        }

        public decimal TrueUnitCost
        {
            get
            {
                if (BaseUnitCost <= 0m || WastagePercentage <= 0m)
                {
                    return BaseUnitCost;
                }

                var yieldPercentage = YieldPercentage;
                if (yieldPercentage <= 0m)
                {
                    return 0m;
                }

                return BaseUnitCost / (yieldPercentage / 100m);
            }
        }

        public decimal Qty
        {
            get => _qty;
            set
            {
                if (_qty != value)
                {
                    _qty = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Quantity));
                    OnPropertyChanged(nameof(LineCost));
                    OnPropertyChanged(nameof(TotalCost));
                }
            }
        }

        public decimal LineCost => Qty * TrueUnitCost;

        // Backward-compatible aliases used by existing repository/view-model code.
        public decimal Quantity
        {
            get => Qty;
            set => Qty = value;
        }

        public decimal CostPerUnit
        {
            get => TrueUnitCost;
            set => BaseUnitCost = value;
        }

        public decimal TotalCost => LineCost;

        private void OnCostingChanged()
        {
            OnPropertyChanged(nameof(BaseUnitCost));
            OnPropertyChanged(nameof(WastagePercentage));
            OnPropertyChanged(nameof(YieldPercentage));
            OnPropertyChanged(nameof(TrueUnitCost));
            OnPropertyChanged(nameof(CostPerUnit));
            OnPropertyChanged(nameof(LineCost));
            OnPropertyChanged(nameof(TotalCost));
        }
    }
}
