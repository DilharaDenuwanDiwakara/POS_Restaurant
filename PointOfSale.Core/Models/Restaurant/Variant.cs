using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Restaurant
{
    public class Variant : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _name;
        private string _portionSize;
        private decimal _price;

        public int Id { get; set; }
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PortionSize
        {
            get => _portionSize;
            set
            {
                if (_portionSize != value)
                {
                    _portionSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal Price
        {
            get => _price;
            set
            {
                if (_price != value)
                {
                    _price = value;
                    OnPropertyChanged();
                }
            }
        }

        // ObservableCollection handles "CollectionChanged" events automatically
        public ObservableCollection<MenuRecipe> RecipeLines { get; set; }
            = new ObservableCollection<MenuRecipe>();
    }
}
