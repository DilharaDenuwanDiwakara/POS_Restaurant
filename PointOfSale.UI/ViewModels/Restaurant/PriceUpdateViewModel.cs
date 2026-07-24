using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class PriceUpdateViewModel : BaseViewModel
    {
        private readonly IMenuItemRepository _repository;
        private readonly int _menuItemId;

        public PriceUpdateViewModel(IMenuItemRepository repository, int menuItemId, string itemName)
        {
            _repository = repository;
            _menuItemId = menuItemId;
            ItemName = itemName;

            Variants = new ObservableCollection<VariantPriceDto>();
            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync());
            CloseCommand = new RelayCommand(win => (win as Window)?.Close());

            // Load data immediately
            _ = LoadVariantsAsync();
        }

        public string ItemName { get; }
        public ObservableCollection<VariantPriceDto> Variants { get; }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private async Task LoadVariantsAsync()
        {
            IsBusy = true;
            try
            {
                var data = await _repository.GetVariantsByItemIdAsync(_menuItemId);
                Variants.Clear();
                foreach (var v in data) Variants.Add(v);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading variants: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private async Task SaveAsync()
        {
            try
            {
                IsBusy = true;
                await _repository.UpdateVariantPricesAsync(Variants);
                MessageBox.Show("Prices Updated Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Optional: Close window automatically? 
                // For now, we just let the user click Close.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update Failed: {ex.Message}");
            }
            finally { IsBusy = false; }
        }
    }
}
