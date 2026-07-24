using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Services;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class MenuItemListViewModel : BaseViewModel
    {
        private readonly IMenuItemRepository _menuItemRepository;
        private readonly IMenuCategoryRepository _categoryRepository;
        private readonly IStationRepository _stationRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly CloudStorageService _storageService;
        private bool _isInitialized = false;

        public MenuItemListViewModel(IMenuItemRepository menuItemRepository,
                                     IMenuCategoryRepository categoryRepository,
                                     IStationRepository stationRepository,
                                     IUserSessionService userSessionService,
                                     CloudStorageService storageService)
        {
            _menuItemRepository = menuItemRepository;
            _categoryRepository = categoryRepository;
            _stationRepository = stationRepository;
            _userSessionService = userSessionService;
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            // COMMANDS
            LoadedCommand = new AsyncRelayCommand(async _ => await OnViewLoadedAsync());
            SearchCommand = new RelayCommand(_ => ExecuteSearch());
            RefreshCommand = new AsyncRelayCommand(async _ => await ReloadDataAsync());

            QuickUpdatePriceCommand = new RelayCommand(obj => ExecuteQuickUpdatePrice(obj));
            EditCommand = new AsyncRelayCommand(async obj => await ExecuteEditAsync(obj));
            DeleteCommand = new AsyncRelayCommand(async obj => await ExecuteDeleteAsync(obj));

            // BEST PRACTICE: Ensure app can download images from modern SSL.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        #region Permissions
        public bool CanEditMenuItem => _userSessionService.HasPermission("RESTAURANT_MENU_ITEM_EDIT");
        public bool CanDeleteMenuItem => _userSessionService.HasPermission("RESTAURANT_MENU_ITEM_DELETE");
        public bool CanSetPrice => _userSessionService.HasPermission("RESTAURANT_MENU_ITEM_SET_PRICE");
        #endregion

        #region Properties

        private ObservableCollection<MenuItemDto> _allMenuItems;

        // FIX: This needs to be a full property to notify the UI when the View changes
        private ICollectionView _filteredMenuItems;
        public ICollectionView FilteredMenuItems
        {
            get => _filteredMenuItems;
            private set => SetProperty(ref _filteredMenuItems, value);
        }

        public ObservableCollection<CategoryLookupItem> Categories { get; } = new ObservableCollection<CategoryLookupItem>();
        public ObservableCollection<Station> Stations { get; } = new ObservableCollection<Station>();

        private CategoryLookupItem _selectedCategory;
        public CategoryLookupItem SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    ApplyFilters();
                }
            }
        }

        private Station _selectedStation;
        public Station SelectedStation
        {
            get => _selectedStation;
            set
            {
                if (SetProperty(ref _selectedStation, value))
                {
                    ApplyFilters();
                }
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        // Summary Properties
        private int _totalCount;
        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }

        private int _activeCount;
        public int ActiveCount { get => _activeCount; set => SetProperty(ref _activeCount, value); }

        private int _inactiveCount;
        public int InactiveCount { get => _inactiveCount; set => SetProperty(ref _inactiveCount, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        #endregion

        #region Commands
        public ICommand LoadedCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand QuickUpdatePriceCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        #endregion

        #region Logic

        private async Task OnViewLoadedAsync()
        {
            if (_isInitialized) return;
            await ReloadDataAsync();
            _isInitialized = true;
        }

        private async Task ReloadDataAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                // 1. Load Categories
                var selectedCategoryId = SelectedCategory?.Id;
                var selectedStationId = SelectedStation?.Id;
                var cats = await _categoryRepository.GetAllAsync();
                Categories.Clear();
                Categories.Add(new CategoryLookupItem { Id = 0, DisplayName = "ALL CATEGORIES", Level = 0 });
                BuildHierarchy(cats, null, 0);

                SelectedCategory = Categories.FirstOrDefault(c => c.Id == selectedCategoryId) ?? Categories.FirstOrDefault();

                // 2. Load Stations
                var stations = await _stationRepository.GetAllAsync(_userSessionService.BranchId);
                Stations.Clear();
                Stations.Add(new Station { Id = 0, Name = "ALL STATIONS" });
                foreach (var station in stations.OrderBy(s => s.Name))
                {
                    Stations.Add(station);
                }

                SelectedStation = Stations.FirstOrDefault(s => s.Id == selectedStationId) ?? Stations.FirstOrDefault();

                // 3. Load Items
                var items = (await _menuItemRepository.GetAllWithDetailsAsync()).ToList();
                foreach (var item in items)
                {
                    item.ImageUrl = _storageService.GetSecureFileUrl(item.ImageUrl);
                }

                _allMenuItems = new ObservableCollection<MenuItemDto>(items);

                // 4. Setup Filter View
                // FIX: Assigning to the property triggers the INotifyPropertyChanged
                FilteredMenuItems = CollectionViewSource.GetDefaultView(_allMenuItems);
                FilteredMenuItems.Filter = FilterLogic;

                // 5. Update Counts
                UpdateSummary();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading data: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BuildHierarchy(IEnumerable<MenuCategory> allCategories, int? parentId, int level)
        {
            var children = allCategories
                .Where(c => c.ParentId == parentId)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name);

            foreach (var category in children)
            {
                Categories.Add(new CategoryLookupItem
                {
                    Id = category.Id,
                    DisplayName = category.Name,
                    Level = level
                });

                BuildHierarchy(allCategories, category.Id, level + 1);
            }
        }

        private void ExecuteSearch()
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            FilteredMenuItems?.Refresh();
            UpdateSummary();
        }

        private bool FilterLogic(object obj)
        {
            var item = obj as MenuItemDto;
            if (item == null) return false;

            // 1. Category Filter
            if (SelectedCategory != null && SelectedCategory.Id != 0)
            {
                if (item.CategoryId != SelectedCategory.Id) return false;
            }

            // 2. Station Filter
            if (SelectedStation != null && SelectedStation.Id != 0)
            {
                if (item.StationId != SelectedStation.Id) return false;
            }

            // 3. Text Search
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.Trim().ToLower();
                bool nameMatch = item.Name != null && item.Name.ToLower().Contains(search);
                if (!nameMatch) return false;
            }

            return true;
        }

        private void UpdateSummary()
        {
            if (_allMenuItems == null) return;
            var visibleItems = _allMenuItems.Where(x => FilterLogic(x)).ToList();

            TotalCount = visibleItems.Count;
            ActiveCount = visibleItems.Count(x => x.IsActive);
            InactiveCount = visibleItems.Count(x => !x.IsActive);
        }

        private void ExecuteQuickUpdatePrice(object obj)
        {
            if (obj is MenuItemDto item)
            {
                // 1. Create the ViewModel
                var vm = new PriceUpdateViewModel(_menuItemRepository, item.Id, item.Name);

                // 2. Create the View
                var window = new PointOfSale.UI.Views.Restaurant.Dialogs.PriceUpdateWindow();
                window.DataContext = vm;

                // 3. Show it
                window.ShowDialog();

                // 4. Optional: Refresh main list to show updated base price
                // (If the logic updates the "Standard" price shown on the main card)
                ExecuteSearch();
            }
        }

        private async Task ExecuteEditAsync(object obj)
        {
            int menuItemId = 0;
            if (obj is MenuItemDto dto)
            {
                menuItemId = dto.Id;
            }
            else if (obj is int id)
            {
                menuItemId = id;
            }

            if (menuItemId == 0) return;

            try
            {
                IsBusy = true;

                var menuItem = await _menuItemRepository.GetByIdAsync(menuItemId);
                if (menuItem == null)
                {
                    System.Windows.MessageBox.Show("Could not find the menu item details.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                var taxIds = await _menuItemRepository.GetSelectedTaxIdsAsync(menuItemId);

                var mainView = System.Windows.Application.Current.Windows.OfType<PointOfSale.UI.Views.Shell.MainView>().FirstOrDefault();
                if (mainView?.DataContext is PointOfSale.UI.ViewModels.Shell.MainViewModel mainVm)
                {
                    mainVm.NavigateToMenuItemCommand.Execute(null);
                    if (mainVm.CurrentViewModel is MenuItemViewModel menuVm)
                    {
                        await menuVm.LoadItemForEditAsync(menuItem, taxIds);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading menu item for edit: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteDeleteAsync(object obj)
        {
            int menuItemId = 0;
            string itemName = "this item";

            if (obj is MenuItemDto dto)
            {
                menuItemId = dto.Id;
                itemName = dto.Name;
            }
            else if (obj is int id)
            {
                menuItemId = id;
            }

            if (menuItemId == 0) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete '{itemName}'? This will permanently remove its variants and recipe configuration.",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    IsBusy = true;
                    await _menuItemRepository.DeleteAsync(menuItemId);
                    System.Windows.MessageBox.Show("Menu Item Deleted Successfully!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    await ReloadDataAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error deleting menu item: {ex.Message}", "Delete Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        #endregion
    }
}
