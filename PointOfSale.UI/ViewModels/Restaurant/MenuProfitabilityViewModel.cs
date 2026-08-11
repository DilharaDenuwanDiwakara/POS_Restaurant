using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class MenuProfitabilityViewModel : BaseViewModel
    {
        private readonly IMenuProfitabilityRepository _menuProfitabilityRepository;
        private readonly IMenuCategoryRepository _categoryRepository;
        private readonly IExcelService _excelService;
        private bool _isInitialized;

        public MenuProfitabilityViewModel(IMenuProfitabilityRepository menuProfitabilityRepository,
                                           IMenuCategoryRepository categoryRepository,
                                           IExcelService excelService)
        {
            _menuProfitabilityRepository = menuProfitabilityRepository ?? throw new ArgumentNullException(nameof(menuProfitabilityRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));

            LoadedCommand = new AsyncRelayCommand(async _ => await OnViewLoadedAsync());
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadMenuProfitabilityAsync());
            ExportToExcelCommand = new AsyncRelayCommand(async _ => await ExportToExcelAsync());
        }

        #region Properties

        public ObservableCollection<MenuProfitabilityDto> MenuProfitabilityItems { get; } = new ObservableCollection<MenuProfitabilityDto>();

        public ObservableCollection<CategoryLookupItem> Categories { get; } = new ObservableCollection<CategoryLookupItem>();

        private CategoryLookupItem _selectedCategory;
        public CategoryLookupItem SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    RefreshCommand.Execute(null);
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        #endregion

        #region Commands
        public ICommand LoadedCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        #endregion

        #region Logic

        private async Task OnViewLoadedAsync()
        {
            if (_isInitialized) return;

            await LoadCategoriesAsync();
            await LoadMenuProfitabilityAsync();

            _isInitialized = true;
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _categoryRepository.GetAllAsync();

                Categories.Clear();
                Categories.Add(new CategoryLookupItem { Id = 0, DisplayName = "ALL CATEGORIES", Level = 0 });

                foreach (var category in categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name))
                {
                    Categories.Add(new CategoryLookupItem { Id = category.Id, DisplayName = category.Name, Level = 0 });
                }

                // Set the backing field directly to avoid triggering a duplicate report load;
                // OnViewLoadedAsync calls LoadMenuProfitabilityAsync explicitly right after this.
                _selectedCategory = Categories.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedCategory));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading categories: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task LoadMenuProfitabilityAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                int? categoryId = SelectedCategory != null && SelectedCategory.Id != 0 ? SelectedCategory.Id : (int?)null;
                var items = await _menuProfitabilityRepository.GetMenuProfitabilityAsync(categoryId);

                MenuProfitabilityItems.Clear();
                foreach (var item in items)
                {
                    MenuProfitabilityItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading Menu Profitability report: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExportToExcelAsync()
        {
            if (MenuProfitabilityItems == null || !MenuProfitabilityItems.Any())
            {
                MessageBox.Show("No Menu Profitability data available to export.", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Export Menu Profitability to Excel",
                FileName = $"MenuProfitability_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            var exportItems = MenuProfitabilityItems.ToList();

            try
            {
                await Task.Run(() => _excelService.ExportMenuProfitability(exportItems, saveFileDialog.FileName));
                MessageBox.Show("Menu Profitability export completed successfully.", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "File Locked", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while exporting Menu Profitability: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
