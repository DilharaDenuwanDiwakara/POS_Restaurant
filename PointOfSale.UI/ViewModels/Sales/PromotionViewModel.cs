using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class PromotionViewModel : BaseViewModel
    {
        private readonly IPromotionRepository _promotionRepository;
        private readonly IMenuItemRepository _menuItemRepository;
        private readonly IUserSessionService _userSessionService;

        public PromotionViewModel(
            IPromotionRepository promotionRepository,
            IMenuItemRepository menuItemRepository,
            IUserSessionService userSessionService)
        {
            _promotionRepository = promotionRepository;
            _menuItemRepository = menuItemRepository;
            _userSessionService = userSessionService;

            PromotionTypes = new ObservableCollection<string>
            {
                "BOGO_SAME_FREE",
                "BOGO_SAME_PERCENT",
                "BUY_A_GET_B_FREE"
            };

            ProductList = new ObservableCollection<MenuVariantDto>();
            PromotionList = new ObservableCollection<PromotionRule>();
            DayOptions = new ObservableCollection<DayOption>
            {
                new DayOption{ Name = "All Days", Mask = 127 },
                new DayOption{ Name = "Friday Only", Mask = 32 },
                new DayOption{ Name = "Weekdays", Mask = 2+4+8+16+32 },
                new DayOption{ Name = "Weekend", Mask = 1+64 }
            };

            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => CanSave);
            LoadCommand = new AsyncRelayCommand(async _ => await LoadAsync());
            EditCommand = new RelayCommand(_ => SetEditMode(), _ => SelectedRule != null);
            NewCommand = new RelayCommand(_ => CreateNew());
            ToggleActiveCommand = new AsyncRelayCommand(async _ => await ToggleActiveAsync(), _ => SelectedRule != null);

            _ = LoadAsync();
        }

        public ObservableCollection<string> PromotionTypes { get; }
        public ObservableCollection<MenuVariantDto> ProductList { get; }
        public ObservableCollection<PromotionRule> PromotionList { get; }
        public ObservableCollection<DayOption> DayOptions { get; }

        private PromotionRule _selectedRule;
        public PromotionRule SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (SetProperty(ref _selectedRule, value))
                    RaiseCanExecuteChanged();
            }
        }

        private int _promotionRuleId;
        public int PromotionRuleId { get => _promotionRuleId; set => SetProperty(ref _promotionRuleId, value); }

        private string _ruleName;
        public string RuleName
        {
            get => _ruleName;
            set
            {
                if (SetProperty(ref _ruleName, value))
                {
                    ValidateRuleName();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _selectedPromotionType = "BOGO_SAME_FREE";
        public string SelectedPromotionType
        {
            get => _selectedPromotionType;
            set
            {
                if (SetProperty(ref _selectedPromotionType, value))
                {
                    if (_selectedPromotionType == "BOGO_SAME_FREE" || _selectedPromotionType == "BUY_A_GET_B_FREE")
                        DiscountPercent = "100.00";

                    ValidatePromotionType();
                    ValidateDiscountPercent();
                    ValidateGetProduct();
                    RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(IsGetProductVisible));
                    OnPropertyChanged(nameof(IsPercentEditable));
                }
            }
        }

        private int _buyProductId;
        public int BuyProductId
        {
            get => _buyProductId;
            set
            {
                if (SetProperty(ref _buyProductId, value))
                {
                    ValidateBuyProduct();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private int? _getProductId;
        public int? GetProductId
        {
            get => _getProductId;
            set
            {
                if (SetProperty(ref _getProductId, value))
                {
                    ValidateGetProduct();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _buyQuantity = "1";
        public string BuyQuantity
        {
            get => _buyQuantity;
            set
            {
                if (SetProperty(ref _buyQuantity, value))
                {
                    ValidateBuyQuantity();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _getQuantity = "1";
        public string GetQuantity
        {
            get => _getQuantity;
            set
            {
                if (SetProperty(ref _getQuantity, value))
                {
                    ValidateGetQuantity();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _discountPercent = "100.00";
        public string DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetProperty(ref _discountPercent, value))
                {
                    ValidateDiscountPercent();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private DayOption _selectedDayOption;
        public DayOption SelectedDayOption
        {
            get => _selectedDayOption;
            set
            {
                if (SetProperty(ref _selectedDayOption, value))
                {
                    ValidateDayOption();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private DateTime? _validFrom = DateTime.Today;
        public DateTime? ValidFrom
        {
            get => _validFrom;
            set
            {
                if (SetProperty(ref _validFrom, value))
                {
                    ValidateValidDates();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private DateTime? _validTo = DateTime.Today.AddMonths(1);
        public DateTime? ValidTo
        {
            get => _validTo;
            set
            {
                if (SetProperty(ref _validTo, value))
                {
                    ValidateValidDates();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set { if (SetProperty(ref _isEditing, value)) OnPropertyChanged(nameof(SaveButtonText)); } }
        public string SaveButtonText => IsEditing ? "Update" : "Save";

        public bool IsGetProductVisible => SelectedPromotionType == "BUY_A_GET_B_FREE";
        public bool IsPercentEditable => SelectedPromotionType == "BOGO_SAME_PERCENT";

        public bool CanSave =>
            !HasErrors &&
            !string.IsNullOrWhiteSpace(RuleName) &&
            !string.IsNullOrWhiteSpace(SelectedPromotionType) &&
            BuyProductId > 0 &&
            TryGetPositiveInt(BuyQuantity, out _) &&
            TryGetPositiveInt(GetQuantity, out _) &&
            TryGetDiscountPercent(out _) &&
            SelectedDayOption != null &&
            (!ValidFrom.HasValue || !ValidTo.HasValue || ValidFrom <= ValidTo) &&
            (!IsGetProductVisible || (GetProductId ?? 0) > 0);

        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand ToggleActiveCommand { get; }

        private async Task LoadAsync()
        {
            try
            {
                await LoadProductsAsync();

                var rules = await _promotionRepository.GetAllAsync();
                var productLookup = BuildProductLookup(ProductList);

                PromotionList.Clear();
                foreach (var r in GetDistinctRules(rules).OrderByDescending(x => x.PromotionRuleId))
                {
                    ApplyProductDisplayNames(r, productLookup);
                    PromotionList.Add(r);
                }

                if (SelectedDayOption == null)
                    SelectedDayOption = DayOptions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load promotions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadProductsAsync()
        {
            var products = await _menuItemRepository.GetAllVariantsForSalesAsync();

            ProductList.Clear();
            foreach (var product in GetDistinctProducts(products))
            {
                ProductList.Add(product);
            }
        }

        private static IReadOnlyDictionary<int, string> BuildProductLookup(IEnumerable<MenuVariantDto> products)
        {
            return (products ?? Enumerable.Empty<MenuVariantDto>())
                .Where(x => x != null && x.VariantId > 0)
                .GroupBy(x => x.VariantId)
                .ToDictionary(g => g.Key, g => g.First().DisplayName);
        }

        private static IEnumerable<MenuVariantDto> GetDistinctProducts(IEnumerable<MenuVariantDto> products)
        {
            if (products == null)
                yield break;

            var addedProductIds = new HashSet<int>();

            foreach (var product in products)
            {
                if (product == null || product.VariantId <= 0)
                    continue;

                if (addedProductIds.Add(product.VariantId))
                    yield return product;
            }
        }

        private static IEnumerable<PromotionRule> GetDistinctRules(IEnumerable<PromotionRule> rules)
        {
            if (rules == null)
                yield break;

            var addedRuleIds = new HashSet<int>();

            foreach (var rule in rules)
            {
                if (rule == null || rule.PromotionRuleId <= 0)
                    continue;

                if (addedRuleIds.Add(rule.PromotionRuleId))
                    yield return rule;
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                ValidateAll();
                if (HasErrors || !CanSave)
                {
                    MessageBox.Show("Please correct the errors before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                TryGetPositiveInt(BuyQuantity, out var buyQuantity);
                TryGetPositiveInt(GetQuantity, out var getQuantity);
                TryGetDiscountPercent(out var discountPercent);

                var model = new PromotionRule
                {
                    PromotionRuleId = PromotionRuleId,
                    RuleName = RuleName?.Trim(),
                    PromotionType = SelectedPromotionType,
                    BuyProductId = BuyProductId,
                    GetProductId = IsGetProductVisible ? GetProductId : null,
                    BuyQuantity = buyQuantity,
                    GetQuantity = getQuantity,
                    DiscountPercent = SelectedPromotionType == "BOGO_SAME_PERCENT" ? discountPercent : 100,
                    DayOfWeekMask = SelectedDayOption?.Mask,
                    ValidFrom = ValidFrom,
                    ValidTo = ValidTo,
                    BranchId = _userSessionService.BranchId,
                    IsActive = IsActive,
                    CreatedBy = _userSessionService.UserId
                };

                if (IsEditing)
                    await _promotionRepository.UpdateAsync(model);
                else
                    await _promotionRepository.CreateAsync(model);

                await LoadAsync();
                CreateNew();
                MessageBox.Show("Promotion saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save promotion: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetEditMode()
        {
            if (SelectedRule == null)
                return;

            ClearAllErrors();
            IsEditing = true;
            PromotionRuleId = SelectedRule.PromotionRuleId;
            RuleName = SelectedRule.RuleName;
            SelectedPromotionType = SelectedRule.PromotionType;
            BuyProductId = SelectedRule.BuyProductId;
            GetProductId = SelectedRule.GetProductId;
            BuyQuantity = SelectedRule.BuyQuantity.ToString(CultureInfo.CurrentCulture);
            GetQuantity = SelectedRule.GetQuantity.ToString(CultureInfo.CurrentCulture);
            DiscountPercent = SelectedRule.DiscountPercent.ToString("0.##");
            SelectedDayOption = DayOptions.FirstOrDefault(x => x.Mask == (SelectedRule.DayOfWeekMask ?? 127)) ?? DayOptions.FirstOrDefault();
            ValidFrom = SelectedRule.ValidFrom;
            ValidTo = SelectedRule.ValidTo;
            IsActive = SelectedRule.IsActive;
        }

        private async Task ToggleActiveAsync()
        {
            if (SelectedRule == null)
                return;

            SelectedRule.IsActive = !SelectedRule.IsActive;
            SelectedRule.CreatedBy = _userSessionService.UserId;
            await _promotionRepository.UpdateAsync(SelectedRule);
            await LoadAsync();
        }

        private void CreateNew()
        {
            IsEditing = false;
            PromotionRuleId = 0;
            RuleName = string.Empty;
            SelectedPromotionType = "BOGO_SAME_FREE";
            BuyProductId = 0;
            GetProductId = null;
            BuyQuantity = "1";
            GetQuantity = "1";
            DiscountPercent = "100.00";
            SelectedDayOption = DayOptions.FirstOrDefault();
            ValidFrom = DateTime.Today;
            ValidTo = DateTime.Today.AddMonths(1);
            IsActive = true;
            ClearAllErrors();
            RaiseCanExecuteChanged();
        }

        private void ApplyProductDisplayNames(PromotionRule rule, IReadOnlyDictionary<int, string> productLookup)
        {
            if (rule == null)
                return;

            rule.BuyProductName = GetProductDisplayName(rule.BuyProductId, productLookup);
            rule.GetProductName = rule.GetProductId.HasValue
                ? GetProductDisplayName(rule.GetProductId.Value, productLookup)
                : string.Empty;
        }

        private string GetProductDisplayName(int productId, IReadOnlyDictionary<int, string> productLookup)
        {
            if (productLookup != null && productLookup.TryGetValue(productId, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
                return displayName;

            return productId > 0 ? $"ID {productId}" : string.Empty;
        }

        #region Validation
        private void ValidateAll()
        {
            ValidateRuleName();
            ValidatePromotionType();
            ValidateBuyProduct();
            ValidateGetProduct();
            ValidateBuyQuantity();
            ValidateGetQuantity();
            ValidateDiscountPercent();
            ValidateDayOption();
            ValidateValidDates();
            RaiseCanExecuteChanged();
        }

        private void ValidateRuleName()
        {
            ClearErrors(nameof(RuleName));

            if (string.IsNullOrWhiteSpace(RuleName))
                AddError(nameof(RuleName), "Rule name is required.");
            else if (RuleName.Trim().Length > 150)
                AddError(nameof(RuleName), "Rule name cannot exceed 150 characters.");
            else if (!Regex.IsMatch(RuleName.Trim(), @"^[a-zA-Z0-9\s]+$"))
                AddError(nameof(RuleName), "Rule name cannot contain special characters.");
        }

        private void ValidatePromotionType()
        {
            ClearErrors(nameof(SelectedPromotionType));

            if (string.IsNullOrWhiteSpace(SelectedPromotionType) || !PromotionTypes.Contains(SelectedPromotionType))
                AddError(nameof(SelectedPromotionType), "Please select a valid promotion type.");
        }

        private void ValidateBuyProduct()
        {
            ClearErrors(nameof(BuyProductId));

            if (BuyProductId <= 0)
                AddError(nameof(BuyProductId), "Please select a buy product.");
        }

        private void ValidateGetProduct()
        {
            ClearErrors(nameof(GetProductId));

            if (IsGetProductVisible && (GetProductId ?? 0) <= 0)
                AddError(nameof(GetProductId), "Please select a get product.");
        }

        private void ValidateBuyQuantity()
        {
            ClearErrors(nameof(BuyQuantity));

            if (string.IsNullOrWhiteSpace(BuyQuantity))
            {
                AddError(nameof(BuyQuantity), "Buy quantity is required.");
                return;
            }

            if (!TryGetPositiveInt(BuyQuantity, out _))
                AddError(nameof(BuyQuantity), "Buy quantity must be greater than zero.");
        }

        private void ValidateGetQuantity()
        {
            ClearErrors(nameof(GetQuantity));

            if (string.IsNullOrWhiteSpace(GetQuantity))
            {
                AddError(nameof(GetQuantity), "Get quantity is required.");
                return;
            }

            if (!TryGetPositiveInt(GetQuantity, out _))
                AddError(nameof(GetQuantity), "Get quantity must be greater than zero.");
        }

        private void ValidateDiscountPercent()
        {
            ClearErrors(nameof(DiscountPercent));

            if (string.IsNullOrWhiteSpace(DiscountPercent))
            {
                AddError(nameof(DiscountPercent), "Discount percent is required.");
                return;
            }

            if (!TryGetDiscountPercent(out _))
                AddError(nameof(DiscountPercent), "Discount percent must be between 0.01 and 100.");
        }

        private void ValidateDayOption()
        {
            ClearErrors(nameof(SelectedDayOption));

            if (SelectedDayOption == null)
                AddError(nameof(SelectedDayOption), "Please select a day filter.");
        }

        private void ValidateValidDates()
        {
            ClearErrors(nameof(ValidFrom));
            ClearErrors(nameof(ValidTo));

            if (ValidFrom.HasValue && ValidTo.HasValue && ValidFrom > ValidTo)
                AddError(nameof(ValidTo), "Valid To must be greater than or equal to Valid From.");
        }
        #endregion

        #region Parsing Helpers
        private bool TryGetPositiveInt(string value, out int result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!int.TryParse(value.Trim(), out var parsedValue))
                return false;

            if (parsedValue <= 0)
                return false;

            result = parsedValue;
            return true;
        }

        private bool TryGetDiscountPercent(out decimal discountPercent)
        {
            discountPercent = 0m;

            if (string.IsNullOrWhiteSpace(DiscountPercent))
                return false;

            if (!decimal.TryParse(DiscountPercent.Trim(), out var parsedValue))
                return false;

            if (parsedValue <= 0 || parsedValue > 100)
                return false;

            discountPercent = parsedValue;
            return true;
        }
        #endregion

        private void RaiseCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ToggleActiveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public class DayOption
    {
        public string Name { get; set; }
        public int Mask { get; set; }
    }
}
