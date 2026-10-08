using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class SubRecipeViewModel : BaseViewModel
    {
        private readonly ISubRecipeService _subRecipeService;
        private readonly IProductRepository _productRepository;
        private readonly IUnitMeasureRepository _unitMeasureRepository;
        private readonly IDialogService _dialogService;

        private int _selectedOutputProductId;
        private int _selectedIngredientId;
        private int _selectedUnitMeasureId;
        private string _quantityInput;
        private bool _isBusy;
        private decimal _totalRecipeCost;
        private Product _selectedPrepItem;
        private Product _selectedIngredient;
        private SubRecipeLineDto _selectedRecipeLine;
        private bool _isSelectedPrepItemUsedInMenuItem;
        private readonly HashSet<string> _loadedRecipeLineKeys = new HashSet<string>();

        public SubRecipeViewModel(
            ISubRecipeService subRecipeService,
            IProductRepository productRepository,
            IUnitMeasureRepository unitMeasureRepository,
            IDialogService dialogService)
        {
            _subRecipeService = subRecipeService;
            _productRepository = productRepository;
            _unitMeasureRepository = unitMeasureRepository;
            _dialogService = dialogService;

            PrepItems = new ObservableCollection<Product>();
            Ingredients = new ObservableCollection<Product>();
            UnitMeasures = new ObservableCollection<UnitMeasure>();
            AvailableUnitMeasures = new ObservableCollection<UnitMeasure>();
            RecipeLines = new ObservableCollection<SubRecipeLineDto>();
            RecipeLines.CollectionChanged += RecipeLines_CollectionChanged;

            AddIngredientCommand = new RelayCommand(_ => AddIngredient(), _ => !IsBusy);
            RemoveIngredientCommand = new RelayCommand(line => RemoveIngredient(line as SubRecipeLineDto), _ => CanRemoveIngredient());
            SaveSubRecipeCommand = new AsyncRelayCommand(async _ => await SaveSubRecipeAsync(), _ => CanSaveSubRecipe());
            RefreshCommand = new AsyncRelayCommand(async _ => await InitializeAsync(), _ => !IsBusy);

            _ = InitializeAsync();
        }

        public ObservableCollection<Product> PrepItems { get; }
        public ObservableCollection<Product> Ingredients { get; }
        public ObservableCollection<UnitMeasure> UnitMeasures { get; }
        public ObservableCollection<UnitMeasure> AvailableUnitMeasures { get; }
        public ObservableCollection<SubRecipeLineDto> RecipeLines { get; }

        public ICommand AddIngredientCommand { get; }
        public ICommand RemoveIngredientCommand { get; }
        public ICommand SaveSubRecipeCommand { get; }
        public ICommand RefreshCommand { get; }

        public int SelectedOutputProductId
        {
            get => _selectedOutputProductId;
            set
            {
                if (SetProperty(ref _selectedOutputProductId, value))
                {
                    if (_selectedPrepItem == null || _selectedPrepItem.ProductId != value)
                    {
                        SelectedPrepItem = PrepItems.FirstOrDefault(p => p.ProductId == value);
                    }
                    else
                    {
                        _ = LoadRecipeAsync(value);
                    }

                    RaiseCommandStates();
                }
            }
        }

        public Product SelectedPrepItem
        {
            get => _selectedPrepItem;
            set
            {
                if (SetProperty(ref _selectedPrepItem, value))
                {
                    var outputProductId = value?.ProductId ?? 0;
                    if (_selectedOutputProductId != outputProductId)
                    {
                        _selectedOutputProductId = outputProductId;
                        OnPropertyChanged(nameof(SelectedOutputProductId));
                    }

                    _ = LoadRecipeAsync(outputProductId);
                    RaiseCommandStates();
                }
            }
        }

        public int SelectedIngredientId
        {
            get => _selectedIngredientId;
            set
            {
                if (SetProperty(ref _selectedIngredientId, value))
                {
                    if (_selectedIngredient == null || _selectedIngredient.ProductId != value)
                    {
                        SelectedIngredient = Ingredients.FirstOrDefault(p => p.ProductId == value);
                    }
                    else
                    {
                        RefreshAvailableUnitMeasures();
                    }

                    RaiseCommandStates();
                }
            }
        }

        public Product SelectedIngredient
        {
            get => _selectedIngredient;
            set
            {
                if (SetProperty(ref _selectedIngredient, value))
                {
                    var ingredientId = value?.ProductId ?? 0;
                    if (_selectedIngredientId != ingredientId)
                    {
                        _selectedIngredientId = ingredientId;
                        OnPropertyChanged(nameof(SelectedIngredientId));
                    }

                    RefreshAvailableUnitMeasures();
                    RaiseCommandStates();
                }
            }
        }

        public int SelectedUnitMeasureId
        {
            get => _selectedUnitMeasureId;
            set
            {
                if (SetProperty(ref _selectedUnitMeasureId, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public string QuantityInput
        {
            get => _quantityInput;
            set
            {
                if (SetProperty(ref _quantityInput, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public SubRecipeLineDto SelectedRecipeLine
        {
            get => _selectedRecipeLine;
            set => SetProperty(ref _selectedRecipeLine, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public decimal TotalRecipeCost
        {
            get => _totalRecipeCost;
            private set => SetProperty(ref _totalRecipeCost, value);
        }

        public bool IsSelectedPrepItemUsedInMenuItem
        {
            get => _isSelectedPrepItemUsedInMenuItem;
            private set
            {
                if (SetProperty(ref _isSelectedPrepItemUsedInMenuItem, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = null;

                var products = (await _productRepository.GetAllAsync())
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.ProductName)
                    .ToList();

                var unitMeasures = (await _unitMeasureRepository.GetAllAsync())
                    .OrderBy(u => u.UnitMeasureName)
                    .ToList();

                PrepItems.Clear();
                Ingredients.Clear();
                UnitMeasures.Clear();
                AvailableUnitMeasures.Clear();

                foreach (var product in products)
                {
                    PrepItems.Add(product);
                    Ingredients.Add(product);
                }

                foreach (var unitMeasure in unitMeasures)
                {
                    UnitMeasures.Add(unitMeasure);
                }

                RefreshAvailableUnitMeasures();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _dialogService.ShowMessage(ex.Message, "Sub-Recipes", DialogMessageType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadRecipeAsync(int outputProductId)
        {
            if (outputProductId <= 0)
            {
                ClearRecipeLines();
                IsSelectedPrepItemUsedInMenuItem = false;
                _loadedRecipeLineKeys.Clear();
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = null;

                var lines = await _subRecipeService.GetSubRecipeAsync(outputProductId);
                IsSelectedPrepItemUsedInMenuItem = await _subRecipeService.IsUsedAsMenuIngredientAsync(outputProductId);
                ClearRecipeLines();
                _loadedRecipeLineKeys.Clear();

                foreach (var line in lines)
                {
                    EnrichRecipeLine(line);
                    RecipeLines.Add(line);
                    _loadedRecipeLineKeys.Add(BuildLineKey(line));
                }

                CalculateTotalCost();
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _dialogService.ShowMessage(ex.Message, "Sub-Recipes", DialogMessageType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void AddIngredient()
        {
            if (!TryGetQuantity(out var quantity))
            {
                _dialogService.ShowMessage("Enter a valid quantity greater than zero.", "Sub-Recipes", DialogMessageType.Warning);
                return;
            }

            if (SelectedOutputProductId <= 0)
            {
                _dialogService.ShowMessage("Select the prep item before adding ingredients.", "Sub-Recipes", DialogMessageType.Warning);
                return;
            }

            if (SelectedIngredientId <= 0 || SelectedUnitMeasureId <= 0)
            {
                _dialogService.ShowMessage("Select an ingredient and unit measure.", "Sub-Recipes", DialogMessageType.Warning);
                return;
            }

            if (SelectedIngredientId == SelectedOutputProductId)
            {
                _dialogService.ShowMessage("The output prep item cannot be used as its own ingredient.", "Sub-Recipes", DialogMessageType.Warning);
                return;
            }

            var ingredient = Ingredients.FirstOrDefault(p => p.ProductId == SelectedIngredientId);
            var unitMeasure = AvailableUnitMeasures.FirstOrDefault(u => u.UnitMeasureId == SelectedUnitMeasureId);
            if (ingredient == null || unitMeasure == null)
            {
                _dialogService.ShowMessage("The selected ingredient or unit measure could not be found.", "Sub-Recipes", DialogMessageType.Warning);
                return;
            }

            var existingLine = RecipeLines.FirstOrDefault(line =>
                line.ProductId == SelectedIngredientId &&
                line.UnitMeasureId == SelectedUnitMeasureId);

            if (existingLine != null)
            {
                existingLine.Quantity += quantity;
            }
            else
            {
                RecipeLines.Add(new SubRecipeLineDto
                {
                    ProductId = ingredient.ProductId,
                    ProductName = ingredient.ProductName,
                    UnitMeasureId = unitMeasure.UnitMeasureId,
                    UnitMeasureName = unitMeasure.UnitMeasureName,
                    Quantity = quantity,
                    StandardCost = ingredient.StandardCost
                });
            }

            QuantityInput = string.Empty;
            CalculateTotalCost();
            RaiseCommandStates();
        }

        private void RefreshAvailableUnitMeasures()
        {
            AvailableUnitMeasures.Clear();

            if (SelectedIngredient == null || SelectedIngredient.UnitMeasureId <= 0)
            {
                SelectedUnitMeasureId = 0;
                return;
            }

            var baseUnitMeasure = UnitMeasures.FirstOrDefault(u => u.UnitMeasureId == SelectedIngredient.UnitMeasureId)
                ?? new UnitMeasure
                {
                    UnitMeasureId = SelectedIngredient.UnitMeasureId,
                    UnitMeasureName = SelectedIngredient.UnitMeasureName,
                    Code = SelectedIngredient.UnitMeasureCode
                };

            AvailableUnitMeasures.Add(baseUnitMeasure);
            SelectedUnitMeasureId = baseUnitMeasure.UnitMeasureId;
        }

        private void RemoveIngredient(SubRecipeLineDto recipeLine)
        {
            if (recipeLine == null)
            {
                return;
            }

            if (IsSelectedPrepItemUsedInMenuItem)
            {
                _dialogService.ShowMessage(
                    "This prep item is used by a menu item. Ingredients cannot be removed.",
                    "Sub-Recipes",
                    DialogMessageType.Warning);
                return;
            }

            RecipeLines.Remove(recipeLine);
            CalculateTotalCost();
            RaiseCommandStates();
        }

        private void EnrichRecipeLine(SubRecipeLineDto recipeLine)
        {
            if (recipeLine == null)
            {
                return;
            }

            var product = Ingredients.FirstOrDefault(p => p.ProductId == recipeLine.ProductId);
            if (product == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(recipeLine.ProductName))
            {
                recipeLine.ProductName = product.ProductName;
            }

            if (string.IsNullOrWhiteSpace(recipeLine.UnitMeasureName))
            {
                recipeLine.UnitMeasureName = product.UnitMeasureName;
            }

            recipeLine.StandardCost = product.StandardCost;
        }

        private void CalculateTotalCost()
        {
            TotalRecipeCost = RecipeLines.Sum(line => line.Quantity * line.StandardCost);
        }

        private void ClearRecipeLines()
        {
            foreach (var line in RecipeLines)
            {
                line.PropertyChanged -= RecipeLine_PropertyChanged;
            }

            RecipeLines.Clear();
            CalculateTotalCost();
        }

        private void RecipeLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SubRecipeLineDto line in e.OldItems)
                {
                    line.PropertyChanged -= RecipeLine_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (SubRecipeLineDto line in e.NewItems)
                {
                    EnrichRecipeLine(line);
                    line.PropertyChanged += RecipeLine_PropertyChanged;
                }
            }

            CalculateTotalCost();
        }

        private void RecipeLine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SubRecipeLineDto.Quantity) ||
                e.PropertyName == nameof(SubRecipeLineDto.StandardCost))
            {
                CalculateTotalCost();
            }
        }

        private async Task SaveSubRecipeAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = null;

                if (IsSelectedPrepItemUsedInMenuItem && HasRemovedLoadedIngredient())
                {
                    _dialogService.ShowMessage(
                        "This prep item is used by a menu item. Ingredients cannot be removed.",
                        "Sub-Recipes",
                        DialogMessageType.Warning);
                    return;
                }

                await _subRecipeService.SaveAsync(SelectedOutputProductId, RecipeLines.ToList());
                _loadedRecipeLineKeys.Clear();
                foreach (var line in RecipeLines)
                {
                    _loadedRecipeLineKeys.Add(BuildLineKey(line));
                }

                _dialogService.ShowMessage("Sub-recipe saved successfully.", "Sub-Recipes", DialogMessageType.Information);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _dialogService.ShowMessage(ex.Message, "Sub-Recipes", DialogMessageType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSaveSubRecipe()
        {
            return !IsBusy && SelectedOutputProductId > 0 && RecipeLines.Any();
        }

        private bool CanRemoveIngredient()
        {
            return !IsBusy && !IsSelectedPrepItemUsedInMenuItem;
        }

        private bool HasRemovedLoadedIngredient()
        {
            if (!_loadedRecipeLineKeys.Any())
            {
                return false;
            }

            var currentKeys = new HashSet<string>(RecipeLines.Select(BuildLineKey));
            return _loadedRecipeLineKeys.Any(key => !currentKeys.Contains(key));
        }

        private static string BuildLineKey(SubRecipeLineDto line)
        {
            return line == null
                ? string.Empty
                : line.ProductId + ":" + line.UnitMeasureId;
        }

        private bool TryGetQuantity(out decimal quantity)
        {
            return decimal.TryParse(
                       QuantityInput,
                       NumberStyles.Number,
                       CultureInfo.CurrentCulture,
                       out quantity)
                   && quantity > 0;
        }

        private void RaiseCommandStates()
        {
            (AddIngredientCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveIngredientCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveSubRecipeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (RefreshCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
