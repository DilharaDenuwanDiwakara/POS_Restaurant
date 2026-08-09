using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Inventory
{
    public class ProvisioningOutputModel : INotifyPropertyChanged
    {
        private int _outputProductId;
        private int _outputUnitId;
        private decimal _outputQty;
        private decimal _costAllocationPercentage;
        private bool _isWastage;
        private string _productName;
        private string _unitName;

        public ProvisioningOutputModel()
        {
            AllowedUOMs = new ObservableCollection<ProductUnitMeasureOption>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ProductUnitMeasureOption> AllowedUOMs { get; }

        public int OutputProductId
        {
            get => _outputProductId;
            set
            {
                if (SetProperty(ref _outputProductId, value))
                {
                    AllowedUOMs.Clear();
                    OutputUnitId = 0;
                    UnitName = null;
                }
            }
        }

        public int OutputUnitId
        {
            get => _outputUnitId;
            set => SetProperty(ref _outputUnitId, value);
        }

        public decimal OutputQty
        {
            get => _outputQty;
            set => SetProperty(ref _outputQty, value);
        }

        public decimal CostAllocationPercentage
        {
            get => _costAllocationPercentage;
            set => SetProperty(ref _costAllocationPercentage, value);
        }

        public bool IsWastage
        {
            get => _isWastage;
            set => SetProperty(ref _isWastage, value);
        }

        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        public string UnitName
        {
            get => _unitName;
            set => SetProperty(ref _unitName, value);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
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
    }
}
